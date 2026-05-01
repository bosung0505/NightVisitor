using System.Collections;
using UnityEngine;
using Michsky.UI.Dark;

public class MissionPanelController : MonoBehaviour
{
    [Tooltip("모든 스테이지의 미션 상세창(Mission_Stage1, Mission_Stage2 ...)을 1번부터 순서대로 넣어주세요.")]
    public GameObject[] missionPanels;

    [Header("Dissolve Transition")]
    [Tooltip("Transition_Overlay에 붙어있는 UIDissolveEffect (항상 활성화, Raycast Target OFF)")]
    public UIDissolveEffect transitionHelper;

    [Tooltip("화면을 덮는 데 걸리는 시간 (초)")]
    public float coverDuration = 0.25f;

    [Tooltip("화면이 다시 드러나는 데 걸리는 시간 (초)")]
    public float revealDuration = 0.3f;

    private int currentActiveIndex = -1; // 현재 열린 패널 인덱스 (-1 = 없음)
    private bool isAnimating = false;

    void OnEnable()
    {
        // 이 오브젝트가 포함된 패널이 꺼졌다 켜질 때 플래그 초기화
        isAnimating = false;
    }

    /// <summary>
    /// 스테이지 버튼 OnClick()에 연결.
    /// (스테이지 1 버튼 → 0 입력, 스테이지 2 버튼 → 1 입력)
    /// 기존 패널이 Dissolve로 닫히고 새 패널이 Dissolve로 열립니다.
    /// </summary>
    public void ActivatePanel(int targetIndex)
    {
        if (isAnimating) return;

        // 같은 패널을 또 누른 경우 무시
        if (targetIndex == currentActiveIndex) return;

        StartCoroutine(SwitchPanelRoutine(targetIndex));
    }

    private IEnumerator SwitchPanelRoutine(int targetIndex)
    {
        isAnimating = true;

        // ── 1단계: Dissolve로 화면을 덮는다 (location: 1 → 0) ──
        if (transitionHelper != null)
        {
            transitionHelper.animationSpeed = 1f / coverDuration;
            transitionHelper.location = 1f;
            transitionHelper.DissolveIn(); // 오버레이가 화면을 가득 채움
        }

        yield return new WaitForSecondsRealtime(coverDuration);

        // ── 2단계: 패널 교체 (화면이 완전히 가려진 순간) ──
        // 기존 패널 끄기
        if (currentActiveIndex >= 0 && currentActiveIndex < missionPanels.Length)
        {
            if (missionPanels[currentActiveIndex] != null)
                missionPanels[currentActiveIndex].SetActive(false);
        }

        // 새 패널 켜기
        if (targetIndex >= 0 && targetIndex < missionPanels.Length)
        {
            if (missionPanels[targetIndex] != null)
                missionPanels[targetIndex].SetActive(true);
        }

        currentActiveIndex = targetIndex;

        // ── 3단계: Dissolve가 걷히며 새 패널이 드러난다 (location: 0 → 1) ──
        if (transitionHelper != null)
        {
            transitionHelper.animationSpeed = 1f / revealDuration;
            transitionHelper.location = 0f;
            transitionHelper.DissolveOut(); // 오버레이가 사라지며 새 패널 드러남
        }

        yield return new WaitForSecondsRealtime(revealDuration);

        isAnimating = false;
    }

    /// <summary>
    /// 닫기(Close) 버튼에 연결. 모든 패널을 즉시 닫습니다.
    /// </summary>
    public void CloseAllPanels()
    {
        StopAllCoroutines();
        isAnimating = false;
        currentActiveIndex = -1;

        for (int i = 0; i < missionPanels.Length; i++)
        {
            if (missionPanels[i] != null)
                missionPanels[i].SetActive(false);
        }
    }
}
