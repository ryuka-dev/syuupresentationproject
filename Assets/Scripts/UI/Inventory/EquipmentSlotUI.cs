using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Core / Armor / Accessory の装備スロットを表示する UI。
/// クリック時に InventoryCanvasUI へ (ItemData, EquipmentSlotType) を通知する。
/// </summary>
public class EquipmentSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text slotLabelText;
    [SerializeField] private TMP_Text equippedItemText;
    [SerializeField] private Button button;

    private ItemData          _equippedItem;
    private EquipmentSlotType _slotType;
    private Action<ItemData, EquipmentSlotType> _onClicked;

    public void Setup(string slotLabel, ItemData equippedItem,
                      Action<ItemData, EquipmentSlotType> onClicked)
    {
        _equippedItem = equippedItem;
        _onClicked    = onClicked;

        // スロットタイプをラベルから判定
        if (slotLabel == "Core")      _slotType = EquipmentSlotType.Core;
        else if (slotLabel == "Armor") _slotType = EquipmentSlotType.Armor;
        else                            _slotType = EquipmentSlotType.Accessory;

        if (slotLabelText)    slotLabelText.text    = slotLabel;
        if (equippedItemText) equippedItemText.text  = equippedItem != null ? equippedItem.ItemName : "未装備";

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        _onClicked?.Invoke(_equippedItem, _slotType);
    }
}
