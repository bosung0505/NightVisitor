using UnityEngine;

public class RaycastShooter : MonoBehaviour
{
    private Camera mainCamera;
    private ParticleSystem bloodSplatter;

    [Header("Impact Settings")]
    public float fleeRadius = 5f;

    [Header("Recoil Settings (Realistic)")]
    [Tooltip("총을 쏠 때 시점이 위로 올라가는 기본 반동 세기")]
    public float recoilUp = 2f;
    [Tooltip("총을 쏠 때 시점이 좌우로 튀는 무작위 반동 세기")]
    public float recoilSide = 1f;

    void Start()
    {
        // Try getting camera from this object, otherwise find main camera
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Try to automatically find the blood splatter object in the scene
        GameObject splatterObj = GameObject.Find("FX_BloodSplatter");
        if (splatterObj != null)
        {
            bloodSplatter = splatterObj.GetComponent<ParticleSystem>();
        }
    }

    void Update()
    {
        // Left mouse button (index 0)
        if (Input.GetMouseButtonDown(0))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (mainCamera == null)
        {
            Debug.LogError("No Camera found to shoot from!");
            return;
        }

        // --- 리얼한 반동 적용 ---
        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null)
        {
            camController.AddRealisticRecoil(recoilUp, recoilSide);
        }

        // Raycast from the center of the screen (0.5, 0.5 viewport)
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        // Perform raycast
        if (Physics.Raycast(ray, out hit))
        {
            // Debug line for visual confirmation in Scene view
            Debug.DrawLine(ray.origin, hit.point, Color.red, 2f);

            // play blood splatter effect
            if (bloodSplatter != null)
            {
                bloodSplatter.transform.position = hit.point;
                bloodSplatter.transform.rotation = Quaternion.LookRotation(hit.normal);
                bloodSplatter.Play();
            }

            // === [신규 로직] 부위별 타격 판정 (히트박스) ===
            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox == null) hitbox = hit.collider.GetComponentInParent<Hitbox>(); // 부모나 조상 탐색

            if (hitbox != null)
            {
                // 히트박스가 부착된 경우 (예: 세팅된 여우) -> 정확히 1의 기본 데미지(부위별 증폭됨)를 전달
                hitbox.TakeDamage(1, hit.point);
                Debug.Log($"Hit Hitbox on {hit.collider.name}");
            }
            else
            {
                // === [기존 로직] 히트박스가 없는 경우 (예: 기존 닭 시스템 유지, 세팅 안된 적) ===
                Animator animator = hit.collider.GetComponent<Animator>();

                // If not on the object itself, try finding it on parents or children
                if (animator == null)
                {
                    animator = hit.collider.GetComponentInParent<Animator>();
                }

                if (animator != null)
                {
                    // Stop the random behavior script if it exists
                    RandomChickenAnimation chickenAnim = animator.GetComponent<RandomChickenAnimation>();
                    if (chickenAnim != null)
                    {
                        chickenAnim.StopAnimation();
                    }

                    RandomFoxAnimation foxAnim = animator.GetComponent<RandomFoxAnimation>();
                    if (foxAnim != null)
                    {
                        foxAnim.StopAnimation();
                    }

                    // Trigger the "Die" parameter
                    animator.SetTrigger("Die");
                    Debug.Log("Hit " + hit.collider.name + " and triggered Die animation.");
                }
                else
                {
                    Debug.Log("Hit " + hit.collider.name + " but no Animator found.");
                }
            }

            // --- FLEE BEHAVIOR (Area of Effect) ---
            Collider[] colliders = Physics.OverlapSphere(hit.point, fleeRadius);
            foreach (Collider nearby in colliders)
            {
                // Don't apply flee behavior to the object we just shot directly
                if (nearby.gameObject == hit.collider.gameObject) continue;

                // Try to find chicken animation
                RandomChickenAnimation chicken = nearby.GetComponent<RandomChickenAnimation>();
                if (chicken == null) chicken = nearby.GetComponentInParent<RandomChickenAnimation>();
                
                if (chicken != null)
                {
                    chicken.FleeFrom(hit.point);
                }

                // Try to find fox animation
                RandomFoxAnimation fox = nearby.GetComponent<RandomFoxAnimation>();
                if (fox == null) fox = nearby.GetComponentInParent<RandomFoxAnimation>();

                if (fox != null)
                {
                    fox.FleeFrom(hit.point);
                }
            }
        }
    }
}
