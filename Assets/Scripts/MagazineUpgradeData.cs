/// <summary>
/// 탄창 업그레이드 레벨 및 스탯 계산을 담당하는 전역 정적 클래스.
/// 앱 실행 중 유지되며, 전 맵/스테이지에 공통 적용됩니다.
/// </summary>
public static class MagazineUpgradeData
{
    // ── 현재 레벨 (앱 세션 유지) ──────────────────────────────────────────
    public static int CurrentLevel { get; private set; } = 1;

    // ── 스탯 계산 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 해당 레벨에서의 1개 탄창 탄 수 (10레벨당 +1)
    /// Lv1~10: 5발 / Lv11~20: 6발 / Lv21~30: 7발 ...
    /// </summary>
    public static int GetAmmoAtLevel(int level)
        => 5 + (level - 1) / 10;

    /// <summary>
    /// 해당 레벨에서의 예비 탄약 수 (레벨당 +1)
    /// Lv1: 5발 / Lv2: 6발 / Lv10: 14발 ...
    /// </summary>
    public static int GetReloadableAtLevel(int level)
        => 5 + (level - 1);

    /// <summary>
    /// 현재 레벨에서 다음 레벨로 업그레이드하는 데 필요한 골드
    /// (10레벨 구간마다 100골드씩 증가. 1~10: 100골드, 11~20: 200골드 ...)
    /// </summary>
    public static int GetUpgradeCost(int currentLevel)
        => ((currentLevel - 1) / 10 + 1) * 100;

    // ── 업그레이드 실행 ───────────────────────────────────────────────────

    /// <summary>
    /// 현재 보유 골드가 충분하면 레벨업하고 true 반환.
    /// 골드 부족 시 false 반환.
    /// </summary>
    public static bool TryUpgrade()
    {
        int cost = GetUpgradeCost(CurrentLevel);
        if (KillCountManager.currentSessionGold < cost) return false;

        KillCountManager.currentSessionGold -= cost;
        CurrentLevel++;
        return true;
    }

    // ── 저장/불러오기 (추후 영구 저장 확장용) ─────────────────────────────

    public static void Save()
    {
        UnityEngine.PlayerPrefs.SetInt("MagLevel", CurrentLevel);
        UnityEngine.PlayerPrefs.Save();
    }

    public static void Load()
    {
        CurrentLevel = UnityEngine.PlayerPrefs.GetInt("MagLevel", 1);
    }
}
