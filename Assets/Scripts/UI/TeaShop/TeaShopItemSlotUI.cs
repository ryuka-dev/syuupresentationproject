using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 茶商店の商品格子一つ分の UI。
/// Icon / 茶名 / 単価 / 所持数を表示し、クリック時にコールバックを呼ぶ。
/// </summary>
public class TeaShopItemSlotUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text ownedText;
    [SerializeField] private Button   button;
    [SerializeField] private GameObject selectedFrame;

    private TeaShopItemData           _itemData;
    private Action<TeaShopItemData>   _onClicked;

    public TeaShopItemData ItemData => _itemData;

    // ── Setup ────────────────────────────────────────────────────────
    public void Setup(TeaShopItemData itemData, int ownedCount, Action<TeaShopItemData> onClicked)
    {
        _itemData  = itemData;
        _onClicked = onClicked;

        if (iconImage)
        {
            var icon = itemData?.TeaItem?.Icon;
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText)
            nameText.text = itemData?.TeaItem?.ItemName ?? string.Empty;

        if (priceText)
            priceText.text = itemData != null ? $"⊙{itemData.Price}" : "";

        if (ownedText)
            ownedText.text = $"持有:{ownedCount}";

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }

        SetSelected(false);
    }

    public void UpdateOwnedCount(int ownedCount)
    {
        if (ownedText) ownedText.text = $"持有:{ownedCount}";
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame) selectedFrame.SetActive(selected);
    }

    private void OnButtonClicked()
    {
        _onClicked?.Invoke(_itemData);
    }
}
