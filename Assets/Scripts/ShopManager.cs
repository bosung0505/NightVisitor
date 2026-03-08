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
    public GameObject completeMessage;
    public GameObject failMessage;

    [Header("Inventory Integration")]
    public InventoryManager inventoryManager;

    private ShopItemData currentSelectedItem;
    private ShopItemUI currentSelectedItemUI;

    private void Start()
    {
        // 시작할 때 상세 정보 창은 숨김
        if (itemInfoPanel != null)
        {
            itemInfoPanel.SetActive(false);
        }

        // 닫기 버튼 이벤트 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseItemInfo);
        }

        // 구매 버튼 이벤트 연결
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(BuySelectedItem);
        }

        // 혹시 켜져있을지 모르는 메시지 초기화
        if (completeMessage != null) completeMessage.SetActive(false);
        if (failMessage != null) failMessage.SetActive(false);

        // 상점 열 때 현재 가진 돈 UI 연동
        UpdateMoneyUI();
    }

    public void UpdateMoneyUI()
    {
        if (playerMoneyText != null)
        {
            playerMoneyText.text = KillCountManager.currentSessionGold.ToString("N0");
        }
    }

    // 아이템UI 버튼을 클릭했을 때 호출될 함수
    // 이제 ShopItemUI 자기 자신도 같이 넘겨받습니다.
    public void OpenItemInfo(ShopItemData itemData, ShopItemUI itemUI)
    {
        if (itemData == null || itemUI == null) return;

        currentSelectedItem = itemData;
        currentSelectedItemUI = itemUI;

        // UI에 데이터 덮어씌우기
        if (itemNameText != null) itemNameText.text = itemData.itemName;
        if (itemIconImage != null)
        {
            itemIconImage.sprite = itemData.itemIcon;
            itemIconImage.rectTransform.sizeDelta = itemData.iconSize; // 아이콘 크기 적용
        }
        if (itemPriceText != null) itemPriceText.text = itemData.itemPrice.ToString() + " Gold"; // 또는 $ 등 기호
        if (itemDescriptionText != null) itemDescriptionText.text = itemData.itemDescription;

        // 패널 켜기
        if (itemInfoPanel != null)
        {
            itemInfoPanel.SetActive(true);
        }
    }

    public void CloseItemInfo()
    {
        currentSelectedItem = null;
        currentSelectedItemUI = null;
        if (itemInfoPanel != null)
        {
            itemInfoPanel.SetActive(false);
        }
    }

    private void BuySelectedItem()
    {
        if (currentSelectedItem == null || currentSelectedItemUI == null) return;

        // 이제 전역 임시 변수 KillCountManager.currentSessionGold를 직접 참조합니다.
        int currentMoney = KillCountManager.currentSessionGold;

        // 골드 비교 로직
        if (currentMoney >= currentSelectedItem.itemPrice)
        {
            // --- 구매 성공: 돈 차감 ---
            KillCountManager.currentSessionGold -= currentSelectedItem.itemPrice;
            
            // UI 돈 텍스트 갱신 (천 단위 콤마 포맷)
            UpdateMoneyUI();

            // 1. 상점 목록의 아이템을 품절(Sold) 처리
            currentSelectedItemUI.MarkAsSold();

            // 2. 인벤토리로 해당 아이템 데이터와 현재 상점에서 쓰던 아이콘 크기를 그대로 전송
            if (inventoryManager != null)
            {
                // currentSelectedItemUI.iconImage.rectTransform.sizeDelta 를 전송합니다.
                Vector2 shopIconSize = currentSelectedItemUI.iconImage.rectTransform.sizeDelta;
                inventoryManager.AddNewItem(currentSelectedItem, shopIconSize);
            }

            // 3. 성공 메시지 띄우는 코루틴 시작
            StartCoroutine(ShowCompleteMessageRoutine());
        }
        else
        {
            // --- 구매 실패: 돈 부족 ---
            // 실패 메시지 띄우는 코루틴 시작
            StartCoroutine(ShowFailMessageRoutine());
        }
    }

    private IEnumerator ShowCompleteMessageRoutine()
    {
        // 1. 성공 메시지 켜기
        if (completeMessage != null) completeMessage.SetActive(true);

        // 2. 플레이어가 글씨를 읽을 수 있게 잠시 1초 대기 (이후 패널 닫음)
        yield return new WaitForSeconds(1.0f);

        // 3. 메시지를 끄고, Info Panel 전체 닫기
        if (completeMessage != null) completeMessage.SetActive(false);
        CloseItemInfo();
    }

    private IEnumerator ShowFailMessageRoutine()
    {
        // 1. 실패 메시지 켜기
        if (failMessage != null) failMessage.SetActive(true);

        // 2. 2초 대기
        yield return new WaitForSeconds(2.0f);

        // 3. 패널은 그대로 둔 채 실패 메시지만 끄기
        if (failMessage != null) failMessage.SetActive(false);
    }
}
