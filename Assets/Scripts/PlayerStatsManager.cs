using UnityEngine;

/// <summary>
/// 플레이어의 맵별 통계 데이터(최고 도달 스테이지, 최대 킬 수, 누적 킬 수)를 저장하고 로드하는 매니저 클래스입니다.
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬이 넘어가도 파괴되지 않음
    }

    // --- 누적 킬 수 (Total Kills) ---
    public int GetTotalKills(int mapIndex)
    {
        return PlayerPrefs.GetInt($"Map{mapIndex}_TotalKills", 0);
    }

    public void AddTotalKill(int mapIndex, int amount = 1)
    {
        int current = GetTotalKills(mapIndex);
        PlayerPrefs.SetInt($"Map{mapIndex}_TotalKills", current + amount);
        PlayerPrefs.Save();
    }

    // --- 스테이지 내 최대 킬 수 (Max Kills in a Single Stage) ---
    public int GetMaxKillsInStage(int mapIndex)
    {
        return PlayerPrefs.GetInt($"Map{mapIndex}_MaxKills", 0);
    }

    public void UpdateMaxKillsInStage(int mapIndex, int kills)
    {
        int currentMax = GetMaxKillsInStage(mapIndex);
        if (kills > currentMax)
        {
            PlayerPrefs.SetInt($"Map{mapIndex}_MaxKills", kills);
            PlayerPrefs.Save();
        }
    }

    // --- 최고 도달 스테이지 (Max Reached Stage) ---
    public int GetMaxReachedStage(int mapIndex)
    {
        // 도달 스테이지의 기본값은 1입니다. (게임을 켜면 최소 1스테이지니까)
        return PlayerPrefs.GetInt($"Map{mapIndex}_MaxReachedStage", 1);
    }

    public void UpdateMaxReachedStage(int mapIndex, int stageNum)
    {
        int currentMax = GetMaxReachedStage(mapIndex);
        if (stageNum > currentMax)
        {
            PlayerPrefs.SetInt($"Map{mapIndex}_MaxReachedStage", stageNum);
            PlayerPrefs.Save();
        }
    }
}
