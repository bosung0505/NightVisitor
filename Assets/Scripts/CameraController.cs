using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 20f;
    public float touchPanSpeed = 5f; // 모바일 터치 드래그 속도
    public bool useWorldSpace = false;

    private bool isPanning = false;
    private int activeTouchId = -1; // 모바일 드래그 중인 손가락 ID 추적용

    // Variables to track cumulative rotation
    private float pitch = 0f;
    private float yaw = 0f;

    [Header("Recoil Settings (Realistic)")]
    [Tooltip("에임이 원래 자리로 돌아오려는 힘 (스프링 속도)")]
    public float snappiness = 6f;
    [Tooltip("반동이 화면에 적용되는 속도 (부드러운 타격감을 조절)")]
    public float returnSpeed = 2f;
    
    // 현재 프레임의 실제 반동 오프셋
    private Vector3 currentRecoilOffset;
    // 반동으로 가야 할 목표 오프셋
    private Vector3 targetRecoilOffset;

    [Header("Idle Breathing")]
    [Tooltip("호흡 속도")]
    public float breathSpeed = 1.5f;
    [Tooltip("호흡 시 카메라 상하좌우 흔들림 정도")]
    public float breathAmount = 0.5f;
    private float breathTimer = 0f;

    [Header("Zoom Settings (Scope)")]
    [Tooltip("줌 속도 (Lerp 값)")]
    public float zoomLerpSpeed = 10f;
    [Tooltip("줌을 할 때 같이 켜질 포스트 프로세싱 볼륨 (Vignette 등)")]
    public UnityEngine.Rendering.Volume scopeVolume;
    [Tooltip("줌 버튼 UI (기본 스코프 장착 시 자동 숨김 처리)")]
    public UnityEngine.UI.Button zoomButton;

    private Camera cam;
    private float defaultFOV;
    private float targetFOV;
    private bool isZooming = false;
    private float currentZoomMultiplier = 1f;

    void Start()
    {
        // Initialize rotation variables with current camera rotation to prevent snapping
        Vector3 initialRotation = transform.eulerAngles;
        pitch = initialRotation.x;
        yaw = initialRotation.y;
        
        // Convert pitch from 0..360 to -180..180 if needed
        if (pitch > 180f) pitch -= 360f;
        if (yaw > 180f) yaw -= 360f;

        cam = GetComponent<Camera>();
        if (cam != null)
        {
            defaultFOV = cam.fieldOfView;
            targetFOV = defaultFOV;
        }

        // 초기화 시 스코프 볼륨 설정
        if (scopeVolume != null)
        {
            scopeVolume.weight = 0f;
            // 우선순위를 항상 높게 유지하여 다른 볼륨에 묻히지 않게 함
            scopeVolume.priority = 100f;
        }
    }

    void OnEnable()
    {
        // 메인 카메라가 켜질 때(스테이지 플레이 시작 시)마다 장착 정보를 읽어 줌 버튼을 덧씌웁니다.
        // Start()에 두면 카메라 활성화 최초 1회만 갱신되므로, 로비에서 변경 후 입장 시 씹히는 현상을 방지합니다.
        UpdateZoomButtonVisibility();
    }

    /// <summary>
    /// 장착 스코프 배율에 따라 줌 버튼을 켜거나 끄고, 배율이 없을 경우 강제로 줌을 해제합니다.
    /// </summary>
    public void UpdateZoomButtonVisibility()
    {
        if (zoomButton != null && InventoryManager.Instance != null)
        {
            float multiplier = InventoryManager.Instance.GetEquippedZoomMultiplier();
            zoomButton.gameObject.SetActive(multiplier > 1.05f);

            // 배율이 낮은 스코프로 교체했는데 현재 줌 상태라면 강제로 품
            if (multiplier <= 1.05f && isZooming)
            {
                ForceZoomOff();
            }
        }
    }

    [Header("New Touch/Click Mechanics")]
    [Tooltip("줌 상태일 때 게임 속도 (슬로우 모션 배율)")]
    public float zoomTimeScale = 0.5f;
    [Tooltip("터치 후 줌이 켜질 때까지 기다리는 최소 대기 시간 (초)")]
    public float holdThreshold = 0.15f;
    [Tooltip("터를 움직이지 않아 줌이 발동되는 거리 허용 범위 (픽셀)")]
    public float touchStationaryThreshold = 20f;
    
    // 상태 머신 변수들
    private float touchHoldTime = 0f;
    private float zoomActiveTimer = 0f;
    private bool isAimMode = false;      // 줌 상태에 진입했는지 여부
    private Vector2 touchStartPosition;  // 터치 시작 지점 (움직임 판독용)

    void Update()
    {
        // --- 부드러운 반동 복원 처리 ---
        // targetRecoil은 시간이 지날수록 0,0,0으로 서서히 돌아갑니다.
        targetRecoilOffset = Vector3.Lerp(targetRecoilOffset, Vector3.zero, returnSpeed * Time.unscaledDeltaTime);
        // currentRecoil은 targetRecoil을 빠르게 따라가며 스프링처럼 부드럽게 튀는 효과를 줍니다.
        currentRecoilOffset = Vector3.Slerp(currentRecoilOffset, targetRecoilOffset, snappiness * Time.unscaledDeltaTime);

        // --- 숨쉬기 (Idle Breathing) 애니메이션 처리 ---
        breathTimer += Time.unscaledDeltaTime * breathSpeed;
        // 상하(Pitch)는 사인함수, 좌우(Yaw)는 코사인함수를 써서 8자 혹은 원형 모양으로 부드럽게 흔들림
        float breathPitch = Mathf.Sin(breathTimer) * breathAmount;
        float breathYaw = Mathf.Cos(breathTimer * 0.5f) * (breathAmount * 0.5f);

        // --- 줌 FOV & Volume 조절 (Lerp) ---
        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, zoomLerpSpeed * Time.unscaledDeltaTime);
            if (Mathf.Abs(cam.fieldOfView - targetFOV) < 0.01f)
            {
                cam.fieldOfView = targetFOV;
            }
        }

        if (scopeVolume != null)
        {
            float targetWeight = isZooming ? 1f : 0f;
            scopeVolume.weight = Mathf.Lerp(scopeVolume.weight, targetWeight, zoomLerpSpeed * Time.unscaledDeltaTime);

            // 소수점 요동 방지
            if (Mathf.Abs(scopeVolume.weight - targetWeight) < 0.001f)
            {
                scopeVolume.weight = targetWeight;
            }
        }

        // 줌 2.5초 자동 해제 로직
        if (isAimMode && isZooming)
        {
            zoomActiveTimer += Time.unscaledDeltaTime; // 현실 시간 기준(unscaled)으로 측정
            if (zoomActiveTimer >= 2.5f)
            {
                CancelAimMode();
            }
        }

        // --- 회전 감도는 줌 배율에 반비례 (줌인하면 느려짐) ---
        float currentPanSensitivity = panSpeed / currentZoomMultiplier;
        float currentTouchSensitivity = touchPanSpeed / currentZoomMultiplier;

        bool handledByTouch = false;

#if ENABLE_INPUT_SYSTEM
        // --- 1. 모바일 환경: 터치 및 드래그 화면 회전 (New Input System) ---
        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.touches.Count > 0)
        {
            var touch = UnityEngine.InputSystem.Touchscreen.current.touches[0];
            var phase = touch.phase.ReadValue();

            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                // UI (버튼 등)를 터치한 것이 아니라, 빈 화면을 터치했을 때만 회전 시작
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue()))
                {
                    isPanning = false; // 아직 회전 모드 아님
                    isAimMode = false;
                    touchHoldTime = 0f;
                    activeTouchId = touch.touchId.ReadValue();
                    touchStartPosition = touch.position.ReadValue();
                }
            }
            else if ((phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary) && activeTouchId == touch.touchId.ReadValue())
            {
                handledByTouch = true;
                
                Vector2 currentPos = touch.position.ReadValue();
                float dist = Vector2.Distance(touchStartPosition, currentPos);

                // 만약 터치 시작점에서 너무 멀리 스와이프됐다면 줌 시도를 취소하고 그냥 패닝 모드로 진입
                if (!isAimMode && dist > touchStationaryThreshold)
                {
                    isPanning = true;
                }

                if (isPanning)
                {
                    // 터치 이동량(deltaPosition)에 따라 회전 (New Input System의 delta는 픽셀단위)
                    Vector2 deltaPos = touch.delta.ReadValue();
                    float moveX = deltaPos.x * currentTouchSensitivity * Time.unscaledDeltaTime * 0.5f;
                    float moveY = deltaPos.y * currentTouchSensitivity * Time.unscaledDeltaTime * 0.5f;

                    yaw += moveX;
                    pitch -= moveY;

                    pitch = Mathf.Clamp(pitch, -70f, 70f);
                    yaw = Mathf.Clamp(yaw, -80f, 80f);
                }
                else if (!isAimMode)
                {
                    // 패닝도 하지 않고, 아직 에임 상태도 아니라면 제자리 터치 유지 시간을 잼
                    touchHoldTime += Time.unscaledDeltaTime;
                    if (touchHoldTime >= holdThreshold)
                    {
                        EnterAimMode();
                        isPanning = true; // 줌 돌입 이후부터는 화면 이동 허용
                    }
                }
            }
            else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                if (touch.touchId.ReadValue() == activeTouchId)
                {
                    if (isAimMode && isZooming)
                    {
                        // 줌 상태에서 손가락을 떼면 격발
                        if (RaycastShooter.Instance != null && Time.timeScale > 0)
                        {
                            RaycastShooter.Instance.Shoot();
                        }
                    }

                    // 터치가 끝나면 모두 초기화
                    CancelAimMode();
                    isPanning = false;
                    activeTouchId = -1;
                }
            }
        }

        // --- 2. PC 환경: 마우스 우클릭 화면 회전 유지 (터치가 없을 때만 작동) ---
        if (!handledByTouch && (UnityEngine.InputSystem.Touchscreen.current == null || UnityEngine.InputSystem.Touchscreen.current.touches.Count == 0))
        {
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
                {
                    isPanning = false;
                    isAimMode = false;
                    touchHoldTime = 0f;
                    touchStartPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                }
                else if (UnityEngine.InputSystem.Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    if (isAimMode && isZooming)
                    {
                        // 줌 상태에서 손가락을 떼면 격발
                        if (RaycastShooter.Instance != null && Time.timeScale > 0)
                        {
                            RaycastShooter.Instance.Shoot();
                        }
                    }

                    CancelAimMode();
                    isPanning = false;
                }

                if (UnityEngine.InputSystem.Mouse.current.rightButton.isPressed)
                {
                    Vector2 currentPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                    float dist = Vector2.Distance(touchStartPosition, currentPos);

                    if (!isAimMode && dist > touchStationaryThreshold)
                    {
                        isPanning = true;
                    }

                    if (isPanning)
                    {
                        float mouseX = UnityEngine.InputSystem.Mouse.current.delta.x.ReadValue() * currentPanSensitivity * Time.unscaledDeltaTime * 0.05f;
                        float mouseY = UnityEngine.InputSystem.Mouse.current.delta.y.ReadValue() * currentPanSensitivity * Time.unscaledDeltaTime * 0.05f;

                        yaw += mouseX;
                        pitch -= mouseY;

                        pitch = Mathf.Clamp(pitch, -70f, 70f);
                        yaw = Mathf.Clamp(yaw, -80f, 80f);
                    }
                    else if (!isAimMode)
                    {
                        touchHoldTime += Time.unscaledDeltaTime;
                        if (touchHoldTime >= holdThreshold)
                        {
                            EnterAimMode();
                            isPanning = true;
                        }
                    }
                }
            }
        }
#else
        // --- 1. 모바일 환경: 터치 및 드래그 화면 회전 (Old Input System) ---
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Began)
            {
                // UI를 터치한 것이 아니라면
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    // 아직 이 터치세션이 없을 때 할당
                    if (activeTouchId == -1)
                    {
                        isPanning = false;
                        isAimMode = false;
                        touchHoldTime = 0f;
                        activeTouchId = touch.fingerId;
                        touchStartPosition = touch.position;
                    }
                }
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && touch.fingerId == activeTouchId)
            {
                handledByTouch = true;
                
                float dist = Vector2.Distance(touchStartPosition, touch.position);

                if (!isAimMode && dist > touchStationaryThreshold)
                {
                    isPanning = true;
                }
                
                if (isPanning)
                {
                    // 터치 이동량(deltaPosition)에 따라 회전
                    float moveX = touch.deltaPosition.x * currentTouchSensitivity * Time.unscaledDeltaTime * 60f; // Old Input 보정
                    float moveY = touch.deltaPosition.y * currentTouchSensitivity * Time.unscaledDeltaTime * 60f;

                    yaw += moveX;
                    pitch -= moveY;

                    pitch = Mathf.Clamp(pitch, -70f, 70f);
                    yaw = Mathf.Clamp(yaw, -80f, 80f);
                }
                else if (!isAimMode)
                {
                    touchHoldTime += Time.unscaledDeltaTime;
                    if (touchHoldTime >= holdThreshold)
                    {
                        EnterAimMode();
                        isPanning = true; 
                    }
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                // 현재 할당된 손가락이 떨어지면
                if (touch.fingerId == activeTouchId)
                {
                    if (isAimMode && isZooming)
                    {
                        if (RaycastShooter.Instance != null && Time.timeScale > 0)
                        {
                            RaycastShooter.Instance.Shoot();
                        }
                    }

                    CancelAimMode();
                    isPanning = false;
                    activeTouchId = -1;
                }
            }
        }

        // --- 2. PC 환경: 마우스 우클릭 화면 회전 유지 (터치가 없을 때만 작동) ---
        if (!handledByTouch && Input.touchCount == 0)
        {
            if (Input.GetMouseButtonDown(1))
            {
                isPanning = false;
                isAimMode = false;
                touchHoldTime = 0f;
                touchStartPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                if (isAimMode && isZooming)
                {
                    if (RaycastShooter.Instance != null && Time.timeScale > 0)
                    {
                        RaycastShooter.Instance.Shoot();
                    }
                }

                CancelAimMode();
                isPanning = false;
            }

            if (Input.GetMouseButton(1))
            {
                float dist = Vector2.Distance(touchStartPosition, Input.mousePosition);

                if (!isAimMode && dist > touchStationaryThreshold)
                {
                    isPanning = true;
                }

                if (isPanning)
                {
                    float mouseX = Input.GetAxis("Mouse X") * currentPanSensitivity * Time.unscaledDeltaTime * 60f;
                    float mouseY = Input.GetAxis("Mouse Y") * currentPanSensitivity * Time.unscaledDeltaTime * 60f;

                    yaw += mouseX;
                    pitch -= mouseY;

                    pitch = Mathf.Clamp(pitch, -70f, 70f);
                    yaw = Mathf.Clamp(yaw, -80f, 80f);
                }
                else if (!isAimMode)
                {
                    touchHoldTime += Time.unscaledDeltaTime;
                    if (touchHoldTime >= holdThreshold)
                    {
                        EnterAimMode();
                        isPanning = true;
                    }
                }
            }
        }
#endif
        
        // 최종 각도 적용 (마우스/터치 회전 + 반동 + 숨쉬기 반영)
        transform.eulerAngles = new Vector3(pitch + currentRecoilOffset.x + breathPitch, yaw + currentRecoilOffset.y + breathYaw, currentRecoilOffset.z);
    }

    // -------------------------------------------------------------
    // 외부(총기 등)에서 사격 시 리얼한 반동을 주기 위해 호출하는 함수
    // -------------------------------------------------------------
    // recoilUp: 시점이 위로 튀는 힘
    // recoilSide: 좌우로 랜덤하게 흔들리는 힘의 최대치
    // recoilTilt: 카메라 자체가 살짝 갸우뚱(Z축 회전)하는 힘 (옵션)
    public void AddRealisticRecoil(float recoilUp, float recoilSide)
    {
        // X는 위로 튀는 앵글 (음수), Y는 좌우 랜덤, Z는 화면 끄덕임(살짝 기울기)
        float randomHorizontal = Random.Range(-recoilSide, recoilSide);
        float randomTilt = Random.Range(-recoilSide * 0.5f, recoilSide * 0.5f);
        
        // 줌을 땡겼을 때 반동이 시야(FOV) 비율만큼 더 크게 요동치게 하려면 배율을 곱합니다.
        // 현재는 감도만 조절하고 실제 앵글이 돌아가는 반동량은 그대로 두어 사실성을 높입니다.
        targetRecoilOffset += new Vector3(-recoilUp, randomHorizontal, randomTilt);
    }

    // ============================================
    // 새로운 조준 모드(Aim Mode) 제어 함수들
    // ============================================
    private void EnterAimMode()
    {
        float targetZoom = 1f;
        if (InventoryManager.Instance != null)
        {
            targetZoom = InventoryManager.Instance.GetEquippedZoomMultiplier();
        }
        
        if (targetZoom > 1.05f) // 유효한 스코프일 때만
        {
            isAimMode = true;
            zoomActiveTimer = 0f;
            
            isZooming = true;
            currentZoomMultiplier = targetZoom;
            targetFOV = defaultFOV / currentZoomMultiplier;
            
            // 슬로우 모션
            Time.timeScale = zoomTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            return;
        }
        
        // 스코프가 없거나 배율이 낮으면 줌 안함
        isAimMode = false;
        isZooming = false;
        currentZoomMultiplier = 1f;
        targetFOV = defaultFOV;
    }

    private void CancelAimMode()
    {
        isAimMode = false;
        zoomActiveTimer = 0f;
        
        isZooming = false;
        currentZoomMultiplier = 1f;
        targetFOV = defaultFOV;
        
        // 시간 배속 원래대로
        if (Time.timeScale < 1f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    // -------------------------------------------------------------
    // 줌 On / Off 토글 함수 (기존 버튼 등 호환용, 안 쓸 수 있음)
    // -------------------------------------------------------------
    public void ToggleZoom()
    {
        // CancelAimMode() 등으로 대체하거나 비워둠
        // 현재는 새로운 터치로직이 완전 제어하므로 사실상 미사용됩니다.
    }

    /// <summary>
    /// 강제로 줌을 해제하고 기본 상태로 되돌립니다. (스테이지 종료/로비 복귀 시 호출)
    /// </summary>
    public void ForceZoomOff()
    {
        CancelAimMode(); // 시간 배속까지 포함해서 완전 초기화
        
        if (cam != null) cam.fieldOfView = defaultFOV;
        if (scopeVolume != null) scopeVolume.weight = 0f;
    }
}
