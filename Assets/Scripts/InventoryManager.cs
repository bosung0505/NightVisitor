using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    [Header("Item Info Panel")]
    public GameObject itemInfoPanel;

    [Header("UI Elements within Panel")]
    public TextMeshProUGUI itemNameText;
    public Image itemIconImage;
    public TextMeshProUGUI itemDescriptionText;
    
    [Header("Buttons within Panel")]
    public Button closeButton;
    public Button equipButton;    // 장착 버튼
    public Button unequipButton;  // 해제 버튼

    [Header("Current Status Slots (Images)")]
    [Tooltip("현재 장착된 아이템의 아이콘을 띄워줄 CurrentStatus 안의 각 파츠 이미지 UI")]
    public Image currentGunImage;
    public Image currentScopeImage;
    public Image currentMagImage;

    [Header("Inventory Slots by Category")]
    [Tooltip("에디터에서 각 카테고리별 비어있는(비활성화된) 아이템 버튼들을 순서대로 연결하세요.")]
    public InventoryItemUI[] gunSlots;
    public InventoryItemUI[] scopeSlots;
    public InventoryItemUI[] magSlots;

    private InventoryItemUI currentSelectedItemUI; // 현재 정보창에 열려있는 아이템
    
    // 현재 각 카테고리별로 장착 중인 '목록 아이템(InventoryItemUI)'들을 기억하는 변수
    private InventoryItemUI equippedGunUI;
    private InventoryItemUI equippedScopeUI;
    private InventoryItemUI equippedMagUI;

    private void Awake()
    {
        // --- 초기화: 비어있는 상태로 시작 ---
        // Awake에서 가장 먼저 지워둬야, 이후 다른 아이템들의 Start()가 불리면서 덮어씌워질 때 지워지지 않습니다.
        ClearCurrentStatusSlot(ItemCategory.Gun);
        ClearCurrentStatusSlot(ItemCategory.Scope);
        ClearCurrentStatusSlot(ItemCategory.Mag);
    }

    private void Start()
    {
        // 시작할 때 정보 창 숨김
        if (itemInfoPanel != null) itemInfoPanel.SetActive(false);

        // 닫기 버튼 이벤트 연결
        if (closeButton != null) closeButton.onClick.AddListener(CloseItemInfo);

        // 장착/해제 버튼 이벤트 연결
        if (equipButton != null) equipButton.onClick.AddListener(OnEquipButtonClicked);
        if (unequipButton != null) unequipButton.onClick.AddListener(OnUnequipButtonClicked);
    }

    // [추가] 시작할 때 이미 장착(Equipped)이 켜져 있는 아이템을 CurrentStatus에 등록해주는 함수
    public void RegisterPreEquippedItem(InventoryItemUI preEquippedItem)
    {
        if (preEquippedItem == null || preEquippedItem.myItemData == null) return;
        
        // 아이콘을 CurrentStatus 슬롯에 띄움
        ApplyToCurrentStatus(preEquippedItem);
        Debug.Log($"시작 시 기본 장착 적용됨: {preEquippedItem.myItemData.itemName}");
    }

    // [추가] 상점 등에서 새로운 아이템을 구매했을 때 인벤토리에 끼워넣는 함수
    public void AddNewItem(ShopItemData newData, Vector2 shopIconSize)
    {
        if (newData == null) return;

        // 1. 어느 카테고리의 배열을 쓸지 결정
        InventoryItemUI[] targetSlots = null;
        switch (newData.category)
        {
            case ItemCategory.Gun: targetSlots = gunSlots; break;
            case ItemCategory.Scope: targetSlots = scopeSlots; break;
            case ItemCategory.Mag: targetSlots = magSlots; break;
        }

        if (targetSlots == null || targetSlots.Length == 0)
        {
            Debug.LogWarning($"[{newData.category}] 카테고리의 인벤토리 슬롯 배열이 비어있거나 할당되지 않았습니다!");
            return;
        }

        // 2. 해당 배열의 0번 인덱스부터 순회하며 빈 슬롯(비활성화된 오브젝트)을 찾음
        for (int i = 0; i < targetSlots.Length; i++)
        {
            InventoryItemUI slot = targetSlots[i];
            
            // 만약 해당 슬롯 오브젝트가 꺼져 있다면 (빈 자리라면)
            if (!slot.gameObject.activeSelf)
            {
                // 3. 데이터 덮어씌우기
                slot.myItemData = newData;

                // 4. 활성화해서 화면에 보이게 함
                slot.gameObject.SetActive(true);

                // 5. 상점에서의 아이콘 크기를 그대로 적용 (이후 InventoryItemUI 갱신 처리용)
                slot.UpdateUIOverrideSize(shopIconSize);

                Debug.Log($"인벤토리에 새 아이템 등록됨: {newData.itemName} (슬롯 인덱스: {i})");
                return; // 성공했으니 함수 종료
            }
        }

        // 반복문을 다 돌았는데 빈자리가 없었다면
        Debug.LogWarning($"인벤토리의 [{newData.category}] 슬롯이 가득 찼습니다!");
    }

    public void OpenItemInfo(ShopItemData itemData, InventoryItemUI itemUI)
    {
        if (itemData == null || itemUI == null) return;

        currentSelectedItemUI = itemUI;

        // UI에 기본 데이터 덮어씌우기
        if (itemNameText != null) itemNameText.text = itemData.itemName;
        if (itemIconImage != null)
        {
            itemIconImage.sprite = itemData.itemIcon;
            itemIconImage.rectTransform.sizeDelta = itemData.iconSize; // 아이콘 크기 적용
        }
        if (itemDescriptionText != null) itemDescriptionText.text = itemData.itemDescription;

        // 장착 상태에 따라 Equip / Unequip 버튼 활성화 여부 업데이트
        UpdateButtonStates();

        if (itemInfoPanel != null) itemInfoPanel.SetActive(true);
    }

    public void CloseItemInfo()
    {
        currentSelectedItemUI = null;
        if (itemInfoPanel != null) itemInfoPanel.SetActive(false);
    }

    private void OnEquipButtonClicked()
    {
        if (currentSelectedItemUI == null) return;
        
        ShopItemData data = currentSelectedItemUI.myItemData;

        // 1. 해당 카테고리에 이미 장착된 무언가가 있다면? -> 먼저 해제시켜줌 (교체 로직)
        UnequipExistingCategory(data.category);

        // 2. 새로운 아이템 자체의 장착 UI(Equipped 눈알) 켜줌
        currentSelectedItemUI.SetEquippedState(true);

        // 3. CurrentStatus 쪽에 현재 장착한 아이템의 아이콘을 띄우고 메모리에 저장
        ApplyToCurrentStatus(currentSelectedItemUI);

        UpdateButtonStates();
        Debug.Log($"[{data.category}] 부위에 {data.itemName} 장착 완료!");
    }

    private void OnUnequipButtonClicked()
    {
        if (currentSelectedItemUI == null) return;
        
        ShopItemData data = currentSelectedItemUI.myItemData;

        // 1. 선택한 아이템의 장착 UI 끄기
        currentSelectedItemUI.SetEquippedState(false);

        // 2. CurrentStatus 쪽에서 해당 슬롯을 비우고 투명하게 만듦
        ClearCurrentStatusSlot(data.category);

        UpdateButtonStates();
        Debug.Log($"[{data.category}] 장착 해제 완료!");
    }

    // --- 핵심 로직: CurrentStatus 패널 이미지 조작 ---
    
    // 특정 카테고리에 미리 장착된 녀석이 있다면 강제로 벗기는 함수
    private void UnequipExistingCategory(ItemCategory category)
    {
        InventoryItemUI existingItemUI = null;
        
        switch (category)
        {
            case ItemCategory.Gun: existingItemUI = equippedGunUI; break;
            case ItemCategory.Scope: existingItemUI = equippedScopeUI; break;
            case ItemCategory.Mag: existingItemUI = equippedMagUI; break;
        }

        // 뭔가 껴입고 있었다면 벗김
        if (existingItemUI != null)
        {
            existingItemUI.SetEquippedState(false);
        }
    }

    // CurrentStatus UI에 아이콘을 띄우고 "장착 중인 아이템"으로 기록해두는 함수
    private void ApplyToCurrentStatus(InventoryItemUI targetUI)
    {
        ShopItemData data = targetUI.myItemData;
        Image targetSlotImage = null;

        // 카테고리별로 타겟 이미지 컴포넌트와 기록 변수 연결
        switch (data.category)
        {
            case ItemCategory.Gun: 
                equippedGunUI = targetUI; 
                targetSlotImage = currentGunImage; 
                break;
            case ItemCategory.Scope: 
                equippedScopeUI = targetUI; 
                targetSlotImage = currentScopeImage; 
                break;
            case ItemCategory.Mag: 
                equippedMagUI = targetUI; 
                targetSlotImage = currentMagImage; 
                break;
        }

        // 실제 UI 이미지 교체 및 활성화
        if (targetSlotImage != null)
        {
            targetSlotImage.sprite = data.itemIcon;
            
            // CurrentStatus에 그려지는 아이콘 크기도 원본 비율을 따르게 함 (선택)
            // targetSlotImage.rectTransform.sizeDelta = data.iconSize; // <- 이 부분을 제거하여 씬에 설정된 크기 유지
            
            targetSlotImage.color = new Color(1, 1, 1, 1); // 투명도 100% (보이게)
        }
    }

    // CurrentStatus UI의 해당 슬롯을 비우고 투명하게 만드는 함수
    private void ClearCurrentStatusSlot(ItemCategory category)
    {
        Image targetSlotImage = null;

        switch (category)
        {
            case ItemCategory.Gun: 
                equippedGunUI = null; 
                targetSlotImage = currentGunImage; 
                break;
            case ItemCategory.Scope: 
                equippedScopeUI = null; 
                targetSlotImage = currentScopeImage; 
                break;
            case ItemCategory.Mag: 
                equippedMagUI = null; 
                targetSlotImage = currentMagImage; 
                break;
        }

        // 아이콘을 지우고 투명하게 만듦
        if (targetSlotImage != null)
        {
            targetSlotImage.sprite = null;
            targetSlotImage.color = new Color(1, 1, 1, 0); // 투명도 0% (안 보이게)
        }
    }

    private void UpdateButtonStates()
    {
        if (currentSelectedItemUI == null) return;

        bool isCurrentlyEquipped = currentSelectedItemUI.IsEquipped();

        if (equipButton != null) equipButton.gameObject.SetActive(!isCurrentlyEquipped);
        if (unequipButton != null) unequipButton.gameObject.SetActive(isCurrentlyEquipped);
    }
}
