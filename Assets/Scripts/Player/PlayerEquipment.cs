using UnityEngine;

/// <summary>
/// プレイヤーの装備スロットを管理する最小コンポーネント。
/// 現在は Core スロット 1 枠のみ実装。
/// 装備が変化したとき OnEquipmentChanged を発火する。
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private ItemData equippedCore;

    /// <summary>装備内容が変化したときに発火するイベント。</summary>
    public event System.Action OnEquipmentChanged;

    /// <summary>現在 Core スロットに装備されている ItemData。未装備なら null。</summary>
    public ItemData EquippedCore => equippedCore;

    /// <summary>Core スロットに何か装備されているか。</summary>
    public bool HasCoreEquipped => equippedCore != null;

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

    /// <summary>
    /// EquipCore の後方互換ラッパー。既存の呼び出し元を壊さない。
    /// </summary>
    public bool EquipCore(ItemData item)
    {
        return EquipCore(item, out _);
    }

    // ─── UnequipCore / ClearEquipment ────────────────────────────

    /// <summary>
    /// Core スロットの装備を外す。成功時は外した ItemData を返す。
    /// </summary>
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

    /// <summary>
    /// 全装備スロットを強制クリアする。装備中だった場合のみイベントを発火する。
    /// </summary>
    public void ClearEquipment()
    {
        bool wasEquipped = equippedCore != null;
        equippedCore = null;
        Debug.Log("[PlayerEquipment] 全装備スロットをクリアしました。");
        if (wasEquipped) OnEquipmentChanged?.Invoke();
    }
}
