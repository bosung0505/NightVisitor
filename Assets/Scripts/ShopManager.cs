using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ShopManager : MonoBehaviour
{
    [Header("Item Info Panel")]
    [Tooltip("가운데 꽉 차게 뜰 아이템 상세 정보 패널")]
    public GameObject itemInfoPanel;

    [Header("UI Elements within Panel")]
    public TextMeshProUGUI itemNameText;
    public Image itemIconImage;
    public TextMeshProUGUI itemPriceText;
    public TextMeshProUGUI itemDescriptionText;
    
    // 닫기 버튼과 구매 버튼 연결용
    public Button closeButton;
    public Button buyButton;

    [Header("Purchase Support (Money & Messages)")]
    [Tooltip("Money -> Case -> Have 에 해당하는 현재 돈 표시 텍스트")]
    public TextMeshProUGUI playerMoneyText;

    [Tooltip("구매 성공 시 자동으로 뜨고 사라지는 팝업 (PopupDissolveTransition 부착 오브젝트)")]
    public PopupDissolveTransition completeMessagePopup;
    [Tooltip("구매 실패 시 자동으로 뜨고 사라지는 팝업 (PopupDissolveTransition 부착 오브젝트)")]
    public PopupDissolveTransition failMessagePopup;

    [Tooltip("성공 메시지 표시 시간 (초)")]
    public float completeDisplayDuration = 1f;
    [Tooltip("실패 메시지 표시 시간 (초)")]
    public float failDisplayDuration = 1.5f;

    [Header("Inventory Integration")]
    public InventoryManager inventoryManager;

    private ShopItemData currentSelectedItem;
    private ShopItemUI currentSelectedItemUI;

    private void Start()
    {
        if (itemInfoPanel != null)
            itemInfoPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseItemInfo);

        if (buyButton != null)
            buyButton.onClick.AddListener(BuySelectedItem);

        UpdateMoneyUI();
    }

    // ★ 쇼핑 패널이 활성화될 때 구독 — 골드가 어디서 바뀀어도 자동 반영
    private void OnEnable()
    {
        KillCountManager.OnGoldChanged += UpdateMoneyUI;
        UpdateMoneyUI(); // 패널 열 때 현재값 즉시 반영
    }

    private void OnDisable()
    {
        KillCountManager.OnGoldChanged -= UpdateMoneyUI;
    }

    public void UpdateMoneyUI()
    {
        if (playerMoneyText != null)
            playerMoneyText.text = KillCountManager.currentSessionGold.ToString("N0");
    }

    // 아이템UI 버튼을 클릭했을 때 호출될 함수
    public void OpenItemInfo(ShopItemData itemData, ShopItemUI itemUI)
    {
        if (itemData == null || itemUI == null) return;

        currentSelectedItem = itemData;
        currentSelectedItemUI = itemUI;

        if (itemNameText != null) itemNameText.text = itemData.itemName;
        if (itemIconImage != null)
        {
            itemIconImage.sprite = itemData.itemIcon;
            itemIconImage.rectTransform.sizeDelta = itemData.iconSize;
        }
        if (itemPriceText != null) itemPriceText.text = itemData.itemPrice.ToString() + " Gold";
        if (itemDescriptionText != null) itemDescriptionText.text = itemData.itemDescription;

        if (itemInfoPanel != null)
            itemInfoPanel.SetActive(true);
    }

    public void CloseItemInfo()
    {
        currentSelectedItem = null;
        currentSelectedItemUI = null;
        if (itemInfoPanel != null)
            itemInfoPanel.SetActive(false);
    }

    private void BuySelectedItem()
    {
        if (currentSelectedItem == null || currentSelectedItemUI == null) return;

        int currentMoney = KillCountManager.currentSessionGold;

        if (currentMoney >= currentSelectedItem.itemPrice)
        {
            // --- 구매 성공 ---

            // 1. 돈 차감 (setter가 PlayerPrefs에 자동 저장)
            KillCountManager.currentSessionGold -= currentSelectedItem.itemPrice;

            // ★ 구매 아이템 영구 저장 — 앱 재시작 시 ShopItemUI가 읽어 Sold + 인벤토리를 복원합니다
            PlayerPrefs.SetInt("Purchased_" + currentSelectedItem.itemName, 1);
            PlayerPrefs.Save();

            // UI 돈 텍스트 갱신
            UpdateMoneyUI();

            // 2. 상점 목록의 아이템을 품절(Sold) 처리
            currentSelectedItemUI.MarkAsSold();

            // 3. 인벤토리로 해당 아이템 데이터와 현재 상점에서 쓰던 아이콘 크기를 그대로 전송
            if (inventoryManager != null)
            {
                Vector2 shopIconSize = currentSelectedItemUI.iconImage.rectTransform.sizeDelta;
                inventoryManager.AddNewItem(currentSelectedItem, shopIconSize);
            }

            // 4. 성공 메시지 띄우는 코루틴 시작
            StartCoroutine(ShowCompleteMessageRoutine());
        }
        else
        {
            // --- 구매 실패: 돈 부족 ---
            StartCoroutine(ShowFailMessageRoutine());
        }
    }

    private IEnumerator ShowCompleteMessageRoutine()
    {
        // 성공 팝업: ShopManager(this)가 코루틴을 대신 실행 (팝업 당사자가 비활성화 상태여도 동작)
        if (completeMessagePopup != null)
            completeMessagePopup.ShowAndAutoClose(completeDisplayDuration, this);

        // 팝업 연출(열기 duration + 표시 시간 + 닫기 duration)이 끝날 때까지 대기 후 Info Panel 닫기
        float totalWait = (completeMessagePopup != null)
            ? completeMessagePopup.duration + completeDisplayDuration + completeMessagePopup.duration
            : completeDisplayDuration;

        yield return new WaitForSeconds(totalWait);
        CloseItemInfo();
    }

    private IEnumerator ShowFailMessageRoutine()
    {
        // 실패 팝업: ShopManager(this)가 코루틴을 대신 실행 (팝업 당사자가 비활성화 상태여도 동작)
        if (failMessagePopup != null)
            failMessagePopup.ShowAndAutoClose(failDisplayDuration, this);

        yield return null; // 코루틴 형식 유지
    }
}
