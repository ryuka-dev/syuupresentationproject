using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 茶商店の全商品カタログ。ScriptableObject。
/// UI 側がカテゴリでフィルタして使用する。
/// </summary>
[CreateAssetMenu(fileName = "TeaShopCatalogData", menuName = "RPG/TeaShop/TeaShopCatalogData")]
public class TeaShopCatalogData : ScriptableObject
{
    [SerializeField] private TeaShopItemData[] items;

    /// <summary>指定カテゴリの unlocked 商品を sortOrder 昇順で返す。</summary>
    public List<TeaShopItemData> GetItemsByCategory(TeaShopCategory category)
    {
        var result = new List<TeaShopItemData>();
        if (items == null) return result;

        foreach (var item in items)
        {
            if (item == null) continue;
            if (!item.Unlocked) continue;
            if (item.TeaItem == null) continue;
            if (item.TeaItem.ItemType != ItemType.Tea)
            {
                Debug.LogWarning($"[TeaShopCatalogData] '{item.name}' teaItem is not Tea type, skipping.");
                continue;
            }
            if (item.Category == category)
                result.Add(item);
        }

        result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return result;
    }
}
