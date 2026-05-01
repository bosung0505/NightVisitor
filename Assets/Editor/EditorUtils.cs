using UnityEngine;
using UnityEditor;

public class EditorUtils
{
    [MenuItem("Tools/Clear All Save Data (PlayerPrefs)")]
    public static void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("모든 세이브 데이터(PlayerPrefs)가 초기화되었습니다! 이제 맵2가 다시 잠깁니다.");
    }
}
