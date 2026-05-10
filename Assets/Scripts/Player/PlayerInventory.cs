using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// プレイヤーが拾得したアイテムをスタック形式で保持する最小インベントリ。
/// 同一 itemId のアイテムは ItemStack にまとめて count で管理する。
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

    /// <summary>異なるアイテム種類数（スタック数）。</summary>
    public int StackCount => _items.Count;

    /// <summary>所持スタックの読み取り専用リスト。</summary>
    public IReadOnlyList<ItemStack> Items => _items;

    /// <summary>
    /// アイテムをインベントリに追加する。
    /// 同一 itemId が既に存在する場合は count を +1 するだけ。
    /// </summary>
    public void AddItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] AddItem called with null item.");
            return;
        }

        if (string.IsNullOrEmpty(item.ItemId))
        {
            Debug.LogWarning($"[PlayerInventory] ItemData has empty itemId: {item.ItemName} — itemId を設定してください。");
        }

        ItemStack existing = FindStack(item);
        if (existing != null)
        {
            existing.AddCount(1);
        }
        else
        {
            _items.Add(new ItemStack(item, 1));
        }

        Debug.Log($"获得：{item.ItemName}（ID: {item.ItemId}），当前持有总数：{ItemCount}");
        PrintInventorySummary();
    }

    private ItemStack FindStack(ItemData item)
    {
        bool hasId = !string.IsNullOrEmpty(item.ItemId);
        foreach (var stack in _items)
        {
            if (stack.ItemData == null) continue;
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
        var sb = new StringBuilder("（当前库存）");
        foreach (var stack in _items)
        {
            string id = string.IsNullOrEmpty(stack.ItemId) ? "(no id)" : stack.ItemId;
            sb.Append($" {stack.ItemName}（ID: {id}）数量：{stack.Count}");
        }
        Debug.Log(sb.ToString());
    }
}
