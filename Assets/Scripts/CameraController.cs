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

    void Start()
    {
        // Initialize rotation variables with current camera rotation to prevent snapping
        Vector3 initialRotation = transform.eulerAngles;
        pitch = initialRotation.x;
        yaw = initialRotation.y;
        
        // Convert pitch from 0..360 to -180..180 if needed
        if (pitch > 180f) pitch -= 360f;
        if (yaw > 180f) yaw -= 360f;
    }

    void Update()
    {
        // --- 부드러운 반동 복원 처리 ---
        // targetRecoil은 시간이 지날수록 0,0,0으로 서서히 돌아갑니다.
        targetRecoilOffset = Vector3.Lerp(targetRecoilOffset, Vector3.zero, returnSpeed * Time.deltaTime);
        // currentRecoil은 targetRecoil을 빠르게 따라가며 스프링처럼 부드럽게 튀는 효과를 줍니다.
        currentRecoilOffset = Vector3.Slerp(currentRecoilOffset, targetRecoilOffset, snappiness * Time.deltaTime);

        // --- 숨쉬기 (Idle Breathing) 애니메이션 처리 ---
        breathTimer += Time.deltaTime * breathSpeed;
        // 상하(Pitch)는 사인함수, 좌우(Yaw)는 코사인함수를 써서 8자 혹은 원형 모양으로 부드럽게 흔들림
        float breathPitch = Mathf.Sin(breathTimer) * breathAmount;
        float breathYaw = Mathf.Cos(breathTimer * 0.5f) * (breathAmount * 0.5f);

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
                    isPanning = true;
                    activeTouchId = touch.touchId.ReadValue();
                }
            }
            else if ((phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary) && isPanning && touch.touchId.ReadValue() == activeTouchId)
            {
                handledByTouch = true;
                
                // 터치 이동량(deltaPosition)에 따라 회전 (New Input System의 delta는 픽셀단위)
                Vector2 deltaPos = touch.delta.ReadValue();
                float moveX = deltaPos.x * touchPanSpeed * Time.deltaTime * 0.5f;
                float moveY = deltaPos.y * touchPanSpeed * Time.deltaTime * 0.5f;

                yaw += moveX;
                pitch -= moveY;

                pitch = Mathf.Clamp(pitch, -70f, 70f);
                yaw = Mathf.Clamp(yaw, -80f, 80f);
            }
            else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                if (touch.touchId.ReadValue() == activeTouchId)
                {
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
                    isPanning = true;
                }
                else if (UnityEngine.InputSystem.Mouse.current.rightButton.wasReleasedThisFrame)
                {
                    isPanning = false;
                }

                if (isPanning && UnityEngine.InputSystem.Mouse.current.rightButton.isPressed)
                {
                    float mouseX = UnityEngine.InputSystem.Mouse.current.delta.x.ReadValue() * panSpeed * Time.deltaTime * 0.05f;
                    float mouseY = UnityEngine.InputSystem.Mouse.current.delta.y.ReadValue() * panSpeed * Time.deltaTime * 0.05f;

                    yaw += mouseX;
                    pitch -= mouseY;

                    pitch = Mathf.Clamp(pitch, -70f, 70f);
                    yaw = Mathf.Clamp(yaw, -80f, 80f);
                }
            }
        }
#else
        // --- 1. 모바일 환경: 터치 및 드래그 화면 회전 (Old Input System) ---
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // UI (버튼 등)를 터치한 것이 아니라, 빈 화면을 터치했을 때만 회전 시작
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    isPanning = true;
                    activeTouchId = touch.fingerId;
                }
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && isPanning && touch.fingerId == activeTouchId)
            {
                handledByTouch = true;
                
                // 터치 이동량(deltaPosition)에 따라 회전
                float moveX = touch.deltaPosition.x * touchPanSpeed * Time.deltaTime;
                float moveY = touch.deltaPosition.y * touchPanSpeed * Time.deltaTime;

                yaw += moveX;
                pitch -= moveY;

                pitch = Mathf.Clamp(pitch, -70f, 70f);
                yaw = Mathf.Clamp(yaw, -80f, 80f);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.fingerId == activeTouchId)
                {
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
                isPanning = true;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isPanning = false;
            }

            if (isPanning && Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime;
                float mouseY = Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime;

                yaw += mouseX;
                pitch -= mouseY;

                pitch = Mathf.Clamp(pitch, -70f, 70f);
                yaw = Mathf.Clamp(yaw, -80f, 80f);
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
        
        // 목표 반동치에 누적
        targetRecoilOffset += new Vector3(-recoilUp, randomHorizontal, randomTilt);
    }
}
