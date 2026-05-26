using UnityEngine;

public enum TeaShopCategory
{
    GreenTea,
    BlackTea,
    HerbalTea,
    SpecialTea
}

/// <summary>
/// 茶商店の商品一件分のデータ。ScriptableObject。
/// teaItem は ItemType.Tea のみ有効。
/// </summary>
[CreateAssetMenu(fileName = "New TeaShopItemData", menuName = "RPG/TeaShop/TeaShopItemData")]
public class TeaShopItemData : ScriptableObject
{
    [SerializeField] private string shopItemId;
    public string ShopItemId => shopItemId;

    [SerializeField] private TeaShopCategory category;
    public TeaShopCategory Category => category;

    [SerializeField] private ItemData teaItem;
    public ItemData TeaItem => teaItem;

    [SerializeField] private int price = 100;
    public int Price => Mathf.Max(0, price);

    [SerializeField] private bool unlocked = true;
    public bool Unlocked => unlocked;

    [SerializeField] private int sortOrder = 0;
    public int SortOrder => sortOrder;

    [SerializeField, TextArea] private string shopDescriptionOverride;
    public string Description => !string.IsNullOrEmpty(shopDescriptionOverride)
        ? shopDescriptionOverride
        : (teaItem != null ? teaItem.Description : string.Empty);

    [SerializeField] private int giftCost = 50;
    public int GiftCost => Mathf.Max(0, giftCost);

    [SerializeField] private float giftCooldownSeconds = 300f;
    public float GiftCooldownSeconds => giftCooldownSeconds;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (price < 0)
        {
            Debug.LogWarning($"[TeaShopItemData] '{name}': price < 0, clamped to 0.");
            price = 0;
        }
        if (teaItem != null && teaItem.ItemType != ItemType.Tea)
            Debug.LogWarning($"[TeaShopItemData] '{name}': teaItem '{teaItem.name}' is not ItemType.Tea.");
    }
#endif
}
