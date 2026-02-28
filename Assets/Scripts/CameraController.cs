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

            // Apply clamped rotation
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
    }
}
