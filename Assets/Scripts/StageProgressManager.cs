using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 진행 상황을 PlayerPrefs에 저장/관리하고
/// 버튼 시각 상태(잠금/해제 색상)를 제어합니다.
///
/// [사용법]
/// 1. 씬의 항상 켜진 오브젝트(예: Managers)에 부착
/// 2. Inspector에서 lockedColor 설정
/// 3. StageSelectManager가 Start()에서 RegisterStages()를 호출해 버튼을 등록
/// 4. 스테이지 시작 시 SetCurrentStage(), 클리어 시 OnCurrentStageClear() 자동 호출
/// </summary>
public class StageProgressManager : MonoBehaviour
{
    public static StageProgressManager Instance;

    [Header("Locked Stage Visual")]
    [Tooltip("아직 해금되지 않은 스테이지 버튼에 적용할 색상 (인스펙터에서 자유롭게 설정)")]
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    // ─── 내부 상태 ────────────────────────────────────────────────────
    private Button[]     _map1Buttons,    _map2Buttons;
    private Color[]      _map1OrigColors, _map2OrigColors;
    private ColorBlock[] _map1OrigBlocks, _map2OrigBlocks;

    private int _currentMapId    = 0;
    private int _currentStageIdx = 0; // 1-based

    // ─── Singleton ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─── 공개 API ─────────────────────────────────────────────────────

    /// <summary>
    /// StageSelectManager.Start()에서 버튼 배열을 넘겨 등록합니다.
    /// 원본 색상을 캐싱한 뒤 즉시 시각 상태를 적용합니다.
    /// </summary>
    public void RegisterStages(Button[] map1Buttons, Button[] map2Buttons)
    {
        _map1Buttons    = map1Buttons;
        _map2Buttons    = map2Buttons;
        _map1OrigColors = CacheImageColors(map1Buttons);
        _map2OrigColors = CacheImageColors(map2Buttons);
        _map1OrigBlocks = CacheColorBlocks(map1Buttons);
        _map2OrigBlocks = CacheColorBlocks(map2Buttons);

        ApplyButtonStates();
    }

    /// <summary>
    /// 스테이지 시작 직전 StageSelectManager에서 호출합니다.
    /// </summary>
    public void SetCurrentStage(int mapId, int stageIndex)
    {
        _currentMapId    = mapId;
        _currentStageIdx = stageIndex;
    }

    /// <summary>
    /// 클리어 패널(MissionClearPanel / SurvivePanel)이 뜰 때 호출합니다.
    /// 현재 스테이지를 저장하고 다음 스테이지를 해금합니다.
    /// </summary>
    public void OnCurrentStageClear()
    {
        if (_currentMapId == 0) return;

        // 클리어한 스테이지 저장
        SetUnlocked(_currentMapId, _currentStageIdx);

        // 다음 스테이지 해금
        int maxStages = _currentMapId == 1
            ? (_map1Buttons?.Length ?? 0)
            : (_map2Buttons?.Length ?? 0);

        int nextIdx = _currentStageIdx + 1;
        if (nextIdx <= maxStages)
            SetUnlocked(_currentMapId, nextIdx);

        ApplyButtonStates();

        Debug.Log($"[StageProgressManager] Map{_currentMapId} Stage{_currentStageIdx} 클리어 " +
                  $"→ Stage{nextIdx} 해금 완료");
    }

    /// <summary>
    /// 버튼 잠금/해금 시각 상태를 전체 갱신합니다.
    /// MapCarouselUI(맵2 해금 애니메이션 완료 후)에서도 호출할 수 있습니다.
    /// </summary>
    public void ApplyButtonStates()
    {
        ApplyToMap(_map1Buttons, _map1OrigColors, _map1OrigBlocks, 1);
        ApplyToMap(_map2Buttons, _map2OrigColors, _map2OrigBlocks, 2);
    }

    // ─── PlayerPrefs 헬퍼 ─────────────────────────────────────────────

    private static string GetKey(int mapId, int stageIdx)
        => $"Map{mapId}_Stage{stageIdx}_Unlocked";

    /// <summary>해당 스테이지가 해금됐는지 확인합니다 (외부에서도 사용 가능).</summary>
    public static bool IsUnlocked(int mapId, int stageIdx)
    {
        // Map1 Stage1은 항상 해금
        if (mapId == 1 && stageIdx == 1) return true;

        // Map2 Stage1은 "Map2_Unlocked" 키(맵1 전체 클리어)로도 해금
        if (mapId == 2 && stageIdx == 1)
            return PlayerPrefs.GetInt("Map2_Unlocked", 0) == 1
                || PlayerPrefs.GetInt(GetKey(2, 1), 0) == 1;

        return PlayerPrefs.GetInt(GetKey(mapId, stageIdx), 0) == 1;
    }

    private static void SetUnlocked(int mapId, int stageIdx)
    {
        PlayerPrefs.SetInt(GetKey(mapId, stageIdx), 1);
        PlayerPrefs.Save();
    }

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────

    private static Color[] CacheImageColors(Button[] buttons)
    {
        if (buttons == null) return null;
        var arr = new Color[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            Image img = buttons[i].GetComponent<Image>();
            arr[i] = img != null ? img.color : Color.white;
        }
        return arr;
    }

    private static ColorBlock[] CacheColorBlocks(Button[] buttons)
    {
        if (buttons == null) return null;
        var arr = new ColorBlock[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) arr[i] = buttons[i].colors;
        return arr;
    }

    private void ApplyToMap(Button[] buttons, Color[] origColors, ColorBlock[] origBlocks, int mapId)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            bool unlocked = IsUnlocked(mapId, i + 1); // i+1 = 1-based index

            buttons[i].interactable = unlocked;

            Image img = buttons[i].GetComponent<Image>();
            if (unlocked)
            {
                // 원래 색상 & ColorBlock 복원
                if (img != null && origColors != null) img.color = origColors[i];
                if (origBlocks != null) buttons[i].colors = origBlocks[i];
            }
            else
            {
                // 잠금 색상 적용 + disabledColor를 흰색으로 맞춰 중복 틴팅 방지
                if (img != null) img.color = lockedColor;
                ColorBlock cb = buttons[i].colors;
                cb.disabledColor = Color.white;
                buttons[i].colors = cb;
            }
        }
    }
}
