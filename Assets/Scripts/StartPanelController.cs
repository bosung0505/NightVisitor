using System.Collections;
using UnityEngine;
using Michsky.UI.Dark;

/// <summary>
/// Start_Panel 터치 → 메인메뉴 전환 시 Dissolve 이펙트를 재생하는 컨트롤러.
///
/// [에디터 세팅]
/// - Transition_Overlay 오브젝트: 항상 활성화 상태 유지
/// - Transition_Overlay의 Image 컴포넌트: Raycast Target → 반드시 체크 해제!
/// - UIDissolveEffect: Effect Material 연결, Location = 1 (처음엔 투명)
/// - 버튼 OnClick() → OnStartButtonClicked() 연결
/// </summary>
public class StartPanelController : MonoBehaviour
{
    [Header("패널 References")]
    [Tooltip("터치하면 사라질 스타트 패널")]
    public GameObject startPanel;

    [Tooltip("터치 후 열릴 메인메뉴 패널")]
    public GameObject mainPanel;

    [Header("Dissolve Effect")]
    [Tooltip("항상 활성화 상태인 Transition_Overlay의 UIDissolveEffect 컴포넌트\n" +
             "(Image의 Raycast Target은 반드시 체크 해제!)")]
    public UIDissolveEffect dissolveOverlay;

    [Tooltip("화면이 덮이는 데 걸리는 시간 (초)")]
    public float fadeOutDuration = 0.5f;

    [Tooltip("화면이 드러나는 데 걸리는 시간 (초)")]
    public float fadeInDuration = 0.5f;

    // 연속 클릭 방지 플래그
    private bool isTransitioning = false;

    void Start()
    {
        // 시작 시 location = 1 → 셰이더로 완전히 투명(보이지 않음)
        // Raycast Target = false 이므로 클릭도 통과됨
        if (dissolveOverlay != null)
        {
            dissolveOverlay.location = 1f;
        }
    }

    /// <summary>
    /// Start_Panel의 버튼 OnClick() 에 연결할 함수.
    /// </summary>
    public void OnStartButtonClicked()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToMainMenu());
    }

    private IEnumerator TransitionToMainMenu()
    {
        isTransitioning = true;

        // ── 1단계: 화면을 디졸브로 덮는다 (location: 1 → 0, 즉 DissolveIn) ──
        // DissolveIn = 요소가 "들어온다" = 오버레이가 화면을 가득 채움
        dissolveOverlay.animationSpeed = 1f / fadeOutDuration;
        dissolveOverlay.location = 1f;
        dissolveOverlay.DissolveIn();

        yield return new WaitForSecondsRealtime(fadeOutDuration);

        // ── 2단계: 패널 전환 ──
        if (startPanel != null) startPanel.SetActive(false);
        if (mainPanel  != null) mainPanel.SetActive(true);

        // ── 3단계: 디졸브가 걷히며 메인메뉴 드러남 (location: 0 → 1, 즉 DissolveOut) ──
        // DissolveOut = 요소가 "나간다" = 오버레이가 사라짐
        dissolveOverlay.animationSpeed = 1f / fadeInDuration;
        dissolveOverlay.location = 0f;
        dissolveOverlay.DissolveOut();

        yield return new WaitForSecondsRealtime(fadeInDuration);

        // location = 1 상태(투명)로 돌아왔으므로 아무것도 할 필요 없음
        isTransitioning = false;
    }
}
