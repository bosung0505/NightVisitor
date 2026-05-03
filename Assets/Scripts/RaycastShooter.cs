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

        // 탄창 업그레이드 데이터에서 초기값 로드
        maxAmmoPerMag   = MagazineUpgradeData.GetAmmoAtLevel(MagazineUpgradeData.CurrentLevel);
        _initReloadable = MagazineUpgradeData.GetReloadableAtLevel(MagazineUpgradeData.CurrentLevel);
    }

    [Header("Ammo & Reload Settings")]
    public int maxAmmoPerMag = 5;
    private int _initReloadable = 5; // 스테이지 시작 시 지급되는 예비 탄약 수 (MagazineUpgradeData에서 설정)
    
    private int currentAmmo;
    private int currentReloadableAmmo;
    private bool isReloading = false;
    private int currentGunDamage = 1; // 장착된 총기 데미지
    private bool currentPenetration = false; // 장착된 총기의 관통탄 여부
    private bool isNextShotAggro = false;    // 다음 발사가 어그로탄인지 여부 (소모품, 1회)

    [Header("Audio Settings")]
    [Tooltip("격발 사운드")]
    public AudioClip shootSound;
    [Tooltip("재장전 사운드")]
    public AudioClip reloadSound;
    [Tooltip("빈 총깍지 소리 (선택사항)")]
    public AudioClip emptyClickSound;
    [Tooltip("재장전 중 재생될 심장박동 사운드 (Map 2 전용)")]
    public AudioClip heartbeatClip;
    private AudioSource audioSource;
    private AudioSource heartbeatAudioSource; // 심장박동 전용 AudioSource

    [Header("UI References")]
    public TextMeshProUGUI currentAmmoText;
    public TextMeshProUGUI reloadableAmmoText;
    public Button reloadButton;
    public Image ammoIconImage; // 총알 아이콘
    private Sprite defaultAmmoIcon; // 원본 총알 아이콘 저장용

    [Header("Impact Settings")]
    public float fleeRadius = 5f;
    [Tooltip("관통탄이 각 뮤턴트에 적중했을 때 방출하는 혈흔 파티클 수. (비관통탄은 ParticleSystem.Play()를 사용하므로 이 값을 사용하지 않습니다.)")]
    public int penetrationBloodEmitCount = 12;

    [Header("Recoil Settings (Realistic)")]
    [Tooltip("총을 쏠 때 시점이 위로 올라가는 기본 반동 세기")]
    public float recoilUp = 2f;
    [Tooltip("총을 쏠 때 시점이 좌우로 튀는 무작위 반동 세기")]
    public float recoilSide = 1f;

    [Header("Reload Tension Settings")]
    [Tooltip("재장전 중 어두워질 볼륨 (Starting Volume / 완전암전 오버레이). 인스펙터에서 드래그 연결.")]
    public UnityEngine.Rendering.Volume reloadDimVolume;
    [Tooltip("재장전 중 StartingVolume의 weight가 올라갈 목표값 (0.6 권장)")]
    public float reloadVolumeTargetWeight = 0.6f;
    [Tooltip("볼륨이 점점 올라오는 데 걸리는 시간 (초)")]
    public float reloadVolumeFadeInDuration = 0.5f;
    [Tooltip("볼륨이 다시 내려가는 데 걸리는 시간 (초). 재장전 완료 직전에 시작됨")]
    public float reloadVolumeFadeOutDuration = 0.5f;
    [Tooltip("재장전 중 카메라 셰이크 강도 (breathAmount 배수)")]
    public float reloadBreathMultiplier = 3.5f;

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
            audioSource.playOnAwake = false;
        }
        // ★ 총소리/장전소리 AudioSource → SFX 믹서 그룹 연결
        GameSettingsManager.Instance?.AssignSFXGroup(audioSource);

        // 심장박동 전용 AudioSource 생성 (루프 재생용)
        heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        heartbeatAudioSource.playOnAwake = false;
        heartbeatAudioSource.loop = true;
        heartbeatAudioSource.spatialBlend = 0f;
        heartbeatAudioSource.volume = 0.7f;
        // ★ 심장박동 AudioSource → SFX 믹서 그룹 연결
        GameSettingsManager.Instance?.AssignSFXGroup(heartbeatAudioSource);
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
    /// MagazineUpgradeData의 현재 레벨을 읽어 탄창 크기와 예비 탄약을 초기화합니다.
    /// StageSelectManager.StartGame()에서 InitGunData() 이후에 호출됩니다.
    /// </summary>
    public void InitAmmoFromMag()
    {
        int magLevel    = MagazineUpgradeData.CurrentLevel;
        maxAmmoPerMag   = MagazineUpgradeData.GetAmmoAtLevel(magLevel);
        _initReloadable = MagazineUpgradeData.GetReloadableAtLevel(magLevel);
        ResetAmmo();
    }

    /// <summary>
    /// 상점에서 장착한 총기의 데이터를 받아와 슈터의 기본 능력치를 덮어씌웁니다.
    /// </summary>
    public void InitGunData(ShopItemData gunData)
    {
        if (gunData != null && gunData.category == ItemCategory.Gun)
        {
            currentGunDamage    = gunData.gunDamage;
            currentPenetration  = gunData.hasPenetration;
            // ★ maxAmmoPerMag는 이제 MagazineUpgradeData에서 설정하므로 여기서 덮어쓰지 않습니다.
            shootSound = gunData.shootSound;
            recoilUp   = gunData.recoilUp;
            recoilSide = gunData.recoilSide;

            Debug.Log($"[총기 데이터 갱신] 데미지:{currentGunDamage}, 반동:{recoilUp}, 관통:{currentPenetration}");
        }
    }

    /// <summary>
    public void ResetAmmo()
    {
        currentAmmo           = maxAmmoPerMag;   // 탄창을 꽉 채운 상태로 시작
        currentReloadableAmmo = _initReloadable; // 예비 탄약을 그대로 지급 (분할 없음)

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

        // ─── 어그로탄 분기 ────────────────────────────────────────────────
        if (isNextShotAggro)
        {
            FireAggroBullet(ray);
            return; // 일반 레이캐스트 발사 로직 건너뜀
        }
        // ─────────────────────────────────────────────────────────────────

        // ─── 관통탄 분기 ─────────────────────────────────────────────────
        if (currentPenetration)
        {
            FirePenetrationRay(ray);
        }
        else
        {
            FireSingleRay(ray);
        }
        // ─────────────────────────────────────────────────────────────────

        // --- 방금 쏜 총알이 마지막 탄약이었을 경우 미션 실패 처리 (가장 마지막에 체크) ---
        if (currentAmmo <= 0 && currentReloadableAmmo <= 0)
        {
            Debug.Log("Run out of all ammo! Mission Failed.");
            if (Map2ResultManager.Instance != null && Map2ResultManager.Instance.isActiveAndEnabled)
            {
                Map2ResultManager.Instance.ShowVillageInvadedPanel("모든 탄약을 소모했습니다.");
            }
            else if (KillCountManager.Instance != null)
            {
                KillCountManager.Instance.ShowMissionFailedPanel();
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



        if (mainCamera == null) return;

        CameraController camController = mainCamera.GetComponent<CameraController>();
        if (camController != null) camController.AddRealisticRecoil(recoilUp, recoilSide);

        // 핵심: 스코프 중앙 화면 좌표에서 레이캐스트
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));

        // ─── 어그로탄 분기 ────────────────────────────────────────────────
        if (isNextShotAggro)
        {
            FireAggroBullet(ray);
            return;
        }
        // ─────────────────────────────────────────────────────────────────

        // ─── 관통탄 분기 ─────────────────────────────────────────────────
        if (currentPenetration)
        {
            FirePenetrationRay(ray);
        }
        else
        {
            FireSingleRay(ray);
        }
        // ─────────────────────────────────────────────────────────────────

        // --- 방금 쏜 총알이 마지막 탄약이었을 경우 미션 실패 처리 (가장 마지막에 체크) ---
        if (currentAmmo <= 0 && currentReloadableAmmo <= 0)
        {
            Debug.Log("Run out of all ammo! Mission Failed.");
            if (Map2ResultManager.Instance != null && Map2ResultManager.Instance.isActiveAndEnabled)
            {
                Map2ResultManager.Instance.ShowVillageInvadedPanel("모든 탄약을 소모했습니다.");
            }
            else if (KillCountManager.Instance != null)
            {
                KillCountManager.Instance.ShowMissionFailedPanel();
            }
        }
    }

    // =========================================================================
    // --- 어그로탄 전용 함수 ---
    // =========================================================================

    /// <summary>
    /// InGameUIBinder의 어그로 버튼 OnClick에서 호출됩니다.
    /// 토글식으로 작동하며, 다음 한 발을 어그로탄으로 지정/해제합니다.
    /// </summary>
    public void SetNextShotAsAggro()
    {
        isNextShotAggro = !isNextShotAggro;

        // 버튼 눌림 시각적 피드백 (어그로탄 색상 틴트)
        if (InGameUIBinder.Instance != null && InGameUIBinder.Instance.aggroBulletButton != null)
        {
            Image btnImg = InGameUIBinder.Instance.aggroBulletButton.GetComponent<Image>();
            if (btnImg != null)
            {
                // 눌려있으면 주황색, 아니면 하얀색으로 복구
                btnImg.color = isNextShotAggro ? new Color(1f, 0.8f, 0.2f, 1f) : Color.white;
            }
        }

        UpdateAmmoUI();
        Debug.Log($"[RaycastShooter] 어그로탄 토글 상태: {(isNextShotAggro ? "활성화" : "해제")}");
    }

    /// <summary>
    /// 외부(UIBinder 등)에서 어그로 탄 사용 불가능한 상태가 될 때 원격으로 초기화
    /// </summary>
    public void ForceCancelAggroShot()
    {
        isNextShotAggro = false;
        UpdateAmmoUI();
    }

    /// <summary>
    /// 어그로탄을 발사합니다.
    /// 착탄 위치에 AggroBulletMarker 프리팹을 생성하고 ShopItemData 수치를 주입합니다.
    /// 일반 탄약은 소모하지 않습니다.
    /// </summary>
    private void FireAggroBullet(Ray ray)
    {
        ShopItemData aggroData = (InventoryManager.Instance != null)
            ? InventoryManager.Instance.GetEquippedAggroAmmoData()
            : null;

        if (aggroData == null)
        {
            Debug.LogWarning("[RaycastShooter] 어그로탄 데이터를 찾을 수 없습니다!");
            return;
        }

        // =====================================================
        // 발사 시도 시 즉시 어그로탄 토글 해제 및 소모 처리
        // (허공에 날렸든 맞췄든 무조건 1회 소모됨)
        // =====================================================
        isNextShotAggro = false;
        UpdateAmmoUI(); // 토글 아이콘 등 복구
        
        if (InGameUIBinder.Instance != null && InGameUIBinder.Instance.aggroBulletButton != null)
        {
            // 사용했으니 버튼은 숨깁니다
            InGameUIBinder.Instance.aggroBulletButton.gameObject.SetActive(false);
            
            // 색상도 흰색으로 초기화해 둡니다
            Image btnImg = InGameUIBinder.Instance.aggroBulletButton.GetComponent<Image>();
            if (btnImg != null) btnImg.color = Color.white;
        }

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit))
        {
            // 허공에 쏘았을 경우 작동 취소. 복구 로직 파기! (그냥 날림)
            Debug.Log("[RaycastShooter] 허공에 발사하여 어그로탄이 낭비되었습니다.");
            return;
        }

        Vector3 spawnPos = hit.point;
        Debug.DrawLine(ray.origin, hit.point, Color.magenta, 3f);

        // --- 맞은 대상에게 딜링 및 피격 이펙트 즉시 적용 ---
        PlayBloodAtPoint(hit.point, hit.normal, false);
        ApplyHitDamage(hit.collider, hit.point);

        // --- AggroBulletMarker 생성 ---
        GameObject markerGO = new GameObject("AggroBulletMarker");
        markerGO.transform.position = spawnPos;
        markerGO.transform.rotation = Quaternion.LookRotation(hit.normal);
        
        // 대상(Hitbox 또는 몸통 등)에 부착하여 대상이 움직일 때 연막도 따라가게 함
        markerGO.transform.SetParent(hit.collider.transform, true);

        AggroBulletMarker marker = markerGO.AddComponent<AggroBulletMarker>();
        marker.Init(aggroData);

        Debug.Log($"[RaycastShooter] 어그로탄 착탄: {spawnPos}, 부착 대상: {hit.collider.name}");
    }

    // =========================================================================
    // --- 공통 발사 헬퍼 함수 (Shoot / ShootAt 양쪽에서 재사용) ---
    // =========================================================================

    /// <summary>
    /// 일반(비관통) 레이캐스트 발사. 레이 위 최초 충돌 1개만 처리합니다.
    /// </summary>
    private void FireSingleRay(Ray ray)
    {
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit)) return;

        Debug.DrawLine(ray.origin, hit.point, Color.red, 2f);
        PlayBloodAtPoint(hit.point, hit.normal, false); // 비관통: Play() 방식
        ApplyHitDamage(hit.collider, hit.point);
        ApplyFleeBehavior(hit.point, hit.collider.gameObject);
    }

    /// <summary>
    /// 관통(Penetration) 레이캐스트 발사.
    /// RaycastAll로 레이 위의 모든 충돌체를 거리 순으로 처리하며,
    /// MutantAI / MonsterAI / Hitbox 계열이면 계속 관통, 그 외 오브젝트에 닿으면 중단합니다.
    /// </summary>
    private void FirePenetrationRay(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray);

        // 거리 오름차순 정렬 (가장 가까운 충돌체부터 순서대로 처리)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // 이미 데미지를 준 루트 오브젝트 중복 방지 Set
        var damagedRoots = new System.Collections.Generic.HashSet<GameObject>();

        foreach (RaycastHit hit in hits)
        {
            // 이 콜라이더가 속한 '뮤턴트/몬스터의 루트 오브젝트'인지 판별
            bool isPenetrable = IsPenetrableTarget(hit.collider, out GameObject root);

            if (isPenetrable)
            {
                // 동일한 뮤턴트를 한 발에 여러 콜라이더로 여러 번 치지 않도록 중복 차단
                if (root != null && damagedRoots.Contains(root)) continue;
                if (root != null) damagedRoots.Add(root);

                Debug.DrawLine(ray.origin, hit.point, Color.cyan, 2f);
                PlayBloodAtPoint(hit.point, hit.normal, true); // 관통: Emit() 방식 (Play 재시작 방지)
                ApplyHitDamage(hit.collider, hit.point);
                // 관통 대상은 Flee를 별도로 트리거하지 않아도 됨 (이미 맞았으므로)
            }
            else
            {
                // 뮤턴트가 아닌 오브젝트(땅, 건물, 울타리 등)에 닿으면 관통 중단
                // 단, 트리거 콜라이더(투명 영역 판정용)는 무시하고 계속 통과
                if (!hit.collider.isTrigger)
                {
                    Debug.DrawLine(ray.origin, hit.point, Color.yellow, 2f);
                    // ★ 비관통 오브젝트에서도 파티클은 터져야 합니다.
                    //    비관통탄과 동일하게 Play() 방식으로 1회 재생 후 중단합니다.
                    PlayBloodAtPoint(hit.point, hit.normal, false);
                    break;
                }
            }
        }

        // 관통탄도 첫 번째 히트 지점에서 Flee 범위 트리거
        if (hits.Length > 0)
            ApplyFleeBehavior(hits[0].point, null);
    }

    /// <summary>
    /// 콜라이더가 뮤턴트/몬스터/히트박스 계열인지 확인합니다.
    /// 해당하면 true와 함께 루트 오브젝트(AI 스크립트가 달린 최상단)를 반환합니다.
    /// </summary>
    private bool IsPenetrableTarget(Collider col, out GameObject root)
    {
        root = null;

        // Hitbox 체크 (여우/뮤턴트 등에 붙어있는 부위 콜라이더)
        Hitbox hitbox = col.GetComponent<Hitbox>() ?? col.GetComponentInParent<Hitbox>();
        if (hitbox != null)
        {
            root = hitbox.transform.root.gameObject;
            return true;
        }

        // MutantAI 체크
        MutantAI mutant = col.GetComponent<MutantAI>() ?? col.GetComponentInParent<MutantAI>();
        if (mutant != null) { root = mutant.gameObject; return true; }

        // MonsterAI 체크
        MonsterAI monster = col.GetComponent<MonsterAI>() ?? col.GetComponentInParent<MonsterAI>();
        if (monster != null) { root = monster.gameObject; return true; }

        return false;
    }

    /// <summary>
    /// BloodSplatter 파티클을 특정 위치에 재생합니다.
    /// - useEmit = false (기본, 비관통): transform 이동 후 Play(). 파티클 시스템 Burst 전체 실행.
    /// - useEmit = true (관통전용): Emit(N개). Play()는 시스템을 '재시작'하므로
    ///   연속 호출 시 이전 파티클이 취소되는 버그가 있어, 관통탄에서는 Emit을 사용합니다.
    /// </summary>
    private void PlayBloodAtPoint(Vector3 point, Vector3 normal, bool useEmit = false)
    {
        if (bloodSplatter == null)
        {
            GameObject splatterObj = GameObject.Find("FX_BloodSplatter");
            if (splatterObj != null)
                bloodSplatter = splatterObj.GetComponent<ParticleSystem>();
        }
        if (bloodSplatter == null) return;

        if (useEmit)
        {
            // 관통탄 전용: Emit() 방식
            // Play()는 시스템을 재시작하여 이전 점의 파티클을 취소합니다.
            // Emit()은 시스템 상태를 유지한 채 지정 위치에서 즉시 N개를 방출합니다.
            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position               = point;
            emitParams.rotation3D             = Quaternion.LookRotation(normal).eulerAngles;
            emitParams.applyShapeToPosition   = true;
            bloodSplatter.Emit(emitParams, penetrationBloodEmitCount);
        }
        else
        {
            // 비관통 기본: 위치/방향 이동 후 Play() — 원래 방식 그대로
            bloodSplatter.transform.position = point;
            bloodSplatter.transform.rotation = Quaternion.LookRotation(normal);
            bloodSplatter.Play();
        }
    }

    /// <summary>
    /// 충돌한 콜라이더의 AI 스크립트를 탐색하여 적절한 TakeDamage / StopAnimation을 호출합니다.
    /// Shoot / ShootAt / FireSingleRay / FirePenetrationRay 가 모두 이 함수를 공유합니다.
    /// </summary>
    private void ApplyHitDamage(Collider col, Vector3 hitPoint)
    {
        // --- 히트박스 (부위별 증폭 데미지) ---
        Hitbox hitbox = col.GetComponent<Hitbox>() ?? col.GetComponentInParent<Hitbox>();
        if (hitbox != null)
        {
            hitbox.TakeDamage(currentGunDamage, hitPoint);
            Debug.Log($"[관통/히트박스] {col.name} 에 데미지 {currentGunDamage} 적용");
            return;
        }

        // --- Animator 탐색 (히트박스 없는 캐릭터) ---
        Animator animator = col.GetComponent<Animator>() ?? col.GetComponentInParent<Animator>();
        if (animator == null) return;

        RandomChickenAnimation chickenAnim = animator.GetComponent<RandomChickenAnimation>();
        if (chickenAnim != null) { chickenAnim.StopAnimation(); return; }

        RandomFoxAnimation foxAnim = animator.GetComponent<RandomFoxAnimation>();
        if (foxAnim != null) { foxAnim.StopAnimation(); return; }

        DecoyFoxAI decoyAnim = animator.GetComponent<DecoyFoxAI>();
        if (decoyAnim != null) { decoyAnim.StopAnimation(); return; }

        MonsterAI monsterAnim = animator.GetComponent<MonsterAI>();
        if (monsterAnim != null) { monsterAnim.TakeDamage(currentGunDamage, hitPoint); return; }

        MutantAI mutantAnim = animator.GetComponent<MutantAI>();
        if (mutantAnim != null) { mutantAnim.TakeDamage(currentGunDamage, hitPoint); return; }

        // 그 외 (기존 닭/여우 Die 트리거)
        animator.SetTrigger("Die");
        if (KillCountManager.Instance != null && (foxAnim != null || decoyAnim != null))
            KillCountManager.Instance.AddKill();
    }

    /// <summary>
    /// 착탄 지점 반경 내 동물들에게 Flee 신호를 보냅니다.
    /// </summary>
    private void ApplyFleeBehavior(Vector3 hitPoint, GameObject excludeObj)
    {
        Collider[] colliders = Physics.OverlapSphere(hitPoint, fleeRadius);
        foreach (Collider nearby in colliders)
        {
            if (excludeObj != null && nearby.gameObject == excludeObj) continue;

            RandomChickenAnimation chicken = nearby.GetComponent<RandomChickenAnimation>() ?? nearby.GetComponentInParent<RandomChickenAnimation>();
            if (chicken != null) { chicken.FleeFrom(hitPoint); continue; }

            RandomFoxAnimation fox = nearby.GetComponent<RandomFoxAnimation>() ?? nearby.GetComponentInParent<RandomFoxAnimation>();
            if (fox != null) { fox.FleeFrom(hitPoint); continue; }

            DecoyFoxAI decoyFox = nearby.GetComponent<DecoyFoxAI>() ?? nearby.GetComponentInParent<DecoyFoxAI>();
            if (decoyFox != null) { decoyFox.FleeFrom(hitPoint); continue; }

            MonsterAI monster = nearby.GetComponent<MonsterAI>() ?? nearby.GetComponentInParent<MonsterAI>();
            if (monster != null) { monster.FleeFrom(hitPoint); continue; }

            MutantAI mutant = nearby.GetComponent<MutantAI>() ?? nearby.GetComponentInParent<MutantAI>();
            if (mutant != null) mutant.FleeFrom(hitPoint);
        }
    }

    // === [신규 로직] 장전 시스템 ===
    public void TryReload()
    {
        // 게임 종료 연출 중(점프 공격/마을 침략)에는 장전 차단
        if (KillCountManager.isGameEnding) return;

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
            audioSource.PlayOneShot(reloadSound);

        // ── 재장전 긴장감 효과 (Map 1/2 공통) ────────────────────────────
        // reloadDimVolume: 인스펙터에서 StartingVolume을 연결해 두면 동작
        CameraController camCtrl = (mainCamera != null)
            ? mainCamera.GetComponent<CameraController>() : null;
        bool hasDimVolume = (reloadDimVolume != null);

        if (hasDimVolume)
        {
            // 1. 심장박동 SFX 시작
            if (heartbeatClip != null && heartbeatAudioSource != null)
            {
                heartbeatAudioSource.clip = heartbeatClip;
                heartbeatAudioSource.Play();
            }

            // 2. 카메라 셰이크 강화
            if (camCtrl != null)
                camCtrl.breathAmount *= reloadBreathMultiplier;

            // 3. StartingVolume Lerp: 0 → reloadVolumeTargetWeight (fadeIn)
            StartCoroutine(FadeThermalVolume(reloadDimVolume, reloadDimVolume.weight, reloadVolumeTargetWeight, reloadVolumeFadeInDuration));
        }
        // ─────────────────────────────────────────────────────────────────

        // 총 재장전 시간 3.1초 중 페이드 아웃 직전까지 대기
        float holdTime = 3.1f - reloadVolumeFadeOutDuration;
        yield return new WaitForSeconds(Mathf.Max(0f, holdTime));

        if (hasDimVolume)
        {
            // 4. StartingVolume Lerp: reloadVolumeTargetWeight → 0 (fadeOut)
            StartCoroutine(FadeThermalVolume(reloadDimVolume, reloadVolumeTargetWeight, 0f, reloadVolumeFadeOutDuration));
        }

        // 페이드 아웃 완료까지 대기
        yield return new WaitForSeconds(reloadVolumeFadeOutDuration);

        if (hasDimVolume)
        {
            // 5. 심장박동 SFX 중지
            if (heartbeatAudioSource != null)
                heartbeatAudioSource.Stop();

            // 6. 카메라 셰이크 복구
            if (camCtrl != null)
                camCtrl.breathAmount /= reloadBreathMultiplier;
        }

        // ── 탄약 보충 ────────────────────────────────────────────────────
        int ammoNeeded = maxAmmoPerMag - currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, currentReloadableAmmo);

        currentAmmo += ammoToReload;
        currentReloadableAmmo -= ammoToReload;

        isReloading = false;
        UpdateAmmoUI();
    }

    /// <summary>
    /// Volume.weight를 duration 초에 걸쳐 from → to로 부드럽게 Lerp합니다.
    /// StartingVolume(재장전 암전)에 사용되며 ThermalVolume은 건드리지 않습니다.
    /// </summary>
    private IEnumerator FadeThermalVolume(UnityEngine.Rendering.Volume vol, float from, float to, float duration)
    {
        if (vol == null || duration <= 0f) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            vol.weight = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        vol.weight = to;
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

        // --- 어그로 토글 상태에 따른 총알 이미지 변경 ---
        if (ammoIconImage != null)
        {
            if (defaultAmmoIcon == null)
            {
                defaultAmmoIcon = ammoIconImage.sprite;
            }

            if (isNextShotAggro && InventoryManager.Instance != null && InventoryManager.Instance.GetEquippedAggroAmmoData() != null)
            {
                ShopItemData aggroData = InventoryManager.Instance.GetEquippedAggroAmmoData();
                ammoIconImage.sprite = (aggroData.inGameAmmoIcon != null) ? aggroData.inGameAmmoIcon : aggroData.itemIcon;
            }
            else
            {
                if (defaultAmmoIcon != null)
                    ammoIconImage.sprite = defaultAmmoIcon;
            }
        }
    }
}
