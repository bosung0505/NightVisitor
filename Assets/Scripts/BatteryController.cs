using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class BatteryController : MonoBehaviour
{
    [Header("UI Battery Counts (순서대로 Count3, Count2, Count1)")]
    [Tooltip("인덱스 0에는 남은 60~40초 상태(Count3), 인덱스 1에는 40~20초 상태(Count2)를 넣어주세요. (꺼질 순서대로)")]
    public GameObject[] batteryCounts; // 보통 Count3, Count2 순서로 배열에 드래그 앤 드롭
    
    [Header("Volume Transition Settings")]
    public Volume thermalVolume; // Global Volume (열화상)
    public Volume normalVolume;  // Volume1 (일반화상)
    
    [Tooltip("볼륨이 서서히 바뀌는데 걸리는 시간 (초)")]
    public float transitionDuration = 2.0f;
    
    [Header("Timer Settings")]
    public float depleteInterval = 20f;
    
    private float timer = 0f;
    private int currentDepleteIndex = 0;
    private bool isTransitioning = false;

    void Start()
    {
        // 시작 시 초기 Weight 설정 보장
        if (thermalVolume != null) thermalVolume.weight = 1f;
        if (normalVolume != null) normalVolume.weight = 0f;
    }

    void Update()
    {
        // 이미 렌더링이 완전히 전환되었거나, 남은 배터리가 없으면 멈춤
        if (isTransitioning || currentDepleteIndex >= batteryCounts.Length) return;

        timer += Time.deltaTime;

        // 20초마다 배터리 칸 소모
        if (timer >= depleteInterval)
        {
            timer = 0f;
            DepleteBattery();
        }
    }

    private void DepleteBattery()
    {
        // 현재 꺼야 할 배터리 칸을 끕니다 (예: Count3 비활성화)
        if (batteryCounts[currentDepleteIndex] != null)
        {
            batteryCounts[currentDepleteIndex].SetActive(false);
            Debug.Log($"Battery Depleted: {batteryCounts[currentDepleteIndex].name}");
        }
        
        currentDepleteIndex++;

        // 배열에 넣은 모든 칸(Count3, Count2)이 다 꺼지고 마지막 칸(Count1)만 남았을 때
        if (currentDepleteIndex >= batteryCounts.Length)
        {
            Debug.Log("Battery Low! Transitioning visual modes...");
            StartCoroutine(TransitionToNormalVolume());
        }
    }

    private IEnumerator TransitionToNormalVolume()
    {
        isTransitioning = true;
        
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
