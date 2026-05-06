using System.Collections;
using UnityEngine;
using Michsky.UI.Dark;
using DG.Tweening;

/// <summary>
/// 배경(검은 반투명 패널)은 서서히 켜지고, 중앙 모달창은 Dissolve 효과와 함께 나타나는 팝업 트랜지션.
/// 
/// [중요: 사용법]
/// ★ 이 스크립트는 팝업 패널이 아니라, **"팝업을 여는 버튼"** 또는 항상 켜져있는 관리자 오브젝트에 부착해야 합니다!
/// (그래야 팝업이 꺼져(비활성화) 있더라도 버튼을 눌러서 켤 수 있습니다.)
/// </summary>
public class PopupDissolveTransition : MonoBehaviour
{
    [Header("Popup Elements")]
    [Tooltip("팝업 전체를 묶고 있는 최상단 오브젝트 (꺼져있어도 됨)")]
    public GameObject popupRoot;

    [Tooltip("팝업 뒤에 깔리는 어두운 반투명 배경의 CanvasGroup")]
    public CanvasGroup backgroundOverlay;

    [Tooltip("팝업 중앙 배경 이미지에 부착된 UIDissolveEffect (location 조절용)")]
    public UIDissolveEffect contentDissolve;

    [Tooltip("텍스트와 버튼들이 확 나타나지 않고 부드럽게 페이드되게 할 CanvasGroup (선택사항)")]
    public CanvasGroup popupContentGroup;

    [Header("Settings")]
    public float duration = 0.4f;

    [Header("Optional: One-Time Story/Event")]
    [Tooltip("스토리 1회성 확인을 위한 PlayerPrefs 키 (예: Map2_StoryPlayed)")]
    public string oneTimePrefKey;
    [Tooltip("키 값이 1일 경우, 팝업을 띄우지 않고 완전히 무시합니다 (스킵용)")]
    public bool skipPopupIfPrefMatches;
    [Tooltip("Open/Close 실행 시 해당 키 값을 1로 저장할지 여부")]
    public bool savePrefOnExecute;

    private bool isTransitioning = false;

    /// <summary>
    /// 버튼 OnClick에 연결할 열기 함수
    /// </summary>
    public void OpenPopup()
    {
        if (isTransitioning) return;

        if (!string.IsNullOrEmpty(oneTimePrefKey))
        {
            if (skipPopupIfPrefMatches && PlayerPrefs.GetInt(oneTimePrefKey, 0) == 1)
            {
                return; // 스토리를 이미 봤다면 팝업을 열지 않고 그냥 넘어감
            }

            if (savePrefOnExecute)
            {
                PlayerPrefs.SetInt(oneTimePrefKey, 1);
                PlayerPrefs.Save();
            }
        }

        StartCoroutine(OpenRoutine());
    }

    /// <summary>
    /// 버튼 OnClick에 연결할 닫기 함수
    /// </summary>
    public void ClosePopup()
    {
        if (isTransitioning) return;

        if (!string.IsNullOrEmpty(oneTimePrefKey) && savePrefOnExecute)
        {
            PlayerPrefs.SetInt(oneTimePrefKey, 1);
            PlayerPrefs.Save();
        }

        StartCoroutine(CloseRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        isTransitioning = true;

        // 꺼져있던 팝업 루트를 켬
        if (popupRoot != null) popupRoot.SetActive(true);

        // 1. 배경 페이드 인
        if (backgroundOverlay != null)
        {
            backgroundOverlay.alpha = 0f;
            backgroundOverlay.DOFade(1f, duration).SetUpdate(true);
            backgroundOverlay.interactable = true;
            backgroundOverlay.blocksRaycasts = true;
        }

        // 2. 텍스트/버튼 페이드 인 (글자들이 팍 뜨는 것 방지)
        if (popupContentGroup != null)
        {
            popupContentGroup.alpha = 0f;
            popupContentGroup.DOFade(1f, duration).SetUpdate(true);
        }

        // 3. 중앙 배경 이미지 디졸브 인 (유저분 말씀대로 location 조절!)
        if (contentDissolve != null)
        {
            contentDissolve.animationSpeed = 1f / duration;
            contentDissolve.location = 1f; // 완전히 지워진 상태에서 시작
            contentDissolve.DissolveIn();  // location을 0으로 자동으로 보내며 그림을 그림
        }

        yield return new WaitForSecondsRealtime(duration);
        isTransitioning = false;
    }

    private IEnumerator CloseRoutine()
    {
        isTransitioning = true;

        if (backgroundOverlay != null)
        {
            backgroundOverlay.interactable = false;
            backgroundOverlay.blocksRaycasts = false;
            backgroundOverlay.DOFade(0f, duration).SetUpdate(true);
        }

        if (popupContentGroup != null)
        {
            popupContentGroup.DOFade(0f, duration).SetUpdate(true);
        }

        if (contentDissolve != null)
        {
            contentDissolve.animationSpeed = 1f / duration;
            contentDissolve.location = 0f; // 그려진 상태에서 시작
            contentDissolve.DissolveOut(); // location을 1로 보내며 지움
        }

        yield return new WaitForSecondsRealtime(duration);
        
        // 완전히 다 사라지면 비활성화
        if (popupRoot != null) popupRoot.SetActive(false);
        isTransitioning = false;
    }
}
