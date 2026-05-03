using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;

/// <summary>
/// 선택한 프리팹/오브젝트 내의 모든 AudioSource를 지정한 AudioMixerGroup에 일괄 연결합니다.
/// 사용법: Unity 메뉴 → NightVisitor → Audio → Assign SFX Mixer Group
/// </summary>
public class SFXMixerAssigner : EditorWindow
{
    private AudioMixerGroup targetGroup;
    private bool includeBGMSources = false;
    private bool onlyUnassigned = true;

    [MenuItem("NightVisitor/Audio/Assign SFX Mixer Group to Selection")]
    public static void ShowWindow()
    {
        GetWindow<SFXMixerAssigner>("SFX Mixer Assigner");
    }

    void OnGUI()
    {
        GUILayout.Label("SFX AudioMixer 일괄 연결 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetGroup = (AudioMixerGroup)EditorGUILayout.ObjectField(
            "SFX Mixer Group",
            targetGroup,
            typeof(AudioMixerGroup),
            false
        );

        onlyUnassigned = EditorGUILayout.Toggle(
            "Output 미연결된 것만 처리",
            onlyUnassigned
        );

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Hierarchy 또는 Project에서 처리할 오브젝트/프리팹을 선택한 후 버튼을 누르세요.",
            MessageType.Info
        );

        if (targetGroup == null)
        {
            EditorGUILayout.HelpBox("SFX Mixer Group을 먼저 지정해주세요.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("선택된 오브젝트의 AudioSource에 SFX 그룹 연결", GUILayout.Height(40)))
        {
            AssignToSelected();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("프로젝트 내 모든 프리팹에 SFX 그룹 연결 (전체 처리)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog(
                "전체 프리팹 처리",
                "Assets 폴더 내 모든 프리팹의 AudioSource에 SFX 그룹을 연결합니다.\n계속하시겠습니까?",
                "진행", "취소"))
            {
                AssignToAllPrefabs();
            }
        }
    }

    private void AssignToSelected()
    {
        int count = 0;
        foreach (var obj in Selection.gameObjects)
        {
            count += ProcessGameObject(obj);
        }

        // 선택된 에셋(프리팹 파일)도 처리
        foreach (var asset in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            count += ProcessPrefabAsset(asset);
        }

        EditorUtility.DisplayDialog("완료", $"{count}개의 AudioSource에 SFX 그룹을 연결했습니다.", "확인");
    }

    private void AssignToAllPrefabs()
    {
        int totalCount = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("프리팹 처리 중...", path, (float)i / guids.Length);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                totalCount += ProcessPrefabAsset(prefab);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("완료", $"전체 {totalCount}개의 AudioSource에 SFX 그룹을 연결했습니다.", "확인");
    }

    private int ProcessPrefabAsset(GameObject prefabAsset)
    {
        int count = 0;
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(path)) return 0;

        // 프리팹 편집 모드로 열기
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        if (prefabRoot == null) return 0;

        count = ProcessGameObject(prefabRoot);

        if (count > 0)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            Debug.Log($"[SFXMixerAssigner] {path} → {count}개 연결");
        }

        PrefabUtility.UnloadPrefabContents(prefabRoot);
        return count;
    }

    private int ProcessGameObject(GameObject root)
    {
        int count = 0;
        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);

        foreach (var src in sources)
        {
            if (onlyUnassigned && src.outputAudioMixerGroup != null) continue;

            Undo.RecordObject(src, "Assign SFX Mixer Group");
            src.outputAudioMixerGroup = targetGroup;
            EditorUtility.SetDirty(src);
            count++;
        }

        return count;
    }
}
