using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// プレイヤーが拾得したアイテムをスタック形式で保持する最小インベントリ。
/// Equipment は必ず独立した ItemStack として追加する（マージ不可）。
/// 非 Equipment は同一 itemId かつ未満スタックにマージする。
/// UI・装備・保存機能はなし。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private readonly List<ItemStack> _items = new List<ItemStack>();

    /// <summary>全スタックの count 合計（総所持アイテム数）。</summary>
    public int ItemCount
    {
        get
        {
            int total = 0;
            foreach (var stack in _items) total += stack.Count;
            return total;
        }
    }

    /// <summary>現在のスタック総数。</summary>
    public int StackCount => _items.Count;

    /// <summary>所持スタックの読み取り専用リスト。</summary>
    public IReadOnlyList<ItemStack> Items => _items;

    /// <summary>
    /// アイテムをインベントリに追加する。
    /// Equipment は常に新規スタックとして追加。
    /// 非 Equipment は同一 itemId の未満スタックにマージ。
    /// </summary>
    public bool AddItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] AddItem called with null item.");
            return false;
        }

        if (item.ItemType == ItemType.Equipment)
        {
            // Equipment は絶対にマージしない。常に新規スタック。
            _items.Add(new ItemStack(item, 1));
        }
        else
        {
            // 同一 itemId で未満のスタックを探してマージ
            ItemStack existing = FindNonFullStack(item);
            if (existing != null)
            {
                existing.AddCount(1);
            }
            else
            {
                _items.Add(new ItemStack(item, 1));
            }
        }

        Debug.Log($"获得：{item.ItemName}（ID: {item.ItemId}），当前持有总数：{ItemCount}，当前 stack 数：{StackCount}");
        PrintInventorySummary();
        return true;
    }

    private ItemStack FindNonFullStack(ItemData item)
    {
        bool hasId = !string.IsNullOrEmpty(item.ItemId);
        foreach (var stack in _items)
        {
            if (stack.ItemData == null) continue;
            if (stack.IsFull) continue;
            if (hasId)
            {
                if (stack.ItemId == item.ItemId) return stack;
            }
            else
            {
                if (stack.ItemName == item.ItemName) return stack;
            }
        }
        return null;
    }

    private void PrintInventorySummary()
    {
        var sb = new StringBuilder("[PlayerInventory] 当前库存：\n");
        foreach (var stack in _items)
        {
            sb.AppendLine($"  - {stack.ItemName} x {stack.Count}");
        }
        Debug.Log(sb.ToString());
    }
}
