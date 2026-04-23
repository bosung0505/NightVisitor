using UnityEngine;

public enum ItemCategory
{
    Gun,
    Scope,
    Mag,
    AggroAmmo  // 어그로 탄 (조명탄 타입 특수 탄)
}

[CreateAssetMenu(fileName = "NewShopItem", menuName = "Shop/Shop Item Data")]
public class ShopItemData : ScriptableObject
{
    [Header("Item Basic Info")]
    public string itemName;
    public ItemCategory category;
    
    [Tooltip("상점과 인벤토리에서 보여질 기본 아이콘")]
    public Sprite itemIcon;
    
    [Tooltip("인게임 탄약 패널에서 활성화 시 보여질 아이콘 (비워두면 기본 아이콘 사용)")]
    public Sprite inGameAmmoIcon;
    
    public Vector2 iconSize = new Vector2(100f, 100f);
    public int itemPrice;

    [Header("Item Details")]
    [TextArea(3, 10)]
    public string itemDescription;
    
    [Header("Gun Settings")]
    [Tooltip("바디샷 데미지 (주로 1 또는 2)")]
    public int gunDamage = 1;
    [Tooltip("격발 사운드")]
    public AudioClip shootSound;
    [Tooltip("발사 시 화면이 위로 튀는 반동 세기")]
    public float recoilUp = 2f;
    [Tooltip("발사 시 화면이 좌우로 떨리는 반동 세기")]
    public float recoilSide = 1f;
    [Tooltip("체크 시 탄이 뮤턴트/몬스터를 관통하여 뒤에 있는 적들도 동시에 타격합니다.")]
    public bool hasPenetration = false;

    [Header("Scope Settings")]
    [Tooltip("스코프 부착 시 화면 줌 배율 (1 = 줌 없음, 2 = 2배 줌)")]
    public float zoomMultiplier = 1f;
    [Tooltip("렌더링 안개 시작 거리 (0이면 기본 환경값 사용)")]
    public float fogStartDistance = 0f;
    [Tooltip("렌더링 안개 끝 거리 (0이면 기본 환경값 사용)")]
    public float fogEndDistance = 0f;
    [Tooltip("배터리 소모 효율 (1 = 정상, 0.5 = 절반 속도로 천천히 소모, 2.0 = 두 배 빨리 소모)")]
    public float batteryEfficiencyMultiplier = 1f;

    [Header("Aggro Ammo Settings")]
    // ─── 타이밍 ─────────────────────────────────────────────────────────────
    [Tooltip("착탄 지점부터 폭발까지의 대기 시간 (초)")]
    public float effectDelay = 0.5f;
    [Tooltip("폭발 이후 연막이 시작되기까지의 추가 대기 시간 (초). 0이면 동시 시작.")]
    public float smokeDelay = 1.5f;
    [Tooltip("연막 시작 후 지속 시간 (초)")]
    public float effectDuration = 5.0f;
    [Tooltip("연막 효과 종료 시 소리/이펙트가 서서히 사라지는 데 걸리는 시간 (초)")]
    public float smokeFadeOutDuration = 1.5f;

    // ─── 어그로 범위 ────────────────────────────────────────────────────────
    [Tooltip("어그로가 끌리는 반경 (m). 이 반경 내 뮤턴트들이 착탄 지점으로 몰려옵니다.")]
    public float aggroRadius = 20f;
    [Tooltip("어그로 끌린 뮤턴트가 어그로 탄 주위를 도는 순찰 반경 (m)")]
    public float circleRadius = 3f;
    [Tooltip("어그로 끌린 뮤턴트가 접근할 때의 걷기 속도 (m/s)")]
    public float luredWalkSpeed = 1.2f;

    // ─── 폭발 이펙트 (1회) ──────────────────────────────────────────────────
    [Tooltip("착탄 후 effectDelay 초 뒤에 1회 재생될 폭발 사운드")]
    public AudioClip explosionSFX;
    [Tooltip("폭발 볼륨 (0~1). 1 = 최대, 0.5 = 절반")]
    [Range(0f, 1f)] public float explosionVolume = 1f;
    [Tooltip("폭발 시 1회 재생할 파티클 프리팹")]
    public GameObject explosionParticlePrefab;
    [Tooltip("폭발 파티클 크기 배율. 1 = 원본, 2 = 두 배. ★ Particle System의 Main > Scaling Mode를 Hierarchy로 설정해야 적용됩니다.")]
    public float explosionScale = 1f;

    // ─── 연막 이펙트 (루프) ─────────────────────────────────────────────────
    [Tooltip("폭발 직후부터 루프 재생될 연막탄(조명탄) 사운드")]
    public AudioClip smokeSFX;
    [Tooltip("연막 볼륨 (0~1). 1 = 최대, 0.5 = 절반")]
    [Range(0f, 1f)] public float smokeVolume = 1f;
    [Tooltip("연막 이펙트 파티클 프리팹 (smokeDelay 초 후 시작, effectDuration 동안 지속)")]
    public GameObject smokeParticlePrefab;
    [Tooltip("연막 파티클 크기 배율. 1 = 원본, 2 = 두 배. ★ Particle System의 Main > Scaling Mode를 Hierarchy로 설정해야 적용됩니다.")]
    public float smokeScale = 1f;

    // 편의용 프로퍼티: 이 아이템이 줌 기능이 있는지 (배율이 1보다 큰지) 확인
    public bool isZoomable => zoomMultiplier > 1f;
}
