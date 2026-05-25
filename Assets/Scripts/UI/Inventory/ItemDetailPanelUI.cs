using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 選択されたアイテムの基礎情報と操作ボタンを表示する詳細パネル。
/// ShowInventoryItem: 背包内物品用（Equip ボタン表示）
/// ShowEquippedItem:  装備槽用（Unequip ボタン表示）
/// </summary>
public class ItemDetailPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemIdText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private TMP_Text itemCountText;
    [SerializeField] private TMP_Text equipSlotText;
    [SerializeField] private TMP_Text atkBonusText;
    [SerializeField] private TMP_Text hpBonusText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;

    private void Awake() { Hide(); }

    // ─── 背包物品を表示（Equip ボタン付き）────────────────────────
    public void ShowInventoryItem(ItemStack stack, Action onEquip)
    {
        if (stack == null || stack.ItemData == null) { Hide(); return; }
        var item = stack.ItemData;
        bool isEquipment = item.ItemType == ItemType.Equipment;

        FillTexts(
            item.ItemName,
            item.ItemId,
            item.ItemType.ToString(),
            stack.Count.ToString(),
            isEquipment ? item.EquipmentSlotType.ToString() : string.Empty,
            isEquipment ? $"+{item.AttackPowerBonus}" : string.Empty,
            isEquipment ? $"+{item.MaxHealthBonus}" : string.Empty,
            item.Description
        );
        SetButton(equipButton,   isEquipment, onEquip);
        SetButton(unequipButton, false,       null);
        if (rootPanel) rootPanel.SetActive(true);
    }

    // ─── 装備中物品を表示（Unequip ボタン付き）──────────────────────
    public void ShowEquippedItem(ItemData item, Action onUnequip)
    {
        if (item == null) { Hide(); return; }

        FillTexts(
            item.ItemName,
            item.ItemId,
            item.ItemType.ToString(),
            "1",
            item.EquipmentSlotType.ToString(),
            $"+{item.AttackPowerBonus}",
            $"+{item.MaxHealthBonus}",
            item.Description
        );
        SetButton(equipButton,   false, null);
        SetButton(unequipButton, true,  onUnequip);
        if (rootPanel) rootPanel.SetActive(true);
    }

    public void Hide()
    {
        if (rootPanel) rootPanel.SetActive(false);
    }

    // ─── 後方互換（コールバックなし）───────────────────────────────
    public void Show(ItemStack stack) { ShowInventoryItem(stack, null); }
    public void Show(ItemData item)   { ShowEquippedItem(item, null); }

    // ─── Private ────────────────────────────────────────────────
    private void FillTexts(string name, string id, string type, string count,
                            string slot, string atk, string hp, string desc)
    {
        if (itemNameText)    itemNameText.text    = name;
        if (itemIdText)      itemIdText.text      = $"ID: {id}";
        if (itemTypeText)    itemTypeText.text    = $"Type: {type}";
        if (itemCountText)   itemCountText.text   = $"Count: {count}";
        if (equipSlotText)   equipSlotText.text   = string.IsNullOrEmpty(slot) ? string.Empty : $"Slot: {slot}";
        if (atkBonusText)    atkBonusText.text    = string.IsNullOrEmpty(atk)  ? string.Empty : $"ATK Bonus: {atk}";
        if (hpBonusText)     hpBonusText.text     = string.IsNullOrEmpty(hp)   ? string.Empty : $"HP Bonus: {hp}";
        if (descriptionText) descriptionText.text = desc ?? string.Empty;
    }

    private void SetButton(Button btn, bool visible, Action onClick)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(visible);
        btn.onClick.RemoveAllListeners();
        if (visible && onClick != null)
            btn.onClick.AddListener(() => onClick());
    }
}
