using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Map 2 전용 뮤턴트 스폰 매니저.
/// StageSelectManager.StartGame()이 호출하는 InitStage()를 통해
/// 스테이지별 스폰 그룹, 부활 여부를 전달받아 코루틴으로 처리합니다.
///
/// 부착 위치: Map2 프리팹 내부의 [MutantSpawner] 빈 오브젝트
/// </summary>
public class MutantSpawner : MonoBehaviour
{
    public static MutantSpawner Instance;

    private bool loopWavesForStage = false;
    private Coroutine spawnCoroutine;
    private List<GameObject> spawnedMutants = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// StageSelectManager가 스테이지 시작 시 호출합니다.
    /// 이전 뮤턴트를 정리하고 새 스폰 코루틴을 시작합니다.
    /// </summary>
    public void InitStage(MutantSpawnGroup[] groups, bool loopWaves = false)
    {
        ClearAllSpawnedMutants();

        loopWavesForStage = loopWaves;

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);

        if (groups != null && groups.Length > 0)
            spawnCoroutine = StartCoroutine(SpawnRoutine(groups));
        else
            Debug.LogWarning("[MutantSpawner] 이 스테이지에 스폰 그룹이 없습니다. StageConfig2.spawnGroups를 설정해주세요.");
    }

    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator SpawnRoutine(MutantSpawnGroup[] groups)
    {
        // spawnDelay 오름차순 정렬 후 순서대로 처리
        System.Array.Sort(groups, (a, b) => a.spawnDelay.CompareTo(b.spawnDelay));

        while (true)
        {
            float elapsed = 0f;

            foreach (MutantSpawnGroup group in groups)
            {
                // 이전 그룹 이후 남은 대기 시간만 추가 대기
                float waitTime = group.spawnDelay - elapsed;
                if (waitTime > 0f)
                {
                    yield return new WaitForSeconds(waitTime);
                    elapsed += waitTime;
                }

                // 그룹 내 모든 뮤턴트 동시 스폰
                SpawnGroup(group);
            }

            // 루프 설정이 꺼져 있으면 코루틴을 정상 종료합니다.
            if (!loopWavesForStage) break;

            // 루프 설정이 켜져 있으면 배열 1바퀴 소환이 끝난 뒤 숨통을 트일 10초 휴식 후 다음 루프 재시작
            yield return new WaitForSeconds(10f);
        }

        spawnCoroutine = null;
    }

    private void SpawnGroup(MutantSpawnGroup group)
    {
        if (group.mutantPrefabs == null) return;

        for (int i = 0; i < group.mutantPrefabs.Length; i++)
        {
            if (group.mutantPrefabs[i] == null) continue;

            // SpawnPoint가 있으면 그 위치, 없으면 스포너 오브젝트 위치
            Transform pt = (group.spawnPoints != null && i < group.spawnPoints.Length && group.spawnPoints[i] != null)
                ? group.spawnPoints[i]
                : transform;

            GameObject obj = Instantiate(group.mutantPrefabs[i], pt.position, pt.rotation);
            spawnedMutants.Add(obj);

            // ★ 스테이지/그룹 설정의 각종 속성 주입
            MutantAI ai = obj.GetComponent<MutantAI>();
            if (ai != null)
            {
                ai.SetRevive(group.enableRevive); // 스테이지 공통에서 그룹 개별 방식으로 변경
                // 0이면 기본값 3 자동 적용
                int hp = group.maxHitPoints > 0 ? group.maxHitPoints : 3;
                ai.SetMaxHitPoints(hp);

                ai.startAsRunner = group.startRunning; // 질주 러너 설정 주입
            }

            Debug.Log($"[MutantSpawner] '{group.mutantPrefabs[i].name}' 스폰 완료 at {pt.name} | enableRevive={group.enableRevive} | startAsRunner={group.startRunning}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>아직 대기 중인 스폰 코루틴을 중단합니다.</summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    /// <summary>이미 생성된 뮤턴트 클론 전부 삭제합니다.</summary>
    public void ClearAllSpawnedMutants()
    {
        foreach (GameObject m in spawnedMutants)
        {
            if (m != null) Destroy(m);
        }
        spawnedMutants.Clear();
    }
}
