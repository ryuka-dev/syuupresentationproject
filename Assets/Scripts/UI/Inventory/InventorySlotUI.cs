using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バックパック内の 1 スタックを表示する UI スロット。
/// クリック時に InventoryCanvasUI へ選択通知を行う。
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemCountText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private Button button;

    private ItemStack _stack;
    private System.Action<ItemStack> _onClicked;

    public void Setup(ItemStack stack, System.Action<ItemStack> onClicked)
    {
        _stack = stack;
        _onClicked = onClicked;

        if (stack == null || stack.ItemData == null)
        {
            if (itemNameText) itemNameText.text = "（空）";
            if (itemCountText) itemCountText.text = "";
            if (itemTypeText)  itemTypeText.text  = "";
            return;
        }

        if (itemNameText) itemNameText.text = stack.ItemData.ItemName;
        if (itemCountText) itemCountText.text = stack.Count > 1 ? $"x{stack.Count}" : "";

        string typeLabel = stack.ItemData.ItemType.ToString();
        if (stack.ItemData.ItemType == ItemType.Equipment && stack.ItemData.EquipmentSlotType != EquipmentSlotType.None)
            typeLabel += $" [{stack.ItemData.EquipmentSlotType}]";
        if (itemTypeText) itemTypeText.text = typeLabel;

        if (button) button.onClick.RemoveAllListeners();
        if (button) button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        _onClicked?.Invoke(_stack);
    }
}
