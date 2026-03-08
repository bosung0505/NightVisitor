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
    
    // 능력치 등 추가 정보가 필요하다면 아래에 변수를 추가하세요.
    // public int damageBonus;
    // public float fireRate;
    // ...
}
