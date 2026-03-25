using System.Collections.Generic; // List를 쓰기 위해 추가
using UnityEngine;
using UnityEngine.Rendering;

public class MapInfo : MonoBehaviour
{
    [Header("Map Configurations")]
    [Tooltip("스테이지 1~10에서 쓸 모든 스폰 위치를 다 넣어두세요!")]
    public Transform[] allSpawnPointsInMap;

    [Tooltip("이 맵에 배치된 모든 디코이 여우들")]
    public DecoyFoxAI[] allDecoysInMap;

    [Tooltip("이 맵이 켜질 때 화면을 덮을 Volume_Start 오브젝트")]
    public Volume startingVolume;

    [Header("Battery Volumes (Map Specific)")]
    public Volume thermalVolume;
    public Volume normalVolume;

    // --- (기존 디코이 세팅 함수 그대로 유지) ---
    public void SetupDecoys(string[] activeDecoyNames)
    {
        foreach (var decoy in allDecoysInMap)
        {
            if (decoy != null) decoy.gameObject.SetActive(false);
        }

        if (activeDecoyNames == null || activeDecoyNames.Length == 0) return;

        foreach (var decoy in allDecoysInMap)
        {
            if (decoy == null) continue;
            foreach (var targetName in activeDecoyNames)
            {
                if (decoy.gameObject.name == targetName)
                {
                    decoy.gameObject.SetActive(true);
                    break;
                }
            }
        }
    }

    // --- ★ [신규 추가] 스테이지별로 지정된 스폰 포인트만 쏙 골라서 반환하는 함수 ---
    public Transform[] GetActiveSpawnPoints(string[] targetSpawnNames)
    {
        // 만약 스테이지 매니저에서 이름을 하나도 안 적어줬다면? 
        // 에러 방지를 위해 일단 맵에 있는 전체 스폰 포인트를 다 넘겨줍니다.
        if (targetSpawnNames == null || targetSpawnNames.Length == 0)
        {
            return allSpawnPointsInMap;
        }

        List<Transform> resultList = new List<Transform>();

        // 맵이 가진 전체 스폰 포인트 중에서
        foreach (Transform t in allSpawnPointsInMap)
        {
            if (t == null) continue;

            // 이름이 일치하는 녀석만 리스트에 담습니다.
            foreach (string name in targetSpawnNames)
            {
                if (t.gameObject.name == name)
                {
                    resultList.Add(t);
                    break;
                }
            }
        }

        return resultList.ToArray(); // 배열로 변환해서 리턴!
    }
}