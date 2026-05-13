using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ShopItemUI : MonoBehaviour
{
    [Header("Item Data")]
    public ShopItemData myItemData;

    [Header("Optional: UI Elements within this Item Button")]
    [Tooltip("아이템 목록 버튼 안에 이름이나 아이콘을 표시하고 싶다면 연결하세요.")]
    public TextMeshProUGUI nameText;
    public Image iconImage;
    public GameObject soldText; // "Sold" 글자 오브젝트 연결용

    private Button itemButton;
    private ShopManager shopManager;

    private void Start()
    {
        itemButton  = GetComponent<Button>();
        shopManager = FindObjectOfType<ShopManager>();

        if (shopManager != null && itemButton != null)
        {
            itemButton.onClick.AddListener(() => shopManager.OpenItemInfo(myItemData, this));
        }

        UpdateUI();

        // 시작 시 Sold 포맷 초기화
        if (soldText != null) soldText.SetActive(false);

        // ★ 앱 재시작 시 이 아이템이 이전에 구매됐다면 Sold 상태 + 인벤토리 복원
        RestoreIfPurchased();
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
        }
    }

    public void MarkAsSold()
    {
        // 1. 더 이상 클릭 불가하게 만듦
        if (itemButton != null)
            itemButton.interactable = false;

        // 2. 아이콘 색깔을 어둡게 (회색) 변경
        if (iconImage != null)
            iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);

        // 3. Sold 텍스트 활성화
        if (soldText != null)
            soldText.SetActive(true);
    }

    /// <summary>
    /// 앱 재시작 시 이 아이템이 이미 구매된 경우 Sold 상태 복원 + 인벤토리 복원.
    /// </summary>
    private void RestoreIfPurchased()
    {
        if (myItemData == null) return;

        // PlayerPrefs에 구매 기록이 있으면 복원
        if (PlayerPrefs.GetInt("Purchased_" + myItemData.itemName, 0) == 1)
        {
            // 상점 아이템 Sold 표시
            MarkAsSold();

            // 인벤토리에 복원 (중복 방지는 InventoryManager.RestorePurchasedItem에서 처리)
            InventoryManager inv = InventoryManager.Instance;
            if (inv == null) inv = FindObjectOfType<InventoryManager>();
            if (inv != null) inv.RestorePurchasedItem(myItemData);
        }
    }
}
