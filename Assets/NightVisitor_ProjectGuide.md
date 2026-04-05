# Night Visitor - Project Development Guide

이 문서는 Night Visitor 프로젝트의 핵심 로직과 게임플레이 메커니즘을 요약한 가이드입니다. 
AI 보조 개발자가 프로젝트 진행 상황을 빠르게 파악하고 맥락을 이어가기 위해 작성되었습니다.

---

## 1. 여우 AI 시스템 (`RandomFoxAnimation.cs`)
여우는 기본적으로 닭을 사냥하고 위협으로부터 도망치는 복잡한 State Machine 구조를 가집니다.

* **상태 (States):** `Walk`, `Sneak`, `Run`, `Jump`, `Flee`, `Dead`, `Catching`, `Escape`
* **주요 행동 원리:**
    * **사냥 (Run/Sneak):** 투명 구역(SneakZone, RunZone)에 진입하면 상태가 변하며, 가장 가까운 닭(`FindClosestChicken()`)을 추적합니다.
    * **닭 포획 체계:** 닭에게 일정 거리(`catchDistance`)만큼 다가가면 기존 닭(`targetChicken`)을 삭제(Destroy)하고, 여우 입에 할당된 가짜 닭 콜라이더(`catchChickenObj`)를 활성화한 뒤 도망 상태(`Escape`)로 바뀝니다.
    * **자연스러운 도망 회전:** 닭을 문 직후 또는 놀라서 달아날 때 뒤로 도는 회전은 `Quaternion.Slerp`와 새로 추가된 `escapeRotationSpeed` 변수를 사용하여 부드럽게 돌아가도록 수학처리 되어 있습니다.
    * **스마트 경로 무작위 탈출:** 닭장에서 바깥쪽으로 향하는 1차 벡터를 계산(`huntingZoneCenter` 기준)하고, 내비메시 위의 유효한 지점(`GetRandomNavMeshLocation`)을 무작위로 계속 새로 갱신하며 영원히 화면 밖으로 달려 나갑니다.
    * **울타리 무시 방지 (Raycast 점프 로직):** 도망치거나 달릴 때 자신의 가고자 하는 방향(앞)으로 레이저를 쏴서 담장과 부딪히면 점프 애니메이션(`JumpRoutine`)을 수행합니다. 이때 옆을 도는 와중에 옆 담장을 스치며 불필요하게 점프하지 않도록 목적지 방향(`agent.desiredVelocity`)과 시선(`transform.forward`)의 각도 차이 45도 필터를 추가했습니다.

## 2. 닭 AI 시스템 (`RandomChickenAnimation.cs`)
* **장애물 회피 (Obstacle Avoidance):** 닭이 울타리 등 반발력 있는 벽돌에 가까이 다가가 비비지 않도록, `Physics.SphereCast`를 통해 전면 장애물을 체크하고 장애물을 발견하면 `Vector3.Reflect`를 이용해 통통 튀듯 방향을 살짝 바꿔 피합니다.
* **패닉 시스템:** 일반적인 배회 중 근처에 총알이 떨어지거나(`FleeFrom`) 여우가 뛰기 시작하면 일정 시간 동안 패닉하며 빠르게 도망칩니다.

## 3. 부위별 피격 및 총알(Shooting) 시스템 (`RaycastShooter.cs` & `Hitbox.cs`)
* **Layer 제한 발사:** `targetLayer`(주로 `Animal` 레이어)에만 총알의 Raycast가 충돌하도록 성능 및 정확도 필터 레이가 설정되어 있습니다. (땅이나 허공 통과)
* **Hitbox.cs (부위별 데미지 증폭):** 
    * `Normal` (몸통 캡슐 콜라이더): 데미지 1. 생존 후 깜짝 놀라서 즉시 도망(`FleeFrom()`)
    * `Critical` (머리나 가슴의 돌출된 박스 콜라이더): 데미지가 곱절(2배) 들어가서 즉사. 
    * **안전망 (Fallback):** 히트박스가 부착되지 않은 기존의 닭이나 일반 객체를 쏘면 옛날 로직 그대로 단발 즉사하는 안전 로직을 `RaycastShooter` 쪽에 `else` 분기로 짜두었습니다.

## 4. 카메라 및 UI 시스템
* **리얼한 반동 및 호흡 (`CameraController.cs`):** 가만히 서 있어도 화면이 위아래/좌우로 아주 미세하게 꿀렁이는 호흡(`breathAmount`, `breathSpeed`) 코사인 수학이 적용되며, 사격 시 `AddRealisticRecoil` 함수를 통해 위와 좌우로 화면이 찰지게 튀어 오릅니다.
* **배터리 타이머 로직 (`BatteryController.cs`):** 20초 주기마다 UI 상의 배터리 칸을 순차적으로 비활성화(Count3 -> Count2)하며, 마지막 1개만 남았을 때는 `transitionDuration` 초에 걸쳐 열화상(`thermalVolume`)을 0으로 깎고 일반화면(`normalVolume`)을 1로 올려 완벽한 렌더러 크로스페이드를 수행합니다.
* **동적 거리 표시기 (`DistanceUI.cs`):** 에임(화면 중앙)에서 `Animal` 레이어 쪽으로 레이캐스트를 지속해서 쏴, 동물 몸통에 일치하면 현재 카메라와의 거리값을 소수점을 뗀 정수의 3자리 텍스트 형식(예: `012`)으로 업데이트합니다. 허공에 조준하면 빈칸이 아닌 `"000"`으로 기본 고정됩니다.

## 5. 메뉴 및 맵 캐러셀 UI 시스템 (`MenuWindowManager.cs` & `MapCarouselUI.cs`)
* **연결된 캔버스 메뉴 전환:** Map, Inventory, Shop 화면이 버튼 클릭 시 단순히 꺼지고 켜지는 것이 아니라, `DOTween`을 이용해 UI 캔버스 전체가 좌우로 부드럽게 슬라이드 되며 전환되도록 `MenuWindowManager.cs`를 구성했습니다. (초기 화면은 Map_Window 고정)
* **3D 맵 캐러셀 (Cover-Flow):** 맵 선택 창(`Map1`, `Map2`, `Map3`)은 `MapCarouselUI.cs`를 통해 입체적으로 관리됩니다. 
    * 현재 선택된 맵은 중앙에 크게, 양옆의 맵은 축소된 채로 뒤로 깔리며(Z-index/SiblingIndex 이용) `Left/Right_Arrow` 버튼 클릭 시 `DOTween`으로 스무스하게 회전하듯 슬라이드 됩니다.
    * 맵이 끝부분(Map1 또는 Map3)에 도달하면 해당 방향의 화살표 버튼이 자동으로 사라러져 범위를 벗어나지 못하게 막으며, 현재 맵 기준 거리가 2칸 이상 떨어진 맵은 투명도(`Alpha`)를 0으로 만들어 시야에서 깔끔하게 숨깁니다.

## 6. 탄약 및 장전 시스템 (`RaycastShooter.cs`)
* **탄창 및 소지 탄약 관리:** 현재 탄창에 있는 총알(`currentAmmo`)과 여분으로 소지한 총알(`reloadableAmmo`)을 추적합니다. (기본 탄창 크기 `maxAmmoPerMag = 5`)
* **발사 및 재장전:** 
    * 좌클릭 시 총알이 남았다면 발사음(`shootSound`)과 함께 탄약이 소진되고, 총알이 없으면 '찰칵'하는 빈 총 소리(`emptyClickSound`)만 납니다. UI 클릭 시 총알이 발사되는 버그는 `EventSystem` 체크로 완전히 방지했습니다.
    * 재장전(R키 혹은 UI 버튼) 시 3.1초간 발사가 지연되며 코루틴(`ReloadCoroutine`)이 돌고 장전음(`reloadSound`)이 재생됩니다. 장전이 완료되면 남은 여분 탄약에서 모자란 만큼 빼서 현재 탄창을 가득 채웁니다.

## 7. 교란용 여우 (Decoy Fox) 시스템 (`DecoyFoxAI.cs`)
* 닭장을 노리지 않고 맵 외곽을 순찰하며 플레이어의 시선과 탄약을 분산시키는 전용 AI입니다.
* **순찰 (Patrol):** 시작 시 가장 가까운 빈 오브젝트(`Waypoint`)를 찾아 이동한 후, 지정된 배열 순서대로 무한히 맵의 테두리를 순찰합니다.
* **상호작용:** 기존 사냥용 여우와 마찬가지로 울타리를 마주치면 뛰어넘으며(`Raycast Jump`), 옆구리를 맞거나 근처에서 총소리가 나면 기겁하고 반대편으로 도망쳐(`Flee/Escape`) 10초 뒤에 맵에서 사라집니다. (기존 여우용 Hitbox 및 Raycast 스크립트와 100% 호환)

## 8. 게임 초기화 및 스테이지 관리 시스템 (`StageSelectManager.cs` & `GunSelectManager.cs`)
* **스테이지 개별 설정 (Stage Configs):** 각 스테이지마다 목표 방어 성공 횟수(`targetKillCount`), 여우 스폰 간격(`foxSpawnInterval`), 고유한 스폰 위치 배열(`spawnPoints`)을 `StageConfig` 구조체로 다르게 적용할 수 있습니다.
* **완전한 씬 초기화 (Complete Game State Reset):** `SceneManager`를 통한 통째 로딩 없이 오브젝트들을 청소/복원하여 새 게임 환경을 구축합니다.
    * **백업 및 복원:** `Start()` 시점에서 씬의 모든 `Chicken`과 `DecoyFoxAI`의 원래 위치/회전/부모 계층을 프리팹(`PrefabRef`)으로 몰래 복사해둡니다. (비활성화된 오브젝트 포함)
    * **청소:** 새로운 스테이지 시작 시 이전 판에 남아있던 시체, 도망가던 여우, 위치를 이탈한 닭과 디코이 여우들을 모두 찾아 파괴(`Destroy`)합니다.
    * **리스폰 및 장전:** 보관해둔 프리팹에서 깨끗한 닭과 미끼 여우들을 원래 자리에 똑같이 재생산하고, `RaycastShooter`의 탄약(`currentAmmo`, `reloadableAmmo`)과 `BatteryController`의 타이머를 100% 상태로 되돌립니다.
* **시간 정지 및 볼륨 제어:** 
    * 다음 스테이지 버튼을 누르자마자 즉시 열화상 볼륨(`volumeStart.weight = 1f`)을 덮어씌워(`PrepareGunSelection`) 화면 색감이 번쩍이는 전환 글리치를 방지합니다.

## 9. 고급 난이도 제어 및 실패(게임 오버) 시스템
* **다중 여우 스폰 (`FoxManager.cs`):** 한 스테이지에 동시에 존재할 수 있는 최대 여우 마리 수(`maxConcurrentFoxes`)를 설정해 난이도를 올려 압박감을 줄 수 있습니다.
* **스테이지별 빡빡한 자원 제약 (`StageConfig` 확장):**
    * **배터리 소모 가속:** 특정 스테이지는 배터리 소모 속도(`batteryDepleteRate`)를 1.5배, 2배율로 설정하여 플레이를 재촉할 수 있습니다.
    * **예비 탄약 제한:** 기존 인스펙터 고정 30발을 무시하고, 각 스테이지마다 지급되는 예비 총알(`maxReloadableAmmo`)을 동적으로 덮어씌워 보급을 통제합니다.
* **완벽한 실패 조건 (Mission Failed):**
    * **배터리 방전:** 마지막 칸(`Count1`)이 소진된 후 특정 시간 동안 케이스가 깜빡이며(`BatteryEmptyGameOverRoutine`) 최종 유예 시간을 주고, 끝내 게임을 정지시키며 실패 패널을 띄웁니다.
    * **탄약 고갈:** 재장전 가능한 예비 총알과 현재 탄창이 모두 0발이 되어 물리적으로 클리어가 불가능해지는 순간 즉시 미션 실패 처리됩니다.
    * 실패 창의 `돌아가기` 버튼을 통해 안전하게 다시 맵 선택 화면으로 빠져나올 수 있습니다.

## 10. 여우 AI 개선 (무한 점프 버그 수정)
* 여우가 닭장 모서리(ㄱ자 구석)에서 울타리를 도약한 뒤, 목표 지점이 여전히 닭장 안쪽에 남아있어 다시 뒤돌아 점프하는 무한 루프를 해결했습니다.
* **도망 방향 즉시 재계산:** `JumpEscapeRoutine`이 끝나는 즉시(땅에 착지하는 순간), 닭장 중심(`huntingZoneCenter`)에서 바깥으로 밀어내는 벡터를 생성하고 여우가 향하던 방향과 섞어 벽을 타고 미끄러지듯 도망치는 무적 회피 경로(`GetSlidingEscapeDestination`)를 강제로 갱신하여 닭장을 영원히 등지게 만들었습니다.

## 11. 게임 결과 정산 및 임시 재화 시스템 (2026.03.09 추가)
* **통계 집계 로직 (`KillCountManager.cs`):** 
    * `RandomFoxAnimation`, `RandomChickenAnimation`, `RaycastShooter` 스크립트를 연동하여 게임 중 여우 사살 수(`AddKill`), 닭 희생 수(`AddDeadChicken`), 사용한 총알 수(`AddConsumedBullet`)를 실시간으로 집계합니다.
* **보상 및 벌금 정산:** 
    * 미션 클리어 또는 실패 시 결과 창(Clear/Fail Panel)에 각 항목별 집계와 스테이지별 설정된 단가(여우 킬 보상, 닭 희생 벌금, 탄창 1개당 소모 벌금)를 적용해 골드 순이익을 계산하여 보여줍니다.
* **임시 글로벌 재화 (Session Gold):** 
    * 아직 영구 저장(PlayerPrefs)을 연동하지 않고, 스태틱 변수 `KillCountManager.currentSessionGold`를 도입하여 앱 종료 전까지 골드가 누적되도록 구축했습니다. 상점 모듈(`ShopManager.cs`) 역시 이 변수와 연동되어 아이템 구매 시 골드를 차감합니다.
* **스테이지 전환 탄약 버그 수정 (`RaycastShooter.cs`):**
    * 새로운 스테이지 진입 시 설정된 예비 총탄 수(`maxReloadableAmmo`)가 꼬이던 현상을 수정했습니다. 기본 최대값을 기억하는 로직을 `Awake()`로 올려 생명주기(Lifecycle) 꼬임을 방지하였고, UI 표시 시 '주어진 전체 탄약량 - 총기 장전분(5발)'을 정확히 계산해 '5 / 예비탄약' 형식으로 출력합니다.

## 12. 스코프(Scope) 확장 및 동적 렌더링 시스템 (2026.03.10 추가)
* **아이템 데이터 확장 (`ShopItemData.cs` & `ShopItemDataEditor.cs`):**
    * 기존 상점 아이템 데이터를 여러 분류(`Gun`, `Scope`, `Mag`, `None`)로 나누도록 `ItemCategory` 인덱싱을 추가했습니다.
    * 카테고리가 `Scope`일 때만 보여지는 커스텀 인스펙터를 구성하여, 줌 배율(`zoomMultiplier`), 안개 거리(`fogStartDistance`, `fogEndDistance`), 배터리 효율 배수(`batteryEfficiencyMultiplier`)를 아이템별로 다르게 지정할 수 있습니다.
* **조준경 줌 동작 (`CameraController.cs`):**
    * 장착된 스코프의 `zoomMultiplier` 값에 따라 시야각(FOV)이 자연스럽게 스무딩(Mathf.Lerp)되어 확대/축소됩니다.
    * 줌 인 상태에서는 마우스 감도(Sensitivity)를 줌 배율만큼 나누어 섬세한 조준이 가능하게 보정했습니다.
    * **안개 및 렌더링 변경:** 스코프를 장착하고 스테이지에 진입하면, `RenderSettings.fogStartDistance`와 `fogEndDistance`를 즉시 덮어씌워 스코프의 고유 가시거리를 적용합니다.
    * 배터리 모듈(`StageSelectManager`)과 연동해, 좋은 스코프를 낄수록 배터리의 소모 속도(타이머)가 늦어지는 효율 시스템을 구축했습니다.

## 13. 총기(Gun) 특성 시스템 연동 (2026.03.10 추가)
* **아이템 데이터 확장 (총기 스탯):**
    * `ShopItemData`의 카테고리가 `Gun` 일 경우, 데미지(`gunDamage`), 격발 사운드(`shootSound`), 기본 탄창 수(`maxAmmoInClip`), 반동 계수(`recoilUp`, `recoilSide`)를 개별적으로 기입할 수 있습니다.
* **시스템 연동 적용:**
    * **사격 로직 (`RaycastShooter.cs`):** 
        * 스테이지 시작 시 `InventoryManager`를 통해 현재 장착된 '총기 데이터'를 가져와 슈터의 능력치를 덮어씌웁니다 (`InitGunData()`).
        * 고정되어 있던 데미지가 이제 각 총기가 가진 `gunDamage`(1 또는 2 지정) 파라미터에 맞게 히트박스로 전달되어 섬세한 밸런싱이 가능해졌습니다.
        * 각 무기 고유의 사운드를 쏠 때마다 `AudioSource`로 출력하며, 예비 탄약 연산 시 총이 지원하는 기본 탄창(`maxAmmoInClip`) 만큼 먼저 빼는 방식으로 완벽하게 장탄수 표기(예: 8 / 22)를 구현했습니다.
    * **동적 카메라 반동:** 기존의 카메라 숨쉬기/반동 로직(`CameraController.cs`) 위에 총의 `recoilUp`, `recoilSide` 값을 인자로 전달해, 쏘는 총의 종류에 따라 화면이 튀는 파워를 다르게 연출합니다.

---
**[다음에 AI를 부르실 때 사용할 프롬프트 예시]**
"Assets 폴더 최상단에 있는 `NightVisitor_ProjectGuide.md` 문서를 먼저 읽고 현재 프로젝트 진행 상황과 코드 구조를 파악해 줘!"

---

## 14. 스코프 UI 이동 방식 (Scope Drag-to-Aim) 구현 (2026.03.13 추가)

기존 캔슬존 드래그 방식에서 완전히 새로운 조준 패러다임으로 전환하였습니다.

### 동작 원리
```
[꾹 누름 0.15초] → isAimMode 활성화 (카메라 완전 고정)
[드래그]          → 카메라 회전 ❌ / 스코프 오버레이 UI만 이동 ✅
[손 뗌 (캔슬존 밖)] → 스코프 중앙 화면 좌표로 레이캐스트 → 격발
[손 뗌 (캔슬존 안)] → 격발 없이 줌 해제 (CancelZone 유지)
[2.5초 초과]       → 자동 줌 해제, 격발 없음
```

### 스코프 오버레이 UI 구조
- `Assets/Material/UI/scope_overlay_mask.png`: 검정 배경 + 중앙 투명 타원 구멍이 뚫린 PNG. 화면의 약 3배 크기로 제작하여 드래그해도 검정 영역이 항상 화면을 덮을 수 있게 함.
- `Scope_Overlay` 오브젝트를 `InGame_Panel` 하위에 배치, 평소 비활성화 상태 유지. 줌 진입 시 자동 활성화되어 화면 중앙에 위치한 뒤 손가락을 따라 이동.

### 주요 코드 변경 (`CameraController.cs`)
- `scopeOverlayUI` (RectTransform), `scopeDragSensitivity`, `scopeMargin` 변수 추가
- `MoveScopeUI(Vector2 delta)`: 스코프 이동 + 화면 경계 클램핑
- `GetScopeScreenCenter()`: 스코프 중앙의 화면 좌표 반환 (레이캐스트 시작점)
- `EnterAimMode()`: 스코프 UI 활성화 및 중앙(0,0) 초기화
- `CancelAimMode()`: 스코프 UI 비활성화 및 원위치 복귀

### 주요 코드 변경 (`RaycastShooter.cs`)
- `ShootAt(Vector2 screenPos)`: 화면 특정 좌표에서 레이를 쏘는 오버로드 추가. `Camera.ScreenPointToRay(screenPos)` 사용. 기존 `Shoot()`의 모든 히트박스/닭/여우/도주 로직을 동일하게 수행.

---

## 15. 코드 성능 최적화 (2026.03.13 추가)

### `RandomChickenAnimation.cs`
- **SphereCast 타이머 적용:** `Update()` 내 매 프레임 실행되던 `Physics.SphereCast` 장애물 검사를 `obstacleCheckTimer`를 도입해 **0.1초 간격**으로만 실행되도록 변경. 닭 이동 속도(1m/s)상 체감 차이 없음.
- **string GC 제거:** `hit.collider.gameObject.name.ToLower().Contains("fence")` → `hit.collider.CompareTag("Fence")`로 교체. 매 프레임 문자열 생성에 의한 GC 할당 제거.

### `RandomFoxAnimation.cs`
- **`agent.SetDestination` 호출 제한 (Walk/Sneak):** Walk·Sneak 상태는 목적지(HuntingZone 중심)가 고정이므로 `destUpdateTimer`를 도입해 **0.2초 간격**으로만 NavMesh 경로 재계산 요청. Run·Escape 상태는 정확도를 위해 매 프레임 유지.
- **string GC 제거:** `CheckForFenceJump()` 내 `hitName.Contains("fence")` → `CompareTag("Fence")`로 교체.

---

## 16. 줌 활성화 신뢰성 버그 수정 (2026.03.13 추가)

### 증상
- 같은 곳을 눌러도 줌이 0.15초 만에 켜질 때도 있고, 3초가 걸리거나 아예 안 되는 현상

### 원인
- 스마트폰에서 손가락이 정지 상태에서도 수 픽셀씩 자연스럽게 흔들림
- `touchStationaryThreshold(20px)`를 초과하면 `isPanning = true`가 되어 `touchHoldTime` 누적 경로 자체가 영구 차단됨

### 수정 내용 (`CameraController.cs`)
```csharp
// 수정 전
if (!isAimMode && dist > touchStationaryThreshold) isPanning = true;

// 수정 후: holdThreshold 시간 전에는 임계값을 2.5배 적용
float panThreshold = (touchHoldTime < holdThreshold)
    ? touchStationaryThreshold * 2.5f   // 줌 대기 중: 50px 이상 밀어야 패닝
    : touchStationaryThreshold;          // 줌 후: 기존 20px
if (!isAimMode && dist > panThreshold) isPanning = true;
```
New Input System 터치, Legacy 터치, 마우스 3가지 입력 경로 모두에 동일하게 적용.

---

## 17. 스테이지 포기(StageGiveUp) 버튼 연결 (2026.03.13 추가)

- `InGame_Panel → Mission → StageGiveUp_BT` 버튼의 OnClick에 `KillCountManager.ShowMissionFailedPanel()`을 연결.
- 미션 목표 달성 전에 누르면 실패 패널이 뜨고, 이미 목표를 달성한 상태라면 자동으로 클리어 패널로 전환됨. 결산(골드 정산 포함) 및 맵 복귀 처리는 기존 로직 그대로 재활용.

---

## 18. 다중 맵 스테이지 관리 및 카메라 커스텀 시스템 (2026.03.16 추가)

### 다중 맵 확장 지원 (`StageSelectManager.cs`)
* **개별 스테이지 구성**: 기존 `StageConfigs` 외에 Map 2를 위한 **`StageConfigs2`** 배열을 추가했습니다. Map 2는 테스트의 편의를 위해 딱 필요한 6가지 핵심 필드(`Play Button`, `Map Prefab`, `Target Kill Count`, `Active Decoy Names`, `Battery Deplete Rate`, `Max Reloadable Ammo`)만 사용하여 관리하도록 이원화했습니다.
* **유연한 패널 전환**: Map 2 전용 스테이지 선택창인 **`mapStagePanel2`** 필드를 추가했습니다. `lastActiveStagePanel` 상태 변수를 도입해, 어떤 맵에서 게임을 시작했느냐에 따라 해당 패널이 부드럽게 사라지고(Fade-out), 게임 종료 후 원래 있던 맵의 선택창으로 정확히 복귀(Fade-in)하도록 로직을 통합 관리합니다.

### 스테이지별 카메라 커스텀 (`CameraController.cs` & `StageSelectManager.cs`)
* **동적 회전 제한**: 하드코딩되어 있던 상하(-70~70)/좌우(-80~80) 회전 제한 각도를 변수화(`pitchLimit`, `yawLimit`)하여 외부에서 동적으로 조절할 수 있게 개선했습니다.
* **위치 및 각도 초기화**: `SetCameraPoseAndLimits()` 함수를 통해 스테이지 시작 시 카메라를 특정 위치(`Transform`)로 옮기고, 각도와 회전 제한을 즉시 갱신합니다.
* **Map 2 특화 설정**: `StageConfig2`에 카메라 시작 포인트와 상하/좌우 제한 각도 필드를 추가하여, Map 2의 각 스테이지마다 사격 위치와 시야 범위를 다르게 연출할 수 있습니다. 
* **하위 호환성 유지**: Map 1 플레이 시에는 별도의 설정 없이도 기존의 카메라 위치와 기본 제한 각도를 사용하도록 예외 처리를 정교하게 구성했습니다.

## 19. 신규 몬스터 AI 구현 및 NavMesh/애니메이터 트러블슈팅 (2026.03.21 추가)

### 신규 몬스터 AI (`MonsterAI.cs`)
* **Map 2 전용 몬스터 도입**: 기본적으로 주변을 배회(Wander)하다가 타격 횟수에 따라 행동 패턴이 변합니다.
* **단계별 추적 시스템**:
  * **0 Hit (배회)**: `idle2`와 `walking` 애니메이션을 오가며 무작위 구역으로 이동합니다.
  * **1 Hit (걷기 추적)**: 그 자리에서 멈춰 `React_attack` 애니메이션 재생. 끝난 직후 카메라 방향으로 돌아서며 걷기(`walking`) 시작.
  * **2 Hit (달리기 추적)**: 리액션 애니메이션 한 번 더 재생 후, 빠른 속도로 돌진(`run`).
  * **3 Hit (사망)**: 사망(`death`)과 함께 모든 콜라이더 비활성화, 상호작용 차단 및 킬 수 증가.
* **타겟팅 최적화**: 게임 시작 시점이 아닌 '첫 피격 시'에 딱 한 번만 메인 카메라를 찾아 저장해둡니다.
* **시스템 연동 (`Hitbox.cs`, `RaycastShooter.cs`)**: 여우처럼 타격 시스템이 공유되며, 총성에 놀라 도망가는 이탈(Flee) 로직에서는 예외 처리되어 도망치지 않습니다.
* **NavMesh 이동 보완 (`MoveToTarget(Vector3)`)**: 카메라(플레이어)가 NavMesh 바닥을 벗어난 공중에 있어 `SetDestination` 경로 расчет이 무시되던 현상 해결. `NavMesh.SamplePosition`을 사용하여 카메라 반경 50m 내에서 가장 가까운 파란색 길 끝자락을 잡아내어 그곳으로 추적하게 수정했습니다.

### 에디터 트러블슈팅 및 셋업 공식 정리
* **다중 맵 NavMesh 병렬 굽기**: `NavMeshSurface` 컴포넌트 안에서 `Collect Objects`를 `All`에서 `Current Object Hierarchy`로 변경하여 구워야 합니다. 이렇게 해야 Map 1과 Map 2가 서로의 파란색 바닥 면적을 지우지 않고 각각 독립적으로 구워집니다. (이 과정을 통해 `FoxManager` 스폰 에러도 완벽히 해결됨)
* **제자리걸음(Apply Root Motion) 해결**: 걷기나 뛰기 애니메이션은 재생되나 앞으로 안 나갈 때, 애니메이터 컴포넌트의 **`Apply Root Motion`** 체크박스를 해제하면 즉시 NavMeshAgent가 밀어주는 속도대로 전진합니다.
* **자연스러운 대기 모션 전환 (Has Exit Time)**: `Any State ➔ React_attack` 처럼 한 번만 재생하고 빠져나와야 하는 모션은, 반드시 `React_attack ➔ idle2` 로 가는 출구 화살표(Transition)를 만들고 `Has Exit Time`을 켜두어야 중간에 굳어버리지 않습니다.

---

## 20. 서바이벌 디펜스 룰 및 2D 거리 기반 AI 시스템 (2026.03.22 추가)

### 서바이벌 타이머 기획 (Map 2 전용)
* 기존의 'N마리 처치' 룰을 탈피하고, 배터리를 관리하며 '아침이 밝을 때까지 생존'하는 감시탑(The Watchtower) 디펜스 콘셉트로 변경 기획을 합의했습니다.

### 몬스터 하산 및 방어선 돌파 AI (`MonsterAI.cs`)
* **자동 목적지 패스파인딩:** 무작위 배회를 삭제하고 씬 내부의 `VillageEntrance`를 향해 가다 서다를 반복하며 내려온 뒤, 도달 시 `VillageInvasion`을 향해 쉬지 않고 돌진합니다.
* **순수 2D 거리 도달 판정 (트리거 버그 해결):** 
  * 투명 큐브(`isTrigger`)가 사격 레이캐스트를 막아버리거나, 큐브 위젯의 높이(Y축) 차이 때문에 3D `Vector3.Distance` 판정이 무한루프에 빠지던 유니티 엔진의 물리 한계를 전부 제거했습니다.
  * 콜라이더(`OnTriggerEnter`) 없이 오직 **X, Z 평면 좌표값(`Vector2.Distance`)** 을 매 프레임 계산하여 백퍼센트의 정확도로 구역 도달을 인지합니다.

### 플레이어 대상 동적 추적 스케일 조절 (ChasePlayer)
* 피격 시 카메라를 쳐다보고 추적을 시작하되, 거리에 따라 행동이 실시간으로 돌변합니다 (인스펙터에서 반경 수치 상시 튜닝 가능).
  * **15m 이상 외곽:** 걷기(`walking`) 유지.
  * **15m 이내 진입 (`runDistanceThreshold`):** 달리기(`run`) 모션으로 자동 전환 및 이동 속도 증가.
  * **3m 이내 진입 (`attackDistanceThreshold`):** 즉시 이동 정지 후 화면을 덮치는 `jump_attack` 모션 수행.
  * **공격 강제 회전 스냅 보정:** NavMesh 끄트머리(절벽) 가장자리를 바라보고 걷던 몬스터가 엉뚱하게 길 밑으로 점프하는 버그를 원천 차단했습니다. 모션 재생 1프레임 직전에 `Quaternion.LookRotation`을 활용해 카메라(수평)를 똑바로 노려보도록 초강제 각도 보정을 통과시킵니다.
* **최후 방어선 붕괴:** 점프(덮치기) 애니메이션의 박진감을 느낄 여유(1초 딜레이 코루틴)를 준 뒤, `KillCountManager.Instance.ShowMissionFailedPanel()`을 호출해 즉시 강제 게임오버(사망) 처리합니다.

---

## 21. Map 2 전용 생존 타이머 구축 및 다중 패널 UI 분리 시스템 (2026.03.25 추가)

### 생존 타이머 및 배터리 동기화 (`SurvivalTimer.cs` & `BatteryController.cs`)
* **생존 06:00 타이머**: Map 2 전용 `InGame_Panel_Map2`에 부착되어 실제 목표 플레이 시간(현실 시간)에 비례해 인게임 시간(01:00 ~ 06:00)이 차오르도록 구축했습니다. 06:00에 도달하면 즉시 미션 클리어(`MissionClearPanel`)를 호출합니다.
* **배터리 수명 동적 변환 및 볼륨 맵핑**: 맵 전환 시 구버전의 볼륨(Volume)만 붙잡고 있어 페이드 효과가 먹통이 되는 현상을 없애기 위해 `MapInfo`에서 전용 볼륨을 불러와 덮어씌우도록(`SetVolumes`) 개편했습니다. 

### 동적 UI 링커 및 싱글톤 붕괴 버그 해결 (`InGameUIBinder.cs` & `KillCountManager.cs`)
* **카메라 UI 자동 할당 링커 (`InGameUIBinder`)**: `InGame_Panel`과 `InGame_Panel_Map2`가 교대될 때 메인 카메라(`RaycastShooter`, `CameraController`, `BatteryController`)가 이전 UI 부품을 계속 쥐고 있어 총알 감소, 줌 조준이 막히는 문제를 완벽히 해결했습니다. 켜진 패널이 즉시 자신의 부품들을 카메라의 뇌에 덮어씌우는 자동 스위칭 방식을 도입했습니다.
* **싱글톤(Singleton) 연쇄 작용 파괴 통제**: UI를 복제하면서 같이 복사된 매니저 스크립트들이 `Awake()` 시 중복 검사를 하며 서로의 타이머 패널이나 미션 패널을 통째로 자폭(Destroy) 시켜버리는 치명적 버그를 막았습니다. 객체 파괴 로직을 버리고 `OnEnable` 시점에 부드럽게 메인 권한만 빼앗아오도록(`Instance = this`) 변경하여, 맵 1과 2 모두 독립적이고 안전한 킬 카운트/타이머/결과창 제어가 가능해졌습니다!

---

## 22. 뮤턴트 전용 AI 분리 및 다중 반응/사운드 기믹 구현 (2026.04.01 추가)

### 뮤턴트 고유 클래스 및 엣지 케이스 통제 (`MutantAI.cs`)
* **단계별 피격 리액션 설계:** 단조로운 배회/추적을 넘어서, 유저의 사격 횟수에 따라 다채롭고 기괴하게 반응하는 페이즈를 도입했습니다.
  * **1타격 (Look Around):** 즉시 전진을 멈추고 제자리에서 카메라를 നോ려보며 지정된 시간(4초) 동안 두리번거립니다.
  * **2타격 (Scream):** 인스펙터 체크박스 옵션에 따라 기괴하게 포효한 뒤(2초 대기), 냅다 달려오게 설계되었습니다.
* **연사(Rapid Fire) 덮어쓰기 로직:** 1대 맞고 대기하는 중에 유저가 급하게 연사할 경우, 기존의 코루틴 타이머를 강제로 파기(`StopCoroutine`)하고 즉각 다음 단계(포효/돌진)로 덮어쓰기 하여 코루틴이 이중으로 꼬이는 것을 완벽 방어했습니다.
* **애니메이터 멈춤(Dead End) 해결 & 기절 억제 보완:** 
  * 리액션 도중 `Update` 루프가 강제로 `isStopped = false`를 남발하지 못하도록 `if(isReacting) return;` 가드를 쳤습니다.
  * 또한 `IsWalking` 부울 값이 켜져 있어 유니티 애니메이터가 엉뚱하게 리액션(두리번)을 스킵하는 버그를 막기 위해, 피격 즉시 `animator.SetBool(animWalkBool, false);`를 선언해 기존 걷기 큐를 날려버립니다.

### 3D 입체 음향 및 타격감 사운드 시스템
* **음향 거리 감쇠 세팅 (Spatial Blend & Rolloff):** 
  * 멀리 있는 감시탑에서 쐈을 때 거리가 멀어 사운드가 폭삭 죽어버리던 유니티 기본 `Logarithmic Rolloff`의 함정을 타파하고, `Linear Rolloff`로 변경하여 현실적이고 빵빵한 3D 사운드를 완성했습니다.
  * 포효 사운드(`screamClip`)와 사망 사운드(`deathClip`)는 `PlayOneShot`으로 적용되며, 특히 사망 시에는 `audioSource.Stop()`을 갈겨 이전의 포효 소리를 즉각 칼같이 잘라버리고 비장함을 살렸습니다.
* **발소리 루프 및 이벤트 훅업 (Animation Events):**
  * `PlayWalkFootstep()` 및 `PlayRunFootstep()` 전용 스피커 함수를 스크립트에 이식해 두었습니다.
  * 유니티 에디터 내 애니메이션 타임라인에 직접 **'이벤트(Event) 핀'**을 꽂아, 몬스터의 발이 땅에 닿는 시각적 프레임과 청각적 사운드가 0.1초의 오차도 없이 맞물리게 하는 최고급 타격감을 도입했습니다. (발 디딜 때마다 볼륨 무작위 할당 로직 포함)
  * 일반 코루틴 타이머가 아닌 애니메이션 이벤트 정석 방식을 채택하여 스케이팅 현상(발이 허공에 떴는데 쿵 소리 남)을 근절했습니다.

---

## 23. 다중 부활 시스템 및 연쇄 어그로 기믹 구현 (2026.04.04 추가)

### 기어오기(Crawling) 및 부활(Revival) 시스템 (`MutantAI.cs`)
* **가짜 죽음 (Fake Death) 트랩 체계:** 인스펙터의 `Enable Revive` 옵션에 따라 죽는 즉시 쿨하게 사라지는 것을 막고, 일단 바닥에 누워 유저를 안심시킵니다 (`reviveDelay` 대기 시간).
* **리스크 & 보상 (Risk vs Reward) 밸런스:**
  * **헤드샷 사망 시:** 머리 뼈대(`headBone.localScale`)가 0으로 찌그러지고 피 분수 파티클이 재생되며, 이후 조용하고 느린 속도(`slowCrawlSpeed`)로 기어옵니다 (보상).
  * **바디샷 사망 시:** 머리가 온전하게 달린 채로 부활하여 굉장히 빠른 속도(`fastCrawlSpeed`)로 훅훅 덮쳐옵니다 (리스크/패널티).
* **크롤링 모드 예외 처리:** 기어오기 상태가 된 좀비는 `hitCount` 장탄수를 무시하고 어떤 총격이든 단 한 발(`isFinalDeath = true`)에 즉시 사망하며 킬 수가 정산됩니다.
* **애니메이터 상태 강제 스냅 (Root Motion 버그 차단):** 
  * "dying" 같은 트리거 파라미터가 아니라 실제 네모 모양의 **State Name**(`animDeathStateName` = "Death")을 `Animator.Play` 로 찔러 넣도록 강제했습니다.
  * 기존 죽는 애니메이션의 후반부(`crawlDeathStartTime`)부터 틀어, 일어서지 않고 누운 자리에서 움찔하고 죽도록 처리하며 강제 이동 루트 모션을 완벽히 얼려버렸습니다.
* **혈편(Blood) 자연 파괴 지원:** `SetActive(false)`로 파티클 덩어리를 통째로 꺼 뿜어져 나온 피가 공중에서 증발하는 어색함을 고치고, `ParticleSystem.Stop()`만 사용하여 스프레이 통만 잠근 뒤 피가 자연스럽게 바닥으로 떨어지도록 수정했습니다.

### 연쇄 폭주 (Chain Aggro / Pack Alarm) 시스템 (`MutantAI.cs`)
* **사운드 기반 포커스 어그로 기획:** 총 쏘는 행위에 온 맵이 반응하는 허큘리스식 웨이브(Horde) 대신, 가장 가까이 뭉쳐있는 녀석들의 멘탈을 터뜨리는 **반경 연쇄 시스템**을 새롭게 구현했습니다. 타겟 우선순위를 계산하게 하는 핵심 재미 시스템입니다.
* **Broadcasting (발신):** 뮤턴트가 포효(2번 맞음)하거나 사망(비명)할 때 방사형 투명 구형 충돌체 영역(`Physics.OverlapSphere`)을 날립니다.
* **에디터 시각화 (Level Design Gizmo):** 이 어그로 사거리(`aggroRadius`)를 유니티 씬(Scene) 뷰 상에서 **빨간색 반투명 동그라미(`OnDrawGizmosSelected`)** 로 한눈에 확인하며 몬스터들을 편하게 배치할 수 있도록 유틸리티를 추가했습니다.
* **Chain Reaction (신호 감지 및 수신):**
  * 반경 안에 있던 돌연변이는 걸음을 즉시 멈추고 **1.0초간 정적**하며 플레이어를 확 쳐다봅니다.
  * 1초 뒤 무리를 죽인 원수를 향해 동반 포효(`Scream`) 하며 즉시 전력 집중 달리기 모드로 페이즈를 전환합니다.
  * 이 과정에서 이미 쫓고 있거나, 죽었거나, 죽은 척(부활 대기) 누워있거나, 기어오고 있는 녀석은 연쇄 작용 트리거를 묵살(Ignore)하게 하여 게임이 터지지 않도록 예외 처리했습니다 (`옵션 B`).

---
**[다음에 AI를 부르실 때 사용할 프롬프트 예시]**
"Assets 폴더 최상단에 있는 `NightVisitor_ProjectGuide.md` 문서를 먼저 읽고 현재 프로젝트 진행 상황과 코드 구조를 파악해 줘!"

---

## 24. 재장전 긴장감 고조 시스템 (2026.04.05 추가)

### 기획 의도
열화상 카메라로 시야를 확보하는 Map 2에서, 재장전 중에는 총기를 내리기 때문에 스코프를 볼 수 없다는 서사적 개연성(Diegetic Design)을 활용하여 3.1초의 재장전 구간 동안 플레이어를 극도의 긴장 상태로 몰아넣는 시스템입니다.

### 적용 대상
`RaycastShooter.cs` → `ReloadCoroutine()` 내부에서 처리. **Map 2 전용**으로, `BatteryController.Instance.thermalVolume`이 존재하고 활성화된 경우(`isMap2 == true`)에만 발동합니다. Map 1은 완전히 무영향.

### 3중 긴장감 연출 타임라인
- 재장전 버튼 클릭 즉시: 심장박동 SFX 루프 시작(2D/loop), 카메라 호흡 셰이크 강화(breathAmount × 3.5), thermalVolume Lerp 0→0.6 (0.5초 fadeIn)
- 약 2.1초간 긴장의 정적: 시야 흐릿, 발소리·포효만 들림
- 재장전 완료 직전 0.5초: thermalVolume Lerp 0.6→0 (fadeOut), 심장박동 정지, 셰이크 복구
- 재장전 완료: 열화상 시야 완전 복구(안도감)

### 인스펙터 튜닝 변수 (RaycastShooter - Reload Tension Settings 헤더)
- `heartbeatClip`: 심장박동 오디오 클립 (인스펙터에서 연결 필요)
- `reloadVolumeTargetWeight`: 0.6 (어두워지는 최대 weight)
- `reloadVolumeFadeInDuration`: 0.5초
- `reloadVolumeFadeOutDuration`: 0.5초
- `reloadBreathMultiplier`: 3.5 (셰이크 강도 배수)

### Volume 충돌 방지
`FadeThermalVolume()` 전용 코루틴을 분리하여 `BatteryController`의 `TransitionToNormalVolume()`과 완전히 독립적으로 작동. 배터리 방전과 재장전이 동시에 일어나도 weight 값이 튀지 않습니다.

---
**[다음에 AI를 부르실 때 사용할 프롬프트 예시]**
`Assets 폴더 최상단에 있는 NightVisitor_ProjectGuide.md 문서를 먼저 읽고 현재 프로젝트 진행 상황과 코드 구조를 파악해 줘!`


---

## 25. 게임 종료 동결 시스템 & 줌 연동 배터리/가시거리 시스템 (2026.04.06 추가)

### 게임 종료 연출 시 전체 동결

- **`isGameEnding` 정적 플래그 (`KillCountManager`):** 미션 실패(점프 공격) 또는 미션 성공(마을 침략) 발생 시 즉시 true. `CameraController` 입력 전체 차단, `RaycastShooter.TryReload()`도 동일 플래그로 차단.
- **점프 애니메이션 동기화 (`MutantAI.JumpAttackAndFailRoutine`):** `Time.timeScale = 0` 직전 `AnimatorUpdateMode.UnscaledTime` 전환 → 점프 정점에서 멈추도록 패널 호출 후 Normal 복구.

### 줌 배터리 가속 소모 시스템

- **배터리 자동 수명 연동 (`BatteryController.SetAutoInterval`):** 생존 목표 시간 기반으로 배터리 1칸 소모 주기 자동 계산 (고정값 제거). `StageSelectManager`에서 맵 생성 시 호출.
- **줌 가시거리 확장:** 줌 진입 시 `RenderSettings.fogEndDistance *= zoomFogMultiplier`(기본 2.0). 줌 해제 시 원상복구.
- **줌 드레인 깜빡임 개선 (`BatteryController`):**
  - 기존: batteryCase만 SetActive 토글 → 배터리 방전 깜빡임과 시각 구분 불가
  - 수정: 배터리 UI 전체(batteryCase + batteryCounts[] + lastBatteryCount)의 Image.color를 zoomDrainBlinkColor로 전환. 원본 색상은 캐싱 후 복구.
  - Inspector 노출: zoomBlinkInterval(깜빡임 속도), zoomDrainBlinkColor(색상 피커)

---

## 26. 옥수수밭 레인 — CornSway 셰이더 & CornfieldTrigger 구현 (2026.04.06 추가)

### 기획 개요
3-Lane 시스템 좌측 레인. 뮤턴트가 옥수수밭을 통과할 때 풀 흔들림과 바스락 소리로 위치를 암시 → 플레이어의 예측샷 유도. 모바일 성능을 위해 파티클 없이 셰이더+트리거 방식으로 구현.

### CornSway_URP 셰이더 (Assets/Shaders/CornSway.shader)

- **URP 전용:** Built-in UnityCG.cginc 대신 Core.hlsl 사용. CBUFFER_START(UnityPerMaterial)로 SRP Batcher 호환.
- **Alpha Clipping 모드:** Queue=AlphaTest, ZWrite On, Blend 없음. URP/Lit Opaque+AlphaClip과 동일 → 투명 PNG 텍스처가 기존 머티리얼처럼 선명하게 보임.
- **2중 흔들림 구조:**
  - 평시: sin(Time x SwaySpeed) x SwayAmount x UV.y (상시 미세 흔들림)
  - 근접: sin(Time x ProximitySpeed + worldX) x ProximityAmount x proximity (뮤턴트 근처만 강하게)
  - UV.y = rootFactor: 뿌리(0) 고정, 끝(1) 최대 흔들림
  - proximity = saturate(1 - dist / ProximityRadius): 거리 기반 0~1 강도
  - _MutantWorldPos.y = -9999이면 proximity = 0 (비활성 상태)
- **ShaderLab 주의사항:** [Header()] 안 공백 금지, [Tooltip()] 안 한국어 금지 → 파서 에러 발생
- **Inspector 주요 항목:** Alpha Clip Threshold(0~1), Color Tint(HDR), Sway Speed/Amount, Proximity Radius(수직 Plane이면 8~12 권장), Proximity Sway Amount

### CornfieldTrigger.cs (Assets/Scripts/CornfieldTrigger.cs)

- **다중 Renderer 지원:** cornRenderers[] 배열(최대 5개 Plane) + cornMats[] 인스턴스 배열로 각 Plane 독립 제어.
- **중복 처리 방지:** HashSet<MutantAI>로 뮤턴트 11개 콜라이더의 중복 Trigger 이벤트 차단. MutantAI 단위로 1회만 처리.
- **컴포넌트 탐색:** GetComponentInParent<MutantAI>() 사용. 콜라이더는 자식에, MutantAI는 최상단 부모에 있는 계층 구조 대응.
- **Kinematic Rigidbody 자동 추가:** NavMeshAgent 뮤턴트는 Rigidbody가 없어 OnTriggerStay 미발동 → Awake()에서 CornfieldZone에 Kinematic Rb 자동 추가.
- **SoundEmitter 자식 분리 (버그 수정):** AudioSource를 자식 오브젝트 SoundEmitter에 생성. soundEmitter.transform.position만 이동 → 부모 BoxCollider가 뮤턴트를 따라 이동하던 버그 해결.
- **사운드 페이드아웃:** 뮤턴트 퇴장 시 FadeOutAndStop() 코루틴으로 볼륨 서서히 0 → 자연스러운 사운드 종료.
- **레이어:** CornfieldZone → Ignore Raycast 레이어. 총 레이캐스트 차단 없이 Physics Trigger는 정상 작동.

---
**[다음에 AI를 부르실 때 사용할 프롬프트 예시]**
`Assets 폴더 최상단에 있는 NightVisitor_ProjectGuide.md 문서를 먼저 읽고 현재 프로젝트 진행 상황과 코드 구조를 파악해 줘!`
