using UnityEngine;

/// <summary>
/// アイテムの種別を表す列挙型。
/// </summary>
public enum ItemType
{
    Material,
    Equipment,
    Consumable,
    Currency,
    Quest,
    Cosmetic
}

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
/// 装備スロットの種別を表す列挙型。
/// Equipment 以外のアイテムは None のまま維持される。
/// </summary>
public enum EquipmentSlotType
{
    None,
    Core,
    Weapon,
    Armor,
    Accessory
}

/// <summary>
/// 掉落物（アイテム）の基本データを定義する ScriptableObject。
/// 将来的なドロップシステム・インベントリシステムの基盤となる。
/// </summary>
[CreateAssetMenu(fileName = "New ItemData", menuName = "RPG/Items/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemId;
    public string ItemId => itemId;

    [SerializeField] private string itemName;
    public string ItemName => itemName;

    [SerializeField] private ItemRarity rarity;
    public ItemRarity Rarity => rarity;

    [SerializeField, TextArea] private string description;
    public string Description => description;

    [SerializeField] private ItemType itemType;
    public ItemType ItemType => itemType;

    [SerializeField] private int maxStack = 99;
    public int MaxStack => maxStack;

    [SerializeField] private EquipmentSlotType equipmentSlotType = EquipmentSlotType.None;
    public EquipmentSlotType EquipmentSlotType => equipmentSlotType;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxStack < 1) maxStack = 1;
        if (itemType == ItemType.Equipment) maxStack = 1;
        if (itemType != ItemType.Equipment) equipmentSlotType = EquipmentSlotType.None;
    }
#endif
}
