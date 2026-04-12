using UnityEngine;

public class MissionPanelController : MonoBehaviour
{
    [Tooltip("모든 스테이지의 미션 상세창(Mission_Stage1, Mission_Stage2 ...)을 1번부터 20번까지 순서대로 넣어주세요.")]
    public GameObject[] missionPanels;

    /// <summary>
    /// 스테이지 메뉴 버튼의 OnClick()에 연결하고, 숫자를 입력하세요. 
    /// (배열 인덱스이므로 스테이지 1 버튼은 0을 입력, 스테이지 20 버튼은 19를 입력합니다)
    /// 지정된 인덱스만 켜고 나머지는 전부 자동으로 끕니다.
    /// </summary>
    public void ActivatePanel(int targetIndex)
    {
        for (int i = 0; i < missionPanels.Length; i++)
        {
            if (missionPanels[i] != null)
            {
                missionPanels[i].SetActive(i == targetIndex);
            }
        }
    }

    /// <summary>
    /// 돌아가기(Close) 버튼 등을 눌러 모든 패널을 닫고 싶을 때 사용합니다.
    /// </summary>
    public void CloseAllPanels()
    {
        for (int i = 0; i < missionPanels.Length; i++)
        {
            if (missionPanels[i] != null)
            {
                missionPanels[i].SetActive(false);
            }
        }
    }
}
