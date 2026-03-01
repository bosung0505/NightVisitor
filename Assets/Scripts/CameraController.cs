using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 20f;
    public bool useWorldSpace = false;

    private Vector3 lastMousePos;
    private bool isPanning = false;

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

        // Right mouse button (index 1)
        if (Input.GetMouseButtonDown(1))
        {
            isPanning = true;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isPanning = false;
        }

        if (isPanning && Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime;

            // Update pitch and yaw based on mouse input
            yaw += mouseX;
            pitch -= mouseY;

            // Clamp rotations
            pitch = Mathf.Clamp(pitch, -70f, 70f);
            yaw = Mathf.Clamp(yaw, -80f, 80f);
        }
        
        // 마우스로 조작한 기본 회전(pitch, yaw) + 부드러운 반동 오프셋 + 숨쉬기 오프셋을 더해서 최종 각도 적용
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
