# CameraController 터치 입력 버그 수정 기록

## 목차
1. [최초 문제 상황](#1-최초-문제-상황)
2. [디버그 로그 추가 및 원인 파악](#2-디버그-로그-추가-및-원인-파악)
3. [수정 과정에서 발생한 연쇄 문제들](#3-수정-과정에서-발생한-연쇄-문제들)
4. [최종 해결 방법 요약](#4-최종-해결-방법-요약)
5. [핵심 교훈](#5-핵심-교훈)

---

## 1. 최초 문제 상황

### 증상
- 화면을 **꾹 누르고 있어도 줌(조준) 기능이 켜지지 않음**
- 꾹 누르다가 **살짝 움직여야** 그제서야 줌이 켜지는 현상
- 에디터 시뮬레이터와 모바일 APK 빌드 모두 동일한 증상

### 원인 (초기 분석)
`isPanning` 변수 하나가 두 가지 역할을 동시에 담당하고 있었음:
1. **드래그 의도 감지** (손가락이 빠르게 움직이는지)
2. **줌 타이머 차단** (줌 타이머를 돌릴지 말지 결정)

```csharp
// 문제의 구조
if (activeTouchId != -1 && isHardwarePressed && !isPanning && !isAimMode)
{
    touchHoldTime += Time.unscaledDeltaTime; // isPanning이 true면 타이머 안 돌아감
}

// 아래에서 미세한 움직임에도 isPanning = true로 바뀜
if (frameSpeed > Screen.width * 0.01f)
    isPanning = true; // → 이 순간 타이머가 영원히 멈춤
```

모바일에서는 손가락이 가만히 있어도 미세하게 떨리기 때문에, 거의 매 프레임 `isPanning = true`가 되어 타이머가 절대 완성되지 않았음.

---

## 2. 디버그 로그 추가 및 원인 파악

### 로그 추가 전략
타이머 진행 상황, 드래그 판정, 줌 ON/OFF 순간에 `Debug.Log`를 추가하여 실제 흐름 추적.

### 로그로 발견한 진짜 원인

**첫 번째 발견**: `터치 시작` 로그가 **670번** 찍힘

```
[19:01:06] [CAM] 터치 시작 — activeTouchId=1  ← 이게 670번 반복
```

→ `TouchPhase.Began`이 매 프레임 반복 실행되고 있었음  
→ `activeTouchId`가 할당됐다가 → 다음 프레임에 또 Began → `touchHoldTime = 0f` 리셋 → 무한 반복  
→ **New Input System에서 `touches[0]`을 매 프레임 읽으면 Stationary 상태에서도 Began으로 읽히는 버그**

### 첫 번째 수정
`TouchPhase.Began` 블록에 **이미 추적 중이면 무시하는 조건** 추가:

```csharp
// 수정 전
if (phase == TouchPhase.Began)
{
    isPanning = false;
    touchHoldTime = 0f;
    activeTouchId = touch.touchId.ReadValue(); // 매 프레임 덮어씌워짐!
}

// 수정 후
if (phase == TouchPhase.Began)
{
    if (activeTouchId == -1) // ← 이 조건 추가
    {
        isPanning = false;
        touchHoldTime = 0f;
        activeTouchId = touch.touchId.ReadValue();
    }
}
```

---

## 3. 수정 과정에서 발생한 연쇄 문제들

### 문제 2: 화면 전환(카메라 패닝)이 됐다 안됐다 하는 현상

**원인**: `activeTouchId == -1` 조건 추가 후, `TouchPhase.Ended` 이벤트가 누락되는 경우 `activeTouchId`가 -1로 돌아오지 않음  
→ 다음 터치 시작 시 `activeTouchId == -1` 조건에 걸려서 할당 자체가 안 됨

**시도한 해결책 (안전장치 추가)**:
```csharp
// 화면에 손가락이 없는데 activeTouchId가 남아있으면 강제 초기화
if (activeTouchId != -1 && !isHardwarePressed)
{
    CancelAimMode();
    isPanning = false;
    activeTouchId = -1;
}
```

### 문제 3: 안전장치 위치가 잘못되어 격발이 안 됨

**원인**: 안전장치가 입력 처리 블록보다 **위**에 있어서, 손을 떼는 순간 격발 로직보다 먼저 `CancelAimMode()`를 호출  
→ Ended 이벤트 처리 시점에 이미 `isAimMode = false`가 되어 격발 조건 미충족

**해결**: 안전장치를 모든 입력 처리가 끝난 **`#endif` 바로 위**로 이동

---

## 4. 최종 해결 방법 요약

### 구조 전면 재설계: `isPanning`/`isAimMode` → `TouchState` enum

여러 변수가 서로 간섭하는 근본 문제를 해결하기 위해 **상태를 enum 하나로 통합**

```csharp
private enum TouchState
{
    Idle,      // 손가락 없음
    Holding,   // 누르고 대기 중 (줌 타이머 진행)
    Dragging,  // 드래그로 판정 → 화면 전환
    Zooming    // 줌 모드 진입 완료
}
```

**상태 전환 흐름**:
```
손가락 닿음 → Idle → Holding
                        ├─ 빠르게 움직임 → Dragging (화면 전환)
                        └─ 0.15초 대기  → Zooming  (줌 모드)

손가락 뗌
    ├─ Zooming 상태였으면 → 격발 후 Idle
    └─ Dragging 상태였으면 → 그냥 Idle
```

이 구조에서는 각 상태가 완전히 분리되어 **서로 간섭할 수 없음**.

---

### 최종적으로 남은 버그: 에디터 시뮬레이터에서 화면 전환이 됐다 안됐다

**원인 추적 과정**:

로그 결과:
```
[CAM] HandleInputBegan — id=1, overUI=False, touchState=Idle    ← 처음엔 잘 됨
[CAM] → Holding 상태 진입
[CAM] HandleInputBegan — id=1, overUI=False, touchState=Holding ← 이미 Holding인데 또 호출됨
```

`touchState=Holding` 상태에서 `HandleInputBegan`이 또 호출된다는 것은  
→ `HandleInputEnded`가 호출되지 않아서 `touchState`가 Idle로 돌아오지 않았다는 뜻

**근본 원인**: 마우스 블록이 `else if`로 연결되어 있었음

```csharp
// 문제의 구조
if (hasTouches)
{
    // 터치 처리
    // Ended 이벤트를 여기서 처리하는데...
    // 시뮬레이터에서 Ended 프레임에 hasTouches가 false가 되어버리면?
    // → Ended를 여기서 못 받음!
}
else if (Mouse.current != null) // ← else if 이므로 hasTouches=false일 때만 실행
{
    // 마우스 처리
    // 그런데 이미 이전 프레임에 Began으로 activeTouchId = 터치ID로 설정됨
    // 마우스 ID(0)와 다르므로 HandleInputEnded(0)도 무시됨!
}
```

시뮬레이터에서 터치가 끝나는 순간의 프레임:
1. `hasTouches = false` (손가락이 이미 없음)
2. 터치 블록 실행 안 됨 → Ended 이벤트 수신 불가
3. 마우스 블록 실행 → `HandleInputEnded(0)` 호출
4. 그런데 `activeTouchId`는 터치 ID(1)이고 마우스 ID는 0 → `if (id != activeTouchId) return;`에 걸려서 무시됨!
5. 결과: `touchState`가 영원히 Idle로 안 돌아옴

**최종 수정 1**: `else if` → `if (!hasTouches &&)` 로 변경

```csharp
// 수정 전
else if (Mouse.current != null) { ... }

// 수정 후
if (!hasTouches && Mouse.current != null) { ... }
```

**최종 수정 2**: 마우스 블록에서 `rightButton`만 체크하던 것을 `leftButton || rightButton`으로 변경

```csharp
// 수정 전 (rightButton만 체크 → 시뮬레이터 좌클릭 무시)
if (mouse.rightButton.wasPressedThisFrame)
    HandleInputBegan(mpos, 0);
else if (mouse.rightButton.isPressed) { ... }
else if (mouse.rightButton.wasReleasedThisFrame)
    HandleInputEnded(0);

// 수정 후 (leftButton 추가 → 시뮬레이터 좌클릭도 감지)
if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
    HandleInputBegan(mpos, 0);
else if (mouse.leftButton.isPressed || mouse.rightButton.isPressed) { ... }
else if (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame)
    HandleInputEnded(0);
```

> **왜 모바일에서는 영향이 없나?**  
> 실제 모바일 터치는 `Touchscreen`으로 들어오므로 `hasTouches = true`가 됨  
> → `!hasTouches` 조건에 의해 마우스 블록 자체가 실행되지 않음  
> → 두 경로가 완전히 분리되어 서로 영향 없음

---

## 5. 핵심 교훈

### 1. 하나의 변수로 여러 역할을 맡기지 말 것
`isPanning` 하나가 "드래그 감지"와 "타이머 차단"을 동시에 담당했기 때문에 수정할 때마다 다른 곳이 터졌음. **상태는 enum 하나로 명확하게 관리**하는 것이 훨씬 안전.

### 2. New Input System에서 `touches[0]`은 매 프레임 Began으로 읽힐 수 있음
`TouchPhase.Began` 처리 시 반드시 **이미 추적 중인지 체크**하는 조건(`activeTouchId == -1`)을 넣을 것.

### 3. `else if` 체이닝은 이벤트 누락을 만든다
터치와 마우스를 `else if`로 연결하면, 터치 Ended 프레임에 두 블록 모두 실행되지 않는 타이밍 구멍이 생길 수 있음. **독립적인 `if` 블록으로 분리**하되, 조건으로 실행 범위를 제어할 것.

### 4. 에디터 시뮬레이터 ≠ 모바일 실기기
시뮬레이터는 터치를 `Mouse.leftButton`으로 처리하지만, 실기기는 `Touchscreen`으로 처리함. 두 경로를 모두 커버하도록 설계해야 하며, 한쪽을 수정할 때 다른 쪽에 영향이 없는지 반드시 확인할 것.

### 5. 안전장치는 항상 입력 처리 블록 이후에 위치시킬 것
안전장치(`activeTouchId` 강제 초기화)가 격발/Ended 처리보다 먼저 실행되면 정상적인 이벤트까지 막아버림. **안전장치는 항상 마지막 수단**이어야 함.
