using System.Collections;
using UnityEngine;
using Michsky.UI.Dark;

/// <summary>
/// 버튼 OnClick에 연결해서 두 CanvasGroup 패널 사이를 Dissolve 효과로 전환하는 범용 컴포넌트.
///
/// [사용법]
/// 1. 씬의 버튼 오브젝트(또는 주변 빈 오브젝트)에 이 스크립트를 Add Component
/// 2. 인스펙터에서:
///    - Transition Helper  → Transition_Overlay에 붙은 UIDissolveEffect
///    - Panel To Hide      → 현재 보이는 패널의 CanvasGroup
///    - Panel To Show      → 보여줄 패널의 CanvasGroup
///    - Duration           → 전환 속도 (초)
/// 3. 버튼의 OnClick() → Execute() 연결
/// </summary>
public class PanelDissolveTransition : MonoBehaviour
{
    [Header("Dissolve Effect")]
    [Tooltip("항상 활성화 상태인 Transition_Overlay의 UIDissolveEffect\n" +
             "(Image Raycast Target은 반드시 체크 해제!)")]
    public UIDissolveEffect transitionHelper;

    [Header("Panels")]
    [Tooltip("전환 시 숨길 패널.\n★ 비워두면 숨기지 않음 (설정창처럼 뒤 패널을 그대로 두고 위에 띄울 때)")]
    public CanvasGroup panelToHide;

    [Tooltip("전환 시 보여줄 패널.\n★ 비워두면 새 패널을 열지 않음 (닫기 버튼처럼 그냥 사라지기만 할 때)")]
    public CanvasGroup panelToShow;

    [Header("Settings")]
    [Tooltip("디졸브 전환에 걸리는 시간 (초)")]
    public float duration = 0.4f;

    private bool isTransitioning = false;

    void OnEnable()
    {
        // ★ 핵심 버그 수정:
        // panelToHide가 이 오브젝트를 포함하는 패널일 경우,
        // SetActive(false) 시점에 코루틴이 강제 종료되어
        // isTransitioning이 true로 굳어버리는 현상 방지.
        // 패널이 다시 켜질 때(OnEnable) 플래그를 초기화합니다.
        isTransitioning = false;
    }

    void Start()
    {
        // 혹시 표시할 패널이 꺼져있으면 미리 준비
        if (panelToShow != null && !panelToShow.gameObject.activeSelf)
        {
            panelToShow.alpha = 0f;
        }
    }

    /// <summary>
    /// 버튼 OnClick()에 연결할 함수.
    /// </summary>
    public void Execute()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;

        // ── 1단계: 화면을 덮는다 (location: 1 → 0) ──
        transitionHelper.animationSpeed = 1f / duration;
        transitionHelper.location = 1f;
        transitionHelper.DissolveIn(); // 오버레이가 화면을 가득 채움

        yield return new WaitForSecondsRealtime(duration);

        // ── 2단계: 패널 교체 (화면이 완전히 가려진 순간) ──
        // panelToHide가 비어있으면 숨기기 생략 (뒤 배경 유지)
        if (panelToHide != null)
        {
            panelToHide.alpha = 0f;
            panelToHide.gameObject.SetActive(false);
        }

        // panelToShow가 비어있으면 새 패널 열기 생략 (그냥 닫히기만)
        if (panelToShow != null)
        {
            panelToShow.gameObject.SetActive(true);
            panelToShow.alpha = 1f;
        }

        // ── 3단계: 화면을 드러낸다 (location: 0 → 1) ──
        transitionHelper.animationSpeed = 1f / duration;
        transitionHelper.location = 0f;
        transitionHelper.DissolveOut(); // 오버레이가 사라지며 새 패널 드러남

        yield return new WaitForSecondsRealtime(duration);

        isTransitioning = false;
    }
}
