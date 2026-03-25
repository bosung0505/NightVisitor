using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[System.Serializable]
public struct StageConfig
{
    [Tooltip("해당 스테이지의 플레이 버튼")]
    public Button playButton;

    [Header("Map Settings - New")]
    [Tooltip("이 스테이지에서 생성할 맵 프리팹 (Project 창에서 드래그)")]
    public GameObject mapPrefab;

    [Header("Stage Rules")]
    [Tooltip("이 스테이지의 목표 킬 수")]
    public int targetKillCount;
    [Tooltip("여우가 스폰되는 간격 (초)")]
    public float foxSpawnInterval;
    [Tooltip("이 스테이지에서 동시에 필드에 존재할 수 있는 최대 여우 마리 수 (기본 1)")]
    public int maxConcurrentFoxes;

    [Tooltip("이 스테이지에서 여우가 나올 스폰 포인트 오브젝트 '이름'들 (예: Spawn_Front, Spawn_Back)")]
    public string[] activeSpawnPointNames;

    [Header("Difficulty Settings")]
    [Tooltip("이 스테이지에서 활성화할 디코이 여우의 '이름'들을 정확히 적어주세요. (예: DecoyFox_1)")]
    public string[] activeDecoyNames;

    [Tooltip("체크하면 사냥 여우가 SneakZone(살금살금 걷기)을 무시하고 즉시 Run(달리기) 상태로 돌입합니다")]
    public bool ignoreSneakZone;
    [Tooltip("배터리 소모 속도 배수 (기본 1.0)")]
    public float batteryDepleteRate;
    [Tooltip("이 스테이지에서 주어지는 총 예비 탄약 수 (음수면 기본 유지)")]
    public int maxReloadableAmmo;
}

[System.Serializable]
public struct StageConfig2
{
    [Tooltip("해당 스테이지의 플레이 버튼")]
    public Button playButton;

    [Header("Map Settings")]
    [Tooltip("이 스테이지에서 생성할 맵 프리팹")]
    public GameObject mapPrefab;

    [Header("Stage Rules")]
    [Tooltip("이 스테이지의 목표 킬 수")]
    public int targetKillCount;

    [Header("Difficulty Settings")]
    [Tooltip("활성화할 디코이 여우 이름들")]
    public string[] activeDecoyNames;
    [Tooltip("배터리 소모 속도 배수 (기본 1.0)")]
    public float batteryDepleteRate;
    [Tooltip("총 예비 탄약 수 (음수면 기본 유지)")]
    public int maxReloadableAmmo;

    [Header("Survival Settings")]
    [Tooltip("생존 목표 시간 (현실 시간 분 기준, 예: 4면 4분 동안 버팀)")]
    public float survivalTimeMinutes;

    [Header("Camera Settings (Map 2 Only)")]
    [Tooltip("카메라 시작 위치 및 회전 (빈 오브젝트 할당)")]
    public Transform cameraSpawnPoint;
    [Tooltip("상하 회전 제한 (X축: 최소, Y축: 최대)")]
    public Vector2 pitchLimit;
    [Tooltip("좌우 회전 제한 (X축: 최소, Y축: 최대)")]
    public Vector2 yawLimit;
}

public class StageSelectManager : MonoBehaviour
{
    [Header("UI Panels")]
    public CanvasGroup mapStagePanel;
    public CanvasGroup mapStagePanel2; // Map 2용 스테이지 패널 추가
    public CanvasGroup inGamePanel;
    public CanvasGroup inGamePanel2; // Map 2 전용 인게임 패널 추가
    public CanvasGroup gunSelectPanel;

    [Header("Stage Configurations (Map 1)")]
    public StageConfig[] stageConfigs;

    [Header("Stage Configurations (Map 2 - Simplified)")]
    public StageConfig2[] stageConfigs2;

    [Header("InGame UI References")]
    public TextMeshProUGUI ingameTargetKillCountText;

    [Header("Transition Settings")]
    public float fadeDuration = 0.5f;

    // --- 상태 추적 변수 ---
    private GameObject currentInstantiatedMap;
    private CanvasGroup lastActiveStagePanel; // 마지막으로 열려있던 스테이지 패널 기억

    private float originalFogStart;
    private float originalFogEnd;

    void Start()
    {
        originalFogStart = RenderSettings.fogStartDistance;
        originalFogEnd = RenderSettings.fogEndDistance;

        // Map 1 버튼 연결
        foreach (StageConfig config in stageConfigs)
        {
            if (config.playButton != null)
            {
                StageConfig capturedConfig = config;
                config.playButton.onClick.AddListener(() => OnPlayStageClicked(capturedConfig));
            }
        }

        // Map 2 버튼 연결
        foreach (StageConfig2 config in stageConfigs2)
        {
            if (config.playButton != null)
            {
                StageConfig2 capturedConfig = config;
                config.playButton.onClick.AddListener(() => OnPlayStage2Clicked(capturedConfig));
            }
        }

        if (inGamePanel != null)
        {
            inGamePanel.alpha = 0f;
            inGamePanel.gameObject.SetActive(false);
        }

        if (inGamePanel2 != null)
        {
            inGamePanel2.alpha = 0f;
            inGamePanel2.gameObject.SetActive(false);
        }

        if (gunSelectPanel != null)
        {
            gunSelectPanel.alpha = 0f;
            gunSelectPanel.gameObject.SetActive(false);
        }
    }

    // Map 1 실행용
    public void OnPlayStageClicked(StageConfig config)
    {
        lastActiveStagePanel = mapStagePanel; // Map 1 패널 기억
        
        // Map 1은 기본 카메라 위치와 기본 제한(-70~70, -80~80)을 그대로 사용
        Vector2 defaultPitch = new Vector2(-70f, 70f);
        Vector2 defaultYaw = new Vector2(-80f, 80f);

        StartGame(config.mapPrefab, config.targetKillCount, config.foxSpawnInterval, config.maxConcurrentFoxes, 
                  config.activeSpawnPointNames, config.activeDecoyNames, config.ignoreSneakZone, 
                  config.batteryDepleteRate, config.maxReloadableAmmo, null, defaultPitch, defaultYaw, 0f, false);
    }

    // Map 2 실행용
    public void OnPlayStage2Clicked(StageConfig2 config)
    {
        lastActiveStagePanel = mapStagePanel2; // Map 2 패널 기억
        
        float defaultSpawnInterval = 5f;
        int defaultMaxFoxes = 1;
        string[] defaultSpawnPoints = null;
        bool defaultIgnoreSneak = false;

        // 인스펙터에서 0으로 초기화되어 있을 경우를 대비한 안전 예외 처리
        Vector2 pLimit = config.pitchLimit == Vector2.zero ? new Vector2(-70f, 70f) : config.pitchLimit;
        Vector2 yLimit = config.yawLimit == Vector2.zero ? new Vector2(-80f, 80f) : config.yawLimit;

        StartGame(config.mapPrefab, config.targetKillCount, defaultSpawnInterval, defaultMaxFoxes, 
                  defaultSpawnPoints, config.activeDecoyNames, defaultIgnoreSneak, 
                  config.batteryDepleteRate, config.maxReloadableAmmo, config.cameraSpawnPoint, pLimit, yLimit, config.survivalTimeMinutes, true);
    }

    // 통합 게임 시작 로직
    private void StartGame(GameObject mapPrefab, int targetKillCount, float foxSpawnInterval, int maxConcurrentFoxes, 
                           string[] activeSpawnPointNames, string[] activeDecoyNames, bool ignoreSneakZone, 
                           float batteryDepleteRate, int maxReloadableAmmo, Transform camSpawn = null, Vector2 pLimit = default, Vector2 yLimit = default, float survivalTimeMinutes = 0f, bool isMap2 = false)
    {
        // 1. 패널 페이드 (마지막에 활성화되었던 패널을 끕니다)
        if (lastActiveStagePanel != null)
        {
            lastActiveStagePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                lastActiveStagePanel.gameObject.SetActive(false);
            });
        }
        
        CanvasGroup activeInGamePanel = isMap2 ? inGamePanel2 : inGamePanel;
        
        if (activeInGamePanel != null)
        {
            activeInGamePanel.gameObject.SetActive(true);
            activeInGamePanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        if (gunSelectPanel != null)
        {
            GunSelectManager gunManager = gunSelectPanel.GetComponent<GunSelectManager>();
            if (gunManager != null && gunManager.inGameEnvironment != null && !gunManager.inGameEnvironment.activeSelf)
            {
                gunManager.inGameEnvironment.SetActive(true);
            }
        }

        // =========================================================
        // ★ 핵심: 기존 맵 파괴 및 새로운 맵(프리팹) 생성!
        // =========================================================
        if (currentInstantiatedMap != null) Destroy(currentInstantiatedMap);

        // 프리팹을 진짜 화면에 생성합니다. (이 순간 닭과 디코이가 원본 그대로 완벽하게 복원됩니다)
        currentInstantiatedMap = Instantiate(mapPrefab);
        currentInstantiatedMap.SetActive(true); // 프리팹이 실수로 꺼진 채로 저장되었을 경우를 대비한 강제 활성화

        // 생성된 맵에서 MapInfo 스크립트를 뽑아옵니다.
        MapInfo currentMapInfo = currentInstantiatedMap.GetComponent<MapInfo>();

        // 맵에 있는 디코이들 중 이번 스테이지 규칙에 맞는 녀석들만 켭니다.
        if (currentMapInfo != null)
        {
            currentMapInfo.SetupDecoys(activeDecoyNames);
        }

        // =========================================================

        // 탄약 및 무기 세팅
        if (RaycastShooter.Instance != null)
        {
            int ammoLimit = maxReloadableAmmo;
            RaycastShooter.Instance.InitAmmoLimit(ammoLimit);

            if (InventoryManager.Instance != null)
            {
                ShopItemData equippedGun = InventoryManager.Instance.GetEquippedGunData();
                if (equippedGun != null) RaycastShooter.Instance.InitGunData(equippedGun);
            }
        }

        // 혹시 무대에 남아있을지 모르는 떠돌이 클론 여우들 청소
        CleanUpActiveFoxes();

        // 킬 수 UI 및 미션 세팅
        if (ingameTargetKillCountText != null)
        {
            ingameTargetKillCountText.text = targetKillCount.ToString();
        }

        if (KillCountManager.Instance == null)
            KillCountManager.Instance = UnityEngine.Object.FindFirstObjectByType<KillCountManager>(UnityEngine.FindObjectsInactive.Include);

        if (KillCountManager.Instance != null)
        {
            if (!KillCountManager.Instance.gameObject.activeSelf) KillCountManager.Instance.gameObject.SetActive(true);
            KillCountManager.Instance.InitMission(targetKillCount);
        }

        // =========================================================
        // ★ 에러 수정된 스폰 매니저 세팅 (MapInfo가 이름에 맞는 스폰 포인트만 찾아줌)
        // =========================================================
        if (FoxManager.Instance != null && currentMapInfo != null)
        {
            int mFoxes = maxConcurrentFoxes <= 0 ? 1 : maxConcurrentFoxes;

            // MapInfo에게 "이번 스테이지 스폰 포인트 이름록 줄 테니까 실제 좌표(Transform)로 바꿔와!" 라고 시킵니다.
            Transform[] currentStageSpawnPoints = currentMapInfo.GetActiveSpawnPoints(activeSpawnPointNames);

            FoxManager.Instance.InitStage(foxSpawnInterval, currentStageSpawnPoints, mFoxes, ignoreSneakZone);
        }

        // 배터리 세팅
        if (BatteryController.Instance != null)
        {
            float batteryRate = batteryDepleteRate <= 0.1f ? 1.0f : batteryDepleteRate;
            if (InventoryManager.Instance != null)
            {
                ShopItemData equippedScope = InventoryManager.Instance.GetEquippedScopeData();
                if (equippedScope != null) batteryRate *= equippedScope.batteryEfficiencyMultiplier;
            }
            BatteryController.Instance.InitBatteryRate(batteryRate);

            if (currentMapInfo != null)
            {
                BatteryController.Instance.SetVolumes(currentMapInfo.thermalVolume, currentMapInfo.normalVolume);
            }

            BatteryController.Instance.ResetBattery();
        }

        // 스코프 렌더링 세팅
        if (InventoryManager.Instance != null)
        {
            ShopItemData equippedScope = InventoryManager.Instance.GetEquippedScopeData();
            if (equippedScope != null && equippedScope.fogEndDistance > 0f)
            {
                RenderSettings.fogStartDistance = equippedScope.fogStartDistance;
                RenderSettings.fogEndDistance = equippedScope.fogEndDistance;
            }
            else
            {
                RenderSettings.fogStartDistance = originalFogStart;
                RenderSettings.fogEndDistance = originalFogEnd;
            }
        }

        // 총기 선택창 페이드 인
        if (gunSelectPanel != null)
        {
            gunSelectPanel.gameObject.SetActive(true);
            GunSelectManager gunManager = gunSelectPanel.GetComponent<GunSelectManager>();

            if (gunManager != null)
            {
                // 알맞은 inGamePanel을 GunSelectManager에 먼저 연결
                gunManager.inGameUIPanel = isMap2 ? inGamePanel2 : inGamePanel;

                // 그 다음 PrepareGunSelection을 호출해야 연결된 패널을 올바르게 끕니다!
                Volume volToPass = (currentMapInfo != null) ? currentMapInfo.startingVolume : null;
                gunManager.PrepareGunSelection(volToPass);
            }

            gunSelectPanel.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                if (gunManager != null) gunManager.StartGunSelection();
            });
        }

        // 카메라 위치 및 제한 적용
        if (Camera.main != null)
        {
            CameraController camController = Camera.main.GetComponent<CameraController>();
            if (camController != null)
            {
                camController.SetCameraPoseAndLimits(camSpawn, pLimit, yLimit);
            }
        }

        // Map 2의 경우 생존 타이머 시작
        if (isMap2 && SurvivalTimer.Instance != null)
        {
            SurvivalTimer.Instance.StartTimer(survivalTimeMinutes);
        }
    }

    /// <summary>
    /// 동적으로 생성되어 맵 밖에 흩어져 있는 여우 클론들만 깔끔하게 제거합니다.
    /// (닭과 디코이는 맵 프리팹을 파괴할 때 자동으로 깔끔하게 사라지므로 치울 필요가 없습니다!)
    /// </summary>
    private void CleanUpActiveFoxes()
    {
        if (RaycastShooter.Instance != null) RaycastShooter.Instance.ResetAmmo();

        RandomFoxAnimation[] allFoxes = UnityEngine.Object.FindObjectsByType<RandomFoxAnimation>(UnityEngine.FindObjectsSortMode.None);
        foreach (var foxAnim in allFoxes)
        {
            if (foxAnim != null && foxAnim.gameObject.activeInHierarchy)
            {
                Destroy(foxAnim.gameObject);
            }
        }
    }

    public void ReturnToMap()
    {
        if (inGamePanel != null && inGamePanel.gameObject.activeSelf)
        {
            inGamePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                inGamePanel.gameObject.SetActive(false);
            });
        }

        if (inGamePanel2 != null && inGamePanel2.gameObject.activeSelf)
        {
            inGamePanel2.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                inGamePanel2.gameObject.SetActive(false);
            });
        }

        if (lastActiveStagePanel != null)
        {
            lastActiveStagePanel.gameObject.SetActive(true);
            lastActiveStagePanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        if (BatteryController.Instance != null) BatteryController.Instance.StopBattery();
        if (FoxManager.Instance != null) FoxManager.Instance.StopSpawning();
        if (SurvivalTimer.Instance != null) SurvivalTimer.Instance.StopTimer();

        // 1. 남은 클론 여우들 삭제
        CleanUpActiveFoxes();

        // 2. ★ 맵 프리팹 완전 삭제 (메모리 100% 반환)
        if (currentInstantiatedMap != null)
        {
            Destroy(currentInstantiatedMap);
        }

        if (Camera.main != null)
        {
            CameraController camController = Camera.main.GetComponent<CameraController>();
            if (camController != null) camController.ForceZoomOff();
        }

        Time.timeScale = 1f;
        RenderSettings.fogStartDistance = originalFogStart;
        RenderSettings.fogEndDistance = originalFogEnd;
    }
}