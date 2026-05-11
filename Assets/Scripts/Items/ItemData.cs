using UnityEngine;

public enum ItemType { Material, Equipment, Consumable, Currency, Quest, Cosmetic }
public enum ItemRarity { Common, Rare, Epic, Legendary }
public enum EquipmentSlotType { None, Core, Weapon, Armor, Accessory }

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

    [SerializeField] private float attackPowerBonus = 0f;
    public float AttackPowerBonus => attackPowerBonus;

    [SerializeField] private float maxHealthBonus = 0f;
    public float MaxHealthBonus => maxHealthBonus;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxStack < 1) maxStack = 1;
        if (itemType == ItemType.Equipment) maxStack = 1;
        if (itemType != ItemType.Equipment) equipmentSlotType = EquipmentSlotType.None;
        if (itemType != ItemType.Equipment) attackPowerBonus = 0f;
        if (attackPowerBonus < 0f) attackPowerBonus = 0f;
        if (itemType != ItemType.Equipment) maxHealthBonus = 0f;
        if (maxHealthBonus < 0f) maxHealthBonus = 0f;
    }
#endif
}
