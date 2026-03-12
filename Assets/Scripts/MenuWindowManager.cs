using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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
    public float slideDuration = 0.6f; // 슬라이드 걸리는 시간 (초)
    [Tooltip("화면 폭 (예: 화면 사이즈가 가로 1920이면 1920으로 설정)")]
    public float screenWidth = 1920f; 

    private int currentIndex = 0; // 현재 열려있는 창의 인덱스 (-1=인벤토리, 0=맵, 1=상점)

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

        // 선택된 창이 정중앙(0)에 오기 위해, 전체 캔버스를 얼마만큼 밀어야 하는지 계산합니다.
        float globalOffset = -targetIndex * screenWidth;

        // 각 창이 가야 할 최종 목적지 X좌표를 계산
        float invTargetX = globalOffset + (-1 * screenWidth);
        float mapTargetX = globalOffset + (0 * screenWidth);
        float shopTargetX = globalOffset + (1 * screenWidth);

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
            if (inventoryWindow != null) inventoryWindow.DOAnchorPosX(invTargetX, slideDuration).SetEase(Ease.OutExpo);
            if (mapWindow != null) mapWindow.DOAnchorPosX(mapTargetX, slideDuration).SetEase(Ease.OutExpo);
            if (shopWindow != null) shopWindow.DOAnchorPosX(shopTargetX, slideDuration).SetEase(Ease.OutExpo);
        }
    }
}
