using UnityEngine;
using System.Collections;

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

    public void InitStage(float spawnInterval, Transform[] customSpawnPoints)
    {
        currentSpawnInterval = spawnInterval;
        currentSpawnPoints = customSpawnPoints;
        currentSpawnPointIndex = 0; // 처음 스폰 포인트부터 시작
        
        Debug.Log($"[FoxManager] InitStage: interval={spawnInterval}, points count={(currentSpawnPoints != null ? currentSpawnPoints.Length : 0)}");

        // 이전에 남아있던 여우들이 있다면 제거
        for (int i = 0; i < foxes.Length; i++)
        {
            if (foxes[i] != null) Destroy(foxes[i]);
            foxes[i] = null;
        }

        currentFoxIndex = -1; // 다음 스폰 때 0으로 됨
        stageStarted = true;
        StopAllCoroutines();

        // 즉시 첫 번째 여우 스폰 시작
        StartCoroutine(SpawnNextFoxRoutine(true));
    }

    void Update()
    {
        if (!stageStarted || foxes.Length == 0) return;
        if (currentFoxIndex < 0 || currentFoxIndex >= foxes.Length) return;

        GameObject currentFox = foxes[currentFoxIndex];

        // Check if the current fox was destroyed (escaped)
        if (currentFox == null)
        {
            if (!isWaitingForNext)
            {
                StartCoroutine(SpawnNextFoxRoutine());
            }
            return;
        }

        // Check if the current fox was shot (Dead state)
        RandomFoxAnimation anim = currentFox.GetComponent<RandomFoxAnimation>();
        if (anim != null && anim.currentState == RandomFoxAnimation.FoxState.Dead)
        {
            if (!isWaitingForNext)
            {
                StartCoroutine(SpawnNextFoxRoutine());
            }
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
            foxes[currentFoxIndex] = Instantiate(foxPrefabs[currentFoxIndex], spawnPos, spawnRot);
            foxes[currentFoxIndex].name = foxPrefabs[currentFoxIndex].name.Replace("_PrefabRef", "");
            
            // Activate the new fox
            foxes[currentFoxIndex].SetActive(true);
        }

        isWaitingForNext = false;
    }
}
