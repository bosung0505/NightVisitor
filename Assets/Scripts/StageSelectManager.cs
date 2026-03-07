using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

[System.Serializable]
public struct StageConfig
{
    [Tooltip("해당 스테이지의 플레이 버튼")]
    public Button playButton;
    [Tooltip("이 스테이지의 목표 킬 수")]
    public int targetKillCount;
    [Tooltip("여우가 스폰되는 간격 (초)")]
    public float foxSpawnInterval;
    [Tooltip("이 스테이지에서 여우가 스폰될 위치들 (빈 게임오브젝트 리스트)")]
    public Transform[] spawnPoints;
    [Tooltip("이 스테이지에서 동시에 필드에 존재할 수 있는 최대 여우 마리 수 (기본 1)")]
    public int maxConcurrentFoxes;
    
    [Header("Difficulty Settings")]
    [Tooltip("배터리 소모 속도 배수 (기본 1.0 = 정상 속도, 1.5 = 1.5배 빠름, 2.0 = 2배 빠름)")]
    public float batteryDepleteRate; // 기본값 처리는 하단에서 1.0f로
    [Tooltip("이 스테이지에서 주어지는 총 예비 탄약 수 (음수면 기본 30발 유지)")]
    public int maxReloadableAmmo;
}

public class StageSelectManager : MonoBehaviour
{
    [Header("UI Panels")]
    public CanvasGroup mapStagePanel; // The entire Map window (or Map1_Stage_Panel)
    public CanvasGroup inGamePanel;   // The IN_GAME object/panel
    public CanvasGroup gunSelectPanel; // The Gun_Select_Panel

    [Header("Stage Configurations")]
    [Tooltip("각 스테이지별 설정 (플레이 버튼과 목표 킬 수)을 여기에 추가하세요.")]
    public StageConfig[] stageConfigs;

    [Header("InGame UI References")]
    [Tooltip("InGame_Panel -> Mission_Opened -> Stage_Mission 에 있는 TargetKillCount_Text")]
    public TextMeshProUGUI ingameTargetKillCountText;

    [Header("Transition Settings")]
    public float fadeDuration = 0.5f;

    private GameObject[] chickenPrefabs; // 새 게임 시작 시 원래 닭들을 복원하기 위한 백업
    private GameObject[] decoyPrefabs; // 새 게임 시작 시 원래 디코이 여우들을 복원하기 위한 백업

    void Start()
    {
        // -------------------------------------------------------------
        // 게임 맨 처음 시작 시, 맵에 예쁘게 배치된 원래 닭들을 싹 다 보관해둡니다.
        // IN_GAME 패널이 꺼져있어 비활성화 상태일 수도 있으므로 Inactive까지 전부 스캔합니다.
        // -------------------------------------------------------------
        RandomChickenAnimation[] allChickens = UnityEngine.Object.FindObjectsByType<RandomChickenAnimation>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        
        // Count valid chickens with the "Chicken" tag
        int validCount = 0;
        foreach (var c in allChickens)
        {
            if (c != null && c.CompareTag("Chicken") && !c.name.EndsWith("_PrefabRef")) validCount++;
        }

        chickenPrefabs = new GameObject[validCount];
        int index = 0;
        
        foreach (var c in allChickens)
        {
            if (c != null && c.CompareTag("Chicken") && !c.name.EndsWith("_PrefabRef"))
            {
                // 나중에 죽거나 날아간 닭을 치우고, 원래 위치에 똑같이 되돌려놓기 위해 부모와 위치를 그대로 복제해서 비활성화
                chickenPrefabs[index] = Instantiate(c.gameObject, c.transform.position, c.transform.rotation, c.transform.parent);
                chickenPrefabs[index].SetActive(false);
                chickenPrefabs[index].name = c.gameObject.name + "_PrefabRef";
                index++;
            }
        }

        // -------------------------------------------------------------
        // 게임 맨 처음 시작 시, 맵에 예쁘게 배치된 디코이 여우들을 보관해둡니다.
        // -------------------------------------------------------------
        DecoyFoxAI[] allDecoys = UnityEngine.Object.FindObjectsByType<DecoyFoxAI>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        int validDecoyCount = 0;
        foreach (var d in allDecoys)
        {
            if (d != null && !d.name.EndsWith("_PrefabRef")) validDecoyCount++;
        }

        decoyPrefabs = new GameObject[validDecoyCount];
        int decoyIndex = 0;
        
        foreach (var d in allDecoys)
        {
            if (d != null && !d.name.EndsWith("_PrefabRef"))
            {
                decoyPrefabs[decoyIndex] = Instantiate(d.gameObject, d.transform.position, d.transform.rotation, d.transform.parent);
                decoyPrefabs[decoyIndex].SetActive(false);
                decoyPrefabs[decoyIndex].name = d.gameObject.name + "_PrefabRef";
                decoyIndex++;
            }
        }

        // Add listeners to all play buttons
        foreach(StageConfig config in stageConfigs)
        {
            if (config.playButton != null)
            {
                // 로컬 변수에 복사하여 람다 캡처 문제 방지
                int targetKills = config.targetKillCount;
                float interval = config.foxSpawnInterval;
                Transform[] points = config.spawnPoints;
                int maxConcurrent = config.maxConcurrentFoxes <= 0 ? 1 : config.maxConcurrentFoxes;
                float batteryRate = config.batteryDepleteRate <= 0.1f ? 1.0f : config.batteryDepleteRate; // 0이거나 너무 작으면 1배속
                int ammoLimit = config.maxReloadableAmmo; // 그대로 넣고, -1 같은 음수 처리는 RaycastShooter에서

                config.playButton.onClick.AddListener(() => OnPlayStageClicked(targetKills, interval, points, maxConcurrent, batteryRate, ammoLimit));
            }
        }

        // Initialize states (Assuming we start in Menu/Map, not In-Game)
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

    public void OnPlayStageClicked(int targetKillCount, float spawnInterval, Transform[] spawnPoints, int maxConcurrentFoxes = 1, float batteryRate = 1.0f, int ammoLimit = -1)
    {
        // 1. Fade out the Map/Stage Panel
        if (mapStagePanel != null)
        {
            mapStagePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                mapStagePanel.gameObject.SetActive(false);
            });
        }

        // 2. Fade in the IN_GAME Panel
        if (inGamePanel != null)
        {
            inGamePanel.gameObject.SetActive(true);
            inGamePanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        // --- 완전 새 게임을 위한 환경 초기화 (시체 치우기, 닭 복구, 총알 장전 등) ---
        // 탄약과 배터리가 초기화되기 전에 이번 스테이지 난이도 정보를 전달
        if (RaycastShooter.Instance != null)
        {
            RaycastShooter.Instance.InitAmmoLimit(ammoLimit);
        }

        ResetGameEnvironment();


        // 선택된 스테이지의 목표 킬 수를 InGame UI에 적용
        if (ingameTargetKillCountText != null)
        {
            ingameTargetKillCountText.text = targetKillCount.ToString();
        }

        // Instance가 만약 아직 Awake 처리가 안 되어서 null이라면 직접 강제로 찾아줍니다. (꺼진 오브젝트 포함)
        if (KillCountManager.Instance == null)
        {
            KillCountManager.Instance = UnityEngine.Object.FindFirstObjectByType<KillCountManager>(UnityEngine.FindObjectsInactive.Include);
        }

        if (KillCountManager.Instance != null)
        {
            // Mission 오브젝트 자체가 꺼져있다면 강제로 켜줍니다.
            if (!KillCountManager.Instance.gameObject.activeSelf)
            {
                KillCountManager.Instance.gameObject.SetActive(true);
            }
            KillCountManager.Instance.InitMission(targetKillCount);
            Debug.Log($"Target Kill Set to: {targetKillCount}");
        }
        else
        {
            Debug.LogError("KillCountManager Instance is still null even after FindFirstObjectByType!");
        }

        // 스폰 매니저(FoxManager)에게 스폰 간격과 포인트 전달
        if (FoxManager.Instance != null)
        {
            FoxManager.Instance.InitStage(spawnInterval, spawnPoints, maxConcurrentFoxes);
        }
        else
        {
            Debug.LogWarning("FoxManager Instance is null! 스폰 데이터를 전달하지 못했습니다.");
        }

        // 4. 배터리 상태 초기화 (새로운 스테이지)
        if (BatteryController.Instance != null)
        {
            BatteryController.Instance.InitBatteryRate(batteryRate);
            BatteryController.Instance.ResetBattery();
        }

        // 3. Fade in the Gun Select Panel
        if (gunSelectPanel != null)
        {
            gunSelectPanel.gameObject.SetActive(true);
            
            // 씬이 렌더링되기 전에 즉시 화면을 어둡게(열화상) 바꾸고 시간을 정지합니다. (번쩍임 방지)
            GunSelectManager gunManager = gunSelectPanel.GetComponent<GunSelectManager>();
            if (gunManager != null)
            {
                gunManager.PrepareGunSelection();
            }

            gunSelectPanel.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() => 
            {
                // 총기 선택창이 다 나타나면 카운트다운을 시작하라고 지시
                if (gunManager != null)
                {
                    gunManager.StartGunSelection();
                }
            });
        }
    }

    /// <summary>
    /// 남은 여우 시체 청소, 닭 재생성, 탄약 재장전을 통해 게임을 완전히 새 판으로 셋업합니다.
    /// </summary>
    private void ResetGameEnvironment()
    {
        // 1. 탄약 초기화
        if (RaycastShooter.Instance != null)
        {
            RaycastShooter.Instance.ResetAmmo();
        }

        // 2. 씬에 있는 모든 닭 지우고 백업(프리팹)에서 새로 복원
        // 비활성화된 상태의 닭 시체나 잔해도 있을 수 있으므로 모두 검색
        RandomChickenAnimation[] currentChickens = UnityEngine.Object.FindObjectsByType<RandomChickenAnimation>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach(var c in currentChickens)
        {
            // 방금 스폰된거나 도망가는 닭들 싹 다 제거 (단, 우리가 숨겨둔 백업 프리팹은 건드리지 않음)
            if (c != null && c.CompareTag("Chicken") && !c.gameObject.name.EndsWith("_PrefabRef")) 
            {
                Destroy(c.gameObject);
            }
        }
        
        if (chickenPrefabs != null)
        {
            foreach (var prefab in chickenPrefabs)
            {
                if (prefab != null)
                {
                    // 숨겨둔 프리팹을 기반으로 원래 부모 아래 동일한 위치/회전값으로 생성
                    GameObject newChicken = Instantiate(prefab, prefab.transform.position, prefab.transform.rotation, prefab.transform.parent);
                    newChicken.SetActive(true);
                    newChicken.name = prefab.name.Replace("_PrefabRef", "");
                }
            }
        }

        // 3. 씬에 남아있는 모든 여우 시체 치우기
        // FindObjectsByType을 사용해 현재 활성화된(죽어 자빠져있거나 도망중인) 모든 여우 스크립트를 찾습니다.
        RandomFoxAnimation[] allFoxes = UnityEngine.Object.FindObjectsByType<RandomFoxAnimation>(UnityEngine.FindObjectsSortMode.None);
        foreach (var foxAnim in allFoxes)
        {
            if (foxAnim != null && !foxAnim.gameObject.name.EndsWith("_PrefabRef"))
            {
                Destroy(foxAnim.gameObject);
            }
        }

        // 4. 씬에 남아있는 모든 디코이 지우고 백업(프리팹)에서 새로 복원
        DecoyFoxAI[] currentDecoys = UnityEngine.Object.FindObjectsByType<DecoyFoxAI>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var d in currentDecoys)
        {
            if (d != null && !d.gameObject.name.EndsWith("_PrefabRef"))
            {
                Destroy(d.gameObject);
            }
        }

        if (decoyPrefabs != null)
        {
            foreach (var prefab in decoyPrefabs)
            {
                if (prefab != null)
                {
                    GameObject newDecoy = Instantiate(prefab, prefab.transform.position, prefab.transform.rotation, prefab.transform.parent);
                    newDecoy.SetActive(true);
                    newDecoy.name = prefab.name.Replace("_PrefabRef", "");
                }
            }
        }
        
        Debug.Log("[StageSelectManager] Game Environment completely resetted.");
    }

    /// <summary>
    /// 게임 클리어 혹은 포기 시 로비(맵 선택)로 돌아가기 위해 호출하는 함수
    /// </summary>
    public void ReturnToMap()
    {
        // 1. Fade out the IN_GAME panel
        if (inGamePanel != null)
        {
            inGamePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                inGamePanel.gameObject.SetActive(false);
            });
        }

        // 2. Fade in the Map panel
        if (mapStagePanel != null)
        {
            mapStagePanel.gameObject.SetActive(true);
            mapStagePanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        // 필요한 경우 열화상 카메라 해제, 게임 시간 원복 등 초기화 로직을 이 곳에 추가
        Time.timeScale = 1f; 
    }
}
