using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 正式バックパック UI のメインコントローラ。
/// PlayerInventory / PlayerEquipment / PlayerCombatStats を参照して表示を管理する。
/// </summary>
public class InventoryCanvasUI : MonoBehaviour
{
    // ─── 選択モード ───────────────────────────────────────────────
    private enum SelectionMode { None, InventoryItem, EquippedItem }

    [Header("References")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerEquipment playerEquipment;
    [SerializeField] private PlayerCombatStats playerCombatStats;

    [Header("Root")]
    [SerializeField] private GameObject rootPanel;

    [Header("Item Grid")]
    [SerializeField] private Transform itemGridRoot;
    [SerializeField] private InventorySlotUI itemSlotPrefab;

    [Header("Equipment Slots")]
    [SerializeField] private EquipmentSlotUI coreSlotUI;
    [SerializeField] private EquipmentSlotUI armorSlotUI;
    [SerializeField] private EquipmentSlotUI accessorySlotUI;

    [Header("Detail Panel")]
    [SerializeField] private ItemDetailPanelUI detailPanel;

    [Header("Stat Summary")]
    [SerializeField] private TMP_Text statSummaryText;

    private readonly List<InventorySlotUI> _spawnedSlots = new List<InventorySlotUI>();
    private bool _isOpen;
    private bool _eventsSubscribed;

    // 選択状態
    private SelectionMode      _selectionMode  = SelectionMode.None;
    private ItemStack          _selectedStack;
    private EquipmentSlotType  _selectedSlotType;

    // ─── Open / Close ─────────────────────────────────────────────
    public bool IsOpen => _isOpen;

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        if (rootPanel) rootPanel.SetActive(true);
        ClearSelection();
        RefreshAll();
        Cursor.visible   = true;
        Cursor.lockState = UnityEngine.CursorLockMode.None;
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        ClearSelection();
        if (rootPanel) rootPanel.SetActive(false);
        if (detailPanel) detailPanel.Hide();
    }

    public void Toggle() { if (_isOpen) Close(); else Open(); }

    // ─── Lifecycle ────────────────────────────────────────────────
    private void Awake()
    {
        if (playerInventory   == null) playerInventory   = FindFirstObjectByType<PlayerInventory>();
        if (playerEquipment   == null) playerEquipment   = FindFirstObjectByType<PlayerEquipment>();
        if (playerCombatStats == null) playerCombatStats = FindFirstObjectByType<PlayerCombatStats>();
        if (rootPanel) rootPanel.SetActive(false);
    }

    private void Start() { SubscribeEvents(); }
    private void OnDestroy() { UnsubscribeEvents(); }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed) return;
        if (playerInventory != null) playerInventory.OnInventoryChanged += OnInventoryChangedHandler;
        if (playerEquipment != null)
        {
            playerEquipment.OnEquipmentChanged += OnEquipmentChangedHandler;
        }
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed) return;
        if (playerInventory != null) playerInventory.OnInventoryChanged -= OnInventoryChangedHandler;
        if (playerEquipment != null)
        {
            playerEquipment.OnEquipmentChanged -= OnEquipmentChangedHandler;
        }
        _eventsSubscribed = false;
    }

    // ─── Event handlers（背包が開いているときのみ刷新）──────────────
    private void OnInventoryChangedHandler() { if (_isOpen) RefreshInventory(); }
    private void OnEquipmentChangedHandler() { if (_isOpen) { RefreshEquipment(); RefreshStats(); } }

    // ─── Refresh ──────────────────────────────────────────────────
    public void RefreshAll()
    {
        RefreshInventory();
        RefreshEquipment();
        RefreshStats();
    }

    private void RefreshInventory()
    {
        if (!_isOpen) return;
        foreach (var slot in _spawnedSlots)
            if (slot) Destroy(slot.gameObject);
        _spawnedSlots.Clear();

        if (playerInventory == null || itemGridRoot == null || itemSlotPrefab == null) return;
        foreach (var stack in playerInventory.Items)
        {
            var slotGO = Instantiate(itemSlotPrefab, itemGridRoot);
            slotGO.Setup(stack, OnItemSlotClicked);
            _spawnedSlots.Add(slotGO);
        }
    }

    private void RefreshEquipment()
    {
        if (!_isOpen) return;
        if (playerEquipment == null) return;
        if (coreSlotUI)      coreSlotUI.Setup("Core",      playerEquipment.EquippedCore,      OnEquipmentSlotClicked);
        if (armorSlotUI)     armorSlotUI.Setup("Armor",    playerEquipment.EquippedArmor,     OnEquipmentSlotClicked);
        if (accessorySlotUI) accessorySlotUI.Setup("Accessory", playerEquipment.EquippedAccessory, OnEquipmentSlotClicked);
    }

    private void RefreshStats()
    {
        if (!_isOpen) return;
        if (statSummaryText == null || playerCombatStats == null) return;
        statSummaryText.text =
            $"Normal ATK: {playerCombatStats.CurrentNormalAttackDamage:F1}\n" +
            $"Max HP: {playerCombatStats.CurrentMaxHealth:F1}\n" +
            $"Equipment ATK Bonus: +{playerCombatStats.EquipmentAttackPowerBonus:F1}\n" +
            $"Equipment HP Bonus:  +{playerCombatStats.EquipmentMaxHealthBonus:F1}";
    }

    // ─── 選択状態管理 ────────────────────────────────────────────
    private void ClearSelection()
    {
        _selectionMode    = SelectionMode.None;
        _selectedStack    = null;
        _selectedSlotType = EquipmentSlotType.None;
    }

    // ─── Click Handlers ──────────────────────────────────────────
    private void OnItemSlotClicked(ItemStack stack)
    {
        _selectionMode    = SelectionMode.InventoryItem;
        _selectedStack    = stack;
        _selectedSlotType = EquipmentSlotType.None;
        if (detailPanel) detailPanel.ShowInventoryItem(stack, OnEquipButtonClicked);
    }

    private void OnEquipmentSlotClicked(ItemData item, EquipmentSlotType slotType)
    {
        _selectionMode    = SelectionMode.EquippedItem;
        _selectedStack    = null;
        _selectedSlotType = slotType;
        if (item == null)
        {
            if (detailPanel) detailPanel.Hide();
            return;
        }
        if (detailPanel) detailPanel.ShowEquippedItem(item, OnUnequipButtonClicked);
    }

    // ─── Equip ────────────────────────────────────────────────────
    private void OnEquipButtonClicked()
    {
        if (_selectionMode != SelectionMode.InventoryItem || _selectedStack == null) return;
        var item = _selectedStack.ItemData;
        if (item == null || item.ItemType != ItemType.Equipment) return;
        if (playerInventory == null || playerEquipment == null) return;

        bool success = false;
        ItemData replaced = null;

        // SkeletonDebugUI のフローに準拠
        switch (item.EquipmentSlotType)
        {
            case EquipmentSlotType.Core:
                success = playerEquipment.EquipCore(item, out replaced);
                break;
            case EquipmentSlotType.Armor:
                success = playerEquipment.EquipArmor(item, out replaced);
                break;
            case EquipmentSlotType.Accessory:
                success = playerEquipment.EquipAccessory(item, out replaced);
                break;
            default:
                Debug.LogWarning($"[InventoryCanvasUI] Unknown EquipmentSlotType: {item.EquipmentSlotType}");
                return;
        }

        if (!success) return;

        // 装備成功：背包から除去
        if (!playerInventory.RemoveItem(item))
            Debug.LogError("[InventoryCanvasUI] Equip succeeded but RemoveItem failed!");

        // 入れ替え装備があれば背包に戻す
        if (replaced != null)
            playerInventory.AddItem(replaced);

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
        // OnEquipmentChanged / OnInventoryChanged により自動刷新
    }

    // ─── Unequip ──────────────────────────────────────────────────
    private void OnUnequipButtonClicked()
    {
        if (_selectionMode != SelectionMode.EquippedItem) return;
        if (playerInventory == null || playerEquipment == null) return;

        ItemData unequipped = null;
        switch (_selectedSlotType)
        {
            case EquipmentSlotType.Core:
                unequipped = playerEquipment.UnequipCore();
                break;
            case EquipmentSlotType.Armor:
                unequipped = playerEquipment.UnequipArmor();
                break;
            case EquipmentSlotType.Accessory:
                unequipped = playerEquipment.UnequipAccessory();
                break;
            default:
                Debug.LogWarning($"[InventoryCanvasUI] Unknown slot to unequip: {_selectedSlotType}");
                return;
        }

        if (unequipped == null) return;

        if (!playerInventory.AddItem(unequipped))
            Debug.LogWarning($"[InventoryCanvasUI] Unequip succeeded but AddItem failed for {unequipped.ItemName}");

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
        // OnEquipmentChanged / OnInventoryChanged により自動刷新
    }
}
