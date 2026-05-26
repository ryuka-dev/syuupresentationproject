using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// プレイヤーが拾得したアイテムをスタック形式で保持する固定スロットインベントリ。
/// _items は常に maxSlots 個の要素を持ち、空スロットは null で表す。
/// Equipment は必ず独立した ItemStack として追加する（マージ不可）。
/// 非 Equipment は同一 itemId かつ未満スタックにマージする。
/// UI・装備・保存機能はなし。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private const int DefaultMinimumSlots = 54;
    [SerializeField] private int maxSlots = DefaultMinimumSlots;

    private readonly List<ItemStack> _items = new List<ItemStack>();

    /// <summary>インベントリの内容が変化したときに発火するイベント。</summary>
    public event Action OnInventoryChanged;

    /// <summary>全スタックの count 合計（総所持アイテム数）。null スロットはスキップ。</summary>
    public int ItemCount
    {
        get
        {
            int total = 0;
            foreach (var stack in _items)
            {
                if (stack == null) continue;
                total += stack.Count;
            }
            return total;
        }
    }

    /// <summary>非 null スタック総数。</summary>
    public int StackCount
    {
        get
        {
            int count = 0;
            foreach (var stack in _items)
                if (stack != null) count++;
            return count;
        }
    }

    /// <summary>固定スロット数（null を含む全要素数）。</summary>
    public int MaxSlots => maxSlots;

    /// <summary>所持スタックの読み取り専用リスト（null = 空スロット）。</summary>
    public IReadOnlyList<ItemStack> Items => _items;

    // ─── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        // Inspector に古い値 (30 など) がシリアライズされていても最低容量を保証
        if (maxSlots < DefaultMinimumSlots)
            maxSlots = DefaultMinimumSlots;
        EnsureSlotCapacity();
    }

    // ─── Slot Helpers ──────────────────────────────────────────────

    /// <summary>_items が maxSlots 個になるよう null 埋めで拡張する（内部用）。</summary>
    private void EnsureSlotCapacity()
    {
        while (_items.Count < maxSlots)
            _items.Add(null);
    }

    /// <summary>
    /// UI などが必要なスロット数を通知するための外部向け拡張。
    /// requiredSlotCount が現在の maxSlots より大きければ maxSlots を引き上げ
    /// _items を null 埋めで拡張する。縮小は行わない。既存物品は保持する。
    /// </summary>
    public void EnsureSlotCapacity(int requiredSlotCount)
    {
        if (requiredSlotCount <= 0) return;
        if (maxSlots < requiredSlotCount)
        {
            Debug.Log($"[PlayerInventory] EnsureSlotCapacity: maxSlots {maxSlots} → {requiredSlotCount}");
            maxSlots = requiredSlotCount;
        }
        EnsureSlotCapacity();
    }

    /// <summary>指定インデックスのスタックを返す。越界または空なら null。</summary>
    public ItemStack GetStackAt(int index)
    {
        if (index < 0 || index >= _items.Count) return null;
        return _items[index];
    }

    /// <summary>指定インデックスに物品があれば true。</summary>
    public bool HasStackAt(int index)
    {
        return index >= 0 && index < _items.Count && _items[index] != null;
    }

    // ─── Add ──────────────────────────────────────────────────────

    /// <summary>
    /// アイテムをインベントリに追加する。
    /// Equipment は常に空スロットに新規追加。
    /// 非 Equipment は同一 itemId の未満スタックにマージ → 空スロットに追加。
    /// 満杯の場合は false を返す。
    /// </summary>
    public bool AddItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] AddItem called with null item.");
            return false;
        }

        EnsureSlotCapacity();

        if (item.ItemType == ItemType.Equipment)
        {
            int emptyIdx = FindFirstEmptySlot();
            if (emptyIdx < 0)
            {
                Debug.LogWarning($"[PlayerInventory] AddItem: no empty slot for Equipment {item.ItemName}.");
                return false;
            }
            _items[emptyIdx] = new ItemStack(item, 1);
        }
        else
        {
            ItemStack existing = FindNonFullStack(item);
            if (existing != null)
            {
                existing.AddCount(1);
            }
            else
            {
                int emptyIdx = FindFirstEmptySlot();
                if (emptyIdx < 0)
                {
                    Debug.LogWarning($"[PlayerInventory] AddItem: no empty slot for {item.ItemName}.");
                    return false;
                }
                _items[emptyIdx] = new ItemStack(item, 1);
            }
        }

        Debug.Log($"获得：{item.ItemName}（ID: {item.ItemId}），当前持有总数：{ItemCount}，当前 stack 数：{StackCount}");
        PrintInventorySummary();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ─── Remove ──────────────────────────────────────────────────

    /// <summary>
    /// itemId が一致する最初のスタックから 1 個を減らす。
    /// Count が 1 の場合はスロットを null に。RemoveAt は使わない（slot index 保持）。
    /// 見つからない場合は false を返す。
    /// </summary>
    public bool RemoveItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] RemoveItem called with null item.");
            return false;
        }

        int idx = FindFirstStackIndex(item);
        if (idx < 0)
        {
            Debug.LogWarning($"[PlayerInventory] RemoveItem: {item.ItemName}（ID: {item.ItemId}）not found in inventory.");
            return false;
        }

        var target = _items[idx];
        if (target.Count > 1)
            target.RemoveCount(1);
        else
            _items[idx] = null;   // スロットを空に（圧縮しない）

        Debug.Log($"移除：{item.ItemName}（ID: {item.ItemId}），当前持有总数：{ItemCount}，当前 stack 数：{StackCount}");
        PrintInventorySummary();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ─── Swap / Move ─────────────────────────────────────────────

    /// <summary>
    /// インデックス A とインデックス B のスタックを交換する。
    /// 片方または両方が null でも交換可能（実質的な移動）。
    /// UI 層の背包内移動に使用する。
    /// </summary>
    public bool SwapStacks(int indexA, int indexB)
    {
        EnsureSlotCapacity();
        if (indexA < 0 || indexA >= _items.Count || indexB < 0 || indexB >= _items.Count)
        {
            Debug.LogWarning($"[PlayerInventory] SwapStacks: invalid indices ({indexA}, {indexB}). Count={_items.Count}");
            return false;
        }
        if (indexA == indexB) return false;
        if (_items[indexA] == null && _items[indexB] == null) return false;

        var tmp         = _items[indexA];
        _items[indexA]  = _items[indexB];
        _items[indexB]  = tmp;
        Debug.Log($"[PlayerInventory] SwapStacks: swapped index {indexA} <-> {indexB}");
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// from スロットの物品を to スロットへ移動する。
    /// to に物品がある場合は false（交換には SwapStacks を使う）。
    /// </summary>
    public bool MoveStack(int fromIndex, int toIndex)
    {
        EnsureSlotCapacity();
        if (fromIndex < 0 || fromIndex >= _items.Count || toIndex < 0 || toIndex >= _items.Count)
        {
            Debug.LogWarning($"[PlayerInventory] MoveStack: invalid indices ({fromIndex}, {toIndex}).");
            return false;
        }
        if (fromIndex == toIndex) return false;
        if (_items[fromIndex] == null) return false;
        if (_items[toIndex] != null)   return false;

        _items[toIndex]   = _items[fromIndex];
        _items[fromIndex] = null;
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ─── Query ───────────────────────────────────────────────────

    /// <summary>
    /// 指定した EquipmentSlotType を持つ最初の Equipment を返す。
    /// 見つからない場合は null。
    /// </summary>
    public ItemData FindFirstEquipmentBySlot(EquipmentSlotType slotType)
    {
        foreach (var stack in _items)
        {
            if (stack == null) continue;
            if (stack.ItemData == null) continue;
            if (stack.Count <= 0) continue;
            if (stack.ItemData.ItemType != ItemType.Equipment) continue;
            if (stack.ItemData.EquipmentSlotType != slotType) continue;
            return stack.ItemData;
        }
        return null;
    }

    // ─── Private ─────────────────────────────────────────────────

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i] == null) return i;
        return -1;
    }

    private ItemStack FindNonFullStack(ItemData item)
    {
        bool hasId = !string.IsNullOrEmpty(item.ItemId);
        foreach (var stack in _items)
        {
            if (stack == null) continue;
            if (stack.ItemData == null) continue;
            if (stack.IsFull) continue;
            if (hasId ? stack.ItemId == item.ItemId : stack.ItemName == item.ItemName)
                return stack;
        }
        return null;
    }

    private int FindFirstStackIndex(ItemData item)
    {
        bool hasId = !string.IsNullOrEmpty(item.ItemId);
        for (int i = 0; i < _items.Count; i++)
        {
            var stack = _items[i];
            if (stack == null) continue;
            if (stack.ItemData == null) continue;
            if (hasId ? stack.ItemId == item.ItemId : stack.ItemName == item.ItemName)
                return i;
        }
        return -1;
    }

    private void PrintInventorySummary()
    {
        var sb = new StringBuilder("[PlayerInventory] 当前库存：\n");
        for (int i = 0; i < _items.Count; i++)
        {
            var stack = _items[i];
            if (stack == null) continue;
            sb.AppendLine($"  [{i}] {stack.ItemName} x {stack.Count}");
        }
        Debug.Log(sb.ToString());
    }
}
