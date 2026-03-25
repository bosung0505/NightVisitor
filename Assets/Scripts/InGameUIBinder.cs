using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Map 1/2 공용: 켜진 캔버스가 자신 안의 UI 부품들을 카메라 스크립트에 즉시 덮어씌워 연결을 복구합니다.
/// </summary>
public class InGameUIBinder : MonoBehaviour
{
    [Header("CameraController UI")]
    public RectTransform cancelZoneUI;
    public Image crosshairImage;
    public RectTransform scopeOverlayUI;
    public Button zoomButton;

    [Header("RaycastShooter UI")]
    public TextMeshProUGUI currentAmmoText;
    public TextMeshProUGUI reloadableAmmoText;
    public Button reloadButton;

    [Header("Battery UI")]
    public GameObject[] batteryCounts; // UI Hierarchy에서 가져옴 (Count3, Count2)
    public GameObject lastBatteryCount; // Count1
    public GameObject batteryCase;

    private void OnEnable()
    {
        if (Camera.main != null)
        {
            // 1. 카메라 컨트롤러 연동
            CameraController camController = Camera.main.GetComponent<CameraController>();
            if (camController != null)
            {
                camController.cancelZoneUI = cancelZoneUI;
                camController.crosshairImage = crosshairImage;
                camController.scopeOverlayUI = scopeOverlayUI;
                camController.zoomButton = zoomButton;
                
                // 장착 스코프 여부에 따라 줌 버튼 최신화
                camController.UpdateZoomButtonVisibility(); 
            }

            // 2. 사격 스크립트 연동
            RaycastShooter shooter = Camera.main.GetComponent<RaycastShooter>();
            if (shooter == null) shooter = RaycastShooter.Instance;

            if (shooter != null)
            {
                shooter.currentAmmoText = currentAmmoText;
                shooter.reloadableAmmoText = reloadableAmmoText;
                
                // 이전 UI 껍데기 버튼에 연결되어 있던 리스너 제거 (오류 및 누수 방지)
                if (shooter.reloadButton != null)
                {
                    shooter.reloadButton.onClick.RemoveListener(shooter.TryReload);
                }

                // 새 활성 패널의 버튼을 물려줌
                shooter.reloadButton = reloadButton;

                if (shooter.reloadButton != null)
                {
                    // 중복 등록 방지 후 리스너 연결
                    shooter.reloadButton.onClick.RemoveListener(shooter.TryReload);
                    shooter.reloadButton.onClick.AddListener(shooter.TryReload);
                }

                // UI 연동이 끝나는 즉시 총알 개수를 화면에 그림
                shooter.UpdateAmmoUI();
            }

            // 3. 배터리 컨트롤러 연동
            if (BatteryController.Instance != null)
            {
                if (batteryCounts != null && batteryCounts.Length > 0)
                    BatteryController.Instance.batteryCounts = batteryCounts;

                if (lastBatteryCount != null)
                    BatteryController.Instance.lastBatteryCount = lastBatteryCount;

                if (batteryCase != null)
                    BatteryController.Instance.batteryCase = batteryCase;
            }
        }
    }
}
