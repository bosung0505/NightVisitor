using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 20f;
    public float touchPanSpeed = 5f;
    public bool useWorldSpace = false;

    private float pitch = 0f;
    private float yaw = 0f;

    [Header("Recoil Settings (Realistic)")]
    [Tooltip("에임이 원래 자리로 돌아오려는 힘 (스프링 속도)")]
    public float snappiness = 6f;
    [Tooltip("반동이 화면에 적용되는 속도 (부드러운 타격감을 조절)")]
    public float returnSpeed = 2f;

    private Vector3 currentRecoilOffset;
    private Vector3 targetRecoilOffset;

    [Header("Rotation Limits")]
    public Vector2 pitchLimit = new Vector2(-70f, 70f);
    public Vector2 yawLimit = new Vector2(-80f, 80f);

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

    [Header("Zoom Fog & Battery Drain Settings")]
    [Tooltip("줌 시 fogEndDistance를 확장할 배율 (2.0 = 2배 멀리 보임)")]
    public float zoomFogMultiplier = 2.0f;
    [Tooltip("이 시간(초) 이상 줌을 유지하면 배터리 소모 가속 시작")]
    public float zoomScanThreshold = 0.8f;
    [Tooltip("줌 지속 시 적용되는 배터리 소모 배율")]
    public float zoomBatteryDrainMultiplier = 2.5f;
    private float savedFogEnd;      // 줌 진입 시 저장하는 원본 Fog 거리
    private bool isZoomDrainActive; // 줌 드레인이 활성화되어 있는지 추적

    private Camera cam;
    private float defaultFOV;
    private float targetFOV;
    private bool isZooming = false;
    private float currentZoomMultiplier = 1f;

    [Header("New Touch/Click Mechanics")]
    [Tooltip("드래그 취소 영역 UI (RectTransform)")]
    public RectTransform cancelZoneUI;
    [Tooltip("조준점(크로스헤어) 이미지 - 취소 구역 진입 시 색상 변경")]
    public UnityEngine.UI.Image crosshairImage;
    public Color normalCrosshairColor = Color.white;
    public Color cancelCrosshairColor = Color.red;

    [Header("Scope UI Movement")]
    [Tooltip("이동할 스코프 오버레이 UI")]
    public RectTransform scopeOverlayUI;
    [Tooltip("스코프 드래그 감도 (1.0 = 손가락과 같은 속도)")]
    public float scopeDragSensitivity = 1f;
    [Tooltip("스코프가 화면 경계에서 유지할 여유 거리 (픽셀)")]
    public Vector2 scopeMargin = new Vector2(80f, 120f);
    private Vector2 scopeDefaultAnchoredPos;

    [Tooltip("줌 상태일 때 게임 속도 (슬로우 모션 배율)")]
    public float zoomTimeScale = 0.5f;
    [Tooltip("터치 후 줌이 켜질 때까지 기다리는 최소 대기 시간 (초)")]
    public float holdThreshold = 0.15f;
    [Tooltip("드래그로 판정하는 최소 속도 (Screen.width 비율, 기본 0.02)")]
    public float dragSpeedThreshold = 0.02f;

    // ★ 터치 상태를 enum 하나로 관리 — bool 여러 개의 충돌을 근본 해결
    private enum TouchState { Idle, Holding, Dragging, Zooming }
    private TouchState touchState = TouchState.Idle;

    private int activeTouchId = -1;
    private float touchHoldTime = 0f;
    private float zoomActiveTimer = 0f;
    private bool isPointerInCancelZone = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // ---------------------------------------------------------------

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        Vector3 rot = transform.eulerAngles;
        pitch = rot.x > 180f ? rot.x - 360f : rot.x;
        yaw = rot.y > 180f ? rot.y - 360f : rot.y;

        cam = GetComponent<Camera>();
        if (cam != null) { defaultFOV = cam.fieldOfView; targetFOV = defaultFOV; }

        if (scopeVolume != null) { scopeVolume.weight = 0f; scopeVolume.priority = 100f; }

        // ★ 저장된 카메라 민감도 자동 적용
        if (GameSettingsManager.Instance != null)
            touchPanSpeed = GameSettingsManager.Instance.CameraSensitivity;
        else
            touchPanSpeed = PlayerPrefs.GetFloat("Setting_CamSensitivity", touchPanSpeed);
    }

    void OnEnable() { UpdateZoomButtonVisibility(); }

    public void UpdateZoomButtonVisibility()
    {
        if (zoomButton != null && InventoryManager.Instance != null)
        {
            float m = InventoryManager.Instance.GetEquippedZoomMultiplier();
            zoomButton.gameObject.SetActive(m > 1.05f);
            if (m <= 1.05f && isZooming) ForceZoomOff();
        }
    }

    // ---------------------------------------------------------------
    // 헬퍼
    // ---------------------------------------------------------------

    private bool IsPointerInCancelZone(Vector2 screenPos)
    {
        if (cancelZoneUI == null || !cancelZoneUI.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(cancelZoneUI, screenPos);
    }

    private void MoveScopeUI(Vector2 delta)
    {
        if (scopeOverlayUI == null) return;
        Vector2 p = scopeOverlayUI.anchoredPosition + delta * scopeDragSensitivity;
        p.x = Mathf.Clamp(p.x, -(Screen.width * .5f - scopeMargin.x), Screen.width * .5f - scopeMargin.x);
        p.y = Mathf.Clamp(p.y, -(Screen.height * .5f - scopeMargin.y), Screen.height * .5f - scopeMargin.y);
        scopeOverlayUI.anchoredPosition = p;
    }

    private Vector2 GetScopeScreenCenter()
    {
        if (scopeOverlayUI == null) return new Vector2(Screen.width * .5f, Screen.height * .5f);
        Vector3[] c = new Vector3[4];
        scopeOverlayUI.GetWorldCorners(c);
        return new Vector2((c[0].x + c[2].x) * .5f, (c[0].y + c[2].y) * .5f);
    }

    // ---------------------------------------------------------------
    // 통합 입력 이벤트 (터치/마우스 공통으로 호출)
    // ---------------------------------------------------------------

    private void HandleInputBegan(Vector2 pos, int id)
    {
        bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                      UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(-1);
        Debug.Log($"[CAM] HandleInputBegan — id={id}, overUI={overUI}, touchState={touchState}");

        if (overUI) return;
        if (touchState != TouchState.Idle) return;

        activeTouchId = id;
        touchHoldTime = 0f;
        touchState = TouchState.Holding;
        isPointerInCancelZone = false;
        Debug.Log("[CAM] → Holding 상태 진입");
    }

    private void HandleInputMoved(Vector2 pos, Vector2 delta, int id)
    {
        if (id != activeTouchId) return;

        switch (touchState)
        {
            case TouchState.Holding:
                // 빠르게 움직이면 드래그로 전환
                if (delta.magnitude > Screen.width * dragSpeedThreshold)
                {
                    Debug.Log($"[CAM] ⚡ 드래그 판정 — delta={delta.magnitude:F1}, threshold={Screen.width * 0.01f:F1} → Dragging");
                    touchState = TouchState.Dragging;
                    ApplyPanDelta(delta); // 전환된 이번 프레임도 바로 패닝
                }
                break;

            case TouchState.Dragging:
                Debug.Log($"[CAM] 드래그 이동 — delta={delta}, yaw={yaw:F2}, pitch={pitch:F2}");
                ApplyPanDelta(delta);
                break;

            case TouchState.Zooming:
                isPointerInCancelZone = IsPointerInCancelZone(pos);
                if (crosshairImage != null)
                    crosshairImage.color = isPointerInCancelZone ? cancelCrosshairColor : normalCrosshairColor;
                if (!isPointerInCancelZone)
                    MoveScopeUI(delta);
                break;
        }
    }

    private void HandleInputEnded(int id)
    {
        if (id != activeTouchId) return;

        // 줌 상태에서 손 뗌 → 격발
        if (touchState == TouchState.Zooming && isZooming)
        {
            if (!isPointerInCancelZone && RaycastShooter.Instance != null && Time.timeScale > 0)
                RaycastShooter.Instance.ShootAt(GetScopeScreenCenter());
        }

        ResetTouchState();
    }

    // delta를 pitch/yaw에 적용 (플랫폼별 스케일 자동 처리)
    private void ApplyPanDelta(Vector2 delta)
    {
        float sens = touchPanSpeed / currentZoomMultiplier;
#if ENABLE_INPUT_SYSTEM
        float mx = delta.x * sens * Time.unscaledDeltaTime * 0.5f;
        float my = delta.y * sens * Time.unscaledDeltaTime * 0.5f;
#else
        float mx = delta.x * sens * Time.unscaledDeltaTime * 60f;
        float my = delta.y * sens * Time.unscaledDeltaTime * 60f;
#endif
        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, pitchLimit.x, pitchLimit.y);
        yaw = Mathf.Clamp(yaw, yawLimit.x, yawLimit.y);
    }

    private void ResetTouchState()
    {
        if (touchState == TouchState.Zooming) ExitZoom();

        touchState = TouchState.Idle;
        activeTouchId = -1;
        touchHoldTime = 0f;
        isPointerInCancelZone = false;
        if (crosshairImage != null) crosshairImage.color = normalCrosshairColor;
    }

    // ---------------------------------------------------------------

    void Update()
    {
        // ★ 게임 종료 연출 중 (점프 공격 / 마을 침략) → 플레이어 입력 전면 차단
        if (KillCountManager.isGameEnding) return;

        // --- 반동 복원 ---
        targetRecoilOffset = Vector3.Lerp(targetRecoilOffset, Vector3.zero, returnSpeed * Time.unscaledDeltaTime);
        currentRecoilOffset = Vector3.Slerp(currentRecoilOffset, targetRecoilOffset, snappiness * Time.unscaledDeltaTime);

        // --- 호흡 ---
        breathTimer += Time.unscaledDeltaTime * breathSpeed;
        float breathPitch = Mathf.Sin(breathTimer) * breathAmount;
        float breathYaw = Mathf.Cos(breathTimer * 0.5f) * (breathAmount * 0.5f);

        // --- FOV Lerp ---
        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, zoomLerpSpeed * Time.unscaledDeltaTime);
            if (Mathf.Abs(cam.fieldOfView - targetFOV) < 0.01f) cam.fieldOfView = targetFOV;
        }

        // --- Scope Volume Lerp ---
        if (scopeVolume != null)
        {
            float tw = isZooming ? 1f : 0f;
            scopeVolume.weight = Mathf.Lerp(scopeVolume.weight, tw, zoomLerpSpeed * Time.unscaledDeltaTime);
            if (Mathf.Abs(scopeVolume.weight - tw) < 0.001f) scopeVolume.weight = tw;
        }

        // --- 줌 2.5초 자동 해제 ---
        if (touchState == TouchState.Zooming && isZooming)
        {
            zoomActiveTimer += Time.unscaledDeltaTime;
            if (zoomActiveTimer >= 2.5f) ResetTouchState();

            // ★ 줌 스캔 임계값 초과 시 배터리 가속 소모 ON
            if (!isZoomDrainActive && zoomActiveTimer >= zoomScanThreshold)
            {
                isZoomDrainActive = true;
                if (BatteryController.Instance != null)
                    BatteryController.Instance.SetZoomDrainActive(true, zoomBatteryDrainMultiplier);
            }
        }

        // ★ 홀드 타이머: Holding 상태일 때만 증가
        if (touchState == TouchState.Holding)
        {
            touchHoldTime += Time.unscaledDeltaTime;
            if (touchHoldTime >= holdThreshold)
                EnterZoom();
        }

        // ---------------------------------------------------------------
        // 입력 처리
        // ---------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        bool hasTouches = ts != null && ts.touches.Count > 0;

        if (hasTouches)
        {
            var touch = ts.touches[0];
            var phase = touch.phase.ReadValue();
            int tid   = touch.touchId.ReadValue();

            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                HandleInputBegan(touch.position.ReadValue(), tid);
            else if (phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                     phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                HandleInputMoved(touch.position.ReadValue(), touch.delta.ReadValue(), tid);
            else if (phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                     phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                HandleInputEnded(tid);
        }
        if (!hasTouches && UnityEngine.InputSystem.Mouse.current != null)
        {
            var mouse  = UnityEngine.InputSystem.Mouse.current;
            Vector2 mpos   = mouse.position.ReadValue();
            Vector2 mdelta = new Vector2(mouse.delta.x.ReadValue(), mouse.delta.y.ReadValue());

            if (mouse.rightButton.wasPressedThisFrame)
                HandleInputBegan(mpos, 0);
            else if (mouse.rightButton.isPressed)
            {
                // 마우스 이동 → Holding이면 Dragging으로 전환
                if (touchState == TouchState.Holding && mdelta.magnitude > 0.5f)
                    touchState = TouchState.Dragging;

                if (touchState == TouchState.Dragging)
                {
                    float sens = panSpeed / currentZoomMultiplier;
                    yaw   += mdelta.x * sens * Time.unscaledDeltaTime * 0.05f;
                    pitch -= mdelta.y * sens * Time.unscaledDeltaTime * 0.05f;
                    pitch = Mathf.Clamp(pitch, pitchLimit.x, pitchLimit.y);
                    yaw   = Mathf.Clamp(yaw,   yawLimit.x, yawLimit.y);
                }
                else if (touchState == TouchState.Zooming)
                {
                    isPointerInCancelZone = IsPointerInCancelZone(mpos);
                    if (crosshairImage != null)
                        crosshairImage.color = isPointerInCancelZone ? cancelCrosshairColor : normalCrosshairColor;
                    if (!isPointerInCancelZone) MoveScopeUI(mdelta);
                }
            }
            else if (mouse.rightButton.wasReleasedThisFrame)
                HandleInputEnded(0);
        }

        // ★ 안전장치: 하드웨어가 안 눌렸는데 Idle이 아니면 강제 초기화
        //   (반드시 입력 처리 블록 이후에 위치해야 격발이 정상 동작함)
        bool hwPressed =
            (ts != null && ts.primaryTouch.press.isPressed) ||      
            (UnityEngine.InputSystem.Mouse.current != null &&
             (UnityEngine.InputSystem.Mouse.current.rightButton.isPressed));
        if (touchState != TouchState.Idle && !hwPressed)
            ResetTouchState();

#else
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began) HandleInputBegan(t.position, t.fingerId);
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) HandleInputMoved(t.position, t.deltaPosition, t.fingerId);
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) HandleInputEnded(t.fingerId);
        }

        if (Input.touchCount == 0)
        {
            if (Input.GetMouseButtonDown(1))
                HandleInputBegan(Input.mousePosition, 0);
            else if (Input.GetMouseButton(1))
            {
                float sens = panSpeed / currentZoomMultiplier;
                Vector2 mdelta = new Vector2(
                    Input.GetAxis("Mouse X") * sens * Time.unscaledDeltaTime * 60f,
                    Input.GetAxis("Mouse Y") * sens * Time.unscaledDeltaTime * 60f);

                if (touchState == TouchState.Holding && mdelta.magnitude > 0.5f)
                    touchState = TouchState.Dragging;

                if (touchState == TouchState.Dragging)
                {
                    yaw += mdelta.x;
                    pitch -= mdelta.y;
                    pitch = Mathf.Clamp(pitch, pitchLimit.x, pitchLimit.y);
                    yaw = Mathf.Clamp(yaw, yawLimit.x, yawLimit.y);
                }
                else if (touchState == TouchState.Zooming)
                {
                    Vector2 mpos = Input.mousePosition;
                    isPointerInCancelZone = IsPointerInCancelZone(mpos);
                    if (crosshairImage != null)
                        crosshairImage.color = isPointerInCancelZone ? cancelCrosshairColor : normalCrosshairColor;
                    if (!isPointerInCancelZone)
                        MoveScopeUI(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * sens * Time.unscaledDeltaTime * 60f);
                }
            }
            else if (Input.GetMouseButtonUp(1))
                HandleInputEnded(0);
        }

        // ★ 안전장치 (Old Input System)
        bool hwPressed = Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1);
        if (touchState != TouchState.Idle && !hwPressed)
            ResetTouchState();
#endif

        // --- 최종 회전 적용 ---
        transform.eulerAngles = new Vector3(
            pitch + currentRecoilOffset.x + breathPitch,
            yaw + currentRecoilOffset.y + breathYaw,
            currentRecoilOffset.z);
    }

    // ---------------------------------------------------------------

    public void AddRealisticRecoil(float recoilUp, float recoilSide)
    {
        float h = Random.Range(-recoilSide, recoilSide);
        float t = Random.Range(-recoilSide * 0.5f, recoilSide * 0.5f);
        targetRecoilOffset += new Vector3(-recoilUp, h, t);
    }

    private void EnterZoom()
    {
        float targetZoom = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetEquippedZoomMultiplier() : 1f;

        if (targetZoom > 1.05f)
        {
            touchState = TouchState.Zooming;
            zoomActiveTimer = 0f;
            isZooming = true;
            currentZoomMultiplier = targetZoom;
            targetFOV = defaultFOV / currentZoomMultiplier;

            Time.timeScale = zoomTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (cancelZoneUI != null) cancelZoneUI.gameObject.SetActive(true);

            if (scopeOverlayUI != null)
            {
                scopeDefaultAnchoredPos = scopeOverlayUI.anchoredPosition;
                scopeOverlayUI.anchoredPosition = Vector2.zero;
                scopeOverlayUI.gameObject.SetActive(true);
            }

            // ★ Fog End Distance 확장 (현재 값을 저장 후 배율 적용)
            savedFogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fogEndDistance = savedFogEnd * zoomFogMultiplier;

            isZoomDrainActive = false; // 드레인 플래그 리셋
        }
        // 스코프 없으면 줌 진입 안 함 — touchState는 Holding 유지
    }

    private void ExitZoom()
    {
        isZooming = false;
        currentZoomMultiplier = 1f;
        targetFOV = defaultFOV;
        zoomActiveTimer = 0f;

        if (Time.timeScale < 1f) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; }

        if (cancelZoneUI != null) cancelZoneUI.gameObject.SetActive(false);
        if (crosshairImage != null) crosshairImage.color = normalCrosshairColor;

        if (scopeOverlayUI != null)
        {
            scopeOverlayUI.anchoredPosition = scopeDefaultAnchoredPos;
            scopeOverlayUI.gameObject.SetActive(false);
        }

        // ★ Fog End Distance 원복
        RenderSettings.fogEndDistance = savedFogEnd;

        // ★ 줌 배터리 가속 해제
        if (isZoomDrainActive)
        {
            isZoomDrainActive = false;
            if (BatteryController.Instance != null)
                BatteryController.Instance.SetZoomDrainActive(false, 1.0f);
        }
    }

    public void ToggleZoom() { }

    public void ForceZoomOff()
    {
        ResetTouchState();
        if (cam != null) cam.fieldOfView = defaultFOV;
        if (scopeVolume != null) scopeVolume.weight = 0f;
    }

    /// <summary>
    /// 외부(StageSelectManager 등)에서 카메라의 위치, 회전, 그리고 회전 제한을 한 번에 설정합니다.
    /// </summary>
    public void SetCameraPoseAndLimits(Transform spawnPoint, Vector2 pLimit, Vector2 yLimit)
    {
        // 1. 위치 및 회전 적용
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        else
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
        }

        // 2. 내부 변수(pitch, yaw) 동기화
        Vector3 rot = transform.eulerAngles;
        pitch = rot.x > 180f ? rot.x - 360f : rot.x;
        yaw = rot.y > 180f ? rot.y - 360f : rot.y;

        // 3. 제한 각도 적용
        pitchLimit = pLimit;
        yawLimit = yLimit;

        // 4. 상태 초기화
        currentRecoilOffset = Vector3.zero;
        targetRecoilOffset = Vector3.zero;
        
        Debug.Log($"[CAM] Pose & Limits Reset: Pos={transform.position}, Rot={rot}, PitchRange={pLimit}, YawRange={yLimit}");
    }
}
