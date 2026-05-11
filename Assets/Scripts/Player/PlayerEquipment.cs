using UnityEngine;

/// <summary>
/// プレイヤーの装備スロットを管理する最小コンポーネント。
/// 現在は Core スロット 1 枠のみ実装。
/// 装備効果・UI・インベントリ連携はなし。
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private ItemData equippedCore;

    /// <summary>現在 Core スロットに装備されている ItemData。未装備なら null。</summary>
    public ItemData EquippedCore => equippedCore;

    /// <summary>Core スロットに何か装備されているか。</summary>
    public bool HasCoreEquipped => equippedCore != null;

    /// <summary>
    /// Core スロットにアイテムを装備する。
    /// 装備できた場合は true、できなかった場合は false を返す。
    /// </summary>
    public bool EquipCore(ItemData item)
    {
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

        equippedCore = item;
        Debug.Log($"[PlayerEquipment] Core スロットに装備：{item.ItemName}（ID: {item.ItemId}）");
        return true;
    }

    /// <summary>
    /// Core スロットの装備を外す。
    /// 外した ItemData を返す。未装備なら null を返す。
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
        return removed;
    }

    /// <summary>
    /// 全装備スロットを強制クリアする。
    /// </summary>
    public void ClearEquipment()
    {
        equippedCore = null;
        Debug.Log("[PlayerEquipment] 全装備スロットをクリアしました。");
    }
}
