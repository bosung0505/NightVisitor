using System.Collections;
using UnityEngine;

/// <summary>
/// 메뉴 BGM ↔ 인게임 BGM 크로스페이드를 관리하는 싱글톤.
///
/// [에디터 세팅]
/// 1. 씬의 영구 오브젝트에 이 컴포넌트 부착
/// 2. Menu BGM Source  → AudioSource (Loop ON, Play On Awake ON)
///                       Output: NightVisitorMixer의 BGM 그룹
/// 3. InGame BGM Source → AudioSource (Loop ON, Play On Awake OFF, Volume 0)
///                        Output: NightVisitorMixer의 BGM 그룹
/// 4. 각 AudioSource에 오디오 클립 할당
/// </summary>
public class BGMAudioManager : MonoBehaviour
{
    public static BGMAudioManager Instance { get; private set; }

    [Header("BGM Sources")]
    [Tooltip("메인메뉴/스타트 화면에서 재생되는 BGM AudioSource\n(Loop ON, Play On Awake ON)")]
    public AudioSource menuBGMSource;

    [Tooltip("인게임에서 재생되는 BGM AudioSource\n(Loop ON, Play On Awake OFF, 초기 Volume 0)")]
    public AudioSource ingameBGMSource;

    [Header("Crossfade Settings")]
    [Tooltip("메뉴 BGM이 서서히 꺼지는 데 걸리는 시간 (초)")]
    public float fadeOutDuration = 1.5f;

    [Tooltip("인게임 BGM이 서서히 켜지는 데 걸리는 시간 (초)")]
    public float fadeInDuration  = 2.0f;

    [Tooltip("페이드아웃 시작 후 페이드인이 시작되기까지의 딜레이 (초)\n0이면 동시에 진행")]
    public float crossfadeDelay  = 0.5f;

    private Coroutine currentFade;

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 시작 시 인게임 BGM은 완전히 무음 상태로 준비
        if (ingameBGMSource != null)
        {
            ingameBGMSource.volume = 0f;
            ingameBGMSource.Stop();
        }
    }

    // ────────────────────────────────────────────────────────
    /// <summary>
    /// 게임 입장 시 호출: 메뉴 BGM 페이드아웃 → 인게임 BGM 페이드인
    /// StageSelectManager.StartGameRoutine() 에서 호출
    /// </summary>
    public void CrossFadeToIngame()
    {
        if (currentFade != null) StopCoroutine(currentFade);
        currentFade = StartCoroutine(CrossFadeRoutine(
            fadeOut: menuBGMSource,
            fadeIn:  ingameBGMSource,
            outDuration: fadeOutDuration,
            inDuration:  fadeInDuration,
            delay:        crossfadeDelay
        ));
    }

    // ────────────────────────────────────────────────────────
    /// <summary>
    /// 맵으로 돌아올 때 호출: 인게임 BGM 페이드아웃 → 메뉴 BGM 페이드인
    /// StageSelectManager.ReturnToMap() 에서 호출
    /// </summary>
    public void CrossFadeToMenu()
    {
        if (currentFade != null) StopCoroutine(currentFade);
        currentFade = StartCoroutine(CrossFadeRoutine(
            fadeOut: ingameBGMSource,
            fadeIn:  menuBGMSource,
            outDuration: fadeOutDuration,
            inDuration:  fadeInDuration,
            delay:        crossfadeDelay
        ));
    }

    // ────────────────────────────────────────────────────────
    private IEnumerator CrossFadeRoutine(
        AudioSource fadeOut, AudioSource fadeIn,
        float outDuration, float inDuration, float delay)
    {
        // 페이드인 소스가 재생 중이 아니면 볼륨 0으로 시작
        if (fadeIn != null && !fadeIn.isPlaying)
        {
            fadeIn.volume = 0f;
            fadeIn.Play();
        }

        float startOutVolume = fadeOut != null ? fadeOut.volume : 0f;
        float elapsed = 0f;

        // ── 1단계: 페이드아웃 ──
        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);

            if (fadeOut != null)
                fadeOut.volume = Mathf.Lerp(startOutVolume, 0f, t);

            yield return null;
        }

        if (fadeOut != null)
        {
            fadeOut.volume = 0f;
            fadeOut.Stop(); // 재생 중지 (루프 방지 & 성능)
        }

        // ── 2단계: 딜레이 ──
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        // ── 3단계: 페이드인 ──
        elapsed = 0f;
        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inDuration);

            if (fadeIn != null)
                fadeIn.volume = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        if (fadeIn != null)
            fadeIn.volume = 1f;

        currentFade = null;
    }
}
