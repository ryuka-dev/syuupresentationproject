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

    /// <summary>このスタックが最大数に達しているか。</summary>
    public bool IsFull => itemData != null && count >= itemData.MaxStack;

    /// <summary>あと何個追加できるか。負数にはならない。</summary>
    public int RemainingCapacity
    {
        get
        {
            if (itemData == null) return 0;
            int remaining = itemData.MaxStack - count;
            return remaining < 0 ? 0 : remaining;
        }
    }

    public ItemStack(ItemData itemData, int count)
    {
        this.itemData = itemData;
        if (count < 1) count = 1;
        if (itemData != null && count > itemData.MaxStack) count = itemData.MaxStack;
        this.count = count;
    }

    /// <summary>
    /// スタック数を加算する。実際に加算した量を返す。
    /// amount が 0 以下の場合や既に満タンの場合は 0 を返す。
    /// </summary>
    public int AddCount(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"[ItemStack] AddCount called with invalid amount: {amount}");
            return 0;
        }
        if (IsFull)
        {
            return 0;
        }
        int actualAdd = Mathf.Min(amount, RemainingCapacity);
        count += actualAdd;
        return actualAdd;
    }
}
