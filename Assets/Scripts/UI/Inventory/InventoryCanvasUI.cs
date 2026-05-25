
using TMPro;
using UnityEngine;

/// <summary>
/// 格子式背包 UI のメインコントローラ。30 格固定グリッド。
/// PlayerInventory / PlayerEquipment / PlayerCombatStats を唯一のデータ源として使用。
/// </summary>
public class InventoryCanvasUI : MonoBehaviour
{
    private enum SelectionMode { None, InventoryItem, EquippedItem }
    [SerializeField] private int visibleSlotCount = 48;

    [Header("References")]
    [SerializeField] private PlayerInventory   playerInventory;
    [SerializeField] private PlayerEquipment   playerEquipment;
    [SerializeField] private PlayerCombatStats playerCombatStats;

    [Header("Root")]
    [SerializeField] private GameObject rootPanel;

    [Header("Inventory Grid")]
    [SerializeField] private Transform           itemGridRoot;
    [SerializeField] private InventoryGridSlotUI gridSlotPrefab;

    [Header("Equipment Slots")]
    [SerializeField] private EquipmentSlotUI coreSlotUI;
    [SerializeField] private EquipmentSlotUI armorSlotUI;
    [SerializeField] private EquipmentSlotUI accessorySlotUI;

    [Header("Detail Window")]
    [SerializeField] private ItemDetailPanelUI detailPanel;

    [Header("Stat Summary")]
    [SerializeField] private TMP_Text statSummaryText;

    [Header("Window Roots (for bring-to-front)")]
    [SerializeField] private RectTransform inventoryWindowRect;
    [SerializeField] private RectTransform equipmentWindowRect;
    [SerializeField] private RectTransform detailWindowRect;

    [Header("Tooltip Settings")]
    [SerializeField] private float detailWindowGap    = 12f;
    [SerializeField] private float detailScreenPadding = 8f;

    // Canvas reference for sortingOrder control
    private Canvas _canvas;
    // Hover 状態：true = クリック選中中（DetailWindow を維持）
    private bool _selectionLocked;

    // ── Grid ────────────────────────────────────────────────────────
    private InventoryGridSlotUI[] _gridSlots;
    private InventoryGridSlotUI   _currentSelectedSlot;

    // ── State ───────────────────────────────────────────────────────
    private bool             _isOpen;
    private bool             _eventsSubscribed;
    private SelectionMode    _selectionMode = SelectionMode.None;
    private ItemStack        _selectedStack;
    private EquipmentSlotType _selectedSlotType;

    public bool IsOpen => _isOpen;

    // ── Open / Close ────────────────────────────────────────────────
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        if (rootPanel) rootPanel.SetActive(true);
        // InventoryCanvas を最前面へ：sortingOrder で確実に上書き + sibling order
        if (_canvas != null) _canvas.sortingOrder = 1000;
        transform.SetAsLastSibling();
        ClearSelection();
        RefreshAll();
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        ClearSelection();   // _selectionLocked = false もここで行われる
        if (rootPanel) rootPanel.SetActive(false);
        if (detailPanel) detailPanel.Hide();
    }

    public void Toggle() { if (_isOpen) Close(); else Open(); }

    // ── Lifecycle ───────────────────────────────────────────────────
    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (playerInventory   == null) playerInventory   = FindFirstObjectByType<PlayerInventory>();
        if (playerEquipment   == null) playerEquipment   = FindFirstObjectByType<PlayerEquipment>();
        if (playerCombatStats == null) playerCombatStats = FindFirstObjectByType<PlayerCombatStats>();
        if (rootPanel) rootPanel.SetActive(false);
        InitializeGrid();
    }

    private void Start()       { SubscribeEvents(); }
    private void OnDestroy()   { UnsubscribeEvents(); }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed) return;
        if (playerInventory != null) playerInventory.OnInventoryChanged  += OnInventoryChangedHandler;
        if (playerEquipment != null) playerEquipment.OnEquipmentChanged  += OnEquipmentChangedHandler;
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed) return;
        if (playerInventory != null) playerInventory.OnInventoryChanged  -= OnInventoryChangedHandler;
        if (playerEquipment != null) playerEquipment.OnEquipmentChanged  -= OnEquipmentChangedHandler;
        _eventsSubscribed = false;
    }

    private void OnInventoryChangedHandler() { if (_isOpen) RefreshInventory(); }
    private void OnEquipmentChangedHandler() { if (_isOpen) { RefreshEquipment(); RefreshStats(); } }

    // ── Grid Init ───────────────────────────────────────────────────
    private void InitializeGrid()
    {
        if (_gridSlots != null || itemGridRoot == null || gridSlotPrefab == null) return;
        _gridSlots = new InventoryGridSlotUI[visibleSlotCount];
        for (int i = 0; i < visibleSlotCount; i++)
        {
            var slot = Instantiate(gridSlotPrefab, itemGridRoot);
            slot.name    = "Slot_" + i.ToString("D2");
            slot.SetEmpty();
            _gridSlots[i] = slot;
        }
    }

    // ── Refresh ─────────────────────────────────────────────────────
    public void RefreshAll()
    {
        RefreshInventory();
        RefreshEquipment();
        RefreshStats();
    }

private void RefreshInventory()
    {
        if (!_isOpen || _gridSlots == null) return;
        var items     = playerInventory?.Items;
        int itemCount = items != null ? items.Count : 0;
        if (itemCount > visibleSlotCount)
            Debug.LogWarning($"[InventoryCanvasUI] Item count ({itemCount}) exceeds {visibleSlotCount} visible slots.");

        for (int i = 0; i < visibleSlotCount; i++)
        {
            if (i < itemCount) _gridSlots[i].SetItem(items[i], OnItemSlotClicked, OnItemSlotHoverEnter, OnItemSlotHoverExit);
            else               _gridSlots[i].SetEmpty();
        }
    }

    private void RefreshEquipment()
    {
        if (!_isOpen || playerEquipment == null) return;
        if (coreSlotUI)      coreSlotUI.Setup("Core",      playerEquipment.EquippedCore,      OnEquipmentSlotClicked);
        if (armorSlotUI)     armorSlotUI.Setup("Armor",    playerEquipment.EquippedArmor,     OnEquipmentSlotClicked);
        if (accessorySlotUI) accessorySlotUI.Setup("Accessory", playerEquipment.EquippedAccessory, OnEquipmentSlotClicked);
    }

    private void RefreshStats()
    {
        if (!_isOpen || statSummaryText == null || playerCombatStats == null) return;
        statSummaryText.text =
            $"ATK: {playerCombatStats.CurrentNormalAttackDamage:F1}\n" +
            $"Max HP: {playerCombatStats.CurrentMaxHealth:F1}\n" +
            $"+ATK Eq: {playerCombatStats.EquipmentAttackPowerBonus:F1}\n" +
            $"+HP Eq: {playerCombatStats.EquipmentMaxHealthBonus:F1}";
    }

    // ── Selection ───────────────────────────────────────────────────
    private void ClearSelection()
    {
        if (_currentSelectedSlot != null) { _currentSelectedSlot.SetSelected(false); _currentSelectedSlot = null; }
        _selectionMode    = SelectionMode.None;
        _selectedStack    = null;
        _selectedSlotType = EquipmentSlotType.None;
        _selectionLocked  = false;
    }

    // ── Click Handlers ──────────────────────────────────────────────
    private void OnItemSlotClicked(ItemStack stack)
    {
        if (inventoryWindowRect != null) inventoryWindowRect.SetAsLastSibling();
        if (_currentSelectedSlot != null) _currentSelectedSlot.SetSelected(false);
        _currentSelectedSlot = null;
        if (_gridSlots != null)
            foreach (var s in _gridSlots)
                if (s != null && s.BoundStack == stack) { _currentSelectedSlot = s; s.SetSelected(true); break; }

        _selectionMode    = SelectionMode.InventoryItem;
        _selectedStack    = stack;
        _selectedSlotType = EquipmentSlotType.None;
        _selectionLocked  = true;  // クリック後はDetailWindowを維持
        if (detailPanel)
        {
            detailPanel.ShowInventoryItem(stack, OnEquipButtonClicked);
            if (_currentSelectedSlot != null)
                PositionDetailWindowNearSlot(_currentSelectedSlot.SlotRect);
        }
    }

    // ── Hover Handlers ──────────────────────────────────────────────
    private void OnItemSlotHoverEnter(InventoryGridSlotUI slot, ItemStack stack)
    {
        // Hover は常に更新（_selectionLocked に関係なく表示）
        if (detailPanel)
        {
            detailPanel.ShowInventoryItem(stack, OnEquipButtonClicked);
            PositionDetailWindowNearSlot(slot.SlotRect);
        }
    }

    private void OnItemSlotHoverExit(InventoryGridSlotUI slot)
    {
        // ロック中（クリック選中）は維持、それ以外は隠す
        if (!_selectionLocked && detailPanel) detailPanel.Hide();
    }

    // ── Tooltip Position ────────────────────────────────────────────
private void PositionDetailWindowNearSlot(RectTransform slotRT)
    {
        if (detailWindowRect == null || slotRT == null) return;

        var rootRT = rootPanel != null ? rootPanel.GetComponent<RectTransform>() : null;
        if (rootRT == null) return;

        var worldCorners = new Vector3[4];
        slotRT.GetWorldCorners(worldCorners);
        // [0]=bottomLeft [1]=topLeft [2]=topRight [3]=bottomRight

        float screenCenterX = (worldCorners[0].x + worldCorners[2].x) * 0.5f;
        bool  isLeftHalf    = screenCenterX < Screen.width * 0.5f;

        Vector2 localTopLeft, localTopRight;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootRT, new Vector2(worldCorners[1].x, worldCorners[1].y), null, out localTopLeft);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootRT, new Vector2(worldCorners[2].x, worldCorners[2].y), null, out localTopRight);

        float detailW = detailWindowRect.rect.width;
        float detailH = detailWindowRect.rect.height;
        float gap     = detailWindowGap;
        float pad     = detailScreenPadding;

        // Pivot=(0,1): anchoredPosition = window 左上角
        Vector2 pos = isLeftHalf
            ? new Vector2(localTopRight.x + gap, localTopRight.y)
            : new Vector2(localTopLeft.x  - gap - detailW, localTopLeft.y);

        // rootRT ローカル座標内で Clamp
        Rect rootBounds = rootRT.rect;
        pos.x = Mathf.Clamp(pos.x, rootBounds.xMin + pad, rootBounds.xMax - detailW - pad);
        pos.y = Mathf.Clamp(pos.y, rootBounds.yMin + detailH + pad, rootBounds.yMax - pad);

        // anchor=(0.5,0.5) にすることで anchoredPosition が rootRT ローカル座標と一致する
        detailWindowRect.pivot        = new Vector2(0f, 1f);
        detailWindowRect.anchorMin    = new Vector2(0.5f, 0.5f);
        detailWindowRect.anchorMax    = new Vector2(0.5f, 0.5f);
        detailWindowRect.anchoredPosition = pos;
        detailWindowRect.SetAsLastSibling();
    }

    private void OnEquipmentSlotClicked(ItemData item, EquipmentSlotType slotType)
    {
        if (equipmentWindowRect != null) equipmentWindowRect.SetAsLastSibling();
        if (_currentSelectedSlot != null) { _currentSelectedSlot.SetSelected(false); _currentSelectedSlot = null; }
        _selectionMode    = SelectionMode.EquippedItem;
        _selectedStack    = null;
        _selectedSlotType = slotType;
        if (item == null) { if (detailPanel) detailPanel.Hide(); return; }
        if (detailPanel) detailPanel.ShowEquippedItem(item, OnUnequipButtonClicked);
    }

    // ── Equip ───────────────────────────────────────────────────────
    private void OnEquipButtonClicked()
    {
        if (_selectionMode != SelectionMode.InventoryItem || _selectedStack == null) return;
        var item = _selectedStack.ItemData;
        if (item == null || item.ItemType != ItemType.Equipment) return;
        if (playerInventory == null || playerEquipment == null) return;

        bool success  = false;
        ItemData replaced = null;
        switch (item.EquipmentSlotType)
        {
            case EquipmentSlotType.Core:      success = playerEquipment.EquipCore(item, out replaced);      break;
            case EquipmentSlotType.Armor:     success = playerEquipment.EquipArmor(item, out replaced);     break;
            case EquipmentSlotType.Accessory: success = playerEquipment.EquipAccessory(item, out replaced); break;
            default: Debug.LogWarning("[InventoryCanvasUI] Unknown slot: " + item.EquipmentSlotType); return;
        }
        if (!success) return;

        if (!playerInventory.RemoveItem(item))
            Debug.LogError("[InventoryCanvasUI] Equip succeeded but RemoveItem failed!");
        if (replaced != null) playerInventory.AddItem(replaced);

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
    }

    // ── Unequip ─────────────────────────────────────────────────────
    private void OnUnequipButtonClicked()
    {
        if (_selectionMode != SelectionMode.EquippedItem) return;
        if (playerInventory == null || playerEquipment == null) return;

        ItemData unequipped = null;
        switch (_selectedSlotType)
        {
            case EquipmentSlotType.Core:      unequipped = playerEquipment.UnequipCore();      break;
            case EquipmentSlotType.Armor:     unequipped = playerEquipment.UnequipArmor();     break;
            case EquipmentSlotType.Accessory: unequipped = playerEquipment.UnequipAccessory(); break;
            default: Debug.LogWarning("[InventoryCanvasUI] Unknown slot: " + _selectedSlotType); return;
        }
        if (unequipped == null) return;

        if (!playerInventory.AddItem(unequipped))
            Debug.LogWarning("[InventoryCanvasUI] AddItem failed for " + unequipped.ItemName);

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
    }
}
