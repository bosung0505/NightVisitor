using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;


public class MenuWindowManager : MonoBehaviour
{
    [Header("Windows (패널들 - 공간순서: 왼/중/오)")]
    public RectTransform inventoryWindow; // 왼쪽 (-1)
    public RectTransform mapWindow;       // 중앙 (0)
    public RectTransform shopWindow;      // 오른쪽 (+1)

    [Header("Buttons (메뉴 이동 버튼들)")]
    public Button inventoryButton;
    public Button mapButton;
    public Button shopButton;

    [Header("Animation Settings")]
    public float slideDuration = 0.4f; // 슬라이드 걸리는 시간 (초)

    [Header("Global Money Settings")]
    [Tooltip("초기 자본금을 파싱할 TextMeshProUGUI 오브젝트")]
    public TextMeshProUGUI startingGoldText;

    private int currentIndex = 0; // 현재 열려있는 창의 인덱스 (-1=인벤토리, 0=맵, 1=상점)
    private static bool isGoldInitialized = false;

    void Awake()
    {
        // 모바일 기본값(30fps)을 60fps로 고정하여 UI 애니메이션 끊김 방지
        Application.targetFrameRate = 60;

        // 게임 최초 실행 시에만 텍스트에서 돈을 파싱하여 적용합니다.
        if (!isGoldInitialized && startingGoldText != null)
        {
            // 텍스트 내에 있는 쉼표(,)나 여타 문자를 제거하고 숫자만 추출
            string textValue = startingGoldText.text;
            string numberOnly = System.Text.RegularExpressions.Regex.Replace(textValue, @"[^0-9]", "");
            
            if (int.TryParse(numberOnly, out int goldValue))
            {
                KillCountManager.currentSessionGold = goldValue;
            }
            else
            {
                Debug.LogWarning("[MenuWindowManager] startingGoldText 에서 숫자를 찾을 수 없어 기본값(0)이 적용됩니다.");
                KillCountManager.currentSessionGold = 0;
            }
            
            isGoldInitialized = true;
        }
    }

    void Start()
    {
        // 각 창마다 고유한 공간 인덱스를 부여합니다.
        // 왼쪽(-1), 중앙(0), 오른쪽(+1)
        if (inventoryButton != null) inventoryButton.onClick.AddListener(() => MoveToWindow(-1));
        if (mapButton != null) mapButton.onClick.AddListener(() => MoveToWindow(0));
        if (shopButton != null) shopButton.onClick.AddListener(() => MoveToWindow(1));

        // 애니메이션을 위해 먼저 모든 창을 활성화 상태로 만듭니다.
        if (inventoryWindow != null) inventoryWindow.gameObject.SetActive(true);
        if (mapWindow != null) mapWindow.gameObject.SetActive(true);
        if (shopWindow != null) shopWindow.gameObject.SetActive(true);

        // 초기 화면은 (0번인 Map)으로 즉시(애니메이션 없이) 배치합니다.
        MoveToWindow(0, true);
    }

    // targetIndex: -1 (Inventory), 0 (Map), 1 (Shop)
    public void MoveToWindow(int targetIndex, bool isInstant = false)
    {
        // 이미 켜진 창 버튼을 또 눌렀다면 무시
        if (!isInstant && targetIndex == currentIndex) return;
        currentIndex = targetIndex;

        // 실제 기기 해상도를 동적으로 읽어 슬라이드 거리를 계산합니다.
        // (인스펙터 고정값 대신 Screen.width를 사용하여 모든 기기에서 정확하게 맞춤)
        float sw = Screen.width;
        float globalOffset = -targetIndex * sw;

        // 각 창이 가야 할 최종 목적지 X좌표를 계산
        float invTargetX  = globalOffset + (-1 * sw);
        float mapTargetX  = globalOffset + (0  * sw);
        float shopTargetX = globalOffset + (1  * sw);

        if (isInstant)
        {
            // 게임 시작 직후 (애니메이션 없이 팍 이동)
            if (inventoryWindow != null) inventoryWindow.anchoredPosition = new Vector2(invTargetX, 0);
            if (mapWindow != null) mapWindow.anchoredPosition = new Vector2(mapTargetX, 0);
            if (shopWindow != null) shopWindow.anchoredPosition = new Vector2(shopTargetX, 0);
        }
        else
        {
            // 부드럽게 슬라이드 애니메이션 (DOTween)
            // SetUpdate(true): Time.timeScale 영향을 받지 않아 일시정지 중에도 UI 전환 가능
            if (inventoryWindow != null) inventoryWindow.DOAnchorPosX(invTargetX, slideDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            if (mapWindow != null)       mapWindow.DOAnchorPosX(mapTargetX,  slideDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            if (shopWindow != null)      shopWindow.DOAnchorPosX(shopTargetX, slideDuration).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}
