using System;
using UnityEngine;

/// <summary>
/// 同一 itemId のアイテムをスタックとして保持するデータクラス。
/// PlayerInventory の内部リストに使用する。
/// </summary>
[Serializable]
public class ItemStack
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int count;

    public ItemData ItemData => itemData;
    public int Count => count;
    public string ItemId => itemData != null ? itemData.ItemId : string.Empty;
    public string ItemName => itemData != null ? itemData.ItemName : string.Empty;

    public ItemStack(ItemData itemData, int count)
    {
        this.itemData = itemData;
        this.count = count < 1 ? 1 : count;
    }

    /// <summary>
    /// スタック数を加算する。amount が 0 以下の場合は何もしない。
    /// </summary>
    public void AddCount(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[ItemStack] AddCount called with invalid amount: {amount}");
            return;
        }
        count += amount;
    }
}
