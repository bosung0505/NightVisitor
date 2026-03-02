using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MapCarouselUI : MonoBehaviour
{
    [Header("Map References (반드시 순서대로 할당: Map1, Map2, Map3)")]
    public RectTransform[] mapCards;

    [Header("Navigation Buttons")]
    public Button leftButton;
    public Button rightButton;

    [Header("Carousel Settings")]
    public float animationDuration = 0.5f;

    [Tooltip("중앙 카드가 배치될 위치와 크기")]
    public Vector2 centerPosition = new Vector2(0, 0);
    public float centerScale = 1.0f;
    
    [Tooltip("양옆(대기 중인) 카드가 배치될 X축 폭")]
    public float sideOffsetX = 400f; 
    [Tooltip("양옆(대기 중인) 카드가 작아질 비율")]
    public float sideScale = 0.6f;

    private int currentIndex = 0; // 시작 시 가운데에 위치할 카드 번호 (0 = Map1)

    void Start()
    {
        // 버튼 연결
        if (leftButton != null) leftButton.onClick.AddListener(MoveLeft);
        if (rightButton != null) rightButton.onClick.AddListener(MoveRight);

        // 초기 상태 세팅 (Map1이 중앙)
        UpdateCarouselUI(true);
    }

    private void MoveLeft()
    {
        // 이미 0번 인덱스면 왼쪽으로 못 감
        if (currentIndex <= 0) return;
        
        currentIndex--;
        UpdateCarouselUI(false);
    }

    private void MoveRight()
    {
        // 마지막 인덱스면 오른쪽으로 못 감
        if (currentIndex >= mapCards.Length - 1) return;
        
        currentIndex++;
        UpdateCarouselUI(false);
    }

    private void UpdateCarouselUI(bool isInstant)
    {
        // 1. 화살표 버튼 숨기기/보이기 갱신
        UpdateArrowButtons();

        // 2. 모든 카드의 위치, 크기, 렌더링 순서(Z-index 위아래)를 재배치합니다.
        for (int i = 0; i < mapCards.Length; i++)
        {
            RectTransform card = mapCards[i];
            if (card == null) continue;

            // 목적지 변수들
            Vector2 targetPos;
            float targetScale;

            int diff = i - currentIndex; // 음수면 왼쪽, 양수면 오른쪽, 0이면 중앙

            if (i == currentIndex)
            {
                // [중앙 메인 카드]
                targetPos = centerPosition;
                targetScale = centerScale;
                card.SetAsLastSibling(); // 맨 앞으로 정렬
                
                CanvasGroup cg = card.GetComponent<CanvasGroup>();
                if (cg != null) 
                {
                    cg.blocksRaycasts = true;
                    if (isInstant) cg.alpha = 1f;
                    else cg.DOFade(1f, animationDuration);
                }
            }
            else
            {
                // [양옆 대기 카드들]
                targetPos = centerPosition + new Vector2(sideOffsetX * diff, 0);
                targetScale = sideScale;
                card.SetSiblingIndex(0); // 뒤로 깔리게 정렬

                CanvasGroup cg = card.GetComponent<CanvasGroup>();
                if (cg != null) 
                {
                    cg.blocksRaycasts = false;
                    
                    // 핵심 로직: 나와 거리가 2칸 이상 차이나면(예: Map1일 때 Map3) 숨김!
                    float targetAlpha = (Mathf.Abs(diff) >= 2) ? 0f : 1f;
                    
                    if (isInstant) cg.alpha = targetAlpha;
                    else cg.DOFade(targetAlpha, animationDuration);
                }
            }

            // 3. DOTween 애니메이션 적용 또는 즉시 이동
            if (isInstant)
            {
                card.anchoredPosition = targetPos;
                card.localScale = new Vector3(targetScale, targetScale, 1f);
            }
            else
            {
                card.DOAnchorPos(targetPos, animationDuration).SetEase(Ease.OutExpo);
                card.DOScale(targetScale, animationDuration).SetEase(Ease.OutExpo);
            }
        }
    }

    // 화살표가 시작과 끝에서 아예 물리적으로 숨겨지도록(SetActive) 처리하는 로직
    private void UpdateArrowButtons()
    {
        if (leftButton != null)
        {
            // Map1 (Index 0) 이면 안 보이고, 그 이상이면 보임
            leftButton.gameObject.SetActive(currentIndex > 0);
        }

        if (rightButton != null)
        {
            // Map3 (마지막 Index) 이면 안 보이고, 그 이전이면 보임
            rightButton.gameObject.SetActive(currentIndex < mapCards.Length - 1);
        }
    }
}
