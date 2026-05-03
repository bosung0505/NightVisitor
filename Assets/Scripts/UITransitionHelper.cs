using System.Collections;
using UnityEngine;
using Michsky.UI.Dark;

/// <summary>
/// 결과창 등장 / 복귀 전환에 공통으로 사용하는 Dissolve 전환 싱글톤.
/// Time.timeScale = 0인 상태에서도 작동합니다. (UIDissolveEffect가 unscaledDeltaTime 사용)
///
/// [에디터 세팅]
/// - 씬의 영구 오브젝트에 부착 (Canvas 루트 등)
/// - dissolveEffect → 항상 활성화된 Transition_Overlay의 UIDissolveEffect 연결
/// </summary>
public class UITransitionHelper : MonoBehaviour
{
    public static UITransitionHelper Instance { get; private set; }

    [Header("Dissolve Effect")]
    [Tooltip("항상 활성화된 Transition_Overlay의 UIDissolveEffect 연결")]
    public UIDissolveEffect dissolveEffect;

    [Header("Timing")]
    [Tooltip("화면을 가리는 데 걸리는 시간 (초)")]
    public float coverDuration  = 0.35f;

    [Tooltip("화면을 드러내는 데 걸리는 시간 (초)")]
    public float revealDuration = 0.45f;

    private bool isBusy = false;

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ────────────────────────────────────────────────────────
    /// <summary>
    /// 결과창 등장용: Dissolve 가림 → 패널 활성화 → Dissolve 해제 → 버튼 활성화
    /// </summary>
    /// <param name="panel">등장시킬 결과창 CanvasGroup</param>
    /// <param name="onMidpoint">화면이 가려진 순간 추가로 실행할 콜백 (선택)</param>
    public void ShowWithTransition(CanvasGroup panel, System.Action onMidpoint = null)
    {
        if (panel == null || dissolveEffect == null) return;
        if (isBusy) return;
        StartCoroutine(ShowRoutine(panel, onMidpoint));
    }

    // ────────────────────────────────────────────────────────
    /// <summary>
    /// 복귀/전환 코드 실행용: Dissolve 가림 → action 실행 → Dissolve 해제
    /// (ReturnToMap 등 코드 기반 전환에 사용)
    /// </summary>
    /// <param name="action">화면이 가려진 순간 실행할 동작</param>
    public void TransitionThen(System.Action action)
    {
        if (dissolveEffect == null) { action?.Invoke(); return; }
        if (isBusy) return;
        StartCoroutine(TransitionThenRoutine(action));
    }

    // ────────────────────────────────────────────────────────
    private IEnumerator ShowRoutine(CanvasGroup panel, System.Action onMidpoint)
    {
        isBusy = true;

        // 패널 사전 준비 (보이지 않는 상태로 대기)
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;

        // 1. DissolveIn: 화면 가림 (location 1→0)
        dissolveEffect.animationSpeed = 1f / coverDuration;
        dissolveEffect.location = 1f;
        dissolveEffect.DissolveIn();
        yield return new WaitForSecondsRealtime(coverDuration);

        // 2. 패널 활성화 (화면이 가려진 순간에 교체)
        panel.gameObject.SetActive(true);
        panel.alpha = 1f;
        onMidpoint?.Invoke();

        // 3. DissolveOut: 화면 드러남 (location 0→1)
        dissolveEffect.animationSpeed = 1f / revealDuration;
        dissolveEffect.location = 0f;
        dissolveEffect.DissolveOut();
        yield return new WaitForSecondsRealtime(revealDuration);

        // 4. 클릭 허용
        panel.interactable = true;
        panel.blocksRaycasts = true;

        isBusy = false;
    }

    // ────────────────────────────────────────────────────────
    private IEnumerator TransitionThenRoutine(System.Action action)
    {
        isBusy = true;

        // 1. DissolveIn: 화면 가림
        dissolveEffect.animationSpeed = 1f / coverDuration;
        dissolveEffect.location = 1f;
        dissolveEffect.DissolveIn();
        yield return new WaitForSecondsRealtime(coverDuration);

        // 2. 복귀/전환 실행
        action?.Invoke();

        // 3. DissolveOut: 화면 드러남
        dissolveEffect.animationSpeed = 1f / revealDuration;
        dissolveEffect.location = 0f;
        dissolveEffect.DissolveOut();
        yield return new WaitForSecondsRealtime(revealDuration);

        isBusy = false;
    }
}
