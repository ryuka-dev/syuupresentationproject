using UnityEngine;

/// <summary>
/// アイテムの稀有度を表す列挙型。
/// </summary>
public enum ItemRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// 掉落物（アイテム）の基本データを定義する ScriptableObject。
/// 将来的なドロップシステム・インベントリシステムの基盤となる。
/// </summary>
[CreateAssetMenu(fileName = "New ItemData", menuName = "RPG/Items/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName;
    public string ItemName => itemName;

    [SerializeField] private ItemRarity rarity;
    public ItemRarity Rarity => rarity;

    [SerializeField, TextArea] private string description;
    public string Description => description;
}
