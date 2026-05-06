using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class Map2ResultManager : MonoBehaviour
{
    public static Map2ResultManager Instance;

    [Header("Map 2 Specific Result Panels")]
    public CanvasGroup survivePanel;
    public CanvasGroup villageInvadedPanel;
    public CanvasGroup youDiedPanel;

    [Header("Return to Map Buttons")]
    [Tooltip("Survive 패널의 돌아가기 버튼")]
    public Button surviveReturnButton;
    [Tooltip("VillageInvaded 패널의 돌아가기 버튼")]
    public Button invadedReturnButton;
    [Tooltip("YouDied 패널의 돌아가기 버튼")]
    public Button youDiedReturnButton;

    [Header("Kill Info UI During Gameplay")]
    [Tooltip("몹 처치 시 화면 중앙에 잠깐 뜨는 Kill_Info 오브젝트")]
    public GameObject killInfoPanel;
    [Tooltip("Kill_Info 안의 숫자 표기용 텍스트")]
    public TextMeshProUGUI killInfoPopText;
    [Tooltip("게임 내내 화면구석(HUD)에 표기할 킬 수 텍스트")]
    public TextMeshProUGUI hudKillCountText;
    
    [Tooltip("Kill_Info 팝업이 떠 있는 시간")]
    public float killInfoDisplayDuration = 1.5f;
    private Coroutine killInfoHideCoroutine;

    [Header("Subtitle Objects for Village Invaded")]
    [Tooltip("배터리 방전 시 띄워줄 부연 설명 텍스트 (게임오브젝트)")]
    public GameObject batteryFailSubtitleObj;
    [Tooltip("탄약 고갈 시 띄워줄 부연 설명 텍스트 (게임오브젝트)")]
    public GameObject ammoFailSubtitleObj;

    [Header("Survive Panel UI Settings (Result)")]
    public TextMeshProUGUI surviveKillCountText;
    public TextMeshProUGUI surviveKillGoldText;
    public TextMeshProUGUI surviveStageClearGoldText; // 생존 기본 보상
    public TextMeshProUGUI surviveTotalRewardText;    // 총합

    [Header("Village Invaded Panel UI Settings (Failed)")]
    public TextMeshProUGUI invadedKillCountText;
    public TextMeshProUGUI invadedKillGoldText;
    public TextMeshProUGUI invadedTotalRewardText; 
    
    [Header("You Died Panel UI Settings (Failed)")]
    public TextMeshProUGUI youDiedKillCountText;
    public TextMeshProUGUI youDiedKillGoldText;
    public TextMeshProUGUI youDiedTotalRewardText; 

    [Header("Global Money UI")]
    [Tooltip("현재 내 누적 보유 골드가 표시될 텍스트 (예: Have)")]
    public TextMeshProUGUI currentHaveGoldText;

    [Header("Reward & Visual Settings")]
    [Tooltip("06:00 도달 시 기본으로 주어지는 스테이지 생존 보상금")]
    public int stageClearReward = 500;
    [Tooltip("뮤턴트 1마리 킬당 주어지는 보상금")]
    public int goldPerKill = 50;
    [Tooltip("결과 패널이 부드럽게 나타나는 시간")]
    public float panelFadeDuration = 0.5f;

    private int currentKills = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (survivePanel != null) { survivePanel.alpha = 0f; survivePanel.gameObject.SetActive(false); }
        if (villageInvadedPanel != null) { villageInvadedPanel.alpha = 0f; villageInvadedPanel.gameObject.SetActive(false); }
        if (youDiedPanel != null) { youDiedPanel.alpha = 0f; youDiedPanel.gameObject.SetActive(false); }
        
        // 시작 시 부연설명 및 킬 팝업 꺼두기
        if (batteryFailSubtitleObj != null) batteryFailSubtitleObj.SetActive(false);
        if (ammoFailSubtitleObj != null) ammoFailSubtitleObj.SetActive(false);
        if (killInfoPanel != null) killInfoPanel.SetActive(false);

        // 초기 HUD 세팅
        if (killInfoPopText != null) killInfoPopText.text = currentKills.ToString();
        if (hudKillCountText != null) hudKillCountText.text = currentKills.ToString();

        // 버튼 클릭 이벤트 연결
        if (surviveReturnButton != null) surviveReturnButton.onClick.AddListener(OnReturnButtonClicked);
        if (invadedReturnButton != null) invadedReturnButton.onClick.AddListener(OnReturnButtonClicked);
        if (youDiedReturnButton != null) youDiedReturnButton.onClick.AddListener(OnReturnButtonClicked);
    }

    public void AddKill()
    {
        currentKills++;

        // 누적 킬 수 갱신 (Map 2)
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.AddTotalKill(2);

        // 실시간 HUD 텍스트 업데이트
        if (killInfoPopText != null) killInfoPopText.text = currentKills.ToString();
        if (hudKillCountText != null) hudKillCountText.text = currentKills.ToString();

        // 화면 킬 팝업 띄우기
        if (killInfoPanel != null)
        {
            if (killInfoHideCoroutine != null) StopCoroutine(killInfoHideCoroutine);
            killInfoPanel.SetActive(true);
            killInfoHideCoroutine = StartCoroutine(HideKillInfoAfterDelay());
        }
    }

    private IEnumerator HideKillInfoAfterDelay()
    {
        yield return new WaitForSeconds(killInfoDisplayDuration);
        if (killInfoPanel != null) killInfoPanel.SetActive(false);
        killInfoHideCoroutine = null;
    }

    // ========= Panel Triggers ========= //

    public void ShowSurvivePanel()
    {
        KillCountManager.isGameEnding = true;
        Time.timeScale = 0f;

        int killBonus = currentKills * goldPerKill;
        int totalReward = stageClearReward + killBonus;
        KillCountManager.currentSessionGold += totalReward;

        // UI 텍스트 연결 (맵 1 스타일)
        if (surviveKillCountText != null) surviveKillCountText.text = currentKills.ToString();
        if (surviveKillGoldText != null) surviveKillGoldText.text = $"+ {killBonus:N0}";
        if (surviveStageClearGoldText != null) surviveStageClearGoldText.text = $"+ {stageClearReward:N0}";
        if (surviveTotalRewardText != null) surviveTotalRewardText.text = $"+ {totalReward:N0}";
        
        if (currentHaveGoldText != null) currentHaveGoldText.text = KillCountManager.currentSessionGold.ToString("N0");

        ShowPanel(survivePanel);
    }

    public void ShowVillageInvadedPanel(string reason = "")
    {
        KillCountManager.isGameEnding = true;
        Time.timeScale = 0f;

        if (batteryFailSubtitleObj != null) batteryFailSubtitleObj.SetActive(reason == "배터리가 전소되었습니다.");
        if (ammoFailSubtitleObj != null) ammoFailSubtitleObj.SetActive(reason == "모든 탄약을 소모했습니다.");

        int killBonus = currentKills * goldPerKill;
        int totalReward = killBonus; // 실패시엔 킬 보너스만 획득
        KillCountManager.currentSessionGold += totalReward;

        // UI 텍스트 연결 (맵 1 스타일)
        if (invadedKillCountText != null) invadedKillCountText.text = currentKills.ToString();
        if (invadedKillGoldText != null) invadedKillGoldText.text = $"+ {killBonus:N0}";
        if (invadedTotalRewardText != null) invadedTotalRewardText.text = $"+ {totalReward:N0}";

        if (currentHaveGoldText != null) currentHaveGoldText.text = KillCountManager.currentSessionGold.ToString("N0");

        ShowPanel(villageInvadedPanel);
    }

    public void ShowYouDiedPanel()
    {
        KillCountManager.isGameEnding = true;
        Time.timeScale = 0f;

        int killBonus = currentKills * goldPerKill;
        int totalReward = killBonus; // 실패시엔 킬 보너스만 획득
        KillCountManager.currentSessionGold += totalReward;

        // UI 텍스트 연결 (맵 1 스타일)
        if (youDiedKillCountText != null) youDiedKillCountText.text = currentKills.ToString();
        if (youDiedKillGoldText != null) youDiedKillGoldText.text = $"+ {killBonus:N0}";
        if (youDiedTotalRewardText != null) youDiedTotalRewardText.text = $"+ {totalReward:N0}";

        if (currentHaveGoldText != null) currentHaveGoldText.text = KillCountManager.currentSessionGold.ToString("N0");

        ShowPanel(youDiedPanel);
    }

    // ========= Core Utilities ========= //

    private void ShowPanel(CanvasGroup targetPanel)
    {
        // 맵 2 한 스테이지 최고 킬 수 갱신
        if (PlayerStatsManager.Instance != null)
        {
            PlayerStatsManager.Instance.UpdateMaxKillsInStage(2, currentKills);
        }

        Time.timeScale = 0f;
        AudioListener.pause = true;

        MutantAI[] allMutants = UnityEngine.Object.FindObjectsByType<MutantAI>(UnityEngine.FindObjectsSortMode.None);
        foreach (MutantAI m in allMutants) m.StopAnimation();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (UITransitionHelper.Instance != null)
        {
            UITransitionHelper.Instance.ShowWithTransition(targetPanel);
        }
        else if (targetPanel != null) // fallback
        {
            targetPanel.gameObject.SetActive(true);
            targetPanel.DOFade(1f, panelFadeDuration).SetUpdate(true).OnComplete(() =>
            {
                targetPanel.interactable = true;
                targetPanel.blocksRaycasts = true;
            });
        }
    }

    private void OnReturnButtonClicked()
    {
        StageSelectManager ssm = FindFirstObjectByType<StageSelectManager>();

        if (UITransitionHelper.Instance != null)
        {
            // ★ Dissolve 전환 후 복귀
            UITransitionHelper.Instance.TransitionThen(() =>
            {
                HidePanel(survivePanel);
                HidePanel(villageInvadedPanel);
                HidePanel(youDiedPanel);

                if (ssm != null) ssm.ReturnToMap();

                // ReturnToMap()이 맵 Destroy 후 AudioListener 재개
                // (순서 반대 시 사운드 글리치 발생)
                AudioListener.pause = false;
            });
        }
        else
        {
            // fallback
            HidePanel(survivePanel);
            HidePanel(villageInvadedPanel);
            HidePanel(youDiedPanel);
            if (ssm != null) ssm.ReturnToMap();
            AudioListener.pause = false;
        }
    }

    private void HidePanel(CanvasGroup panel)
    {
        if (panel != null && panel.gameObject.activeSelf)
        {
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.DOFade(0f, panelFadeDuration).SetUpdate(true).OnComplete(() =>
            {
                panel.gameObject.SetActive(false);
            });
        }
    }
}
