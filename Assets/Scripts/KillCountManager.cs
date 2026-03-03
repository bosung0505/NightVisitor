using UnityEngine;
using TMPro; // TextMeshPro를 위한 네임스페이스
using System.Collections;

public class KillCountManager : MonoBehaviour
{
    // 싱글톤 패턴으로 어디서든 쉽게 접근 가능하도록 설정
    public static KillCountManager Instance;

    [Header("UI References")]
    [Tooltip("Canvas/InGame_Panel/Mission 안의 'Kill_Info' 게임오브젝트를 할당하세요")]
    public GameObject killInfoPanel;
    
    [Tooltip("킬 수가 표시될 TextMeshProUGUI 컴포넌트 (Text_KillCount)")]
    public TextMeshProUGUI killCountText;

    [Header("Settings")]
    [Tooltip("Kill_Info UI가 켜져 있는 시간 (초)")]
    public float displayDuration = 1.5f;

    private int currentKills = 0;
    private Coroutine hideCoroutine; // 현재 진행중인 숨김 코루틴

    private void Awake()
    {
        // 간단한 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
        }
        else
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
        if (killCountText != null)
        {
            killCountText.text = currentKills.ToString();
        }
    }

    /// <summary>
    /// 여우를 킬 했을 때 외부(예: RandomFoxAnimation.cs)에서 호출하는 함수
    /// </summary>
    public void AddKill()
    {
        currentKills++; // 킬 카운트 1 증가
        Debug.Log($"[KillCountManager] AddKill() called! Current kills: {currentKills}");

        // UI 텍스트 업데이트
        if (killCountText != null)
        {
            killCountText.text = currentKills.ToString();
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
}
