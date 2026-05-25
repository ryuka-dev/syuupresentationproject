
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 格子式背包スロット。アイコン + 数量のみ表示。テキストは DetailWindow に委ねる。
/// </summary>
public class InventoryGridSlotUI : MonoBehaviour
{
    [SerializeField] private Image      iconImage;
    [SerializeField] private TMP_Text  countText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button    button;

    private ItemStack            _stack;
    private Action<ItemStack>    _onClicked;

    public bool      IsEmpty    => _stack == null;
    public ItemStack BoundStack => _stack;

    // ── Empty ──────────────────────────────────────────────────────
    public void SetEmpty()
    {
        _stack     = null;
        _onClicked = null;
        if (iconImage)   { iconImage.sprite  = null; iconImage.enabled = false; }
        if (countText)     countText.gameObject.SetActive(false);
        SetSelected(false);
        if (button) button.onClick.RemoveAllListeners();
    }

    // ── Occupied ───────────────────────────────────────────────────
    public void SetItem(ItemStack stack, Action<ItemStack> onClicked)
    {
        if (stack == null || stack.ItemData == null) { SetEmpty(); return; }

        _stack     = stack;
        _onClicked = onClicked;

        if (iconImage)
        {
            var icon = stack.ItemData.Icon;
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        if (countText)
        {
            bool show = stack.Count > 1;
            countText.gameObject.SetActive(show);
            if (show) countText.text = stack.Count.ToString();
        }

        SetSelected(false);
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    // ── Selected frame ─────────────────────────────────────────────
    public void SetSelected(bool selected)
    {
        if (selectedFrame) selectedFrame.SetActive(selected);
    }

    private void OnButtonClicked() { _onClicked?.Invoke(_stack); }
}
