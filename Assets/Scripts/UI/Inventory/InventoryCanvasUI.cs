
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    [Header("Tea")]
    [SerializeField] private PlayerTeaBuffController playerTeaBuffController;

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
    [SerializeField] private ItemDetailPanelUI     detailPanel;
    [SerializeField] private InventoryContextMenuUI contextMenu;

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
    private EquipmentSlotUI        _currentSelectedEquipSlot;

    // ── State ───────────────────────────────────────────────────────
    private bool             _isOpen;
    private bool             _eventsSubscribed;
    private SelectionMode    _selectionMode = SelectionMode.None;
    private ItemStack        _selectedStack;
    private EquipmentSlotType _selectedSlotType;
    private int              _selectedSlotIndex = -1;   // 右クリック時の slot index

    // ── Move State (two-click swap) ──────────────────────────────────
    private InventoryGridSlotUI _pendingMoveSourceSlot;
    private int                 _pendingMoveSourceIndex = -1;

    // ── Drag Visual ──────────────────────────────────────────────────
    private GameObject _dragIconRoot;
    private Image      _dragIconImage;
    private TMP_Text   _dragIconCount;

    // ── Cancel Guards ────────────────────────────────────────────────
    // 右键取消後に同フレームの OnItemSlotRightClicked でメニューが開くのを防ぐ
    private bool _suppressNextRightClickMenu;
    // EventSystem Raycast 結果再利用（毎フレームの new List を避ける）
    private readonly System.Collections.Generic.List<RaycastResult> _raycastResults =
        new System.Collections.Generic.List<RaycastResult>();

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
        // UI 格子数をデータ層に同期（SerializeField 差異・容量不一致を防ぐ）
        playerInventory?.EnsureSlotCapacity(visibleSlotCount);
        RefreshAll();
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        ClearSelection();   // _selectionLocked = false もここで行われる
        contextMenu?.Hide();
        if (rootPanel) rootPanel.SetActive(false);
        if (detailPanel) detailPanel.Hide();
    }

    public void Toggle() { if (_isOpen) Close(); else Open(); }

    // ── Lifecycle ───────────────────────────────────────────────────
    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (playerInventory       == null) playerInventory       = FindFirstObjectByType<PlayerInventory>();
        if (playerEquipment       == null) playerEquipment       = FindFirstObjectByType<PlayerEquipment>();
        if (playerCombatStats     == null) playerCombatStats     = FindFirstObjectByType<PlayerCombatStats>();
        if (playerTeaBuffController == null) playerTeaBuffController = FindFirstObjectByType<PlayerTeaBuffController>();
        if (rootPanel) rootPanel.SetActive(false);
        InitializeGrid();
        CreateDragIcon();
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
        var items = playerInventory?.Items;

        for (int i = 0; i < visibleSlotCount; i++)
        {
            _gridSlots[i].SlotIndex = i;
            var stack = (items != null && i < items.Count) ? items[i] : null;
            if (stack != null)
                _gridSlots[i].SetItem(stack, OnItemSlotClicked, OnItemSlotHoverEnter, OnItemSlotHoverExit, OnItemSlotRightClicked);
            else
                _gridSlots[i].SetEmpty(OnEmptySlotClicked);
        }

        // pending move source が刷新後に無効になった場合はキャンセル
        if (_pendingMoveSourceIndex >= 0 && playerInventory != null)
        {
            var stackAtSource = playerInventory.GetStackAt(_pendingMoveSourceIndex);
            if (stackAtSource == null || stackAtSource != _selectedStack)
            {
                Debug.Log("[InventoryCanvasUI] Drag source slot changed after refresh; cancelling pending move.");
                ClearMoveState();
                ClearSelection();
            }
        }
    }

    private void RefreshEquipment()
    {
        if (!_isOpen || playerEquipment == null) return;
        if (coreSlotUI)      coreSlotUI.Setup(EquipmentSlotType.Core,      playerEquipment.EquippedCore,      OnEquipmentSlotClicked, OnEquipSlotHoverEnter, OnEquipSlotHoverExit, OnEquipSlotRightClicked);
        if (armorSlotUI)     armorSlotUI.Setup(EquipmentSlotType.Armor,     playerEquipment.EquippedArmor,     OnEquipmentSlotClicked, OnEquipSlotHoverEnter, OnEquipSlotHoverExit, OnEquipSlotRightClicked);
        if (accessorySlotUI) accessorySlotUI.Setup(EquipmentSlotType.Accessory, playerEquipment.EquippedAccessory, OnEquipmentSlotClicked, OnEquipSlotHoverEnter, OnEquipSlotHoverExit, OnEquipSlotRightClicked);
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
    private void ClearMoveState()
    {
        if (_pendingMoveSourceSlot != null) { _pendingMoveSourceSlot.SetSelected(false); _pendingMoveSourceSlot = null; }
        _pendingMoveSourceIndex = -1;
        HideDragIcon();
    }

    private InventoryGridSlotUI FindGridSlotByStack(ItemStack stack)
    {
        if (_gridSlots == null || stack == null) return null;
        foreach (var s in _gridSlots)
            if (s != null && s.BoundStack == stack) return s;
        return null;
    }

    private int FindInventoryIndex(ItemStack stack)
    {
        if (playerInventory == null || stack == null) return -1;
        var items = playerInventory.Items;
        for (int i = 0; i < items.Count; i++)
            if (items[i] == stack) return i;
        return -1;
    }

    private void ClearSelection()
    {
        ClearMoveState();
        if (_currentSelectedSlot != null) { _currentSelectedSlot.SetSelected(false); _currentSelectedSlot = null; }
        if (_currentSelectedEquipSlot != null) { _currentSelectedEquipSlot.SetSelected(false); _currentSelectedEquipSlot = null; }
        _selectionMode    = SelectionMode.None;
        _selectedStack    = null;
        _selectedSlotType = EquipmentSlotType.None;
        _selectedSlotIndex = -1;
        _selectionLocked  = false;
    }

    // ── Click Handlers ──────────────────────────────────────────────
    private void OnItemSlotClicked(ItemStack stack)
    {
        if (inventoryWindowRect != null) inventoryWindowRect.SetAsLastSibling();
        contextMenu?.Hide();

        // ── 移動待機中：2回目のクリック ──────────────────────────────
        if (_pendingMoveSourceIndex >= 0)
        {
            int targetIndex             = FindInventoryIndex(stack);
            InventoryGridSlotUI targetSlot = FindGridSlotByStack(stack);

            // 同じ格子をクリック → キャンセル
            if (targetSlot == _pendingMoveSourceSlot || targetIndex == _pendingMoveSourceIndex)
            {
                ClearMoveState();
                ClearSelection();
                return;
            }

            // 別の格子で物品あり → 交換
            if (targetIndex >= 0 && playerInventory != null)
                playerInventory.SwapStacks(_pendingMoveSourceIndex, targetIndex);
            else
                Debug.Log("[InventoryCanvasUI] Target slot is empty; compact list does not support empty slot move. Cancelling.");

            ClearMoveState();
            ClearSelection();
            return;
        }

        // ── 移動元選択：1回目のクリック ──────────────────────────────
        if (stack == null || stack.ItemData == null) return;

        int sourceIndex = FindInventoryIndex(stack);
        if (sourceIndex < 0)
        {
            Debug.LogWarning("[InventoryCanvasUI] OnItemSlotClicked: stack not found in inventory.");
            return;
        }

        if (_currentSelectedSlot != null) _currentSelectedSlot.SetSelected(false);
        _currentSelectedSlot = null;

        InventoryGridSlotUI sourceSlot  = FindGridSlotByStack(stack);
        _pendingMoveSourceSlot          = sourceSlot;
        _pendingMoveSourceIndex         = sourceIndex;
        if (sourceSlot != null) sourceSlot.SetSelected(true);
        _currentSelectedSlot = sourceSlot;

        _selectionMode    = SelectionMode.InventoryItem;
        _selectedStack    = stack;
        _selectedSlotType = EquipmentSlotType.None;
        ShowDragIcon(stack);   // 常驻抓取視覚表現
    }

    // ── Empty Slot Click ─────────────────────────────────────────────
    private void OnEmptySlotClicked(int slotIndex)
    {
        if (_pendingMoveSourceIndex < 0) return;   // 移動元がなければ何もしない
        if (playerInventory == null) return;

        // 移動元から空スロットへ移動（SwapStacks で null <-> stack を交換）
        playerInventory.SwapStacks(_pendingMoveSourceIndex, slotIndex);
        // OnInventoryChanged イベント経由で RefreshInventory が呼ばれる
        ClearMoveState();
        ClearSelection();
    }

    // ── Hover Handlers ──────────────────────────────────────────────
    private void OnItemSlotHoverEnter(InventoryGridSlotUI slot, ItemStack stack)
    {
        // Hover は常に更新（_selectionLocked に関係なく表示）
        if (detailPanel)
        {
            detailPanel.ShowInventoryItem(stack);
            PositionDetailWindowNearSlot(slot.SlotRect);
        }
    }

    private void OnItemSlotHoverExit(InventoryGridSlotUI slot)
    {
        // contextMenu は HoverExit で閉じない（右クリックメニュー表示後に PointerExit が発火して消えるのを防ぐ）
        if (detailPanel) detailPanel.Hide();
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
        // 装備スロット左クリック：選中ハイライトのみ
        if (_currentSelectedEquipSlot != null) _currentSelectedEquipSlot.SetSelected(false);
        _currentSelectedEquipSlot = GetEquipSlotUI(slotType);
        if (_currentSelectedEquipSlot != null) _currentSelectedEquipSlot.SetSelected(true);
        _selectionMode    = SelectionMode.EquippedItem;
        _selectedStack    = null;
        _selectedSlotType = slotType;
        contextMenu?.Hide();
        if (item == null && detailPanel) detailPanel.Hide();
    }

    // ── Equipment Hover ─────────────────────────────────────────────
    private void OnEquipSlotHoverEnter(EquipmentSlotUI slot, ItemData item, EquipmentSlotType slotType)
    {
        if (detailPanel)
        {
            detailPanel.ShowEquippedItem(item);
            PositionDetailWindowNearSlot(slot.SlotRect);
        }
    }

    private void OnEquipSlotHoverExit(EquipmentSlotUI slot)
    {
        // contextMenu は HoverExit で閉じない
        if (detailPanel) detailPanel.Hide();
    }

    private EquipmentSlotUI GetEquipSlotUI(EquipmentSlotType type)
    {
        if (type == EquipmentSlotType.Core)      return coreSlotUI;
        if (type == EquipmentSlotType.Armor)     return armorSlotUI;
        if (type == EquipmentSlotType.Accessory) return accessorySlotUI;
        return null;
    }

    // ── Right-Click Handlers ────────────────────────────────────────
    /// <summary>
    /// 右クリックで選択した slot が現在も有効（同じ ItemStack が存在）かどうかを確認する。
    /// 固定 slot 背包では ItemStack 参照の同一性で判断できる。
    /// </summary>
    private bool IsSelectedSlotStillValid()
    {
        if (_selectedSlotIndex < 0 || playerInventory == null) return false;
        var current = playerInventory.GetStackAt(_selectedSlotIndex);
        if (current == null || _selectedStack == null) return false;
        return current == _selectedStack;   // 参照同一性で確認
    }

    private void OnItemSlotRightClicked(InventoryGridSlotUI slot, ItemStack stack, Vector2 screenPos)
    {
        // Update() の右键取消と同フレームのメニュー開放を抑制
        if (_suppressNextRightClickMenu)
        {
            _suppressNextRightClickMenu = false;
            return;
        }

        // 移動待機中は右クリックでキャンセルのみ（メニューは開かない）
        if (_pendingMoveSourceIndex >= 0)
        {
            ClearMoveState();
            ClearSelection();
            return;
        }

        if (stack?.ItemData == null) return;
        _selectedStack     = stack;
        _selectedSlotIndex = slot.SlotIndex;   // slot index を保存（同名複数物品対応）
        _selectedSlotType  = EquipmentSlotType.None;
        _selectionMode     = SelectionMode.InventoryItem;
        var rootRT = rootPanel?.GetComponent<RectTransform>();

        // Tea 物品：Use ボタンを表示
        System.Action onUse = stack.ItemData.ItemType == ItemType.Tea ? (System.Action)OnUseButtonClicked : null;
        contextMenu?.ShowForInventoryItem(stack, rootRT, screenPos, OnEquipButtonClicked, onUse);
    }

    private void OnEquipSlotRightClicked(EquipmentSlotUI slot, ItemData item,
                                          EquipmentSlotType slotType, Vector2 screenPos)
    {
        if (item == null) return;
        _selectedSlotType = slotType;
        _selectionMode    = SelectionMode.EquippedItem;
        var rootRT = rootPanel?.GetComponent<RectTransform>();
        contextMenu?.ShowForEquippedItem(rootRT, screenPos, OnUnequipButtonClicked);
    }

    // ── Equip ───────────────────────────────────────────────────────
    private void OnEquipButtonClicked()
    {
        if (_selectionMode != SelectionMode.InventoryItem || _selectedStack == null) return;
        // slot が依然として有効（同一 ItemStack）かを確認してから操作
        if (!IsSelectedSlotStillValid())
        {
            Debug.LogWarning("[InventoryCanvasUI] Equip: selected slot is no longer valid. Aborting.");
            contextMenu?.Hide();
            ClearSelection();
            return;
        }
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

        // slot index を使って正確なスロットを消費（同名複数物品対応）
        if (!playerInventory.RemoveOneAt(_selectedSlotIndex))
            Debug.LogError("[InventoryCanvasUI] Equip succeeded but RemoveOneAt(" + _selectedSlotIndex + ") failed!");
        if (replaced != null) playerInventory.AddItem(replaced);

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
    }

    // ── Use Tea ─────────────────────────────────────────────────────
    private void OnUseButtonClicked()
    {
        if (_selectionMode != SelectionMode.InventoryItem || _selectedStack == null) return;
        // slot が依然として有効（同一 ItemStack）かを確認してから操作
        if (!IsSelectedSlotStillValid())
        {
            Debug.LogWarning("[InventoryCanvasUI] Use Tea: selected slot is no longer valid. Aborting.");
            contextMenu?.Hide();
            ClearSelection();
            return;
        }
        var item = _selectedStack.ItemData;
        if (item == null || item.ItemType != ItemType.Tea) return;
        if (playerInventory == null) return;

        if (playerTeaBuffController == null)
        {
            Debug.LogWarning("[InventoryCanvasUI] PlayerTeaBuffController not found. Cannot use tea.");
            return;
        }

        bool success = playerTeaBuffController.TryUseTea(item);
        if (!success) return;   // TryUseTea が Warning を出す

        // slot index を使って正確なスロットを消費（同名複数 Tea 対応）
        if (!playerInventory.RemoveOneAt(_selectedSlotIndex))
            Debug.LogError("[InventoryCanvasUI] Use tea succeeded but RemoveOneAt(" + _selectedSlotIndex + ") failed!");

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
    }

    // ── Unequip ─────────────────────────────────────────────────────
    private void OnUnequipButtonClicked()
    {
        if (_selectionMode != SelectionMode.EquippedItem) return;
        if (playerInventory == null || playerEquipment == null) return;

        // 背包に空きがない場合は卸装を禁止（装備が消えるのを防ぐ）
        if (!playerInventory.HasEmptySlot())
        {
            Debug.LogWarning("[InventoryCanvasUI] 背包已满，无法卸下装备。");
            return;
        }

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
        {
            // HasEmptySlot 後でも失敗した場合は装備栏に戻す（安全ロールバック）
            Debug.LogWarning("[InventoryCanvasUI] AddItem failed for " + unequipped.ItemName + "。装备栏に戻します。");
            switch (_selectedSlotType)
            {
                case EquipmentSlotType.Core:      playerEquipment.EquipCore(unequipped);      break;
                case EquipmentSlotType.Armor:     playerEquipment.EquipArmor(unequipped);     break;
                case EquipmentSlotType.Accessory: playerEquipment.EquipAccessory(unequipped); break;
            }
            return;
        }

        ClearSelection();
        if (detailPanel) detailPanel.Hide();
    }
    // ── Drag Visual ──────────────────────────────────────────────────

    /// <summary>
    /// ドラッグアイコンを runtime で生成する（Prefab / Scene 変更なし）。
    /// Image / TMP_Text は raycastTarget = false、CanvasGroup は blocksRaycasts = false
    /// に設定し、背包格子の UI 入力を妨げない。
    /// </summary>
    private void CreateDragIcon()
    {
        var canvasRT = transform as RectTransform;
        if (canvasRT == null) return;

        _dragIconRoot = new GameObject("DraggedItemIcon");
        _dragIconRoot.transform.SetParent(transform, false);

        var rt       = _dragIconRoot.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(56f, 56f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        // Raycast ブロックを無効化（背包格子への入力を妨げない）
        var cg              = _dragIconRoot.AddComponent<CanvasGroup>();
        cg.blocksRaycasts  = false;
        cg.interactable    = false;

        // アイテムアイコン
        var iconGO      = new GameObject("Icon");
        iconGO.transform.SetParent(_dragIconRoot.transform, false);
        _dragIconImage                  = iconGO.AddComponent<Image>();
        _dragIconImage.raycastTarget    = false;
        var iconRT                      = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin                = Vector2.zero;
        iconRT.anchorMax                = Vector2.one;
        iconRT.sizeDelta                = Vector2.zero;
        iconRT.anchoredPosition         = Vector2.zero;

        // 個数テキスト（右下）
        var countGO     = new GameObject("Count");
        countGO.transform.SetParent(_dragIconRoot.transform, false);
        var tmp                         = countGO.AddComponent<TextMeshProUGUI>();
        _dragIconCount                  = tmp;
        tmp.raycastTarget               = false;
        tmp.fontSize                    = 14f;
        tmp.fontStyle                   = FontStyles.Bold;
        tmp.alignment                   = TextAlignmentOptions.BottomRight;
        tmp.color                       = Color.white;
        var countRT                     = countGO.GetComponent<RectTransform>();
        countRT.anchorMin               = Vector2.zero;
        countRT.anchorMax               = Vector2.one;
        countRT.sizeDelta               = Vector2.zero;
        countRT.anchoredPosition        = Vector2.zero;

        _dragIconRoot.SetActive(false);
    }

    private void ShowDragIcon(ItemStack stack)
    {
        if (_dragIconRoot == null || stack?.ItemData == null) return;
        _dragIconImage.sprite  = stack.ItemData.Icon;
        _dragIconImage.enabled = stack.ItemData.Icon != null;
        bool showCount = stack.Count > 1;
        _dragIconCount.gameObject.SetActive(showCount);
        if (showCount) _dragIconCount.text = stack.Count.ToString();
        _dragIconRoot.SetActive(true);
        _dragIconRoot.transform.SetAsLastSibling();
    }

    private void HideDragIcon()
    {
        if (_dragIconRoot != null) _dragIconRoot.SetActive(false);
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // ── 抓取状態のキャンセル検出 ─────────────────────────────────
        if (_pendingMoveSourceIndex >= 0)
        {
            // 右键：任意位置で取消、メニューを抑制
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _suppressNextRightClickMenu = true;
                ClearMoveState();
                ClearSelection();
                return;
            }

            // 左键：背包格子以外の位置をクリックした場合に取消
            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverInventoryGridSlot())
            {
                ClearMoveState();
                ClearSelection();
                return;
            }
        }

        // ── ドラッグ icon マウス追従 ──────────────────────────────────
        if (_dragIconRoot == null || !_dragIconRoot.activeSelf) return;
        var canvasRT = transform as RectTransform;
        if (canvasRT == null) return;
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, mouse.position.ReadValue(), null, out localPoint))
        {
            ((RectTransform)_dragIconRoot.transform).anchoredPosition = localPoint;
        }
    }
    // ── Raycast Helper ───────────────────────────────────────────────

    /// <summary>
    /// 現在のマウス位置が InventoryGridSlotUI を持つ UI オブジェクト上かを返す。
    /// EventSystem.RaycastAll を使用し _raycastResults を再利用。
    /// </summary>
    private bool IsPointerOverInventoryGridSlot()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, _raycastResults);
        foreach (var result in _raycastResults)
            if (result.gameObject != null &&
                result.gameObject.GetComponentInParent<InventoryGridSlotUI>() != null)
                return true;
        return false;
    }
}
