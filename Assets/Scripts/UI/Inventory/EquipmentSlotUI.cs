using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Core / Armor / Accessory 装備スロット UI。
/// SlotLabel / EquippedItemText は不使用（削除済み）。Icon のみ表示。
/// </summary>
public class EquipmentSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image      iconImage;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button     button;

    private Image _bgImage;

    private static readonly Color ColNormal   = new Color(0.22f, 0.16f, 0.10f, 0.90f);
    private static readonly Color ColHover    = new Color(0.38f, 0.27f, 0.13f, 0.95f);
    private static readonly Color ColSelected = new Color(0.55f, 0.40f, 0.10f, 1.00f);

    private ItemData          _equippedItem;
    private EquipmentSlotType _slotType;
    private bool              _isSelected;

    private Action<ItemData, EquipmentSlotType>                          _onClicked;
    private Action<EquipmentSlotUI, ItemData, EquipmentSlotType>         _onHoverEnter;
    private Action<EquipmentSlotUI>                                      _onHoverExit;
    private Action<EquipmentSlotUI, ItemData, EquipmentSlotType, Vector2> _onRightClicked;

    public RectTransform SlotRect    => GetComponent<RectTransform>();
    public bool          HasEquipment => _equippedItem != null;
    public EquipmentSlotType SlotType => _slotType;

    private void Awake()
    {
        _bgImage = GetComponent<Image>();
        if (_bgImage) _bgImage.color = ColNormal;
    }

    public void Setup(
        EquipmentSlotType slotType,
        ItemData equippedItem,
        Action<ItemData, EquipmentSlotType> onClicked,
        Action<EquipmentSlotUI, ItemData, EquipmentSlotType> onHoverEnter = null,
        Action<EquipmentSlotUI> onHoverExit = null,
        Action<EquipmentSlotUI, ItemData, EquipmentSlotType, Vector2> onRightClicked = null)
    {
        _slotType       = slotType;
        _equippedItem   = equippedItem;
        _onClicked      = onClicked;
        _onHoverEnter   = onHoverEnter;
        _onHoverExit    = onHoverExit;
        _onRightClicked = onRightClicked;

        if (iconImage)
        {
            if (equippedItem != null && equippedItem.Icon != null)
            { iconImage.sprite = equippedItem.Icon; iconImage.enabled = true; }
            else { iconImage.sprite = null; iconImage.enabled = false; }
        }

        SetSelected(false);
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    // 後方互換 string overload
    public void Setup(string slotLabel, ItemData equippedItem,
                      Action<ItemData, EquipmentSlotType> onClicked)
    {
        EquipmentSlotType t = EquipmentSlotType.Core;
        if      (slotLabel == "Armor")     t = EquipmentSlotType.Armor;
        else if (slotLabel == "Accessory") t = EquipmentSlotType.Accessory;
        Setup(t, equippedItem, onClicked);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (selectedFrame) selectedFrame.SetActive(selected);
        if (_bgImage) _bgImage.color = selected ? ColSelected : ColNormal;
    }

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            _onRightClicked?.Invoke(this, _equippedItem, _slotType, eventData.position);
        // 左クリックは Button.onClick に任せる
    }

    private void OnButtonClicked() { _onClicked?.Invoke(_equippedItem, _slotType); }
}
