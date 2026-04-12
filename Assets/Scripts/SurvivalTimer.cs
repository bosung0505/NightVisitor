using UnityEngine;
using TMPro;

public class SurvivalTimer : MonoBehaviour
{
    public static SurvivalTimer Instance;

    [Header("UI References")]
    [Tooltip("시간을 표시할 텍스트 (예: 01, 02)")]
    public TextMeshProUGUI timeHourText;
    [Tooltip("분을 표시할 텍스트 (예: 00, 30)")]
    public TextMeshProUGUI timeMinuteText;

    private int currentHour = 1;
    private float currentMinute = 0f;
    private float timeMultiplier = 1f; // 현실 1초당 차오르는 분의 속도
    private bool isRunning = false;
    private bool isCleared = false;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    /// <summary>
    /// 생존 타이머 시작 (realSurvivalMinutes: 목표 생존 시간(분))
    /// </summary>
    public void StartTimer(float realSurvivalMinutes)
    {
        currentHour = 1;
        currentMinute = 0f;
        isCleared = false;

        // 현실 target 시간(분)을 바탕으로, 타이머(5시간 = 300분)가 차오르는 배율 계산
        // 5 게임 시간(Game Hours) = 300 게임 분(Game Minutes).
        // 만약 realSurvivalMinutes 가 5라면 대상 현실 시간은 300초. 
        // 1초당 1게임 분이 올라야 하므로 multiplier = 300 / 300 = 1.
        // 만약 realSurvivalMinutes 가 4라면 대상 현실 시간은 240초.
        // 1초당 (300 / 240) = 1.25 게임 분이 올라야 함.
        
        if (realSurvivalMinutes <= 0.1f) 
            realSurvivalMinutes = 5f; // 기본값 방어 (0 이하일 경우 5분으로 고정)

        float totalRealSeconds = realSurvivalMinutes * 60f;
        timeMultiplier = 300f / totalRealSeconds; 

        UpdateUIText();
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    private void Update()
    {
        if (!isRunning || isCleared) return;

        // TimeScale의 영향을 받는 deltaTime 사용 (정지 시 시간 멈춤)
        currentMinute += Time.deltaTime * timeMultiplier;

        if (currentMinute >= 60f)
        {
            currentMinute -= 60f;
            currentHour++;
            
            // 06:00 도달 검사
            if (currentHour >= 6)
            {
                currentHour = 6;
                currentMinute = 0f;
                TriggerClear();
            }
            else
            {
                // 시간이 바뀌었으므로 UI만 업데이트
                UpdateUIText();
            }
        }
        else
        {
            // 분만 업데이트
            if (timeMinuteText != null)
                timeMinuteText.text = Mathf.FloorToInt(currentMinute).ToString("00");
        }
    }

    private void UpdateUIText()
    {
        if (timeHourText != null)
            timeHourText.text = currentHour.ToString("00");

        if (timeMinuteText != null)
            timeMinuteText.text = Mathf.FloorToInt(currentMinute).ToString("00");
    }

    private void TriggerClear()
    {
        isCleared = true;
        isRunning = false;

        UpdateUIText(); // 정확히 06:00 으로 텍스트 고정
        
        if (Map2ResultManager.Instance != null)
        {
            Map2ResultManager.Instance.ShowSurvivePanel();
        }
    }
}
