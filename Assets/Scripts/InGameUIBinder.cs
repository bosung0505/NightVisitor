using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Map 1/2 공용: 켜진 캔버스가 자신 안의 UI 부품들을 카메라 스크립트에 즉시 덮어씌워 연결을 복구합니다.
/// </summary>
public class InGameUIBinder : MonoBehaviour
{
    /// <summary>InventoryManager 등 외부에서 RefreshAggroButton()을 호출하기 위한 싱글톤.</summary>
    public static InGameUIBinder Instance;
    [Header("CameraController UI")]
    public RectTransform cancelZoneUI;
    public Image crosshairImage;
    public RectTransform scopeOverlayUI;
    public Button zoomButton;

    [Header("RaycastShooter UI")]
    public TextMeshProUGUI currentAmmoText;
    public TextMeshProUGUI reloadableAmmoText;
    public Button reloadButton;

    [Header("Aggro Bullet UI (Map 2 Only)")]
    [Tooltip("어그로 탄 발사 버튼. Map2 패널에만 달고 부모 게임오브젝트 비활성화로 두세요.")]
    public Button aggroBulletButton;
    [Tooltip("체크 시 이 UIBinder가 Map2 패널임을 나타냅니다. Map1 UIBinder는 체크 해제.")]
    public bool isMap2UIPanel = false;

    [Header("Battery UI")]
    public GameObject[] batteryCounts; // UI Hierarchy에서 가져옴 (Count3, Count2)
    public GameObject lastBatteryCount; // Count1
    public GameObject batteryCase;

    private void Awake()
    {
        Instance = this;
    }

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

            // 4. 어그로 버튼 초기 상태 갱신
            RefreshAggroButton();
        }
    }

    /// <summary>
    /// 어그로 버튼의 활성/비활성을 현재 장착 상태와 맵 판별에 따라 갱신합니다.
    /// - Map2 UIBinder + AggroAmmo 장착 → 버튼 활성화
    /// - Map1 UIBinder OR 미장착 → 버튼 비활성화
    /// 버튼은 소모품이므로 RaycastShooter.FireAggroBullet() 호출 후 스스로 비활성화됩니다.
    /// </summary>
    public void RefreshAggroButton()
    {
        if (aggroBulletButton == null) return;

        bool hasAmmo   = (InventoryManager.Instance != null &&
                          InventoryManager.Instance.GetEquippedAggroAmmoData() != null);
        bool shouldShow = isMap2UIPanel && hasAmmo;

        aggroBulletButton.gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            // 기존 리스너 누적 방지 후 연결
            aggroBulletButton.onClick.RemoveAllListeners();
            aggroBulletButton.onClick.AddListener(() =>
            {
                if (RaycastShooter.Instance != null)
                    RaycastShooter.Instance.SetNextShotAsAggro();
            });
        }
    }
}
