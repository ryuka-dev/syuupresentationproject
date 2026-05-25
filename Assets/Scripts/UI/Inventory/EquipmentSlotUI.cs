using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Core / Armor / Accessory 装備スロット UI。
/// Hover Tooltip と SetSelected に対応。
/// </summary>
public class EquipmentSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image      iconImage;
    [SerializeField] private TMP_Text   slotLabelText;
    [SerializeField] private TMP_Text   equippedItemText;
    [SerializeField] private GameObject selectedFrame;   // optional: 選択ハイライト用 GameObject
    [SerializeField] private Button     button;

    // 背景 Image は同 GameObject の Image を自動取得
    private Image _bgImage;

    // 棕色テーマカラー
    private static readonly Color ColNormal   = new Color(0.22f, 0.16f, 0.10f, 0.90f);
    private static readonly Color ColHover    = new Color(0.38f, 0.27f, 0.13f, 0.95f);
    private static readonly Color ColSelected = new Color(0.55f, 0.40f, 0.10f, 1.00f);

    private ItemData          _equippedItem;
    private EquipmentSlotType _slotType;
    private bool              _isSelected;

    private Action<ItemData, EquipmentSlotType>                      _onClicked;
    private Action<EquipmentSlotUI, ItemData, EquipmentSlotType>     _onHoverEnter;
    private Action<EquipmentSlotUI>                                  _onHoverExit;

    public RectTransform SlotRect    => GetComponent<RectTransform>();
    public bool          HasEquipment => _equippedItem != null;
    public EquipmentSlotType SlotType => _slotType;

    private void Awake()
    {
        _bgImage = GetComponent<Image>();
        if (_bgImage) _bgImage.color = ColNormal;
    }

    // ── Setup ────────────────────────────────────────────────────
    public void Setup(
        EquipmentSlotType slotType,
        ItemData equippedItem,
        Action<ItemData, EquipmentSlotType> onClicked,
        Action<EquipmentSlotUI, ItemData, EquipmentSlotType> onHoverEnter = null,
        Action<EquipmentSlotUI> onHoverExit = null)
    {
        _slotType     = slotType;
        _equippedItem = equippedItem;
        _onClicked    = onClicked;
        _onHoverEnter = onHoverEnter;
        _onHoverExit  = onHoverExit;

        if (slotLabelText) slotLabelText.text = slotType.ToString();

        if (equippedItem != null)
        {
            if (iconImage)
            {
                iconImage.sprite  = equippedItem.Icon;
                iconImage.enabled = equippedItem.Icon != null;
            }
            if (equippedItemText) equippedItemText.text = equippedItem.ItemName;
        }
        else
        {
            if (iconImage) { iconImage.sprite = null; iconImage.enabled = false; }
            if (equippedItemText) equippedItemText.text = "--";
        }

        SetSelected(false);
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    // 後方互換 - 文字列 slotLabel 版（既存呼び出し互換）
    public void Setup(string slotLabel, ItemData equippedItem,
                      Action<ItemData, EquipmentSlotType> onClicked)
    {
        EquipmentSlotType type = EquipmentSlotType.Core;
        if      (slotLabel == "Armor")     type = EquipmentSlotType.Armor;
        else if (slotLabel == "Accessory") type = EquipmentSlotType.Accessory;
        Setup(type, equippedItem, onClicked);
    }

    // ── Selection ────────────────────────────────────────────────
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (selectedFrame) selectedFrame.SetActive(selected);
        if (_bgImage) _bgImage.color = selected ? ColSelected : ColNormal;
    }

    // ── Hover ────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!HasEquipment) return;
        if (_bgImage && !_isSelected) _bgImage.color = ColHover;
        _onHoverEnter?.Invoke(this, _equippedItem, _slotType);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_bgImage) _bgImage.color = _isSelected ? ColSelected : ColNormal;
        _onHoverExit?.Invoke(this);
    }

    private void OnButtonClicked() { _onClicked?.Invoke(_equippedItem, _slotType); }
}
