using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

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
    public Image currentAggroAmmoImage; // 어그로 탄 현재 장착 슬롯

    [Header("Inventory Slots by Category")]
    [Tooltip("에디터에서 각 카테고리별 비어있는(비활성화된) 아이템 버튼들을 순서대로 연결하세요.")]
    public InventoryItemUI[] gunSlots;
    public InventoryItemUI[] scopeSlots;
    public InventoryItemUI[] magSlots;
    public InventoryItemUI[] aggroAmmoSlots; // 어그로 탄 슬롯

    private InventoryItemUI currentSelectedItemUI; // 현재 정보창에 열려있는 아이템
    
    [System.Serializable]
    public class LoadoutData
    {
        public InventoryItemUI equippedGunUI;
        public InventoryItemUI equippedScopeUI;
        public InventoryItemUI equippedMagUI;
        public InventoryItemUI equippedAggroAmmoUI;
    }

    [Header("Loadout Settings")]
    [Tooltip("상단 로드아웃 번호 1, 2, 3 버튼을 순서대로 연결하세요.")]
    public Button[] loadoutButtons;
    public Color selectedLoadoutColor = new Color(1f, 1f, 1f, 1f); // 선택됨 (기본 흰색)
    public Color unselectedLoadoutColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 선택 안됨 (회색)

    private LoadoutData[] loadouts = new LoadoutData[3];
    private int currentLoadoutIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        // 로드아웃 배열 초기화
        for (int i = 0; i < 3; i++)
        {
            loadouts[i] = new LoadoutData();
        }

        // --- 초기화: 비어있는 상태로 시작 ---
        // Awake에서 가장 먼저 지워둬야, 이후 다른 아이템들의 Start()가 불리면서 덮어씌워질 때 지워지지 않습니다.
        ClearCurrentStatusSlot(ItemCategory.Gun);
        ClearCurrentStatusSlot(ItemCategory.Scope);
        ClearCurrentStatusSlot(ItemCategory.Mag);
        ClearCurrentStatusSlot(ItemCategory.AggroAmmo);
    }

    /// <summary>
    /// 현재 장착된 스코프의 줌 배율을 반환합니다. (없거나 기본이면 1f)
    /// </summary>
    public float GetEquippedZoomMultiplier()
    {
        if (loadouts[currentLoadoutIndex].equippedScopeUI != null && loadouts[currentLoadoutIndex].equippedScopeUI.myItemData != null)
        {
            return loadouts[currentLoadoutIndex].equippedScopeUI.myItemData.zoomMultiplier;
        }
        return 1f;
    }

    /// <summary>
    /// 현재 장착된 스코프의 원본 데이터(ShopItemData)를 반환합니다.
    /// </summary>
    public ShopItemData GetEquippedScopeData()
    {
        if (loadouts[currentLoadoutIndex].equippedScopeUI != null)
        {
            return loadouts[currentLoadoutIndex].equippedScopeUI.myItemData;
        }
        return null;
    }

    /// <summary>
    /// 현재 장착된 총기의 원본 데이터(ShopItemData)를 반환합니다.
    /// </summary>
    public ShopItemData GetEquippedGunData()
    {
        if (loadouts[currentLoadoutIndex].equippedGunUI != null)
        {
            return loadouts[currentLoadoutIndex].equippedGunUI.myItemData;
        }
        return null;
    }

    /// <summary>
    /// 현재 장착된 탄창(Mag)의 원본 데이터를 반환합니다 (아이콘/이름 표시 용도).
    /// 탄창 수치(Ammo/Reloadable)는 MagazineUpgradeData에서 직접 읽으세요.
    /// </summary>
    public ShopItemData GetEquippedMagData()
    {
        if (loadouts[currentLoadoutIndex].equippedMagUI != null) return loadouts[currentLoadoutIndex].equippedMagUI.myItemData;
        return null;
    }

    /// <summary>
    /// 현재 장착된 어그로 탄의 원본 데이터(ShopItemData)를 반환합니다.
    /// 어그로 탄이 장착되어 있지 않으면 null을 반환합니다.
    /// </summary>
    public ShopItemData GetEquippedAggroAmmoData()
    {
        if (loadouts[currentLoadoutIndex].equippedAggroAmmoUI != null) return loadouts[currentLoadoutIndex].equippedAggroAmmoUI.myItemData;
        return null;
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

        // 로드아웃 버튼 이벤트 연결 및 초기화
        if (loadoutButtons != null)
        {
            for (int i = 0; i < loadoutButtons.Length; i++)
            {
                int index = i; // 클로저 이슈 방지
                if (loadoutButtons[index] != null)
                {
                    loadoutButtons[index].onClick.AddListener(() => SelectLoadout(index));
                }
            }
        }
        
        // 0번 로드아웃 기본 선택은 모든 아이템의 Start()가 끝난 뒤에 안전하게 실행하기 위해 1프레임 지연시킵니다.
        StartCoroutine(InitFirstLoadoutDelayed());
    }

    private System.Collections.IEnumerator InitFirstLoadoutDelayed()
    {
        yield return null;
        SelectLoadout(0);
    }

    /// <summary>
    /// 외부 스크립트(GunSelectManager 등)에서 특정 로드아웃의 정보를 읽어갈 때 사용합니다.
    /// </summary>
    public LoadoutData GetLoadout(int index)
    {
        if (index < 0 || index >= 3) return null;
        return loadouts[index];
    }

    public void SelectLoadout(int index)
    {
        if (index < 0 || index >= 3) return;
        currentLoadoutIndex = index;

        // 1. 버튼 색상 업데이트
        if (loadoutButtons != null)
        {
            for (int i = 0; i < loadoutButtons.Length; i++)
            {
                if (loadoutButtons[i] != null)
                {
                    Image btnImage = loadoutButtons[i].GetComponent<Image>();
                    if (btnImage != null)
                    {
                        btnImage.color = (i == currentLoadoutIndex) ? selectedLoadoutColor : unselectedLoadoutColor;
                    }
                }
            }
        }

        // 2. 인벤토리 목록 내 모든 아이템의 "Equipped" 눈알 UI 끄기
        DisableAllEquippedIndicators();

        // 3. CurrentStatus 아이콘 시각 효과만 비우기 (실제 데이터는 날리지 않음!)
        if (currentGunImage != null) { currentGunImage.sprite = null; currentGunImage.color = new Color(1, 1, 1, 0); }
        if (currentScopeImage != null) { currentScopeImage.sprite = null; currentScopeImage.color = new Color(1, 1, 1, 0); }
        if (currentMagImage != null) { currentMagImage.sprite = null; currentMagImage.color = new Color(1, 1, 1, 0); }
        if (currentAggroAmmoImage != null) { currentAggroAmmoImage.sprite = null; currentAggroAmmoImage.color = new Color(1, 1, 1, 0); }

        // 4. 선택된 로드아웃에 있는 아이템들을 다시 화면에 반영 (눈알 켜기 + CurrentStatus 아이콘 띄우기)
        LoadoutData currentData = loadouts[currentLoadoutIndex];
        if (currentData.equippedGunUI != null) ApplyToCurrentStatus(currentData.equippedGunUI);
        if (currentData.equippedScopeUI != null) ApplyToCurrentStatus(currentData.equippedScopeUI);
        if (currentData.equippedMagUI != null) ApplyToCurrentStatus(currentData.equippedMagUI);
        if (currentData.equippedAggroAmmoUI != null) ApplyToCurrentStatus(currentData.equippedAggroAmmoUI);

        // 5. 어그로 탄 버튼 갱신 (만약 비어있다면 끄고, 들어있으면 켜지도록)
        if (InGameUIBinder.Instance != null) InGameUIBinder.Instance.RefreshAggroButton();

        // 6. 우측 정보창이 열려있다면 버튼 상태(Equip/Unequip) 갱신
        UpdateButtonStates();
    }

    private void DisableAllEquippedIndicators()
    {
        void DisableArray(InventoryItemUI[] arr)
        {
            if (arr == null) return;
            foreach (var item in arr)
            {
                if (item != null && item.gameObject.activeSelf)
                    item.SetEquippedState(false);
            }
        }
        DisableArray(gunSlots);
        DisableArray(scopeSlots);
        DisableArray(magSlots);
        DisableArray(aggroAmmoSlots);
    }

    // [추가] 시작할 때 이미 장착(Equipped)이 켜져 있는 아이템을 CurrentStatus에 등록해주는 함수
    public void RegisterPreEquippedItem(InventoryItemUI preEquippedItem)
    {
        if (preEquippedItem == null || preEquippedItem.myItemData == null) return;
        
        // 에디터에서 켜둔 기본 아이템은 1, 2, 3번 로드아웃 모두에 기본값으로 저장합니다.
        for (int i = 0; i < 3; i++)
        {
            switch (preEquippedItem.myItemData.category)
            {
                case ItemCategory.Gun:       loadouts[i].equippedGunUI = preEquippedItem;       break;
                case ItemCategory.Scope:     loadouts[i].equippedScopeUI = preEquippedItem;     break;
                case ItemCategory.Mag:       loadouts[i].equippedMagUI = preEquippedItem;       break;
                case ItemCategory.AggroAmmo: loadouts[i].equippedAggroAmmoUI = preEquippedItem; break;
            }
        }

        Debug.Log($"시작 시 기본 장착(모든 조합) 적용됨: {preEquippedItem.myItemData.itemName}");
    }

    // [추가] 상점 등에서 새로운 아이템을 구매했을 때 인벤토리에 끼워넣는 함수
    public void AddNewItem(ShopItemData newData, Vector2 shopIconSize)
    {
        if (newData == null) return;

        // 1. 어느 카테고리의 배열을 쓸지 결정
        InventoryItemUI[] targetSlots = null;
        switch (newData.category)
        {
            case ItemCategory.Gun:       targetSlots = gunSlots;       break;
            case ItemCategory.Scope:     targetSlots = scopeSlots;     break;
            case ItemCategory.Mag:       targetSlots = magSlots;       break;
            case ItemCategory.AggroAmmo: targetSlots = aggroAmmoSlots; break;
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

    /// <summary>
    /// 앱 재시작 시 PlayerPrefs에서 복원 — 이미 해당 아이템이 슬롯에 있으면 중복 추가하지 않습니다.
    /// ShopItemUI.RestoreIfPurchased()에서 호출합니다.
    /// </summary>
    public void RestorePurchasedItem(ShopItemData data)
    {
        if (data == null) return;

        // 이미 인벤토리에 이 아이템이 활성화된 슬롯으로 존재하면 건너뜁니다
        InventoryItemUI[] slots = GetSlotsForCategory(data.category);
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null && slot.gameObject.activeSelf && slot.myItemData == data)
                {
                    Debug.Log($"[InventoryManager] 이미 복원된 아이템, 건너뜀: {data.itemName}");
                    return;
                }
            }
        }

        // 없으면 일반 추가 (iconSize는 ShopItemData의 기본값 사용)
        AddNewItem(data, data.iconSize);
        Debug.Log($"[InventoryManager] 구매 아이템 복원 완료: {data.itemName}");
    }

    private InventoryItemUI[] GetSlotsForCategory(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Gun:       return gunSlots;
            case ItemCategory.Scope:     return scopeSlots;
            case ItemCategory.Mag:       return magSlots;
            case ItemCategory.AggroAmmo: return aggroAmmoSlots;
            default:                     return null;
        }
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

        // 줌 버튼 가시성 실시간 업데이트 (인게임에서 장착 변경 시)
        if (data.category == ItemCategory.Scope && Camera.main != null)
        {
            CameraController camCtrl = Camera.main.GetComponent<CameraController>();
            if (camCtrl != null) camCtrl.UpdateZoomButtonVisibility();
        }
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

        // 줌 버튼 가시성 실시간 업데이트 (인게임에서 장착 해제 시 기본스펙으로 줌버튼 끔)
        if (data.category == ItemCategory.Scope && Camera.main != null)
        {
            CameraController camCtrl = Camera.main.GetComponent<CameraController>();
            if (camCtrl != null) camCtrl.UpdateZoomButtonVisibility();
        }
    }

    // --- 핵심 로직: CurrentStatus 패널 이미지 조작 ---
    
    // 특정 카테고리에 미리 장착된 녀석이 있다면 강제로 벗기는 함수
    private void UnequipExistingCategory(ItemCategory category)
    {
        InventoryItemUI existingItemUI = null;
        
        switch (category)
        {
            case ItemCategory.Gun:       existingItemUI = loadouts[currentLoadoutIndex].equippedGunUI;       break;
            case ItemCategory.Scope:     existingItemUI = loadouts[currentLoadoutIndex].equippedScopeUI;     break;
            case ItemCategory.Mag:       existingItemUI = loadouts[currentLoadoutIndex].equippedMagUI;       break;
            case ItemCategory.AggroAmmo: existingItemUI = loadouts[currentLoadoutIndex].equippedAggroAmmoUI; break;
        }

        // 뭔가 껴입고 있었다면 벗김 (인벤토리 목록 아이콘 눈알 끄기)
        if (existingItemUI != null)
        {
            existingItemUI.SetEquippedState(false);
        }
    }

    // CurrentStatus UI에 아이콘을 띄우고 "현재 로드아웃 장착 중인 아이템"으로 기록 및 눈알 켜기
    private void ApplyToCurrentStatus(InventoryItemUI targetUI)
    {
        if (targetUI == null) return;
        
        ShopItemData data = targetUI.myItemData;
        Image targetSlotImage = null;

        // 1. 해당 슬롯의 인벤토리 목록 아이콘 '눈알' 켜주기
        targetUI.SetEquippedState(true);

        // 2. 카테고리별로 타겟 이미지 컴포넌트와 현재 로드아웃 기록 변수 연결
        switch (data.category)
        {
            case ItemCategory.Gun:
                loadouts[currentLoadoutIndex].equippedGunUI   = targetUI;
                targetSlotImage = currentGunImage;
                break;
            case ItemCategory.Scope:
                loadouts[currentLoadoutIndex].equippedScopeUI = targetUI;
                targetSlotImage = currentScopeImage;
                break;
            case ItemCategory.Mag:
                loadouts[currentLoadoutIndex].equippedMagUI   = targetUI;
                targetSlotImage = currentMagImage;
                break;
            case ItemCategory.AggroAmmo:
                loadouts[currentLoadoutIndex].equippedAggroAmmoUI = targetUI;
                targetSlotImage     = currentAggroAmmoImage;
                // 어그로 탄 장착 시 InGameUIBinder의 어그로 버튼 갱신
                if (InGameUIBinder.Instance != null)
                    InGameUIBinder.Instance.RefreshAggroButton();
                break;
        }

        // 3. 실제 UI 이미지 교체 및 활성화 (CurrentStatus 화면)
        if (targetSlotImage != null)
        {
            targetSlotImage.sprite = data.itemIcon;
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
                loadouts[currentLoadoutIndex].equippedGunUI   = null;
                targetSlotImage = currentGunImage;
                break;
            case ItemCategory.Scope:
                loadouts[currentLoadoutIndex].equippedScopeUI = null;
                targetSlotImage = currentScopeImage;
                break;
            case ItemCategory.Mag:
                loadouts[currentLoadoutIndex].equippedMagUI   = null;
                targetSlotImage = currentMagImage;
                break;
            case ItemCategory.AggroAmmo:
                loadouts[currentLoadoutIndex].equippedAggroAmmoUI = null;
                targetSlotImage     = currentAggroAmmoImage;
                // 어그로 탄 해제 시 InGameUIBinder의 버튼 숨김
                if (InGameUIBinder.Instance != null)
                    InGameUIBinder.Instance.RefreshAggroButton();
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

        // 현재 켜져있는 로드아웃에 이 아이템이 장착되었는가?
        bool isCurrentlyEquipped = false;
        LoadoutData currentData = loadouts[currentLoadoutIndex];
        
        switch (currentSelectedItemUI.myItemData.category)
        {
            case ItemCategory.Gun:       isCurrentlyEquipped = (currentData.equippedGunUI == currentSelectedItemUI); break;
            case ItemCategory.Scope:     isCurrentlyEquipped = (currentData.equippedScopeUI == currentSelectedItemUI); break;
            case ItemCategory.Mag:       isCurrentlyEquipped = (currentData.equippedMagUI == currentSelectedItemUI); break;
            case ItemCategory.AggroAmmo: isCurrentlyEquipped = (currentData.equippedAggroAmmoUI == currentSelectedItemUI); break;
        }

        if (equipButton != null) equipButton.gameObject.SetActive(!isCurrentlyEquipped);
        if (unequipButton != null) unequipButton.gameObject.SetActive(isCurrentlyEquipped);
    }
}
