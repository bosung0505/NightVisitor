using UnityEngine;
using TMPro; // TextMeshPro를 위한 네임스페이스
using UnityEngine.UI;
using DG.Tweening; // DOTween을 위한 네임스페이스
using System.Collections;

public class KillCountManager : MonoBehaviour
{
    // 싱글톤 패턴으로 어디서든 쉽게 접근 가능하도록 설정
    public static KillCountManager Instance;

    [Header("UI References")]
    [Tooltip("Canvas/InGame_Panel/Mission 안의 'Kill_Info' 게임오브젝트를 할당하세요")]
    public GameObject killInfoPanel;
    
    [Tooltip("킬 추가 시 화면 중앙에 잠깐 뜨는 TextMeshProUGUI 컴포넌트 (Text_KillCount)")]
    public TextMeshProUGUI killCountText;

    [Header("Mission UI References")]
    [Tooltip("InGame_Panel -> Mission_Opened -> Stage_Mission 에 있는 CurrentKillCount_Text")]
    public TextMeshProUGUI currentKillCountText;

    [Tooltip("미션 달성 시 나타날 StageClear 버튼 게임 오브젝트")]
    public GameObject stageClearButtonObj;

    [Tooltip("StageClear 버튼을 눌렀을 때 등장할 MissionClear_Panel (CanvasGroup 추천)")]
    public CanvasGroup missionClearPanel;

    [Tooltip("MissionClear_Panel 내부의 돌아가기 버튼")]
    public Button backToStageButton;

    [Tooltip("배터리 방전 등 게임 실패 시 나타날 MissionFailed_Panel (CanvasGroup 추천)")]
    public CanvasGroup missionFailedPanel;

    [Tooltip("MissionFailed_Panel 내부의 돌아가기(재시도/포기) 버튼")]
    public Button failedBackToStageButton;

    [Header("Reward & Result UI Settings (Clear Panel)")]
    public TextMeshProUGUI clearKillCountText;
    public TextMeshProUGUI clearKillGoldText;
    public TextMeshProUGUI clearStageRewardGoldText; // 스테이지 클리어 기본 보상
    public TextMeshProUGUI clearTotalRewardText;

    [Header("Reward & Result UI Settings (Failed Panel)")]
    public TextMeshProUGUI failKillCountText;
    public TextMeshProUGUI failKillGoldText;
    public TextMeshProUGUI failTotalRewardText;

    [Header("Global Money UI")]
    [Tooltip("현재 내 누적 보유 골드가 표시될 텍스트 (예: Have)")]
    public TextMeshProUGUI currentHaveGoldText;

    [Header("Reward Values (Per Stage)")]
    [Tooltip("여우 1마리 처치 시 획득 골드")]
    public int goldPerKill = 100;
    [Tooltip("스테이지 클리어 시 기본 지급 골드")]
    public int stageClearReward = 500;

    [Header("Settings")]
    [Tooltip("Kill_Info UI가 켜져 있는 시간 (초)")]
    public float displayDuration = 1.5f;
    
    [Tooltip("미션 클리어 패널이 등장하는 데 걸리는 시간")]
    public float panelFadeDuration = 0.5f;

    // 현재 플레이 세션(스테이지)에서 누적 중인 수치들
    private int currentKills = 0;
    
    private int targetKills = 0; // 이번 스테이지의 목표 킬 수

    // 글로벌 누적 골드 (앱을 끄면 날아가는 임시 저장소)
    public static int currentSessionGold = 0;

    private bool isCleared = false; // 클리어 여부 플래그
    private Coroutine hideCoroutine; // 현재 진행중인 숨김 코루틴

    [HideInInspector]
    public bool isFinalStageOfMap1 = false; // 현재 플레이 중인 스테이지가 맵1의 마지막 스테이지인지 여부

    /// <summary>
    /// 점프 공격/마을 침략 등 게임 종료 연출이 시작된 순간부터 true.
    /// CameraController가 이 플래그를 감지해 플레이어 입력을 즉시 차단합니다.
    /// </summary>
    public static bool isGameEnding = false;

    private void Awake()
    {
        // 기존의 파괴(Destroy) 로직 삭제: 
        // 맵 판널마다 각자의 KillCountManager를 안전하게 가질 수 있도록 합니다.
        Instance = this;
    }

    private void OnEnable()
    {
        // 켜진(활성화된) 판넬의 매니저가 메인 인스턴스 권한을 가져옵니다!
        Instance = this;
    }

    private void Start()
    {
        // 시작할 때는 UI가 보이지 않게 꺼둡니다.
        if (killInfoPanel != null)
        {
            killInfoPanel.SetActive(false);
        }
        
        // 텍스트 초기화
        if (killCountText != null) killCountText.text = currentKills.ToString();
        if (currentKillCountText != null) currentKillCountText.text = currentKills.ToString();

        // 클리어 관련 UI 숨기기
        if (stageClearButtonObj != null) stageClearButtonObj.SetActive(false);
        
        if (missionClearPanel != null)
        {
            missionClearPanel.alpha = 0f;
            missionClearPanel.gameObject.SetActive(false);
        }

        if (missionFailedPanel != null)
        {
            missionFailedPanel.alpha = 0f;
            missionFailedPanel.gameObject.SetActive(false);
        }

        // 돌아가기 버튼 이벤트 연결
        if (backToStageButton != null)
        {
            backToStageButton.onClick.AddListener(OnBackToStageClicked);
        }

        if (failedBackToStageButton != null)
        {
            failedBackToStageButton.onClick.AddListener(OnBackToStageClicked);
        }

        // 로비에 있는 StageSelectManager 에서 OnPlayStageClicked 할 때 InitMission()을 호출해줄 예정
    }

    /// <summary>
    /// 스테이지 시작 시 목표 킬 수를 설정하고 초기화하는 함수
    /// StageSelectManager에서 씬 넘어가기 전후로 호출해 줍니다.
    /// </summary>
    public void InitMission(int target)
    {
        currentKills = 0;
        
        targetKills = target;
        isCleared = false;
        isGameEnding = false; // ★ 새 스테이지 시작 시 게임 종료 플래그 리셋

        if (killCountText != null) killCountText.text = currentKills.ToString();
        if (currentKillCountText != null) currentKillCountText.text = currentKills.ToString();

        if (stageClearButtonObj != null) stageClearButtonObj.SetActive(false);
        if (missionClearPanel != null)
        {
            missionClearPanel.alpha = 0f;
            missionClearPanel.gameObject.SetActive(false);
        }

        if (missionFailedPanel != null)
        {
            missionFailedPanel.alpha = 0f;
            missionFailedPanel.gameObject.SetActive(false);
        }

        // TimeScale 복구 (스테이지 재시작용)
        Time.timeScale = 1f;

        // StageClear 버튼 자체에 이벤트가 미리 연결안되어 있다면 여기서 연결
        Button btn = stageClearButtonObj != null ? stageClearButtonObj.GetComponent<Button>() : null;
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ShowMissionClearPanel);
        }
        else if (stageClearButtonObj != null)
        {
            Debug.LogError("[KillCountManager] stageClearButtonObj 로 연결된 게임 오브젝트에 Button 컴포넌트가 없습니다!");
        }
    }

    /// <summary>
    /// 여우를 킬 했을 때 외부(예: RandomFoxAnimation.cs)에서 호출하는 함수
    /// </summary>
    public void AddKill()
    {
        // 이미 깼으면 추가 처리는 안해도 되거나 카운트만 계속 올려도 무방
        // 여기서는 카운트를 계속 올리게 설정
        currentKills++; 
        
        // 누적 킬 수 갱신 (Map 1)
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.AddTotalKill(1);

        Debug.Log($"[KillCountManager] AddKill() called! Current kills: {currentKills} / Target: {targetKills}");

        // UI 텍스트 업데이트
        if (killCountText != null) killCountText.text = currentKills.ToString();
        if (currentKillCountText != null) currentKillCountText.text = currentKills.ToString();

        // 클리어 체크
        if (!isCleared && currentKills >= targetKills)
        {
            isCleared = true;
            OnMissionCleared();
        }
        else if (isCleared)
        {
            Debug.Log("[KillCountManager] Already cleared the mission.");
        }

        // UI 켜기 및 예약된 끄기 코루틴 실행
        if (killInfoPanel != null)
        {
            // 만약 이미 켜져있는 상태에서 또 킬을 했다면 이전 숨김 예약을 취소하고 갱신
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
            
            killInfoPanel.SetActive(true);
            hideCoroutine = StartCoroutine(HideInfoAfterDelay());
        }
    }

    private IEnumerator HideInfoAfterDelay()
    {
        // 정해진 시간만큼 대기
        yield return new WaitForSeconds(displayDuration);

        // 시간 지나면 다시 끕니다
        if (killInfoPanel != null)
        {
            killInfoPanel.SetActive(false);
        }
        
        hideCoroutine = null;
    }

    /// <summary>
    /// 보상 시스템 개편으로 닭 희생 패널티가 삭제되었습니다. (참조 에러 방지를 위해 빈 함수 유지)
    /// </summary>
    public void AddDeadChicken() { }

    /// <summary>
    /// 보상 시스템 개편으로 탄약 소모 패널티가 삭제되었습니다. (참조 에러 방지를 위해 빈 함수 유지)
    /// </summary>
    public void AddConsumedBullet() { }

    private void CalculateAndShowResults(bool isSuccess)
    {
        // 맵 1 한 스테이지 최고 킬 수 갱신
        if (PlayerStatsManager.Instance != null)
        {
            PlayerStatsManager.Instance.UpdateMaxKillsInStage(1, currentKills);
        }

        // 1. 카운트 텍스트 갱신 (Clear & Fail)
        if (clearKillCountText != null) clearKillCountText.text = currentKills.ToString();
        if (failKillCountText != null) failKillCountText.text = currentKills.ToString();

        // 2. 항목별 보상 계산
        int killBonus = currentKills * goldPerKill;
        
        if (clearKillGoldText != null) clearKillGoldText.text = $"+ {killBonus:N0}";
        if (failKillGoldText != null) failKillGoldText.text = $"+ {killBonus:N0}";

        if (clearStageRewardGoldText != null) clearStageRewardGoldText.text = $"+ {stageClearReward:N0}";

        // 3. 총합 보상 계산 (성공하면 클리어 보상 추가, 실패하면 킬 보너스만)
        int totalReward = isSuccess ? (killBonus + stageClearReward) : killBonus;
        
        if (clearTotalRewardText != null) clearTotalRewardText.text = $"+ {totalReward:N0}";
        if (failTotalRewardText != null) failTotalRewardText.text = $"+ {totalReward:N0}";

        // 4. 누적 세션 보유 골드에 합산
        currentSessionGold += totalReward;

        if (currentHaveGoldText != null) currentHaveGoldText.text = currentSessionGold.ToString("N0");
        
        Debug.Log($"[KillCountManager] 정산 완료 - 번 돈: {totalReward}, 현재 가진 돈: {currentSessionGold}");
    }

    private void OnMissionCleared()
    {
        Debug.Log("Mission Cleared! Activating StageClear Button.");
        // 클리어 버튼 활성화
        if (stageClearButtonObj != null)
        {
            // 만약 부모 객체가 꺼져있다면 버튼을 켜도 보이지 않으므로 부모도 확인
            if (stageClearButtonObj.transform.parent != null && !stageClearButtonObj.transform.parent.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[KillCountManager] StageClear_BT 의 부모 오브젝트가 꺼져있어서 버튼이 화면에 안 보일 수 있습니다!");
                stageClearButtonObj.transform.parent.gameObject.SetActive(true);
            }
            stageClearButtonObj.SetActive(true);
        }
        else
        {
            Debug.LogError("[KillCountManager] stageClearButtonObj 랑 연결된 버튼이 없습니다! 인스펙터를 확인해주세요.");
        }
    }

    public void ShowMissionClearPanel()
    {
        Debug.Log("[KillCountManager] ShowMissionClearPanel() Triggered!");

        isGameEnding = true;
        Time.timeScale = 0f;
        CalculateAndShowResults(true); // 성공 (스테이지 보상 포함)

        if (UITransitionHelper.Instance != null)
        {
            UITransitionHelper.Instance.ShowWithTransition(missionClearPanel);
        }
        else if (missionClearPanel != null) // UITransitionHelper 없을 때 fallback
        {
            missionClearPanel.gameObject.SetActive(true);
            missionClearPanel.DOFade(1f, panelFadeDuration).SetUpdate(true).OnComplete(() =>
            {
                missionClearPanel.interactable = true;
                missionClearPanel.blocksRaycasts = true;
            });
        }
    }

    public void ShowMissionFailedPanel()
    {
        Debug.Log("[KillCountManager] ShowMissionFailedPanel() Triggered!");

        if (isCleared)
        {
            Debug.Log("[KillCountManager] Target was already reached. Redirecting to Mission Clear Panel instead.");
            ShowMissionClearPanel();
            return;
        }

        isGameEnding = true;
        Time.timeScale = 0f;
        CalculateAndShowResults(false); // 실패 (킬 보상만)

        if (UITransitionHelper.Instance != null)
        {
            UITransitionHelper.Instance.ShowWithTransition(missionFailedPanel);
        }
        else if (missionFailedPanel != null) // fallback
        {
            missionFailedPanel.gameObject.SetActive(true);
            missionFailedPanel.DOFade(1f, panelFadeDuration).SetUpdate(true).OnComplete(() =>
            {
                missionFailedPanel.interactable = true;
                missionFailedPanel.blocksRaycasts = true;
            });
        }
    }

    private void OnBackToStageClicked()
    {
        Debug.Log("[KillCountManager] Returning to Map.");

        // 맵2 영구 해금 로직 (맵1 마지막 스테이지를 클리어한 경우)
        if (isCleared && isFinalStageOfMap1)
        {
            if (PlayerPrefs.GetInt("Map2_Unlocked", 0) == 0)
            {
                PlayerPrefs.SetInt("Map2_Unlocked", 1);
                PlayerPrefs.SetInt("Map2_JustUnlocked", 1);
                PlayerPrefs.Save();
                Debug.Log("[KillCountManager] 맵2 해금 연출 트리거 활성화 됨!");
            }
        }

        StageSelectManager ssm = FindFirstObjectByType<StageSelectManager>();

        if (UITransitionHelper.Instance != null)
        {
            // ★ Dissolve 전환 후 복귀 (timeScale = 0 상태에서도 작동)
            UITransitionHelper.Instance.TransitionThen(() =>
            {
                // 화면이 가려진 순간 결과창 즉시 비활성화
                if (missionClearPanel != null)
                {
                    missionClearPanel.interactable    = false;
                    missionClearPanel.blocksRaycasts  = false;
                    missionClearPanel.alpha           = 0f;
                    missionClearPanel.gameObject.SetActive(false);
                }
                if (missionFailedPanel != null)
                {
                    missionFailedPanel.interactable   = false;
                    missionFailedPanel.blocksRaycasts = false;
                    missionFailedPanel.alpha          = 0f;
                    missionFailedPanel.gameObject.SetActive(false);
                }

                if (ssm != null) ssm.ReturnToMap();
                else Debug.LogError("StageSelectManager를 찾을 수 없습니다.");
            });
        }
        else
        {
            // fallback: 전환 없이 즉시 복귀
            if (missionClearPanel != null)  { missionClearPanel.alpha = 0f;  missionClearPanel.gameObject.SetActive(false); }
            if (missionFailedPanel != null) { missionFailedPanel.alpha = 0f; missionFailedPanel.gameObject.SetActive(false); }
            if (ssm != null) ssm.ReturnToMap();
            else Debug.LogError("StageSelectManager를 찾을 수 없습니다.");
        }
    }
}
