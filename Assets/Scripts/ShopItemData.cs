using UnityEngine;

public enum ItemCategory
{
    Gun,
    Scope,
    Mag // 탄창/야간투시경 등
}

[CreateAssetMenu(fileName = "NewShopItem", menuName = "Shop/Shop Item Data")]
public class ShopItemData : ScriptableObject
{
    [Header("Item Basic Info")]
    public string itemName;
    public ItemCategory category; // 추가됨: 아이템 카테고리
    public Sprite itemIcon;
    public Vector2 iconSize = new Vector2(100f, 100f); // 에디터에서 가로/세로 조절
    public int itemPrice;

    [Header("Item Details")]
    [TextArea(3, 10)]
    public string itemDescription;
    
    [Header("Gun Settings")]
    [Tooltip("바디샷 데미지 (주로 1 또는 2)")]
    public int gunDamage = 1;
    [Tooltip("격발 사운드")]
    public AudioClip shootSound;
    [Tooltip("1개의 탄창에 최대로 들어가는 총알 수")]
    public int maxAmmoInClip = 8;
    [Tooltip("발사 시 화면이 위로 튀는 반동 세기")]
    public float recoilUp = 2f;
    [Tooltip("발사 시 화면이 좌우로 떨리는 반동 세기")]
    public float recoilSide = 1f;

    [Header("Scope Settings")]
    [Tooltip("스코프 부착 시 화면 줌 배율 (1 = 줌 없음, 2 = 2배 줌)")]
    public float zoomMultiplier = 1f;
    
    [Tooltip("렌더링 안개 시작 거리 (0이면 기본 환경값 사용)")]
    public float fogStartDistance = 0f;
    [Tooltip("렌더링 안개 끝 거리 (0이면 기본 환경값 사용)")]
    public float fogEndDistance = 0f;
    
    [Tooltip("배터리 소모 효율 (1 = 정상, 0.5 = 절반 속도로 천천히 소모, 2.0 = 두 배 빨리 소모)")]
    public float batteryEfficiencyMultiplier = 1f;

    // 편의용 프로퍼티: 이 아이템이 줌 기능이 있는지 (배율이 1보다 큰지) 확인
    public bool isZoomable => zoomMultiplier > 1f;
}
