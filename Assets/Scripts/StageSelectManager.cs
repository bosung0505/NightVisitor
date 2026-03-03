using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StageSelectManager : MonoBehaviour
{
    [Header("UI Panels")]
    public CanvasGroup mapStagePanel; // The entire Map window (or Map1_Stage_Panel)
    public CanvasGroup inGamePanel;   // The IN_GAME object/panel
    public CanvasGroup gunSelectPanel; // The Gun_Select_Panel

    [Header("Stage Entry Buttons")]
    // Since you have multiple stages, you can hook these up in the inspector
    public Button[] playButtons; 

    [Header("Transition Settings")]
    public float fadeDuration = 0.5f;

    void Start()
    {
        // Add listeners to all play buttons
        foreach(Button btn in playButtons)
        {
            if (btn != null)
            {
                btn.onClick.AddListener(OnPlayStageClicked);
            }
        }

        // Initialize states (Assuming we start in Menu/Map, not In-Game)
        if (inGamePanel != null)
        {
            inGamePanel.alpha = 0f;
            inGamePanel.gameObject.SetActive(false);
        }
        
        if (gunSelectPanel != null)
        {
            gunSelectPanel.alpha = 0f;
            gunSelectPanel.gameObject.SetActive(false);
        }
    }

    public void OnPlayStageClicked()
    {
        // 1. Fade out the Map/Stage Panel
        if (mapStagePanel != null)
        {
            mapStagePanel.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
            {
                mapStagePanel.gameObject.SetActive(false);
            });
        }

        // 2. Fade in the IN_GAME Panel (맵 화면 뒤로 실제 인게임 숲이 서서히 드러납니다)
        if (inGamePanel != null)
        {
            inGamePanel.gameObject.SetActive(true);
            inGamePanel.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        // 3. Fade in the Gun Select Panel
        if (gunSelectPanel != null)
        {
            gunSelectPanel.gameObject.SetActive(true);
            gunSelectPanel.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() => 
            {
                // 총기 선택창이 다 나타나면 매니저에게 카운트다운을 시작하라고 지시
                GunSelectManager gunManager = gunSelectPanel.GetComponent<GunSelectManager>();
                if (gunManager != null)
                {
                    gunManager.StartGunSelection();
                }
            });
        }
    }
}
