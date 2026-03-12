using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FoxManager : MonoBehaviour
{
    public static FoxManager Instance;

    [Tooltip("List of Fox GameObjects in order of appearance.")]
    public GameObject[] foxes;
    
    // 이 값들은 기존의 고정값을 유지하되, InitStage가 호출되면 덮어씌워집니다.
    [Tooltip("기본 스폰 간격 (InitStage로 덮어씌워질 수 있음)")]
    public float currentSpawnInterval = 2.0f;
    
    // 스테이지에서 받아온 커스텀 스폰 위치
    private Transform[] currentSpawnPoints;
    private int currentSpawnPointIndex = 0; // 순차적으로 스폰하기 위한 인덱스
    [Tooltip("현재 스테이지의 동시 활성화 여우 수 제한")]
    public int currentMaxConcurrentFoxes = 1;

    [Tooltip("현재 스테이지에서 여우가 Sneak 과정을 무시하는지 여부")]
    public bool currentIgnoreSneakZone = false;

    [Tooltip("현재 필드에 활성화되어 있는 여우들")]
    public List<GameObject> activeFoxes = new List<GameObject>();

    private int currentFoxIndex = 0;
    private bool isWaitingForNext = false;
    private bool stageStarted = false;
    
    // We will store the original prefab/instantiated object information to clone them later if they are destroyed.
    private GameObject[] foxPrefabs;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeFoxPrefabs();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeFoxPrefabs()
    {
        if (foxPrefabs != null) return;

        // Save references to the original GameObjects to act as our pseudo-prefabs.
        foxPrefabs = new GameObject[foxes.Length];
        for (int i = 0; i < foxes.Length; i++)
        {
            if (foxes[i] != null)
            {
                // Instantiate a hidden copy to serve as a clean prefab for respawning
                // We save their EXACT start position and rotation here
                foxPrefabs[i] = Instantiate(foxes[i], foxes[i].transform.position, foxes[i].transform.rotation);
                foxPrefabs[i].SetActive(false);
                foxPrefabs[i].name = foxes[i].name + "_PrefabRef";
                
                // 첫 여우를 미리 켜지 않고, InitStage가 호출될 때까지 대기합니다.
                foxes[i].SetActive(false);
            }
        }
    }

    public void InitStage(float spawnInterval, Transform[] customSpawnPoints, int maxConcurrent = 1, bool ignoreSneakZone = false)
    {
        currentSpawnInterval = spawnInterval;
        currentSpawnPoints = customSpawnPoints;
        currentSpawnPointIndex = 0; // 처음 스폰 포인트부터 시작
        currentMaxConcurrentFoxes = maxConcurrent <= 0 ? 1 : maxConcurrent; // 0이하 방지
        currentIgnoreSneakZone = ignoreSneakZone;
        
        Debug.Log($"[FoxManager] InitStage: interval={spawnInterval}, points count={(currentSpawnPoints != null ? currentSpawnPoints.Length : 0)}, maxConcurrent={currentMaxConcurrentFoxes}, ignoreSneakZone={currentIgnoreSneakZone}");

        // 이전에 남아있던 여우들이 있다면 제거 (구 배열 참조)
        // [수정] foxes 배열 원본 오브젝트들을 무조건 Destroy하면 다음 생성 때 에러가 날 수 있습니다. (원본이 파괴됨)
        // 원본 배열은 비활성화만 유지하고 냅둡니다.
        for (int i = 0; i < foxes.Length; i++)
        {
            if (foxes[i] != null)
            {
                foxes[i].SetActive(false); // 무조건 끄기만 함
            }
        }
        
        // 새로 관리할 활성화 리스트는 인스턴스화된 클론들이므로 완전히 파괴하고 지워줍니다.
        foreach (var fox in activeFoxes)
        {
            if (fox != null) Destroy(fox);
        }
        activeFoxes.Clear();

        currentFoxIndex = -1; // 다음 스폰 때 0으로 됨
        stageStarted = true;
        StopAllCoroutines();

        // 즉시 첫 번째 여우 스폰 시작
        StartCoroutine(SpawnNextFoxRoutine(true));
    }

    public void StopSpawning()
    {
        stageStarted = false;
        StopAllCoroutines();
        Debug.Log("[FoxManager] Spawning stopped.");
    }

    void Update()
    {
        if (!stageStarted || foxPrefabs == null || foxPrefabs.Length == 0) return;

        // 1. 활성화 리스트 청소 (죽거나 완전히 사라진(null) 여우는 목록에서 제거)
        // 리스트에서 지울 때는 뒤에서부터 지워야 인덱스가 꼬이지 않습니다.
        for (int i = activeFoxes.Count - 1; i >= 0; i--)
        {
            GameObject fox = activeFoxes[i];
            if (fox == null)
            {
                activeFoxes.RemoveAt(i);
            }
            else
            {
                RandomFoxAnimation anim = fox.GetComponent<RandomFoxAnimation>();
                // 여우가 죽어서 바닥에 누워있다면 자리 하나를 내어줍니다.
                if (anim != null && anim.currentState == RandomFoxAnimation.FoxState.Dead)
                {
                    activeFoxes.RemoveAt(i);
                }
            }
        }

        // 2. 스폰 조건 확인: 현재 활성화된 마릿수가 허용치보다 적고, 스폰 대기 중이 아니라면 충원!
        if (activeFoxes.Count < currentMaxConcurrentFoxes && !isWaitingForNext)
        {
            StartCoroutine(SpawnNextFoxRoutine(false));
        }
    }

    private IEnumerator SpawnNextFoxRoutine(bool immediate = false)
    {
        isWaitingForNext = true;
        
        if (!immediate)
        {
            // 새로 적용된 스테이지별 스폰 간격 대기
            yield return new WaitForSeconds(currentSpawnInterval);
        }
        
        currentFoxIndex++;

        // --- Loop back to 0 if we passed the end ---
        if (currentFoxIndex >= foxPrefabs.Length)
        {
            currentFoxIndex = 0;
            Debug.Log("FoxManager: Last fox reached, looping back to the first fox.");
        }

        // --- Always revive by Instantiating a BRAND NEW copy from the prefab ---
        if (foxPrefabs[currentFoxIndex] != null)
        {
            // 스폰 포인트 결정: StageSelectManager에서 넘어온 포인트 배열에서 순차적으로 하나씩
            Vector3 spawnPos = foxPrefabs[currentFoxIndex].transform.position;
            Quaternion spawnRot = foxPrefabs[currentFoxIndex].transform.rotation;

            if (currentSpawnPoints != null && currentSpawnPoints.Length > 0)
            {
                if (currentSpawnPoints[currentSpawnPointIndex] != null)
                {
                    spawnPos = currentSpawnPoints[currentSpawnPointIndex].position;
                    spawnRot = currentSpawnPoints[currentSpawnPointIndex].rotation;
                }
                
                // 다음 번엔 다음 스폰 포인트를 쓰도록 인덱스 증가 (배열 끝에 도달하면 0으로 뺑뺑이)
                currentSpawnPointIndex = (currentSpawnPointIndex + 1) % currentSpawnPoints.Length;
            }

            // We overwrite the array index with a newly spawned fox at the calculated coordinates.
            GameObject newlySpawnedFox = Instantiate(foxPrefabs[currentFoxIndex], spawnPos, spawnRot);
            newlySpawnedFox.name = foxPrefabs[currentFoxIndex].name.Replace("_PrefabRef", "");
            
            // --- [신규 로직] 스폰된 여우에게 현재 스테이지의 SneakZone 무시 옵션을 전달 ---
            RandomFoxAnimation foxAnim = newlySpawnedFox.GetComponent<RandomFoxAnimation>();
            if (foxAnim != null)
            {
                foxAnim.ignoreSneakZone = currentIgnoreSneakZone;
            }

            // Activate the new fox
            newlySpawnedFox.SetActive(true);
            
            // 관리 리스트에 추가
            activeFoxes.Add(newlySpawnedFox);
        }

        isWaitingForNext = false;
    }
}
