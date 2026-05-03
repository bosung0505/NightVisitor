using UnityEngine;

/// <summary>
/// 인게임 설정창의 "포기하기" 버튼에 부착합니다.
/// 맵 1: KillCountManager.ShowMissionFailedPanel()
/// 맵 2: Map2ResultManager.ShowVillageInvadedPanel()
/// 포기하기 버튼 OnClick() → Execute() 연결
/// </summary>
public class GiveUpHandler : MonoBehaviour
{
    [Tooltip("Settings_Panel_InGame의 CanvasGroup 연결 (포기 시 즉시 닫힘)")]
    public CanvasGroup settingsPanelGroup;

    public void Execute()
    {
        CloseSettingsPanel();

        if (Map2ResultManager.Instance != null && Map2ResultManager.Instance.isActiveAndEnabled)
            Map2ResultManager.Instance.ShowVillageInvadedPanel();
        else if (KillCountManager.Instance != null)
            KillCountManager.Instance.ShowMissionFailedPanel();
        else
            Debug.LogWarning("[GiveUpHandler] 결과창 매니저를 찾을 수 없습니다.");
    }

    private void CloseSettingsPanel()
    {
        if (settingsPanelGroup == null) return;
        settingsPanelGroup.alpha = 0f;
        settingsPanelGroup.interactable = false;
        settingsPanelGroup.blocksRaycasts = false;
        settingsPanelGroup.gameObject.SetActive(false);
    }
}
