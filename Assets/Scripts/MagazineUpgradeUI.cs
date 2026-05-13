using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 Mag 패널의 업그레이드 UI를 제어합니다.
/// TMP 오브젝트에 라벨("Lv :" 등)이 이미 적혀 있으므로, 코드는 숫자만 설정합니다.
/// </summary>
public class MagazineUpgradeUI : MonoBehaviour
{
    [Header("▼ 상단 행 — 화살표 왼쪽 (현재 값)")]
    [Tooltip("화살표 왼쪽 현재 레벨 숫자만 (예: TMP에 '1' 출력)")]
    public TextMeshProUGUI topCurLvText;
    [Tooltip("화살표 왼쪽 현재 Ammo 숫자만")]
    public TextMeshProUGUI topCurAmmoText;
    [Tooltip("화살표 왼쪽 현재 Reloadable 숫자만")]
    public TextMeshProUGUI topCurRelText;

    [Header("▼ 상단 행 — 화살표 오른쪽 (다음 레벨 값)")]
    [Tooltip("화살표 오른쪽 다음 레벨 숫자만 (예: TMP에 '2' 출력)")]
    public TextMeshProUGUI topNextLvText;
    [Tooltip("화살표 오른쪽 다음 Ammo 숫자만")]
    public TextMeshProUGUI topNextAmmoText;
    [Tooltip("화살표 오른쪽 다음 Reloadable 숫자만")]
    public TextMeshProUGUI topNextRelText;

    [Header("▼ 하단 행 — 현재 장착 정보 (숫자만)")]
    [Tooltip("하단 현재 레벨 숫자만 (TMP에 이미 'Lv :' 라벨이 있으므로 숫자만 설정)")]
    public TextMeshProUGUI curLvText;
    [Tooltip("하단 현재 Ammo 숫자만")]
    public TextMeshProUGUI curAmmoText;
    [Tooltip("하단 현재 Reloadable 숫자만")]
    public TextMeshProUGUI curRelText;

    [Header("▼ 업그레이드 버튼")]
    [Tooltip("Upgrade 버튼 (MagazineUpgradeUI 오브젝트와 같은 계층 or 자식)")]
    public Button upgradeButton;
    [Tooltip("비용 숫자 텍스트 (TMP에 이미 '-' 기호가 있거나, 여기서 '-100' 형태로 출력)")]
    public TextMeshProUGUI costText;

    // ─────────────────────────────────────────────────────────────────────
    
    private void OnEnable()
    {
        KillCountManager.OnGoldChanged += RefreshUI; // 골드 변경 시 버튼 활성화 상태 자동 갱신
        RefreshUI();
    }

    private void OnDisable()
    {
        KillCountManager.OnGoldChanged -= RefreshUI;
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>현재 레벨 기준으로 모든 텍스트와 버튼 상태를 갱신합니다.</summary>
    public void RefreshUI()
    {
        int lv   = MagazineUpgradeData.CurrentLevel;
        int next = lv + 1;
        int cost = MagazineUpgradeData.GetUpgradeCost(lv);

        int curAmmo  = MagazineUpgradeData.GetAmmoAtLevel(lv);
        int curRel   = MagazineUpgradeData.GetReloadableAtLevel(lv);
        int nextAmmo = MagazineUpgradeData.GetAmmoAtLevel(next);
        int nextRel  = MagazineUpgradeData.GetReloadableAtLevel(next);

        // ── 상단 화살표 왼쪽 (현재 값 — 숫자만)
        if (topCurLvText   != null) topCurLvText.text   = lv.ToString();
        if (topCurAmmoText  != null) topCurAmmoText.text  = curAmmo.ToString();
        if (topCurRelText   != null) topCurRelText.text   = curRel.ToString();

        // ── 상단 화살표 오른쪽 (다음 레벨 값 — 숫자만)
        if (topNextLvText   != null) topNextLvText.text   = next.ToString();
        if (topNextAmmoText  != null) topNextAmmoText.text  = nextAmmo.ToString();
        if (topNextRelText   != null) topNextRelText.text   = nextRel.ToString();

        // ── 하단 현재 장착 정보 (숫자만)
        if (curLvText   != null) curLvText.text   = lv.ToString();
        if (curAmmoText  != null) curAmmoText.text  = curAmmo.ToString();
        if (curRelText   != null) curRelText.text   = curRel.ToString();

        // ── 비용 텍스트
        if (costText != null) costText.text = $"-{cost}";

        // ── 버튼 활성/비활성 (골드 부족 시 grayed-out)
        if (upgradeButton != null)
            upgradeButton.interactable = KillCountManager.currentSessionGold >= cost;
    }

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 업그레이드 버튼 OnClick에 연결할 함수.
    /// 골드 차감 → 레벨업 → UI 즉시 갱신.
    /// </summary>
    public void OnUpgradeButtonClicked()
    {
        if (MagazineUpgradeData.TryUpgrade())
        {
            RefreshUI();
            Debug.Log($"[MagUpgrade] Lv {MagazineUpgradeData.CurrentLevel} | " +
                      $"Ammo:{MagazineUpgradeData.GetAmmoAtLevel(MagazineUpgradeData.CurrentLevel)} | " +
                      $"Reload:{MagazineUpgradeData.GetReloadableAtLevel(MagazineUpgradeData.CurrentLevel)} | " +
                      $"Gold:{KillCountManager.currentSessionGold}");
        }
        else
        {
            Debug.Log("[MagUpgrade] 골드 부족!");
        }
    }
}
