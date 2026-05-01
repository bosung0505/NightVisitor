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

    [Header("Map 2 Unlock UI")]
    [Tooltip("맵2가 잠겨있을 때 보여질 자물쇠 UI (CanvasGroup 필요)")]
    public CanvasGroup map2LockImage;
    [Tooltip("맵2 입장 버튼 (CanvasGroup 필요)")]
    public CanvasGroup map2EnterButton;

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

        // [신규 로직] Map 2 (currentIndex == 1) 도달 시 해금 상태 확인 및 연출
        if (currentIndex == 1)
        {
            int unlocked = PlayerPrefs.GetInt("Map2_Unlocked", 0);
            int justUnlocked = PlayerPrefs.GetInt("Map2_JustUnlocked", 0);

            if (justUnlocked == 1)
            {
                // 1회성 트리거 소모
                PlayerPrefs.SetInt("Map2_JustUnlocked", 0);
                PlayerPrefs.Save();
                StartCoroutine(PlayUnlockAnimation());
            }
            else if (unlocked == 1)
            {
                // 이미 해금됨
                if (map2LockImage != null)
                {
                    map2LockImage.alpha = 0f;
                    map2LockImage.gameObject.SetActive(false);
                }
                if (map2EnterButton != null)
                {
                    map2EnterButton.gameObject.SetActive(true);
                    map2EnterButton.alpha = 1f;
                    map2EnterButton.interactable = true;
                    map2EnterButton.blocksRaycasts = true;
                }
            }
            else
            {
                // 잠김
                if (map2LockImage != null)
                {
                    map2LockImage.gameObject.SetActive(true);
                    map2LockImage.alpha = 1f;
                }
                if (map2EnterButton != null)
                {
                    map2EnterButton.alpha = 0f;
                    map2EnterButton.interactable = false;
                    map2EnterButton.blocksRaycasts = false;
                    map2EnterButton.gameObject.SetActive(false);
                }
            }
        }

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

    private System.Collections.IEnumerator PlayUnlockAnimation()
    {
        UnityEngine.EventSystems.EventSystem evSystem = UnityEngine.EventSystems.EventSystem.current;

        // 글로벌 UI 이벤트 시스템 차단 (유저 상호작용 원천 봉쇄)
        if (evSystem != null)
            evSystem.enabled = false;

        // 중앙으로 카드가 슬라이드 오기까지 대기
        yield return new WaitForSeconds(animationDuration + 0.1f);

        // 1단계: 자물쇠 서서히 사라짐
        if (map2LockImage != null)
        {
            map2LockImage.DOFade(0f, 1f).OnComplete(() =>
            {
                map2LockImage.gameObject.SetActive(false);
            });
        }

        // 2단계: 입장 버튼 서서히 나타남 (자물쇠가 반쯤 사라질 때쯤 0.5초 딜레이 후 시작)
        if (map2EnterButton != null)
        {
            map2EnterButton.gameObject.SetActive(true);
            map2EnterButton.alpha = 0f;
            map2EnterButton.interactable = false;
            map2EnterButton.blocksRaycasts = false;
            
            map2EnterButton.DOFade(1f, 1f).SetDelay(0.5f);
        }

        // 애니메이션이 완전히 끝날 때까지 대기 (딜레이 0.5초 + 페이드 1초 = 1.5초)
        yield return new WaitForSeconds(1.5f);

        if (map2EnterButton != null)
        {
            map2EnterButton.interactable = true;
            map2EnterButton.blocksRaycasts = true;
        }

        // 글로벌 UI 이벤트 시스템 완벽 복구 보장
        if (evSystem != null)
            evSystem.enabled = true;
    }
}
