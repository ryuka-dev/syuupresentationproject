using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 選択されたアイテムの基礎情報と操作ボタンを表示する詳細パネル。
/// ShowInventoryItem: 背包内物品用（Equip ボタン表示）
/// ShowEquippedItem:  装備槽用（Unequip ボタン表示）
/// 純 Tooltip：ボタンなし、Raycast ブロックなし
/// </summary>
public class ItemDetailPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemIdText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private TMP_Text itemCountText;
    [SerializeField] private TMP_Text equipSlotText;
    [SerializeField] private TMP_Text atkBonusText;
    [SerializeField] private TMP_Text hpBonusText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button   equipButton;
    [SerializeField] private Button   unequipButton;

    [Header("Height Auto-Fit")]
    [SerializeField] private float minWindowHeight       = 220f;
    [SerializeField] private float maxWindowHeight       = 560f;
    [SerializeField] private float buttonAreaReservedH   = 0f;    // buttons hidden in tooltip mode
    [SerializeField] private float contentBottomPadding  = 8f;

    // ── Height Auto-Fit ─────────────────────────────────────────────
    private void RefreshHeight()
    {
        if (rootPanel == null || descriptionText == null) return;
        var windowRT = rootPanel.GetComponent<UnityEngine.RectTransform>();
        if (windowRT == null) return;

        // TMP に最新テキストで優先高さを計算させる
        descriptionText.ForceMeshUpdate();

        var descRT = descriptionText.GetComponent<UnityEngine.RectTransform>();
        // descRT.anchoredPosition.y は負値（上端からの距離）
        float descTop    = Mathf.Abs(descRT.anchoredPosition.y);
        float descH      = descriptionText.preferredHeight;
        float targetH    = descTop + descH + buttonAreaReservedH + contentBottomPadding;
        targetH = Mathf.Clamp(targetH, minWindowHeight, maxWindowHeight);

        // 幅は変えない
        windowRT.sizeDelta = new Vector2(windowRT.sizeDelta.x, targetH);
    }

    private void Awake() { Hide(); }

    // 背包物品を表示（純 Tooltip - ボタンなし）
    public void ShowInventoryItem(ItemStack stack)
    {
        if (stack == null || stack.ItemData == null) { Hide(); return; }
        var item = stack.ItemData;
        bool isEquipment = item.ItemType == ItemType.Equipment;
        bool isTea       = item.ItemType == ItemType.Tea;

        SetIcon(item.Icon);

        if (isTea)
        {
            // Tea 向け表示
            string buffName   = "";
            string effectDesc = "";
            string duration   = "";

            if (item.TeaBuffData != null)
            {
                buffName = item.TeaBuffData.DisplayName;
                duration = $"{item.TeaBuffData.DurationSeconds:F0}秒";
                switch (item.TeaBuffData.EffectType)
                {
                    case TeaBuffEffectType.NonGuaranteedDropChanceMultiplier:
                        effectDesc = $"非必定掉落概率 x{item.TeaBuffData.Value:F2}";
                        break;
                    case TeaBuffEffectType.MaterialExtraQuantityChance:
                        effectDesc = $"Material 掉落时 {item.TeaBuffData.Value * 100f:F0}% 概率额外 +1";
                        break;
                    default:
                        effectDesc = item.TeaBuffData.EffectType.ToString();
                        break;
                }
            }

            string teaDesc = item.Description ?? "";
            if (!string.IsNullOrEmpty(buffName))   teaDesc += $"\n[{buffName}]";
            if (!string.IsNullOrEmpty(effectDesc)) teaDesc += $"\n{effectDesc}";
            if (!string.IsNullOrEmpty(duration))   teaDesc += $"\n持续时间：{duration}";

            FillTexts(
                item.ItemName, item.ItemId, "Tea", stack.Count.ToString(),
                "", "", "", teaDesc
            );
        }
        else
        {
            FillTexts(
                item.ItemName, item.ItemId, item.ItemType.ToString(), stack.Count.ToString(),
                isEquipment ? item.EquipmentSlotType.ToString() : "",
                isEquipment ? $"+{item.AttackPowerBonus}" : "",
                isEquipment ? $"+{item.MaxHealthBonus}" : "",
                item.Description
            );
        }

        SetButton(equipButton,   false, null);
        SetButton(unequipButton, false, null);
        if (rootPanel) { rootPanel.SetActive(true); rootPanel.transform.SetAsLastSibling(); }
        RefreshHeight();
    }

    // 装備中物品を表示（純 Tooltip - ボタンなし）
    public void ShowEquippedItem(ItemData item)
    {
        if (item == null) { Hide(); return; }
        SetIcon(item.Icon);
        FillTexts(
            item.ItemName, item.ItemId, item.ItemType.ToString(), "1",
            item.EquipmentSlotType.ToString(),
            $"+{item.AttackPowerBonus}", $"+{item.MaxHealthBonus}",
            item.Description
        );
        SetButton(equipButton,   false, null);
        SetButton(unequipButton, false, null);
        if (rootPanel) { rootPanel.SetActive(true); rootPanel.transform.SetAsLastSibling(); }
        RefreshHeight();
    }

    public void Hide()
    {
        if (rootPanel) rootPanel.SetActive(false);
    }

    // 後方互換
    public void Show(ItemStack stack) { ShowInventoryItem(stack); }
    public void Show(ItemData  item)  { ShowEquippedItem(item); }

    private void SetIcon(Sprite sprite)
    {
        if (iconImage == null) return;
        if (sprite != null) { iconImage.sprite = sprite; iconImage.enabled = true; }
        else                  iconImage.enabled = false;
    }

    private void FillTexts(string name, string id, string type, string count,
                            string slot, string atk, string hp, string desc)
    {
        if (itemNameText)    itemNameText.text    = name;
        if (itemIdText)      itemIdText.text      = $"ID: {id}";
        if (itemTypeText)    itemTypeText.text    = $"Type: {type}";
        if (itemCountText)   itemCountText.text   = $"Count: {count}";
        if (equipSlotText)   equipSlotText.text   = string.IsNullOrEmpty(slot) ? "" : $"Slot: {slot}";
        if (atkBonusText)    atkBonusText.text    = string.IsNullOrEmpty(atk)  ? "" : $"ATK: {atk}";
        if (hpBonusText)     hpBonusText.text     = string.IsNullOrEmpty(hp)   ? "" : $"HP: {hp}";
        if (descriptionText) descriptionText.text = desc ?? "";
    }

    private void SetButton(Button btn, bool visible, Action onClick)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(visible);
        btn.onClick.RemoveAllListeners();
        if (visible && onClick != null) btn.onClick.AddListener(() => onClick());
    }
}
