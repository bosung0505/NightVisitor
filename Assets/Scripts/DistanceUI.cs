using UnityEngine;
using TMPro; // TextMeshPro를 위한 네임스페이스

public class DistanceUI : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI distanceText; // Yard 하위에 있는 Distance 텍스트 컴포넌트를 직접 연결

    [Header("Raycast Settings")]
    public Camera mainCamera;
    public float maxDetectionDistance = 100f; // 최대 감지 거리
    
    [Tooltip("에임을 맞췄을 때 거리가 표시될 대상을 레이어로 지정하세요 (예: Animal 레이어)")]
    public LayerMask targetLayer;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        if (distanceText == null)
        {
            // 이 스크립트를 Text 컴포넌트에 바로 넣었을 경우를 대비해 자동 찾기 시도
            distanceText = GetComponent<TextMeshProUGUI>();
        }
    }

    void Update()
    {
        if (mainCamera == null || distanceText == null) return;

        // 화면 정중앙(에임) 위치에서 레이 생성
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        // 지정한 targetLayer에 속하는 오브젝트(동물)에만 레이가 닿을 경우
        if (Physics.Raycast(ray, out hit, maxDetectionDistance, targetLayer))
        {
            // 카메라와 맞은 물체 사이의 거리를 계산
            float distance = Vector3.Distance(mainCamera.transform.position, hit.point);
            
            // 소수점을 버리고 정수로 변환하여 깔끔하게 표시 (예: 15m)
            int intDistance = Mathf.RoundToInt(distance);
            
            // 텍스트 업데이트 (숫자 뒤에 m나 cm 등 원하시는 단위를 붙일 수 있습니다)
            distanceText.text = intDistance.ToString("000"); // 3자리 숫자 포맷 "015", "005" 등 필요 시 "0" 대신 ToString() 그냥 써도 됨
        }
        else
        {
            // 허공이거나 다른 물체를 가리킬 때는 기본 거리를 000으로 표시
            distanceText.text = "000";
        }
    }
}
