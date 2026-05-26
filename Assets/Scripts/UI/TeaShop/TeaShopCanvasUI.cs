using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 茶商店 UI メインコントローラ。
/// 
/// 【開く方法】
///   - 他のスクリプトから Open() / Close() / Toggle() を呼ぶ
///   - テスト用：T キー（未使用キー確認済み）
/// 
/// 【構成要素】
///   - 4 種カテゴリタブ
///   - 商品格子（動的生成、最大 6 個/ページ）
///   - 前後ページボタン
///   - 商品詳細エリア
///   - 購入数量コントロール
///   - 購買 / 試饮 / 赠送 ボタン
///   - 所持金表示
///   - 閉じるボタン
/// </summary>
public class TeaShopCanvasUI : MonoBehaviour
{
    // ─── Constants ────────────────────────────────────────────────────
    private const int    ItemsPerPage          = 6;
    private const float  SampleCooldownSeconds = 3600f;
    private const string TestOpenKey           = "t"; // [TEMP TEST] T キーで開閉（未使用キー）

    // ─── Inspector References ─────────────────────────────────────────
    [Header("Data")]
    [SerializeField] private TeaShopCatalogData catalog;

    [Header("Player Systems")]
    [SerializeField] private PlayerWallet               playerWallet;
    [SerializeField] private PlayerInventory            playerInventory;
    [SerializeField] private PlayerTeaBuffController    playerTeaBuffController;

    [Header("Root")]
    [SerializeField] private GameObject rootPanel;

    [Header("Category Tabs (順: GreenTea / BlackTea / HerbalTea / SpecialTea)")]
    [SerializeField] private Button[] categoryButtons;   // 4 buttons
    [SerializeField] private TMP_Text[] categoryLabels;  // optional labels on buttons

    [Header("Item List")]
    [SerializeField] private Transform          itemListRoot;
    [SerializeField] private TeaShopItemSlotUI  itemSlotPrefab;

    [Header("Pagination")]
    [SerializeField] private Button   prevPageButton;
    [SerializeField] private Button   nextPageButton;
    [SerializeField] private TMP_Text pageInfoText;      // optional "1/2" text

    [Header("Detail Area")]
    [SerializeField] private GameObject  detailPanel;
    [SerializeField] private Image       detailIcon;
    [SerializeField] private TMP_Text    detailNameText;
    [SerializeField] private TMP_Text    detailDescText;
    [SerializeField] private TMP_Text    detailPriceText;
    [SerializeField] private TMP_Text    detailOwnedText;
    [SerializeField] private TMP_Text    detailTotalCostText;
    [SerializeField] private TMP_Text    emptyDetailText;   // "请选择茶" message

    [Header("Quantity Control")]
    [SerializeField] private Button   minusButton;
    [SerializeField] private Button   plusButton;
    [SerializeField] private Button   maxButton;
    [SerializeField] private TMP_Text quantityText;

    [Header("Action Buttons")]
    [SerializeField] private Button   buyButton;
    [SerializeField] private Button   sampleButton;
    [SerializeField] private Button   giftButton;

    [Header("Button Labels")]
    [SerializeField] private TMP_Text sampleButtonLabel;
    [SerializeField] private TMP_Text giftButtonLabel;

    [Header("Bottom Bar")]
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private Button   closeButton;

    // ─── Runtime State ────────────────────────────────────────────────
    private bool              _isOpen;
    private TeaShopCategory   _currentCategory   = TeaShopCategory.GreenTea;
    private int               _currentPage       = 0;
    private int               _currentQuantity   = 1;
    private TeaShopItemData   _selectedItem;
    private List<TeaShopItemData>       _filteredItems     = new List<TeaShopItemData>();
    private List<TeaShopItemSlotUI>     _activeSlots       = new List<TeaShopItemSlotUI>();

    // Sample cooldown (runtime only, resets on restart)
    private float _nextSampleAvailableTime = 0f;

    // Gift state (runtime only)
    [SerializeField] private int affinity = 0;
    private float _nextGiftAvailableTime  = 0f;

    private static readonly string[] CategoryDisplayNames =
        { "绿茶", "红茶", "花茶", "特饮" };

    private static readonly TeaShopCategory[] CategoryOrder =
    {
        TeaShopCategory.GreenTea,
        TeaShopCategory.BlackTea,
        TeaShopCategory.HerbalTea,
        TeaShopCategory.SpecialTea
    };

    // ─── Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        // Fallback finds
        if (playerWallet            == null) playerWallet            = FindFirstObjectByType<PlayerWallet>();
        if (playerInventory         == null) playerInventory         = FindFirstObjectByType<PlayerInventory>();
        if (playerTeaBuffController == null) playerTeaBuffController = FindFirstObjectByType<PlayerTeaBuffController>();

        if (playerWallet            == null) Debug.LogWarning("[TeaShopCanvasUI] PlayerWallet not found.");
        if (playerInventory         == null) Debug.LogWarning("[TeaShopCanvasUI] PlayerInventory not found.");
        if (playerTeaBuffController == null) Debug.LogWarning("[TeaShopCanvasUI] PlayerTeaBuffController not found.");

        if (rootPanel) rootPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerWallet    != null) playerWallet.OnGoldChanged        += OnGoldChangedHandler;
        if (playerInventory != null) playerInventory.OnInventoryChanged += OnInventoryChangedHandler;
    }

    private void OnDisable()
    {
        if (playerWallet    != null) playerWallet.OnGoldChanged        -= OnGoldChangedHandler;
        if (playerInventory != null) playerInventory.OnInventoryChanged -= OnInventoryChangedHandler;
    }

    private void OnGoldChangedHandler(int _) { if (_isOpen) RefreshGoldDisplay(); }
    private void OnInventoryChangedHandler()  { if (_isOpen) RefreshSlotOwnedCounts(); }

    private void Start()
    {
        SetupCategoryButtons();
        SetupPaginationButtons();
        SetupQuantityButtons();
        SetupActionButtons();
        if (closeButton) closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        // [TEMP TEST] T キーで開閉 - 後でNPC連携時に削除
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            Toggle();

        if (_isOpen)
            RefreshCooldownButtons();
    }

    // ─── Open / Close ─────────────────────────────────────────────────
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        if (rootPanel) rootPanel.SetActive(true);
        transform.SetAsLastSibling();

        _currentPage     = 0;
        _currentQuantity = 1;
        _selectedItem    = null;

        RefreshItemList();
        RefreshGoldDisplay();
        RefreshDetailEmpty();
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        if (rootPanel) rootPanel.SetActive(false);
    }

    public void Toggle() { if (_isOpen) Close(); else Open(); }

    // ─── Category Setup ───────────────────────────────────────────────
    private void SetupCategoryButtons()
    {
        if (categoryButtons == null) return;
        for (int i = 0; i < categoryButtons.Length && i < CategoryOrder.Length; i++)
        {
            int idx = i; // capture
            var btn = categoryButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnCategorySelected(CategoryOrder[idx]));

            // Set label text if labels array provided
            if (categoryLabels != null && i < categoryLabels.Length && categoryLabels[i] != null)
                categoryLabels[i].text = CategoryDisplayNames[i];
        }
    }

    private void OnCategorySelected(TeaShopCategory category)
    {
        _currentCategory = category;
        _currentPage     = 0;
        _currentQuantity = 1;
        _selectedItem    = null;
        RefreshItemList();
        RefreshDetailEmpty();
    }

    // ─── Item List ────────────────────────────────────────────────────
    private void RefreshItemList()
    {
        if (catalog == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] catalog is null.");
            ClearSlots();
            RefreshPaginationButtons();
            return;
        }

        _filteredItems = catalog.GetItemsByCategory(_currentCategory);

        // Clamp page
        int totalPages = GetTotalPages();
        if (totalPages == 0) _currentPage = 0;
        else _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);

        ClearSlots();
        GenerateSlotsForCurrentPage();
        RefreshPaginationButtons();

        // Auto-select first item on page
        if (_activeSlots.Count > 0)
            SelectItem(_activeSlots[0].ItemData);
        else
        {
            _selectedItem = null;
            RefreshDetailEmpty();
        }
    }

    private void ClearSlots()
    {
        foreach (var slot in _activeSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        _activeSlots.Clear();
    }

    private void GenerateSlotsForCurrentPage()
    {
        if (itemListRoot == null || itemSlotPrefab == null) return;

        int start = _currentPage * ItemsPerPage;
        int end   = Mathf.Min(start + ItemsPerPage, _filteredItems.Count);

        for (int i = start; i < end; i++)
        {
            var itemData  = _filteredItems[i];
            var slotGO    = Instantiate(itemSlotPrefab, itemListRoot);
            slotGO.name   = $"TeaSlot_{i}";
            int ownedCount = GetOwnedCount(itemData);
            slotGO.Setup(itemData, ownedCount, OnItemSlotClicked);
            _activeSlots.Add(slotGO);
        }
    }

    private void OnItemSlotClicked(TeaShopItemData itemData)
    {
        SelectItem(itemData);
    }

    private void SelectItem(TeaShopItemData itemData)
    {
        _selectedItem    = itemData;
        _currentQuantity = 1;

        // Update selected frames
        foreach (var slot in _activeSlots)
        {
            if (slot == null) continue;
            slot.SetSelected(slot.ItemData == _selectedItem);
        }

        if (_selectedItem != null)
            RefreshDetail();
        else
            RefreshDetailEmpty();
    }

    // ─── Pagination ───────────────────────────────────────────────────
    private int GetTotalPages()
    {
        if (_filteredItems.Count == 0) return 0;
        return Mathf.CeilToInt((float)_filteredItems.Count / ItemsPerPage);
    }

    private void SetupPaginationButtons()
    {
        if (prevPageButton) prevPageButton.onClick.AddListener(OnPrevPage);
        if (nextPageButton) nextPageButton.onClick.AddListener(OnNextPage);
    }

    private void OnPrevPage()
    {
        if (_currentPage <= 0) return;
        _currentPage--;
        _selectedItem    = null;
        _currentQuantity = 1;
        RefreshItemList();
    }

    private void OnNextPage()
    {
        if (_currentPage >= GetTotalPages() - 1) return;
        _currentPage++;
        _selectedItem    = null;
        _currentQuantity = 1;
        RefreshItemList();
    }

    private void RefreshPaginationButtons()
    {
        int totalPages = GetTotalPages();
        bool singlePage = totalPages <= 1;

        if (prevPageButton) prevPageButton.interactable = !singlePage && _currentPage > 0;
        if (nextPageButton) nextPageButton.interactable = !singlePage && _currentPage < totalPages - 1;

        if (pageInfoText)
        {
            if (totalPages == 0)
                pageInfoText.text = "0/0";
            else
                pageInfoText.text = $"{_currentPage + 1}/{totalPages}";
        }
    }

    // ─── Detail ───────────────────────────────────────────────────────
    private void RefreshDetail()
    {
        if (_selectedItem == null) { RefreshDetailEmpty(); return; }

        if (detailPanel)    detailPanel.SetActive(true);
        if (emptyDetailText) emptyDetailText.gameObject.SetActive(false);

        var teaItem = _selectedItem.TeaItem;
        if (detailIcon)
        {
            var icon = teaItem?.Icon;
            detailIcon.sprite  = icon;
            detailIcon.enabled = icon != null;
        }
        if (detailNameText)  detailNameText.text  = teaItem?.ItemName ?? "";
        if (detailDescText)  detailDescText.text  = _selectedItem.Description;
        if (detailPriceText) detailPriceText.text = $"单价：⊙{_selectedItem.Price}";

        RefreshDetailOwned();
        RefreshDetailQuantityAndCost();
        RefreshActionButtons();
    }

    private void RefreshDetailEmpty()
    {
        if (detailPanel)     detailPanel.SetActive(false);
        if (emptyDetailText)
        {
            emptyDetailText.gameObject.SetActive(true);
            emptyDetailText.text = "请选择茶";
        }

        SetBuyButtonInteractable(false);
        SetSampleButtonInteractable(false);
        SetGiftButtonInteractable(false);
    }

    private void RefreshDetailOwned()
    {
        if (detailOwnedText == null || _selectedItem == null) return;
        int owned = GetOwnedCount(_selectedItem);
        detailOwnedText.text = $"持有：{owned}";
    }

    private void RefreshDetailQuantityAndCost()
    {
        if (quantityText) quantityText.text = _currentQuantity.ToString();

        if (detailTotalCostText && _selectedItem != null)
        {
            int total = _selectedItem.Price * _currentQuantity;
            detailTotalCostText.text = $"合计：⊙{total}";
        }
    }

    // ─── Quantity ─────────────────────────────────────────────────────
    private void SetupQuantityButtons()
    {
        if (minusButton) minusButton.onClick.AddListener(OnMinusClicked);
        if (plusButton)  plusButton.onClick.AddListener(OnPlusClicked);
        if (maxButton)   maxButton.onClick.AddListener(OnMaxClicked);
    }

    private void OnMinusClicked()
    {
        if (_currentQuantity <= 1) return;
        _currentQuantity--;
        RefreshDetailQuantityAndCost();
        RefreshBuyButtonState();
    }

    private void OnPlusClicked()
    {
        _currentQuantity++;
        RefreshDetailQuantityAndCost();
        RefreshBuyButtonState();
    }

    private void OnMaxClicked()
    {
        if (_selectedItem == null) return;
        int price = _selectedItem.Price;
        int gold  = playerWallet != null ? playerWallet.Gold : 0;

        if (price > 0)
            _currentQuantity = Mathf.Max(1, gold / price);
        else
            _currentQuantity = 99;

        RefreshDetailQuantityAndCost();
        RefreshBuyButtonState();
    }

    // ─── Action Buttons Setup ─────────────────────────────────────────
    private void SetupActionButtons()
    {
        if (buyButton)    buyButton.onClick.AddListener(OnBuyClicked);
        if (sampleButton) sampleButton.onClick.AddListener(OnSampleClicked);
        if (giftButton)   giftButton.onClick.AddListener(OnGiftClicked);
    }

    private void RefreshActionButtons()
    {
        RefreshBuyButtonState();
        RefreshCooldownButtons();
    }

    private void RefreshBuyButtonState()
    {
        if (buyButton == null || _selectedItem == null) { SetBuyButtonInteractable(false); return; }
        int totalCost = _selectedItem.Price * _currentQuantity;
        bool canAfford = playerWallet != null && playerWallet.CanSpendGold(totalCost);
        SetBuyButtonInteractable(canAfford);
    }

    private void RefreshCooldownButtons()
    {
        if (!_isOpen) return;

        // Sample button
        if (sampleButton != null && _selectedItem != null)
        {
            float now         = Time.time;
            bool sampleReady  = now >= _nextSampleAvailableTime;
            SetSampleButtonInteractable(sampleReady);
            if (sampleButtonLabel)
            {
                if (sampleReady)
                    sampleButtonLabel.text = "试饮";
                else
                {
                    float remaining = _nextSampleAvailableTime - now;
                    sampleButtonLabel.text = $"试饮 {FormatCooldown(remaining)}";
                }
            }
        }
        else
        {
            SetSampleButtonInteractable(false);
            if (sampleButtonLabel) sampleButtonLabel.text = "试饮";
        }

        // Gift button
        if (giftButton != null && _selectedItem != null)
        {
            float now       = Time.time;
            bool giftReady  = now >= _nextGiftAvailableTime;
            bool canAfford  = playerWallet != null && playerWallet.CanSpendGold(_selectedItem.GiftCost);
            SetGiftButtonInteractable(giftReady && canAfford);
            if (giftButtonLabel)
            {
                if (giftReady)
                    giftButtonLabel.text = "赠送";
                else
                {
                    float remaining = _nextGiftAvailableTime - now;
                    giftButtonLabel.text = $"赠送 {FormatCooldown(remaining)}";
                }
            }
        }
        else
        {
            SetGiftButtonInteractable(false);
            if (giftButtonLabel) giftButtonLabel.text = "赠送";
        }
    }

    // ─── Buy ──────────────────────────────────────────────────────────
    private void OnBuyClicked()
    {
        if (_selectedItem == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Buy: no item selected.");
            return;
        }
        if (playerWallet == null || playerInventory == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Buy: wallet or inventory is null.");
            return;
        }

        int totalCost = _selectedItem.Price * _currentQuantity;
        if (!playerWallet.TrySpendGold(totalCost))
        {
            Debug.LogWarning($"[TeaShopCanvasUI] Buy: not enough gold ({playerWallet.Gold} < {totalCost}).");
            return;
        }

        for (int i = 0; i < _currentQuantity; i++)
            playerInventory.AddItem(_selectedItem.TeaItem);

        Debug.Log($"[TeaShopCanvasUI] Bought {_currentQuantity}x {_selectedItem.TeaItem.ItemName} for {totalCost} gold.");

        _currentQuantity = 1;
        RefreshGoldDisplay();
        RefreshDetail();
        RefreshSlotOwnedCounts();
    }

    // ─── Sample (試飲) ────────────────────────────────────────────────
    private void OnSampleClicked()
    {
        if (_selectedItem == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Sample: no item selected.");
            return;
        }

        float now = Time.time;
        if (now < _nextSampleAvailableTime)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Sample: still on cooldown.");
            return;
        }

        if (playerTeaBuffController == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Sample: PlayerTeaBuffController not found.");
            return;
        }

        bool success = playerTeaBuffController.TryUseTea(_selectedItem.TeaItem);
        if (!success)
        {
            Debug.LogWarning($"[TeaShopCanvasUI] Sample: TryUseTea failed for {_selectedItem.TeaItem?.ItemName}.");
            return;
        }

        _nextSampleAvailableTime = now + SampleCooldownSeconds;
        Debug.Log($"[TeaShopCanvasUI] Sampled {_selectedItem.TeaItem.ItemName}. Next sample available at {_nextSampleAvailableTime:F0}s.");

        // Note: NOT added to inventory, NOT deducting gold
        RefreshCooldownButtons();
    }

    // ─── Gift (赠送) ──────────────────────────────────────────────────
    private void OnGiftClicked()
    {
        if (_selectedItem == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Gift: no item selected.");
            return;
        }

        float now = Time.time;
        if (now < _nextGiftAvailableTime)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Gift: still on cooldown.");
            return;
        }

        if (playerWallet == null)
        {
            Debug.LogWarning("[TeaShopCanvasUI] Gift: PlayerWallet not found.");
            return;
        }

        int cost = _selectedItem.GiftCost;
        if (!playerWallet.TrySpendGold(cost))
        {
            Debug.LogWarning($"[TeaShopCanvasUI] Gift: not enough gold ({playerWallet.Gold} < {cost}).");
            return;
        }

        affinity++;
        _nextGiftAvailableTime = now + _selectedItem.GiftCooldownSeconds;
        Debug.Log($"[TeaShopCanvasUI] Gifted {_selectedItem.TeaItem?.ItemName}. Affinity: {affinity}. Next gift at {_nextGiftAvailableTime:F0}s.");

        // Note: NOT adding to inventory, NOT applying buff
        RefreshGoldDisplay();
        RefreshCooldownButtons();
    }

    // ─── Gold Display ─────────────────────────────────────────────────
    private void RefreshGoldDisplay()
    {
        if (goldText == null) return;
        int gold = playerWallet != null ? playerWallet.Gold : 0;
        goldText.text = $"⊙{gold}";
    }

    // ─── Helpers ──────────────────────────────────────────────────────
    private int GetOwnedCount(TeaShopItemData shopItem)
    {
        if (shopItem == null || shopItem.TeaItem == null || playerInventory == null) return 0;
        string targetId = shopItem.TeaItem.ItemId;
        int count = 0;
        foreach (var stack in playerInventory.Items)
        {
            if (stack == null || stack.ItemData == null) continue;
            if (!string.IsNullOrEmpty(targetId) && stack.ItemData.ItemId == targetId)
                count += stack.Count;
            else if (string.IsNullOrEmpty(targetId) && stack.ItemData.ItemName == shopItem.TeaItem.ItemName)
                count += stack.Count;
        }
        return count;
    }

    private void RefreshSlotOwnedCounts()
    {
        foreach (var slot in _activeSlots)
        {
            if (slot == null || slot.ItemData == null) continue;
            slot.UpdateOwnedCount(GetOwnedCount(slot.ItemData));
        }
        if (_selectedItem != null) RefreshDetailOwned();
    }

    private void SetBuyButtonInteractable(bool value)
    {
        if (buyButton) buyButton.interactable = value;
    }

    private void SetSampleButtonInteractable(bool value)
    {
        if (sampleButton) sampleButton.interactable = value;
    }

    private void SetGiftButtonInteractable(bool value)
    {
        if (giftButton) giftButton.interactable = value;
    }

    private static string FormatCooldown(float seconds)
    {
        if (seconds <= 0f) return "00:00";
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        return $"{m:D2}:{s:D2}";
    }
}
