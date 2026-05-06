using UnityEngine;
using TMPro;

/// <summary>
/// 에디터의 Player Info 패널 등에 부착되어,
/// 맵 번호에 따른 통계를 화면에 뿌려주는 UI 스크립트입니다.
/// </summary>
public class PlayerInfoUI : MonoBehaviour
{
    [Header("Map Setting")]
    [Tooltip("이 패널이 표시할 맵 번호 (1 또는 2)")]
    public int mapIndex = 1;

    [Header("UI Text References")]
    [Tooltip("최고 도달 스테이지를 띄울 TextMeshPro")]
    public TextMeshProUGUI maxReachedStageText;

    [Tooltip("한 스테이지 내 최대 킬 수를 띄울 TextMeshPro")]
    public TextMeshProUGUI maxKillsText;

    [Tooltip("해당 맵 누적 킬 수를 띄울 TextMeshPro")]
    public TextMeshProUGUI totalKillsText;

    private void OnEnable()
    {
        RefreshUI();
    }

    /// <summary>
    /// UI가 켜질 때, 또는 외부 버튼 클릭 시 수동으로 호출하여 텍스트를 최신화합니다.
    /// </summary>
    public void RefreshUI()
    {
        if (PlayerStatsManager.Instance == null)
        {
            Debug.LogWarning("[PlayerInfoUI] PlayerStatsManager.Instance가 없습니다! 씬에 매니저가 있는지 확인하세요.");
            return;
        }

        if (maxReachedStageText != null)
            maxReachedStageText.text = PlayerStatsManager.Instance.GetMaxReachedStage(mapIndex).ToString();

        if (maxKillsText != null)
            maxKillsText.text = PlayerStatsManager.Instance.GetMaxKillsInStage(mapIndex).ToString();

        if (totalKillsText != null)
            totalKillsText.text = PlayerStatsManager.Instance.GetTotalKills(mapIndex).ToString();
    }
}
