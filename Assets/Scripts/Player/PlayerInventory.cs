using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーが拾得したアイテムを保持する最小インベントリ。
/// UI・装備・スタック機能はなし。データ記録のみ。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private readonly List<ItemData> _items = new List<ItemData>();

    /// <summary>現在の所持アイテム数。</summary>
    public int ItemCount => _items.Count;

    /// <summary>所持アイテムの読み取り専用リスト。</summary>
    public IReadOnlyList<ItemData> Items => _items;

    /// <summary>
    /// アイテムをインベントリに追加する。
    /// null の場合は warning を出して何もしない。
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
            Debug.LogWarning($"[PlayerInventory] ItemData has empty itemId: {item.ItemName}");
        }

        _items.Add(item);
        Debug.Log($"获得：{item.ItemName}（ID: {item.ItemId}），当前持有总数：{_items.Count}");
        PrintInventorySummary();
    }


private void PrintInventorySummary()
    {
        // ItemId をキーに数量を一時集計（List のまま走査）
        var counts = new System.Collections.Generic.Dictionary<string, (string displayName, int count)>();

        foreach (var it in _items)
        {
            if (it == null)
            {
                Debug.LogWarning("[PlayerInventory] Null entry found in inventory list.");
                continue;
            }

            string key;
            if (string.IsNullOrEmpty(it.ItemId))
            {
                Debug.LogWarning($"[PlayerInventory] Empty itemId in summary, falling back to ItemName: {it.ItemName}");
                key = "__name__" + it.ItemName;
            }
            else
            {
                key = it.ItemId;
            }

            if (counts.TryGetValue(key, out var entry))
            {
                counts[key] = (entry.displayName, entry.count + 1);
            }
            else
            {
                counts[key] = (it.ItemName, 1);
            }
        }

        var sb = new System.Text.StringBuilder("（当前库存）");
        foreach (var kv in counts)
        {
            string id = kv.Key.StartsWith("__name__") ? "(no id)" : kv.Key;
            sb.Append($" {kv.Value.displayName}（ID: {id}）数量：{kv.Value.count}");
        }
        Debug.Log(sb.ToString());
    }
}
