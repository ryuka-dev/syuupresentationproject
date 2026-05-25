using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バックパック内の 1 スタックを表示する UI スロット。
/// クリック時に InventoryCanvasUI へ選択通知を行う。
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemCountText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private Button   button;

    private ItemStack              _stack;
    private System.Action<ItemStack> _onClicked;

    public void Setup(ItemStack stack, System.Action<ItemStack> onClicked)
    {
        _stack     = stack;
        _onClicked = onClicked;

        if (stack == null || stack.ItemData == null)
        {
            SetIcon(null);
            if (itemNameText)  itemNameText.text  = "(empty)";
            if (itemCountText) itemCountText.text = "";
            if (itemTypeText)  itemTypeText.text  = "";
            return;
        }

        var item = stack.ItemData;
        SetIcon(item.Icon);
        if (itemNameText)  itemNameText.text  = item.ItemName;
        if (itemCountText) itemCountText.text = stack.Count > 1 ? $"x{stack.Count}" : "";

        string typeLabel = item.ItemType.ToString();
        if (item.ItemType == ItemType.Equipment && item.EquipmentSlotType != EquipmentSlotType.None)
            typeLabel += $" [{item.EquipmentSlotType}]";
        if (itemTypeText) itemTypeText.text = typeLabel;

        if (button) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(OnButtonClicked); }
    }

    private void SetIcon(Sprite sprite)
    {
        if (iconImage == null) return;
        if (sprite != null) { iconImage.sprite = sprite; iconImage.enabled = true; }
        else                  iconImage.enabled = false;
    }

    private void OnButtonClicked() { _onClicked?.Invoke(_stack); }
}
