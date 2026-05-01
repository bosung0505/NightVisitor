using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 게임 전역 설정을 관리하는 싱글톤.
/// BGM/SFX 볼륨, 카메라 민감도, 언어 설정을 PlayerPrefs에 저장/불러오기하고 실시간 적용합니다.
///
/// [에디터 세팅]
/// - 씬의 영구 오브젝트(Canvas 루트 또는 별도 Manager 오브젝트)에 부착
/// - AudioMixer: NightVisitorMixer 에셋 연결
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Audio")]
    [Tooltip("NightVisitorMixer 에셋을 여기에 연결하세요.")]
    public AudioMixer audioMixer;

    [Header("Default Values")]
    [Range(0f, 1f)] public float defaultBGMVolume      = 1f;
    [Range(0f, 1f)] public float defaultSFXVolume      = 1f;
    public float defaultCameraSensitivity = 5f;   // CameraController.touchPanSpeed 기본값
    public int   defaultLanguage          = 0;    // 0 = 한국어, 1 = English

    // ── PlayerPrefs 키 ──
    private const string KEY_BGM  = "Setting_BGM";
    private const string KEY_SFX  = "Setting_SFX";
    private const string KEY_LANG = "Setting_Language";
    private const string KEY_CAM  = "Setting_CamSensitivity";

    // ── 현재 설정값 프로퍼티 (외부 읽기용) ──
    public float BGMVolume          { get; private set; }
    public float SFXVolume          { get; private set; }
    public int   Language           { get; private set; }
    public float CameraSensitivity  { get; private set; }

    // ────────────────────────────────────────────────────────
    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAndApplyAll();
    }

    // ────────────────────────────────────────────────────────
    // 저장된 값 전부 불러와서 즉시 적용
    // ────────────────────────────────────────────────────────
    public void LoadAndApplyAll()
    {
        SetBGMVolume           (PlayerPrefs.GetFloat(KEY_BGM,  defaultBGMVolume));
        SetSFXVolume           (PlayerPrefs.GetFloat(KEY_SFX,  defaultSFXVolume));
        SetLanguage            (PlayerPrefs.GetInt  (KEY_LANG, defaultLanguage));
        SetCameraSensitivity   (PlayerPrefs.GetFloat(KEY_CAM,  defaultCameraSensitivity));
    }

    // ────────────────────────────────────────────────────────
    // BGM 볼륨 (0~1 → dB 변환 후 AudioMixer 적용)
    // ────────────────────────────────────────────────────────
    public void SetBGMVolume(float value)
    {
        BGMVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_BGM, BGMVolume);

        if (audioMixer != null)
            audioMixer.SetFloat("BGMVolume", LinearTodB(BGMVolume));
    }

    // ────────────────────────────────────────────────────────
    // SFX 볼륨 (0~1 → dB 변환 후 AudioMixer 적용)
    // ────────────────────────────────────────────────────────
    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_SFX, SFXVolume);

        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", LinearTodB(SFXVolume));
    }

    // ────────────────────────────────────────────────────────
    // 카메라 민감도 → CameraController.touchPanSpeed에 직접 적용
    // ────────────────────────────────────────────────────────
    public void SetCameraSensitivity(float value)
    {
        CameraSensitivity = value;
        PlayerPrefs.SetFloat(KEY_CAM, CameraSensitivity);

        // Camera.main에 붙은 CameraController에 즉시 반영
        if (Camera.main != null)
        {
            CameraController cam = Camera.main.GetComponent<CameraController>();
            if (cam != null) cam.touchPanSpeed = CameraSensitivity;
        }
    }

    // ────────────────────────────────────────────────────────
    // 언어 설정 (0=한국어, 1=English)
    // → 추후 LocalizationManager 연동 예정
    // ────────────────────────────────────────────────────────
    public void SetLanguage(int index)
    {
        Language = index;
        PlayerPrefs.SetInt(KEY_LANG, Language);

        // TODO: LocalizationManager.Instance?.ApplyLanguage(Language);
        Debug.Log($"[GameSettingsManager] 언어 변경: {(Language == 0 ? "한국어" : "English")}");
    }

    // ────────────────────────────────────────────────────────
    // 유틸: 선형 볼륨(0~1) → 데시벨(dB) 변환
    // 값이 0이면 -80dB(사실상 무음)으로 처리
    // ────────────────────────────────────────────────────────
    private float LinearTodB(float linear)
    {
        return linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
    }
}
