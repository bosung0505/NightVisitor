using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class InventoryItemUI : MonoBehaviour
{
    [Header("Item Data")]
    // 상점에서 쓰던 ShopItemData를 그대로 활용합니다. (이름, 아이콘 등)
    public ShopItemData myItemData; 

    [Header("Equipped Indicator")]
    [Tooltip("장착 중임을 표시하는 오브젝트 (이 아이템의 자식인 'Equipped' 객체)")]
    public GameObject equippedIndicator;

    [Header("Optional UI Elements")]
    public TextMeshProUGUI nameText;
    public Image iconImage;

    private Button itemButton;
    private InventoryManager inventoryManager;

    // 상점에서 덮어씌워진 크기인지 저장
    private bool hasOverriddenSize = false;
    private Vector2 overriddenSize;

    private void Start()
    {
        itemButton = GetComponent<Button>();
        
        // 씬에서 InventoryManager를 찾습니다.
        inventoryManager = FindObjectOfType<InventoryManager>();

        if (inventoryManager != null && itemButton != null)
        {
            // 이 인벤토리 버튼을 누르면 InventoryManager에게 내 데이터와 '나 자신(this)'을 통째로 보냅니다!
            itemButton.onClick.AddListener(() => inventoryManager.OpenItemInfo(myItemData, this));
            
            // [추가] 씬에 미리 Equipped 오브젝트가 켜진 채로 시작했다면, 매니저에게 나를 등록하라고 알림!
            if (IsEquipped())
            {
                inventoryManager.RegisterPreEquippedItem(this);
            }
        }

        UpdateUI();
    }

    private void OnValidate()
    {
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (myItemData == null) return;

        if (nameText != null) nameText.text = myItemData.itemName;
        if (iconImage != null)
        {
            iconImage.sprite = myItemData.itemIcon;
            
            // 상점에서 구매해서 넘어온 아이템일 때만 상점 크기를 강제로 덮어씌움
            if (hasOverriddenSize)
            {
                iconImage.rectTransform.sizeDelta = overriddenSize;
            }
            // else 삭제됨: 기본 인벤토리 아이템은 에디터 씬에서 잡아둔 크기를 그대로 유지함
        }
    }

    // 외부(InventoryManager)에서 이 아이템 슬롯에 데이터를 밀어 넣을 때, 상점용 크기를 강제 적용하는 함수
    public void UpdateUIOverrideSize(Vector2 newSize)
    {
        hasOverriddenSize = true;
        overriddenSize = newSize;
        UpdateUI();
    }

    // 현재 이 아이템이 장착 상태인지(Equipped 오브젝트가 켜져 있는지) 반환
    public bool IsEquipped()
    {
        if (equippedIndicator != null)
        {
            return equippedIndicator.activeSelf;
        }
        return false;
    }

    // 외부(InventoryManager)에서 이 아이템의 장착 상태를 켜거나 끄게 해주는 함수
    public void SetEquippedState(bool isEquipped)
    {
        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(isEquipped);
        }
    }
}
