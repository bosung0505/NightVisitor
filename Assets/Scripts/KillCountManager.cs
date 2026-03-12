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
    [Tooltip("각 결과 패널 안의 'DayResult -> Coyote_eliminated -> Count' 텍스트")]
    public TextMeshProUGUI resultFoxCountText;
    public TextMeshProUGUI resultFoxGoldText;  // Earned_Gold 텍스트

    [Tooltip("각 결과 패널 안의 'DayResult -> Dead_Chicken -> Count' 텍스트")]
    public TextMeshProUGUI resultChickenCountText;
    public TextMeshProUGUI resultChickenGoldText; // Earned_Gold 텍스트

    [Tooltip("각 결과 패널 안의 'DayResult -> Consumed_Bullet -> Count' 텍스트")]
    public TextMeshProUGUI resultBulletCountText;
    public TextMeshProUGUI resultBulletGoldText;  // Earned_Gold 텍스트

    [Tooltip("총 합산 보상이 표시될 텍스트 (예: Total Reward)")]
    public TextMeshProUGUI totalRewardText;

    [Header("Reward & Result UI Settings (Failed Panel)")]
    [Tooltip("실패 패널 전용 텍스트들 (Clear Panel과 동일하게 연결해주세요)")]
    public TextMeshProUGUI failFoxCountText;
    public TextMeshProUGUI failFoxGoldText;
    public TextMeshProUGUI failChickenCountText;
    public TextMeshProUGUI failChickenGoldText;
    public TextMeshProUGUI failBulletCountText;
    public TextMeshProUGUI failBulletGoldText;
    public TextMeshProUGUI failTotalRewardText;

    [Header("Global Money UI")]
    [Tooltip("현재 내 누적 보유 골드가 표시될 텍스트 (예: Have)")]
    public TextMeshProUGUI currentHaveGoldText;

    [Header("Reward Values (Per Stage)")]
    [Tooltip("여우 1마리 처치 시 획득 골드")]
    public int goldPerKill = 100;
    [Tooltip("닭 1마리 희생 시 차감 골드")]
    public int penaltyPerDeadChicken = 30;
    [Tooltip("탄창 1개(5발) 소모 시 차감 골드")]
    public int penaltyPerMagazine = 5;

    [Header("Settings")]
    [Tooltip("Kill_Info UI가 켜져 있는 시간 (초)")]
    public float displayDuration = 1.5f;
    
    [Tooltip("미션 클리어 패널이 등장하는 데 걸리는 시간")]
    public float panelFadeDuration = 0.5f;

    // 현재 플레이 세션(스테이지)에서 누적 중인 수치들
    private int currentKills = 0;
    private int deadChickens = 0;
    private int consumedBullets = 0;
    
    private int targetKills = 0; // 이번 스테이지의 목표 킬 수

    // 글로벌 누적 골드 (앱을 끄면 날아가는 임시 저장소)
    public static int currentSessionGold = 0;

    private bool isCleared = false; // 클리어 여부 플래그
    private Coroutine hideCoroutine; // 현재 진행중인 숨김 코루틴

    private void Awake()
    {
        // 간단한 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
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
        deadChickens = 0;
        consumedBullets = 0;
        
        targetKills = target;
        isCleared = false;

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
    /// 닭이 희생되었을 때 (여우에게 잡히거나 플레이어 오발) 호출
    /// </summary>
    public void AddDeadChicken()
    {
        deadChickens++;
        Debug.Log($"[KillCountManager] 닭 사망! 현재 누적 죽은 닭: {deadChickens}");
    }

    /// <summary>
    /// 플레이어가 총알을 쏠 때 호출
    /// </summary>
    public void AddConsumedBullet()
    {
        consumedBullets++;
    }

    private void CalculateAndShowResults()
    {
        // 1. 카운트 텍스트 갱신 (Clear & Fail)
        if (resultFoxCountText != null) resultFoxCountText.text = currentKills.ToString();
        if (failFoxCountText != null) failFoxCountText.text = currentKills.ToString();

        if (resultChickenCountText != null) resultChickenCountText.text = deadChickens.ToString();
        if (failChickenCountText != null) failChickenCountText.text = deadChickens.ToString();

        if (resultBulletCountText != null) resultBulletCountText.text = consumedBullets.ToString();
        if (failBulletCountText != null) failBulletCountText.text = consumedBullets.ToString();

        // 2. 항목별 획득/차감 골드 계산
        int foxGold = currentKills * goldPerKill;
        int chickenPenalty = deadChickens * penaltyPerDeadChicken;
        
        // 총알은 5발(1탄창)당 차감 (소수점 버림)
        int magazinesUsed = consumedBullets / 5;
        int bulletPenalty = magazinesUsed * penaltyPerMagazine;

        // 3. UI에 개별 획득 골드를 형식에 맞게 텍스트로 표기 (예: "+ 500", "- 30")
        if (resultFoxGoldText != null) resultFoxGoldText.text = $"+ {foxGold:N0}";
        if (failFoxGoldText != null) failFoxGoldText.text = $"+ {foxGold:N0}";

        if (resultChickenGoldText != null) resultChickenGoldText.text = $"- {chickenPenalty:N0}";
        if (failChickenGoldText != null) failChickenGoldText.text = $"- {chickenPenalty:N0}";

        if (resultBulletGoldText != null) resultBulletGoldText.text = $"- {bulletPenalty:N0}";
        if (failBulletGoldText != null) failBulletGoldText.text = $"- {bulletPenalty:N0}";

        // 4. 총합 보상 계산
        int totalReward = foxGold - chickenPenalty - bulletPenalty;
        string totalRewardStr = totalReward >= 0 ? $"+ {totalReward:N0}" : $"- {Mathf.Abs(totalReward):N0}";
        
        if (totalRewardText != null) totalRewardText.text = totalRewardStr;
        if (failTotalRewardText != null) failTotalRewardText.text = totalRewardStr;

        // 5. 누적 세션 보유 골드에 합산
        currentSessionGold += totalReward;
        if (currentSessionGold < 0) currentSessionGold = 0; // 보유 자산 마이너스 방지

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
        
        // 미션 클리어 시 인게임 진행(적 움직임, 탄약, 시간 등)을 모두 정지합니다.
        Time.timeScale = 0f;

        // 골드 계산 및 결과창 텍스트 세팅
        CalculateAndShowResults();

        if (missionClearPanel != null)
        {
            missionClearPanel.gameObject.SetActive(true);
            missionClearPanel.DOFade(1f, panelFadeDuration).SetUpdate(true).OnComplete(() => 
            {
                missionClearPanel.interactable = true;
                missionClearPanel.blocksRaycasts = true;
            });
        }
        else
        {
            Debug.LogError("[KillCountManager] missionClearPanel 이 할당되지 않았습니다!");
        }
    }

    public void ShowMissionFailedPanel()
    {
        Debug.Log("[KillCountManager] ShowMissionFailedPanel() Triggered!");
        
        // 만약 이미 미션 목표를 달성한 상태라면, 자원 고갈 시 실패가 아니라 클리어로 간주합니다.
        if (isCleared)
        {
            Debug.Log("[KillCountManager] Target was already reached. Redirecting to Mission Clear Panel instead.");
            ShowMissionClearPanel();
            return;
        }

        // 미션 실패 시 인게임 진행(적 움직임, 탄약, 시간 등)을 모두 정지합니다.
        Time.timeScale = 0f;

        // 골드 계산 및 결과창 텍스트 세팅 (실패 시에도 집계하여 보여줌)
        CalculateAndShowResults();

        if (missionFailedPanel != null)
        {
            missionFailedPanel.gameObject.SetActive(true);
            missionFailedPanel.DOFade(1f, panelFadeDuration).SetUpdate(true).OnComplete(() => 
            {
                missionFailedPanel.interactable = true;
                missionFailedPanel.blocksRaycasts = true;
            });
        }
        else
        {
            Debug.LogError("[KillCountManager] missionFailedPanel 이 할당되지 않았습니다!");
        }
    }

    private void OnBackToStageClicked()
    {
        Debug.Log("[KillCountManager] Returning to Map. Hiding Mission Clear Panel.");
        
        // 미션 클리어 패널을 다시 숨깁니다.
        if (missionClearPanel != null)
        {
            missionClearPanel.interactable = false;
            missionClearPanel.blocksRaycasts = false;
            missionClearPanel.DOFade(0f, panelFadeDuration).SetUpdate(true).OnComplete(() =>
            {
                missionClearPanel.gameObject.SetActive(false);
            });
        }

        if (missionFailedPanel != null)
        {
            missionFailedPanel.interactable = false;
            missionFailedPanel.blocksRaycasts = false;
            missionFailedPanel.DOFade(0f, panelFadeDuration).SetUpdate(true).OnComplete(() =>
            {
                missionFailedPanel.gameObject.SetActive(false);
            });
        }

        // 맵으로 돌아갑니다. StageSelectManager의 인스턴스를 찾아서 복귀 로직을 수행합니다.
        StageSelectManager ssm = FindFirstObjectByType<StageSelectManager>();
        if (ssm != null)
        {
            ssm.ReturnToMap();
        }
        else
        {
            Debug.LogError("StageSelectManager를 찾을 수 없습니다.");
        }
    }
}
