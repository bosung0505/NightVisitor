using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 게임 전역 설정을 관리하는 싱글톤.
/// BGM/SFX 볼륨, 카메라 민감도, 언어 설정을 PlayerPrefs에 저장/불러오기하고 실시간 적용합니다.
///
/// [에디터 세팅]
/// - 씬의 영구 오브젝트(Canvas 루트 또는 별도 Manager 오브젝트)에 부착
/// - AudioMixer:    NightVisitorMixer 에셋 연결
/// - SFX Mixer Group: NightVisitorMixer의 SFX 그룹 연결
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Audio")]
    [Tooltip("NightVisitorMixer 에셋을 여기에 연결하세요.")]
    public AudioMixer audioMixer;

    [Tooltip("NightVisitorMixer의 BGM 그룹.\n스크립트로 재생하는 BGM AudioSource에 자동 연결할 때 사용합니다.")]
    public AudioMixerGroup bgmMixerGroup;

    [Tooltip("NightVisitorMixer의 SFX 그룹.\n런타임에 생성되는 AudioSource(총소리 등)에 자동 연결됩니다.")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("Default Values")]
    [Range(0f, 1f)] public float defaultBGMVolume      = 0.6f;
    [Range(0f, 1f)] public float defaultSFXVolume      = 0.6f;
    public float defaultCameraSensitivity = 5f;
    public int   defaultLanguage          = 0;   // 0=한국어, 1=English

    // ── PlayerPrefs 키 ──
    private const string KEY_BGM  = "Setting_BGM";
    private const string KEY_SFX  = "Setting_SFX";
    private const string KEY_LANG = "Setting_Language";
    private const string KEY_CAM  = "Setting_CamSensitivity";

    // ── 현재 설정값 프로퍼티 (외부 읽기용) ──
    public float BGMVolume         { get; private set; }
    public float SFXVolume         { get; private set; }
    public int   Language          { get; private set; }
    public float CameraSensitivity { get; private set; }

    // ── 슬라이더 동기화용 이벤트 ──
    // SettingsPanelBinder들이 구독해서 두 설정창의 슬라이더를 연동합니다.
    public event System.Action<float> OnBGMVolumeChanged;
    public event System.Action<float> OnSFXVolumeChanged;

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAndApplyAll();
    }

    void Start()
    {
        // 유니티 엔진 고질적 버그: Awake 시점에는 AudioMixer의 SetFloat가 무시되는 현상이 있습니다.
        // 이 때문에 게임 시작 직후 들리는 실제 볼륨이 100(0dB)으로 초기화되어 버리는 문제를 막기 위해,
        // Start 시점에서 믹서에 명시적으로 한 번 더 값을 때려 넣어줍니다.
        if (audioMixer != null)
        {
            audioMixer.SetFloat("BGMVolume", LinearTodB(BGMVolume));
            audioMixer.SetFloat("SFXVolume", LinearTodB(SFXVolume));
        }
    }

    // ────────────────────────────────────────────────────────
    public void LoadAndApplyAll()
    {
        SetBGMVolume         (PlayerPrefs.GetFloat(KEY_BGM,  defaultBGMVolume));
        SetSFXVolume         (PlayerPrefs.GetFloat(KEY_SFX,  defaultSFXVolume));
        SetLanguage          (PlayerPrefs.GetInt  (KEY_LANG, defaultLanguage));
        SetCameraSensitivity (PlayerPrefs.GetFloat(KEY_CAM,  defaultCameraSensitivity));
    }

    // ────────────────────────────────────────────────────────
    // BGM 볼륨
    // ────────────────────────────────────────────────────────
    public void SetBGMVolume(float value)
    {
        BGMVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_BGM, BGMVolume);

        if (audioMixer != null)
            audioMixer.SetFloat("BGMVolume", LinearTodB(BGMVolume));

        // ★ 다른 설정창 슬라이더도 같은 값으로 동기화
        OnBGMVolumeChanged?.Invoke(BGMVolume);
    }

    // ────────────────────────────────────────────────────────
    // SFX 볼륨
    // ────────────────────────────────────────────────────────
    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_SFX, SFXVolume);

        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", LinearTodB(SFXVolume));

        // ★ 다른 설정창 슬라이더도 같은 값으로 동기화
        OnSFXVolumeChanged?.Invoke(SFXVolume);
    }

    // ────────────────────────────────────────────────────────
    // 카메라 민감도
    // ────────────────────────────────────────────────────────
    public void SetCameraSensitivity(float value)
    {
        CameraSensitivity = value;
        PlayerPrefs.SetFloat(KEY_CAM, CameraSensitivity);

        CameraController cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null) cam.touchPanSpeed = CameraSensitivity;
    }

    // ────────────────────────────────────────────────────────
    // 언어 설정
    // ────────────────────────────────────────────────────────
    public void SetLanguage(int index)
    {
        Language = index;
        PlayerPrefs.SetInt(KEY_LANG, Language);
        Debug.Log($"[GameSettingsManager] 언어 변경: {(Language == 0 ? "한국어" : "English")}");
    }

    // ────────────────────────────────────────────────────────
    // 런타임 생성 AudioSource에 SFX 믹서 그룹 연결 (RaycastShooter 등에서 호출)
    // ────────────────────────────────────────────────────────
    public void AssignSFXGroup(AudioSource source)
    {
        if (source != null && sfxMixerGroup != null)
            source.outputAudioMixerGroup = sfxMixerGroup;
    }

    // ────────────────────────────────────────────────────────
    // 런타임 생성 AudioSource에 BGM 믹서 그룹 연결
    // ────────────────────────────────────────────────────────
    public void AssignBGMGroup(AudioSource source)
    {
        if (source != null && bgmMixerGroup != null)
            source.outputAudioMixerGroup = bgmMixerGroup;
    }

    // ────────────────────────────────────────────────────────
    private float LinearTodB(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }
}
