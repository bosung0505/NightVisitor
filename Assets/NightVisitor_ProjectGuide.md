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

---
**[다음에 AI를 부르실 때 사용할 프롬프트 예시]**
"Assets 폴더 최상단에 있는 `NightVisitor_ProjectGuide.md` 문서를 먼저 읽고 현재 프로젝트 진행 상황과 코드 구조를 파악해 줘!"
