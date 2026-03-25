using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class BatteryController : MonoBehaviour
{
    public static BatteryController Instance;

    [Header("UI Battery Counts (순서대로 Count3, Count2, Count1)")]
    [Tooltip("인덱스 0에는 남은 60~40초 상태(Count3), 인덱스 1에는 40~20초 상태(Count2)를 넣어주세요. (꺼질 순서대로)")]
    public GameObject[] batteryCounts; // 보통 Count3, Count2 순서로 배열에 드래그 앤 드롭
    
    [Tooltip("마지막에 남을 Count1 오브젝트를 여기에 넣어주세요.")]
    public GameObject lastBatteryCount; // Count1

    [Tooltip("배터리 케이스 오브젝트 (방전 시 깜빡임 용도)")]
    public GameObject batteryCase;
    
    [Header("Volume Transition Settings")]
    public Volume thermalVolume; // Global Volume (열화상)
    public Volume normalVolume;  // Volume1 (일반화상)
    
    [Tooltip("볼륨이 서서히 바뀌는데 걸리는 시간 (초)")]
    public float transitionDuration = 2.0f;
    
    [Header("Timer Settings")]
    public float depleteInterval = 20f;
    
    [Header("Game Over Settings")]
    [Tooltip("빈 배터리 시 케이스가 깜빡이는 간격")]
    public float blinkInterval = 0.5f;
    [Tooltip("깜빡이기 시작한 후 게임 오버 패널이 뜨기까지의 대기 시간")]
    public float gameOverDelay = 3.0f;
    
    private float timer = 0f;
    private int currentDepleteIndex = 0;
    private bool isCount1Depleted = false;
    private bool isGameOver = false;

    // 추가: 이번 스테이지의 배터리 소모 배수
    private float currentDepleteRate = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        // 시작 시 초기 Weight 설정 보장
        if (thermalVolume != null) thermalVolume.weight = 1f;
        if (normalVolume != null) normalVolume.weight = 0f;
    }

    public void InitBatteryRate(float rate)
    {
        currentDepleteRate = rate > 0 ? rate : 1.0f;
    }

    public void SetVolumes(Volume tVol, Volume nVol)
    {
        if (tVol != null) thermalVolume = tVol;
        if (nVol != null) normalVolume = nVol;
    }

    public void ResetBattery()
    {
        StopAllCoroutines();
        
        timer = 0f;
        currentDepleteIndex = 0;
        isCount1Depleted = false;
        isGameOver = false;

        // 모든 배터리 UI 다시 켜기
        if (batteryCounts != null)
        {
            foreach (GameObject count in batteryCounts)
            {
                if (count != null) count.SetActive(true);
            }
        }

        if (lastBatteryCount != null) lastBatteryCount.SetActive(true);
        if (batteryCase != null) batteryCase.SetActive(true);

        // 볼륨 초기화
        if (thermalVolume != null)
        {
            thermalVolume.gameObject.SetActive(true);
            thermalVolume.weight = 1f;
        }
        if (normalVolume != null)
        {
            normalVolume.gameObject.SetActive(true);
            normalVolume.weight = 0f;
        }
        
        Debug.Log("[BatteryController] Battery and Volumes Reset!");
    }

    public void StopBattery()
    {
        isGameOver = true;
        StopAllCoroutines();
        // 잔류하는 열화상 등 혹시 모를 볼륨 효과 즉시 차단
        if (thermalVolume != null) thermalVolume.weight = 0f;
        if (normalVolume != null) normalVolume.weight = 1f;
        
        Debug.Log("[BatteryController] Battery processing stopped (Returned to Map).");
    }

    void Update()
    {
        // 이미 게임 오버이거나 처리가 끝났으면 중지
        if (isGameOver) return;

        // 배수를 곱해서 타이머를 가속/감속합니다
        timer += Time.deltaTime * currentDepleteRate;

        // depleteInterval 마다 배터리 칸 소모
        if (timer >= depleteInterval)
        {
            timer = 0f;
            DepleteBattery();
        }
    }

    private void DepleteBattery()
    {
        // 1. 일반 배열 (Count3, Count2) 끄기
        if (currentDepleteIndex < batteryCounts.Length)
        {
            if (batteryCounts[currentDepleteIndex] != null)
            {
                batteryCounts[currentDepleteIndex].SetActive(false);
                Debug.Log($"Battery Depleted: {batteryCounts[currentDepleteIndex].name}");
            }
            currentDepleteIndex++;

            // 방금 꺼서 Count1만 남게 되었다면 볼륨 전환 시작!
            if (currentDepleteIndex >= batteryCounts.Length)
            {
                Debug.Log("Battery Low! Transitioning visual modes...");
                StartCoroutine(TransitionToNormalVolume());
            }
        }
        // 2. 이미 Count1만 남은 상태(볼륨 전환 중이거나 끝남)에서 타이머가 또 돌았다면 Count1 마저 끔
        else if (!isCount1Depleted)
        {
            isCount1Depleted = true;
            if (lastBatteryCount != null)
            {
                lastBatteryCount.SetActive(false);
            }
            Debug.Log("Battery Empty! Blinking case...");
            
            // 배터리 케이스 깜빡임 및 게임 오버 타이머 시작
            StartCoroutine(BatteryEmptyGameOverRoutine());
        }
    }

    private IEnumerator BatteryEmptyGameOverRoutine()
    {
        float elapsed = 0f;
        bool caseActive = true;
        
        // gameOverDelay 시간 동안 blinkInterval 주기로 번갈아가며 깜빡임
        while (elapsed < gameOverDelay)
        {
            if (batteryCase != null)
            {
                caseActive = !caseActive;
                batteryCase.SetActive(caseActive);
            }
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        // 시간이 다 되면 케이스 꺼버리기 (원치 않으시면 지워도 됩니다)
        if (batteryCase != null) batteryCase.SetActive(false);

        isGameOver = true;
        
        Debug.Log("Time over! Triggering Mission Failed...");
        
        // KillCountManager의 실패 패널 띄우기 함수 호출
        if (KillCountManager.Instance != null)
        {
            KillCountManager.Instance.ShowMissionFailedPanel();
        }
    }

    private IEnumerator TransitionToNormalVolume()
    {
        // Volume1 활성화 (만약 꺼져있을 경우를 대비)
        if (normalVolume != null && !normalVolume.gameObject.activeInHierarchy)
        {
            normalVolume.gameObject.SetActive(true);
        }

        float elapsedTime = 0f;

        // transitionDuration 초 동안 부드럽게 크로스페이드 (Lerp)
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / transitionDuration;

            if (thermalVolume != null)
                thermalVolume.weight = Mathf.Lerp(1f, 0f, t);
                
            if (normalVolume != null)
                normalVolume.weight = Mathf.Lerp(0f, 1f, t);

            yield return null; // 다음 프레임까지 대기
        }

        // 혹시 모를 오차를 위해 마지막에 확실하게 0과 1로 고정
        if (thermalVolume != null) 
        {
            thermalVolume.weight = 0f;
            thermalVolume.gameObject.SetActive(false); // 완전히 끔으로 처리비용 절약
        }
        if (normalVolume != null) normalVolume.weight = 1f;

        Debug.Log("Volume transition complete.");
    }
}
