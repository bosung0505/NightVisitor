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
