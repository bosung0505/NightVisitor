using UnityEngine;
using Michsky.UI.Dark;

/// <summary>
/// 설정창 UI 요소(SliderManager, HorizontalSelector)를 GameSettingsManager에 연결하는 바인더.
/// 메인메뉴 설정창과 인게임 설정창 양쪽에 모두 사용 가능.
/// 인게임 전용(sensitivitySlider) 또는 메인메뉴 전용(languageSelector) 필드는 없으면 비워두세요.
///
/// [중요] 각 SliderManager 인스펙터에서:
///   - Save Value: ☐ 체크 해제  ← GameSettingsManager가 저장을 담당합니다
///   - Min Value : 볼륨 슬라이더는 0, 민감도 슬라이더는 1
///   - Max Value : 볼륨 슬라이더는 1, 민감도 슬라이더는 20
/// </summary>
public class SettingsPanelBinder : MonoBehaviour
{
    [Header("공통 볼륨 슬라이더 (메인메뉴 + 인게임)")]
    public SliderManager bgmSlider;
    public SliderManager sfxSlider;

    [Header("인게임 전용 (없으면 비워두세요)")]
    public SliderManager sensitivitySlider;

    [Header("메인메뉴 전용 (없으면 비워두세요)")]
    public HorizontalSelector languageSelector;

    private bool listenersRegistered = false;

    // ────────────────────────────────────────────────────────
    void Start()
    {
        RegisterListeners();
    }

    void OnEnable()
    {
        // 패널이 열릴 때마다 GameSettingsManager의 값으로 슬라이더 초기화
        InitializeFromSettings();

        // GameSettingsManager 이벤트 구독 (다른 설정창에서 값이 바뀌면 이 슬라이더도 동기화)
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnBGMVolumeChanged += SyncBGMSlider;
            GameSettingsManager.Instance.OnSFXVolumeChanged += SyncSFXSlider;
        }
    }

    void OnDisable()
    {
        // 패널이 닫힐 때 이벤트 구독 해제 (메모리 누수 방지)
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnBGMVolumeChanged -= SyncBGMSlider;
            GameSettingsManager.Instance.OnSFXVolumeChanged -= SyncSFXSlider;
        }
    }

    // ────────────────────────────────────────────────────────
    // GameSettingsManager의 현재 설정값을 슬라이더 UI에 반영
    // ────────────────────────────────────────────────────────
    private void InitializeFromSettings()
    {
        if (GameSettingsManager.Instance == null) return;

        // mainSlider.value = x 방식으로 설정:
        // → SliderManager의 onValueChanged 이벤트도 발생 → 표시 텍스트(%) 자동 업데이트
        // → 리스너(OnBGMChanged 등)는 Start()에서 등록되므로 첫 OnEnable 시에는 중복 호출 없음
        if (bgmSlider != null && bgmSlider.mainSlider != null)
            bgmSlider.mainSlider.value = GameSettingsManager.Instance.BGMVolume;

        if (sfxSlider != null && sfxSlider.mainSlider != null)
            sfxSlider.mainSlider.value = GameSettingsManager.Instance.SFXVolume;

        if (sensitivitySlider != null && sensitivitySlider.mainSlider != null)
            sensitivitySlider.mainSlider.value = GameSettingsManager.Instance.CameraSensitivity;
    }

    // ────────────────────────────────────────────────────────
    // GameSettingsManager 이벤트 핸들러 — 다른 설정창이 값을 바꾸면 이쪽도 업데이트
    // ────────────────────────────────────────────────────────
    private void SyncBGMSlider(float value)
    {
        if (bgmSlider != null && bgmSlider.mainSlider != null)
            bgmSlider.mainSlider.SetValueWithoutNotify(value);
    }

    private void SyncSFXSlider(float value)
    {
        if (sfxSlider != null && sfxSlider.mainSlider != null)
            sfxSlider.mainSlider.SetValueWithoutNotify(value);
    }

    // ────────────────────────────────────────────────────────
    // GameSettingsManager 연동 리스너 등록 (한 번만)
    // ────────────────────────────────────────────────────────
    private void RegisterListeners()
    {
        if (listenersRegistered) return;
        listenersRegistered = true;

        if (bgmSlider != null && bgmSlider.mainSlider != null)
            bgmSlider.mainSlider.onValueChanged.AddListener(OnBGMChanged);

        if (sfxSlider != null && sfxSlider.mainSlider != null)
            sfxSlider.mainSlider.onValueChanged.AddListener(OnSFXChanged);

        if (sensitivitySlider != null && sensitivitySlider.mainSlider != null)
            sensitivitySlider.mainSlider.onValueChanged.AddListener(OnSensitivityChanged);

        if (languageSelector != null)
            languageSelector.onValueChanged.AddListener(OnLanguageChanged);
    }

    // ────────────────────────────────────────────────────────
    // 각 UI 요소 → GameSettingsManager 콜백
    // ────────────────────────────────────────────────────────
    private void OnBGMChanged(float value)      => GameSettingsManager.Instance?.SetBGMVolume(value);
    private void OnSFXChanged(float value)      => GameSettingsManager.Instance?.SetSFXVolume(value);
    private void OnSensitivityChanged(float v)  => GameSettingsManager.Instance?.SetCameraSensitivity(v);
    private void OnLanguageChanged(int index)   => GameSettingsManager.Instance?.SetLanguage(index);
}
