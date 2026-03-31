using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RaycastShooter : MonoBehaviour
{
    public static RaycastShooter Instance;
    
    private Camera mainCamera;
    private ParticleSystem bloodSplatter;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        
        // Awake 단계에서 초기 인스펙터 값을 저장해 둡니다. (StageSelectManager보다 먼저 세팅되기 위함)
        originalMaxReloadableAmmo = maxReloadableAmmo;
        currentStageMaxAmmo = originalMaxReloadableAmmo;
    }

    [Header("Ammo & Reload Settings")]
    public int maxAmmoPerMag = 5;
    [Tooltip("총 예비 탄약량 (시작할 때 주어지는 탄약 총량)")]
    public int maxReloadableAmmo = 30;
    private int originalMaxReloadableAmmo; // 원래 설정된 기본 예비 탄약량 기억용
    private int currentStageMaxAmmo;       // 이번 스테이지에 적용될 예비 탄약량
    
    private int currentAmmo;
    private int currentReloadableAmmo;
    private bool isReloading = false;
    private int currentGunDamage = 1; // 장착된 총기 데미지

    [Header("Audio Settings")]
    [Tooltip("격발 사운드")]
    public AudioClip shootSound;
    [Tooltip("재장전 사운드")]
    public AudioClip reloadSound;
    [Tooltip("빈 총깍지 소리 (선택사항)")]
    public AudioClip emptyClickSound;
    private AudioSource audioSource;

    [Header("UI References")]
    public TextMeshProUGUI currentAmmoText;
    public TextMeshProUGUI reloadableAmmoText;
    public Button reloadButton;

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

        // --- 탄약 초기화 및 UI 바인딩 ---
        ResetAmmo(); // 초기화 로직을 분리

        if (reloadButton != null)
        {
            // 인스펙터에 연결하지 않아도 스크립트 상에서 클릭 이벤트 바인딩
            reloadButton.onClick.AddListener(TryReload);
        }
        UpdateAmmoUI();

        // AudioSource 컴포넌트 가져오기 (없으면 자동 생성)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // 자동 재생 방지
        }
    }

    void Update()
    {
        // 게임이 일시정지(시간 정지) 된 상태라면 조작 무시
        if (Time.timeScale == 0f) return;

        // -------------------------------------------------------------
        // [New Touch/Click Mechanic]
        // 기존의 화면 터치 시 / 빈 화면 우클릭 시 즉발하던 사격 코드를 모두 주석 처리합니다.
        // 이제부터 사격은 CameraController.cs에서 조준(줌) 상태에서 손을 뗄 때 명시적으로
        // RaycastShooter.Instance.Shoot() 을 호출하는 방식으로만 작동합니다.
        // -------------------------------------------------------------
        
        /*
#if ENABLE_INPUT_SYSTEM
        bool isTouching = UnityEngine.InputSystem.Touchscreen.current != null && 
                          UnityEngine.InputSystem.Touchscreen.current.touches.Count > 0 && 
                          UnityEngine.InputSystem.Touchscreen.current.touches[0].isInProgress;
                          
        if (!isTouching && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Shoot();
        }
#else
        // PC 환경을 위한 마우스 사격 유지 (터치 중일 때는 동작 안함)
        // 안드로이드에서는 빈 화면 터치가 기본적으로 마우스 좌클릭(GetMouseButtonDown(0))으로
        // 함께 인식되기 때문에, touchCount == 0 조건을 넣어 화면 회전 중 오발사를 막음.
        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
        {
            // UI를 클릭했을 때는 격발 무시
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Shoot();
        }
#endif
        */
    }

    /// <summary>
    /// 스테이지 매니저가 이번 스테이지의 탄약 한도를 전달하는 함수
    /// </summary>
    public void InitAmmoLimit(int customLimit)
    {
        if (customLimit < 0)
        {
            // 음수(-1)라면 기존 인스펙터의 디폴트값 사용
            currentStageMaxAmmo = originalMaxReloadableAmmo;
        }
        else
        {
            // 양수(또는 0 제한)라면 해당 스테이지 전용 탄발 수 적용
            currentStageMaxAmmo = customLimit;
        }
    }

    /// <summary>
    /// 상점에서 장착한 총기의 데이터를 받아와 슈터의 기본 능력치를 덮어씌웁니다.
    /// </summary>
    public void InitGunData(ShopItemData gunData)
    {
        if (gunData != null && gunData.category == ItemCategory.Gun)
        {
            currentGunDamage = gunData.gunDamage;
            maxAmmoPerMag = gunData.maxAmmoInClip;
            shootSound = gunData.shootSound;
            recoilUp = gunData.recoilUp;
            recoilSide = gunData.recoilSide;
            
            Debug.Log($"[총기 데이터 갱신] 데미지:{currentGunDamage}, 장탄수:{maxAmmoPerMag}, 반동:{recoilUp}");
        }
    }

    /// <summary>
    public void ResetAmmo()
    {
        currentAmmo = maxAmmoPerMag;
        
        // 예비 탄약에서 기본 지급된 5성을 먼저 뺌
        int startingReserve = currentStageMaxAmmo - maxAmmoPerMag;
        currentReloadableAmmo = Mathf.Max(0, startingReserve);

        isReloading = false; // 혹시 재장전 중이었다면 취소 처리
        UpdateAmmoUI();
    }

    public void Shoot()
    {
        // 장전 중이거나 총알이 없으면 쏠 수 없음 (빈 총소리 재생)
        if (isReloading || currentAmmo <= 0)
        {
            // 빈 총깍지 소리 재생
            if (emptyClickSound != null && audioSource != null)
                audioSource.PlayOneShot(emptyClickSound);
            return;
        }

        // --- 사운드 재생 ---
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // 탄약 소모 집계
        if (KillCountManager.Instance != null)
        {
            KillCountManager.Instance.AddConsumedBullet();
        }

        // 격발 시 총알 감소 및 UI 갱신
        currentAmmo--;
        UpdateAmmoUI();

        // --- [신규 추가] 방금 쏜 총알이 마지막 탄약이었을 경우 미션 실패 처리 ---
        if (currentAmmo <= 0 && currentReloadableAmmo <= 0)
        {
            Debug.Log("Run out of all ammo! Mission Failed.");
            if (KillCountManager.Instance != null)
            {
                // 약간의 딜레이(예: 탄피 떨어지는 시간) 후 패널을 띄우고 싶다면 코루틴 사용 권장, 여기서는 즉시 호출
                KillCountManager.Instance.ShowMissionFailedPanel();
            }
        }

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
                // 히트박스가 부착된 경우 (예: 세팅된 여우) -> 총기 고유의 바디샷 데미지(부위별 증폭됨)를 전달
                hitbox.TakeDamage(currentGunDamage, hit.point);
                Debug.Log($"Hit Hitbox on {hit.collider.name} with Damage: {currentGunDamage}");
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

                    // 교란용 여우 정지
                    DecoyFoxAI decoyAnim = animator.GetComponent<DecoyFoxAI>();
                    if (decoyAnim != null)
                    {
                        decoyAnim.StopAnimation();
                    }

                    MonsterAI monsterAnim = animator.GetComponent<MonsterAI>();
                    if (monsterAnim != null)
                    {
                        // 괴물은 즉사가 아니라 자체 TakeDamage 로직 분기를 사용
                        monsterAnim.TakeDamage(currentGunDamage, hit.point);
                    }
                    else
                    {
                        MutantAI mutantAnim = animator.GetComponent<MutantAI>();
                        if (mutantAnim != null)
                        {
                            mutantAnim.TakeDamage(currentGunDamage, hit.point);
                        }
                        else
                        {
                            // Trigger the "Die" parameter
                            animator.SetTrigger("Die");
                            Debug.Log("Hit " + hit.collider.name + " and triggered Die animation.");
                            
                            // --- [신규 로직] 우측 상단 킬 정보 UI 갱신 (히트박스가 없는 동물을 쐈을 때) ---
                            if (KillCountManager.Instance != null && (foxAnim != null || decoyAnim != null))
                            {
                                KillCountManager.Instance.AddKill();
                            }
                        }
                    }
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

                // Try to find decoy fox animation
                DecoyFoxAI decoyFox = nearby.GetComponent<DecoyFoxAI>();
                if (decoyFox == null) decoyFox = nearby.GetComponentInParent<DecoyFoxAI>();

                if (decoyFox != null)
                {
                    decoyFox.FleeFrom(hit.point);
                }

                MonsterAI monster = nearby.GetComponent<MonsterAI>();
                if (monster == null) monster = nearby.GetComponentInParent<MonsterAI>();
                if (monster != null) monster.FleeFrom(hit.point);
            }
        }
    }

    /// <summary>
    /// 특정 화면 좌표(스코프 중앙)에서 레이캐스트를 쏩니다.
    /// Shoot()의 모든 로직을 공유하되, 레이 방향만 스코프 중앙 화면 좌표를 사용합니다.
    /// </summary>
    public void ShootAt(Vector2 screenPos)
    {
        if (isReloading || currentAmmo <= 0)
        {
            if (emptyClickSound != null && audioSource != null) audioSource.PlayOneShot(emptyClickSound);
            return;
        }

        if (shootSound != null && audioSource != null) audioSource.PlayOneShot(shootSound);

        if (KillCountManager.Instance != null) KillCountManager.Instance.AddConsumedBullet();

        currentAmmo--;
        UpdateAmmoUI();

        if (currentAmmo <= 0 && currentReloadableAmmo <= 0)
        {
            if (KillCountManager.Instance != null) KillCountManager.Instance.ShowMissionFailedPanel();
        }

        if (mainCamera == null) return;

        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null) camController.AddRealisticRecoil(recoilUp, recoilSide);

        // 핵심: 스코프 중앙 화면 좌표에서 레이캐스트
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.green, 2f);

            if (bloodSplatter != null)
            {
                bloodSplatter.transform.position = hit.point;
                bloodSplatter.transform.rotation = Quaternion.LookRotation(hit.normal);
                bloodSplatter.Play();
            }

            Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox == null) hitbox = hit.collider.GetComponentInParent<Hitbox>();

            if (hitbox != null)
            {
                hitbox.TakeDamage(currentGunDamage, hit.point);
            }
            else
            {
                Animator animator = hit.collider.GetComponent<Animator>();
                if (animator == null) animator = hit.collider.GetComponentInParent<Animator>();
                if (animator != null)
                {
                    RandomChickenAnimation chickenAnim = animator.GetComponent<RandomChickenAnimation>();
                    if (chickenAnim != null) chickenAnim.StopAnimation();

                    RandomFoxAnimation foxAnim = animator.GetComponent<RandomFoxAnimation>();
                    if (foxAnim != null) foxAnim.StopAnimation();

                    DecoyFoxAI decoyAnim = animator.GetComponent<DecoyFoxAI>();
                    if (decoyAnim != null) decoyAnim.StopAnimation();

                    MonsterAI monsterAnim = animator.GetComponent<MonsterAI>();
                    if (monsterAnim != null)
                    {
                        monsterAnim.TakeDamage(currentGunDamage, hit.point);
                    }
                    else
                    {
                        MutantAI mutantAnim = animator.GetComponent<MutantAI>();
                        if (mutantAnim != null)
                        {
                            mutantAnim.TakeDamage(currentGunDamage, hit.point);
                        }
                        else
                        {
                            animator.SetTrigger("Die");

                            if (KillCountManager.Instance != null && (foxAnim != null || decoyAnim != null))
                                KillCountManager.Instance.AddKill();
                        }
                    }
                }
            }

            Collider[] colliders = Physics.OverlapSphere(hit.point, fleeRadius);
            foreach (Collider nearby in colliders)
            {
                if (nearby.gameObject == hit.collider.gameObject) continue;
                RandomChickenAnimation chicken = nearby.GetComponent<RandomChickenAnimation>();
                if (chicken == null) chicken = nearby.GetComponentInParent<RandomChickenAnimation>();
                if (chicken != null) chicken.FleeFrom(hit.point);

                RandomFoxAnimation fox = nearby.GetComponent<RandomFoxAnimation>();
                if (fox == null) fox = nearby.GetComponentInParent<RandomFoxAnimation>();
                if (fox != null) fox.FleeFrom(hit.point);

                DecoyFoxAI decoyFox = nearby.GetComponent<DecoyFoxAI>();
                if (decoyFox == null) decoyFox = nearby.GetComponentInParent<DecoyFoxAI>();
                if (decoyFox != null) decoyFox.FleeFrom(hit.point);

                MonsterAI monster = nearby.GetComponent<MonsterAI>();
                if (monster == null) monster = nearby.GetComponentInParent<MonsterAI>();
                if (monster != null) monster.FleeFrom(hit.point);

                MutantAI mutant = nearby.GetComponent<MutantAI>();
                if (mutant == null) mutant = nearby.GetComponentInParent<MutantAI>();
                if (mutant != null) mutant.FleeFrom(hit.point);
            }
        }
    }

    // === [신규 로직] 장전 시스템 ===
    public void TryReload()
    {
        // 이미 장전 중이거나, 장전할 예비 탄약이 없거나, 이미 탄창이 꽉 차있으면 액션 무시
        if (isReloading || currentReloadableAmmo <= 0 || currentAmmo >= maxAmmoPerMag)
            return;

        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        
        // 장전 도중 버튼 연타 방지를 위해 일시 비활성화
        if (reloadButton != null)
            reloadButton.interactable = false;

        // 재장전 사운드 재생
        if (reloadSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        // 3.1초 대기
        yield return new WaitForSeconds(3.1f);

        int ammoNeeded = maxAmmoPerMag - currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, currentReloadableAmmo);

        currentAmmo += ammoToReload;
        currentReloadableAmmo -= ammoToReload;

        isReloading = false;
        UpdateAmmoUI();
    }

    public void UpdateAmmoUI()
    {
        if (currentAmmoText != null)
            currentAmmoText.text = currentAmmo.ToString();

        if (reloadableAmmoText != null)
            reloadableAmmoText.text = currentReloadableAmmo.ToString();

        if (reloadButton != null)
        {
            // 요구사항: 예비 탄력이 0 이하면 버튼 비활성화
            // (또한 장전 중일 때도 클릭되지 않게 방지)
            reloadButton.interactable = (currentReloadableAmmo > 0 && !isReloading && currentAmmo < maxAmmoPerMag);
        }
    }
}
