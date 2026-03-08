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
        itemButton = GetComponent<Button>();
        
        // 씬에서 ShopManager를 찾습니다. (오브젝트 직접 연결보다 편함)
        shopManager = FindObjectOfType<ShopManager>();

        if (shopManager != null && itemButton != null)
        {
            // 이 버튼(아이템)을 누르면 ShopManager의 OpenItemInfo 함수에 '내 데이터'와 '나 자신(this)'을 보냄
            itemButton.onClick.AddListener(() => shopManager.OpenItemInfo(myItemData, this));
        }

        // 만약 버튼 자체에 이름이나 이미지를 데이터 기반으로 띄우고 싶다면 갱신
        UpdateUI();
        
        // 시작 시 Sold 포맷 초기화 (혹시 켜져있을까봐 끔)
        if (soldText != null) soldText.SetActive(false);
    }

    // 에디터에서 데이터가 바뀌면 자동으로 버튼UI 모양도 바뀌게 함 (선택)
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
            // iconImage.rectTransform.sizeDelta = myItemData.iconSize; // 제거됨: 에디터에서 설정한 원래 크기를 유지
        }
    }

    public void MarkAsSold()
    {
        // 1. 더 이상 클릭 불가하게 만듦
        if (itemButton != null)
        {
            itemButton.interactable = false;
        }

        // 2. 부모/자신의 아이콘 색깔을 어둡게 (회색) 변경
        if (iconImage != null)
        {
            iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f); 
        }

        // 3. Sold 텍스트 활성화
        if (soldText != null)
        {
            soldText.SetActive(true);
        }
    }
}
