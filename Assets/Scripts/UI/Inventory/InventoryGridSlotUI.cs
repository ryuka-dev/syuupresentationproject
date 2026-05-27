using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 格子式背包スロット。アイコン + 数量のみ表示。Hover で詳情 Tooltip を通知。
/// </summary>
public class InventoryGridSlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler
{
    [SerializeField] private Image      iconImage;
    [SerializeField] private TMP_Text   countText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button     button;

    private ItemStack          _stack;
    private Action<ItemStack>  _onClicked;
    private Action<InventoryGridSlotUI, ItemStack> _onHoverEnter;
    private Action<InventoryGridSlotUI>            _onHoverExit;
    private Action<InventoryGridSlotUI, ItemStack, Vector2> _onRightClicked;
    private Action<int>        _onEmptyClicked;

    private const bool DebugRightClickTrace = true;

    public int       SlotIndex  { get; set; }
    public bool      IsEmpty    => _stack == null;
    public ItemStack BoundStack => _stack;
    public RectTransform SlotRect => GetComponent<RectTransform>();

    // ── Empty ──────────────────────────────────────────────────────
    public void SetEmpty(Action<int> onEmptyClicked = null)
    {
        _stack          = null;
        _onClicked      = null;
        _onHoverEnter   = null;
        _onHoverExit    = null;
        _onRightClicked = null;
        _onEmptyClicked = onEmptyClicked;
        if (iconImage)  { iconImage.sprite = null; iconImage.enabled = false; }
        if (countText)    countText.gameObject.SetActive(false);
        SetSelected(false);
        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (_onEmptyClicked != null)
                button.onClick.AddListener(() => _onEmptyClicked?.Invoke(SlotIndex));
        }
    }

    // ── Occupied ───────────────────────────────────────────────────
    public void SetItem(ItemStack stack, Action<ItemStack> onClicked,
                        Action<InventoryGridSlotUI, ItemStack> onHoverEnter = null,
                        Action<InventoryGridSlotUI>            onHoverExit  = null,
                        Action<InventoryGridSlotUI, ItemStack, Vector2> onRightClicked = null)
    {
        if (stack == null || stack.ItemData == null) { SetEmpty(); return; }

        _stack          = stack;
        _onClicked      = onClicked;
        _onHoverEnter   = onHoverEnter;
        _onHoverExit    = onHoverExit;
        _onRightClicked = onRightClicked;

        if (iconImage)
        {
            var icon = stack.ItemData.Icon;
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        if (countText)
        {
            bool show = stack.Count > 1;
            countText.gameObject.SetActive(show);
            if (show) countText.text = stack.Count.ToString();
        }

        SetSelected(false);
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    // ── Selected frame ─────────────────────────────────────────────
    public void SetSelected(bool selected)
    {
        if (selectedFrame) selectedFrame.SetActive(selected);
    }

    // ── Hover ──────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsEmpty) _onHoverEnter?.Invoke(this, _stack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _onHoverExit?.Invoke(this);
    }

    // 右键は OnPointerDown で処理（初回クリック信頼性のため）
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (DebugRightClickTrace)
                Debug.Log("[InventoryRightClickTrace] GridSlot.OnPointerDown Right" +
                    " slotIndex=" + SlotIndex +
                    " isEmpty=" + IsEmpty +
                    " hasStack=" + (_stack != null) +
                    " stackName=" + (_stack?.ItemName ?? "null") +
                    " hasCallback=" + (_onRightClicked != null) +
                    " pos=" + eventData.position);
            if (!IsEmpty)
                _onRightClicked?.Invoke(this, _stack, eventData.position);
        }
    }

    // 左键は Button.onClick で処理。OnPointerClick では右键を重複させない。
    public void OnPointerClick(PointerEventData eventData)
    {
        if (DebugRightClickTrace && eventData.button == PointerEventData.InputButton.Right)
            Debug.Log("[InventoryRightClickTrace] GridSlot.OnPointerClick Right slotIndex=" + SlotIndex + " (right handled by OnPointerDown, NOT here)");
    }

    private void OnButtonClicked() { _onClicked?.Invoke(_stack); }
}
