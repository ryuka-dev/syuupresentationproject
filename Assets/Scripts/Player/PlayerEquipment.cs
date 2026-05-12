using UnityEngine;

/// <summary>
/// プレイヤーの装備スロットを管理するコンポーネント。
/// 対応スロット：Core / Armor / Accessory
/// 装備が変化したとき OnEquipmentChanged を発火する。
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private ItemData equippedCore;
    [SerializeField] private ItemData equippedArmor;
    [SerializeField] private ItemData equippedAccessory;

    /// <summary>装備内容が変化したときに発火するイベント。</summary>
    public event System.Action OnEquipmentChanged;

    // ─── Core プロパティ ─────────────────────────────────────────

    /// <summary>現在 Core スロットに装備されている ItemData。未装備なら null。</summary>
    public ItemData EquippedCore => equippedCore;

    /// <summary>Core スロットに何か装備されているか。</summary>
    public bool HasCoreEquipped => equippedCore != null;

    // ─── Armor プロパティ ────────────────────────────────────────

    /// <summary>現在 Armor スロットに装備されている ItemData。未装備なら null。</summary>
    public ItemData EquippedArmor => equippedArmor;

    /// <summary>Armor スロットに何か装備されているか。</summary>
    public bool HasArmorEquipped => equippedArmor != null;

    // ─── Accessory プロパティ ────────────────────────────────────

    /// <summary>現在 Accessory スロットに装備されている ItemData。未装備なら null。</summary>
    public ItemData EquippedAccessory => equippedAccessory;

    /// <summary>Accessory スロットに何か装備されているか。</summary>
    public bool HasAccessoryEquipped => equippedAccessory != null;

    // ─── EquipCore ───────────────────────────────────────────────

    /// <summary>
    /// Core スロットにアイテムを装備する。
    /// 装備できた場合は true を返し OnEquipmentChanged を発火する。
    /// replacedItem に、装備前に入っていた旧 Core を返す（なければ null）。
    /// </summary>
    public bool EquipCore(ItemData item, out ItemData replacedItem)
    {
        replacedItem = null;

        if (item == null)
        {
            Debug.LogWarning("[PlayerEquipment] EquipCore called with null item.");
            return false;
        }
        if (item.ItemType != ItemType.Equipment)
        {
            Debug.LogWarning($"[PlayerEquipment] {item.ItemName}（ID: {item.ItemId}）は Equipment タイプではないため装備できません。ItemType: {item.ItemType}");
            return false;
        }
        if (item.EquipmentSlotType != EquipmentSlotType.Core)
        {
            Debug.LogWarning($"[PlayerEquipment] {item.ItemName}（ID: {item.ItemId}）は Core スロット用ではありません。EquipmentSlotType: {item.EquipmentSlotType}");
            return false;
        }

        replacedItem = equippedCore;
        equippedCore = item;

        if (replacedItem != null)
            Debug.Log($"[PlayerEquipment] Core 装備を交換：{replacedItem.ItemName}→{item.ItemName}（ID: {item.ItemId}）");
        else
            Debug.Log($"[PlayerEquipment] Core スロットに装備：{item.ItemName}（ID: {item.ItemId}）");

        OnEquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>EquipCore の後方互換ラッパー。</summary>
    public bool EquipCore(ItemData item)
    {
        return EquipCore(item, out _);
    }

    // ─── EquipArmor ──────────────────────────────────────────────

    /// <summary>
    /// Armor スロットにアイテムを装備する。
    /// 装備できた場合は true を返し OnEquipmentChanged を発火する。
    /// replacedItem に、装備前に入っていた旧 Armor を返す（なければ null）。
    /// </summary>
    public bool EquipArmor(ItemData item, out ItemData replacedItem)
    {
        replacedItem = null;

        if (item == null)
        {
            Debug.LogWarning("[PlayerEquipment] EquipArmor called with null item.");
            return false;
        }
        if (item.ItemType != ItemType.Equipment)
        {
            Debug.LogWarning($"[PlayerEquipment] {item.ItemName}（ID: {item.ItemId}）は Equipment タイプではないため装備できません。ItemType: {item.ItemType}");
            return false;
        }
        if (item.EquipmentSlotType != EquipmentSlotType.Armor)
        {
            Debug.LogWarning($"[PlayerEquipment] {item.ItemName}（ID: {item.ItemId}）は Armor スロット用ではありません。EquipmentSlotType: {item.EquipmentSlotType}");
            return false;
        }

        replacedItem  = equippedArmor;
        equippedArmor = item;

        if (replacedItem != null)
            Debug.Log($"[PlayerEquipment] Armor 装備を交換：{replacedItem.ItemName}→{item.ItemName}（ID: {item.ItemId}）");
        else
            Debug.Log($"[PlayerEquipment] Armor スロットに装備：{item.ItemName}（ID: {item.ItemId}）");

        OnEquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>EquipArmor の後方互換ラッパー。</summary>
    public bool EquipArmor(ItemData item)
    {
        return EquipArmor(item, out _);
    }

    // ─── EquipAccessory ──────────────────────────────────────────

    /// <summary>
    /// Accessory スロットにアイテムを装備する。
    /// 装備できた場合は true を返し OnEquipmentChanged を発火する。
    /// replacedItem に、装備前に入っていた旧 Accessory を返す（なければ null）。
    /// </summary>
    public bool EquipAccessory(ItemData item, out ItemData replacedItem)
    {
        replacedItem = null;

        if (item == null)
        {
            Debug.LogWarning("[PlayerEquipment] EquipAccessory called with null item.");
            return false;
        }
        if (item.ItemType != ItemType.Equipment)
        {
            Debug.LogWarning($"[PlayerEquipment] {item.ItemName}（ID: {item.ItemId}）は Equipment タイプではないため装備できません。ItemType: {item.ItemType}");
            return false;
        }
        if (item.EquipmentSlotType != EquipmentSlotType.Accessory)
        {
            Debug.LogWarning($"[PlayerEquipment] {item.ItemName}（ID: {item.ItemId}）は Accessory スロット用ではありません。EquipmentSlotType: {item.EquipmentSlotType}");
            return false;
        }

        replacedItem       = equippedAccessory;
        equippedAccessory  = item;

        if (replacedItem != null)
            Debug.Log($"[PlayerEquipment] Accessory 装備を交換：{replacedItem.ItemName}→{item.ItemName}（ID: {item.ItemId}）");
        else
            Debug.Log($"[PlayerEquipment] Accessory スロットに装備：{item.ItemName}（ID: {item.ItemId}）");

        OnEquipmentChanged?.Invoke();
        return true;
    }

    /// <summary>EquipAccessory の後方互換ラッパー。</summary>
    public bool EquipAccessory(ItemData item)
    {
        return EquipAccessory(item, out _);
    }

    // ─── UnequipCore / UnequipArmor / UnequipAccessory ───────────

    /// <summary>Core スロットの装備を外す。成功時は外した ItemData を返す。</summary>
    public ItemData UnequipCore()
    {
        if (equippedCore == null)
        {
            Debug.LogWarning("[PlayerEquipment] UnequipCore called but Core slot is empty.");
            return null;
        }
        ItemData removed = equippedCore;
        equippedCore = null;
        Debug.Log($"[PlayerEquipment] Core スロットから取り外し：{removed.ItemName}（ID: {removed.ItemId}）");
        OnEquipmentChanged?.Invoke();
        return removed;
    }

    /// <summary>Armor スロットの装備を外す。成功時は外した ItemData を返す。</summary>
    public ItemData UnequipArmor()
    {
        if (equippedArmor == null)
        {
            Debug.LogWarning("[PlayerEquipment] UnequipArmor called but Armor slot is empty.");
            return null;
        }
        ItemData removed = equippedArmor;
        equippedArmor = null;
        Debug.Log($"[PlayerEquipment] Armor スロットから取り外し：{removed.ItemName}（ID: {removed.ItemId}）");
        OnEquipmentChanged?.Invoke();
        return removed;
    }

    /// <summary>Accessory スロットの装備を外す。成功時は外した ItemData を返す。</summary>
    public ItemData UnequipAccessory()
    {
        if (equippedAccessory == null)
        {
            Debug.LogWarning("[PlayerEquipment] UnequipAccessory called but Accessory slot is empty.");
            return null;
        }
        ItemData removed = equippedAccessory;
        equippedAccessory = null;
        Debug.Log($"[PlayerEquipment] Accessory スロットから取り外し：{removed.ItemName}（ID: {removed.ItemId}）");
        OnEquipmentChanged?.Invoke();
        return removed;
    }

    // ─── ClearEquipment ──────────────────────────────────────────

    /// <summary>
    /// 全装備スロット（Core / Armor / Accessory）を強制クリアする。
    /// 1 つ以上のスロットに装備があった場合のみ OnEquipmentChanged を 1 回だけ発火する。
    /// すべてのスロットが空だった場合はイベントを発火しない。
    /// </summary>
    public void ClearEquipment()
    {
        bool anyEquipped = equippedCore != null || equippedArmor != null || equippedAccessory != null;

        equippedCore      = null;
        equippedArmor     = null;
        equippedAccessory = null;

        Debug.Log("[PlayerEquipment] 全装備スロットをクリアしました。");
        if (anyEquipped) OnEquipmentChanged?.Invoke();
    }
}
