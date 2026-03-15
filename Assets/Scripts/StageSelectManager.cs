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

public class StageSelectManager : MonoBehaviour
{
    [Header("UI Panels")]
    public CanvasGroup mapStagePanel;
    public CanvasGroup inGamePanel;
    public CanvasGroup gunSelectPanel;

    [Header("Stage Configurations")]
    public StageConfig[] stageConfigs;

    [Header("InGame UI References")]
    public TextMeshProUGUI ingameTargetKillCountText;

    [Header("Transition Settings")]
    public float fadeDuration = 0.5f;

    // --- 동적으로 생성될 맵을 추적하는 변수 ---
    private GameObject currentInstantiatedMap;

    private float originalFogStart;
    private float originalFogEnd;

    void Start()
    {
        originalFogStart = RenderSettings.fogStartDistance;
        originalFogEnd = RenderSettings.fogEndDistance;

        // 버튼 이벤트 연결 (람다 캡처 문제 방지를 위해 로컬 변수로 복사 후 전달)
        foreach (StageConfig config in stageConfigs)
        {
            if (config.playButton != null)
            {
                StageConfig capturedConfig = config;
                config.playButton.onClick.AddListener(() => OnPlayStageClicked(capturedConfig));
            }
        }

        if (inGamePanel != null)
        {
            inGamePanel.alpha = 0f;
            inGamePanel.gameObject.SetActive(false);
        }

        if (gunSelectPanel != null)
        {
            gunSelectPanel.alpha = 0f;
            gunSelectPanel.gameObject.SetActive(false);
        }
    }

    // 통째로 받은 Config 데이터를 바탕으로 게임 시작!
    public void OnPlayStageClicked(StageConfig config)
    {
        // 1. 패널 페이드
        if (mapStagePanel != null)
        {
            mapStagePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                mapStagePanel.gameObject.SetActive(false);
            });
        }
        if (inGamePanel != null)
        {
            inGamePanel.gameObject.SetActive(true);
            inGamePanel.DOFade(1f, fadeDuration).SetUpdate(true);
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
        currentInstantiatedMap = Instantiate(config.mapPrefab);

        // 생성된 맵에서 MapInfo 스크립트를 뽑아옵니다.
        MapInfo currentMapInfo = currentInstantiatedMap.GetComponent<MapInfo>();

        // 맵에 있는 디코이들 중 이번 스테이지 규칙에 맞는 녀석들만 켭니다.
        if (currentMapInfo != null)
        {
            currentMapInfo.SetupDecoys(config.activeDecoyNames);
        }

        // =========================================================

        // 탄약 및 무기 세팅
        if (RaycastShooter.Instance != null)
        {
            int ammoLimit = config.maxReloadableAmmo;
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
            ingameTargetKillCountText.text = config.targetKillCount.ToString();
        }

        if (KillCountManager.Instance == null)
            KillCountManager.Instance = UnityEngine.Object.FindFirstObjectByType<KillCountManager>(UnityEngine.FindObjectsInactive.Include);

        if (KillCountManager.Instance != null)
        {
            if (!KillCountManager.Instance.gameObject.activeSelf) KillCountManager.Instance.gameObject.SetActive(true);
            KillCountManager.Instance.InitMission(config.targetKillCount);
        }

        // =========================================================
        // ★ 에러 수정된 스폰 매니저 세팅 (MapInfo가 이름에 맞는 스폰 포인트만 찾아줌)
        // =========================================================
        if (FoxManager.Instance != null && currentMapInfo != null)
        {
            int maxFoxes = config.maxConcurrentFoxes <= 0 ? 1 : config.maxConcurrentFoxes;

            // MapInfo에게 "이번 스테이지 스폰 포인트 이름록 줄 테니까 실제 좌표(Transform)로 바꿔와!" 라고 시킵니다.
            Transform[] currentStageSpawnPoints = currentMapInfo.GetActiveSpawnPoints(config.activeSpawnPointNames);

            FoxManager.Instance.InitStage(config.foxSpawnInterval, currentStageSpawnPoints, maxFoxes, config.ignoreSneakZone);
        }

        // 배터리 세팅
        if (BatteryController.Instance != null)
        {
            float batteryRate = config.batteryDepleteRate <= 0.1f ? 1.0f : config.batteryDepleteRate;
            if (InventoryManager.Instance != null)
            {
                ShopItemData equippedScope = InventoryManager.Instance.GetEquippedScopeData();
                if (equippedScope != null) batteryRate *= equippedScope.batteryEfficiencyMultiplier;
            }
            BatteryController.Instance.InitBatteryRate(batteryRate);
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
                // currentMapInfo가 쥐고 있는 Volume_Start를 뽑아서 넘겨줍니다.
                Volume volToPass = (currentMapInfo != null) ? currentMapInfo.startingVolume : null;
                gunManager.PrepareGunSelection(volToPass);
            }

            gunSelectPanel.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                if (gunManager != null) gunManager.StartGunSelection();
            });
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
        if (inGamePanel != null)
        {
            inGamePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                inGamePanel.gameObject.SetActive(false);
            });
        }

        if (mapStagePanel != null)
        {
            mapStagePanel.gameObject.SetActive(true);
            mapStagePanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        if (BatteryController.Instance != null) BatteryController.Instance.StopBattery();
        if (FoxManager.Instance != null) FoxManager.Instance.StopSpawning();

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