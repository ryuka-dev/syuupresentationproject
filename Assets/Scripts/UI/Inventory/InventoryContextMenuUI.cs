using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 右クリックコンテキストメニュー。
/// - 任意のメニュー項目クリック後、自動的に Hide
/// - 任意の外部クリック（左/右）後、自動的に Hide
/// - 同一フレームで Show が呼ばれた場合は閉じない（_justOpened フラグ）
/// </summary>
public class InventoryContextMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Button     equipButton;
    [SerializeField] private Button     unequipButton;
    [SerializeField] private float      screenPadding = 8f;

    private RectTransform _rt;
    private bool          _justOpened;   // Show 直後フレームは外部クリック無視

    // ── 状態 ────────────────────────────────────────────────────────
    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    // ── Lifecycle ───────────────────────────────────────────────────
    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        Hide();
    }

    private void Update()
    {
        // Show と同フレームはスキップ
        if (_justOpened) { _justOpened = false; return; }
        if (!IsOpen || Mouse.current == null) return;

        bool anyClick = Mouse.current.leftButton.wasPressedThisFrame
                     || Mouse.current.rightButton.wasPressedThisFrame;
        if (anyClick && !IsPointerInside(Mouse.current.position.ReadValue()))
            Hide();
    }

    // ── Public API ──────────────────────────────────────────────────
    public void ShowForInventoryItem(ItemStack stack, RectTransform rootRT,
                                     Vector2 screenPos, Action onEquip)
    {
        if (stack?.ItemData == null) { Hide(); return; }
        bool isEquip = stack.ItemData.ItemType == ItemType.Equipment;
        SetButton(equipButton,   isEquip, onEquip);
        SetButton(unequipButton, false,   null);
        Show(rootRT, screenPos);
    }

    public void ShowForEquippedItem(RectTransform rootRT, Vector2 screenPos, Action onUnequip)
    {
        SetButton(equipButton,   false, null);
        SetButton(unequipButton, true,  onUnequip);
        Show(rootRT, screenPos);
    }

    public void Hide()
    {
        if (rootPanel) rootPanel.SetActive(false);
        _justOpened = false;
    }

    // ── 内部 ────────────────────────────────────────────────────────
    private void Show(RectTransform rootRT, Vector2 screenPos)
    {
        if (rootPanel == null) return;
        rootPanel.SetActive(true);
        _justOpened = true;   // 今フレームの外部クリック判定をスキップ

        if (rootRT == null || _rt == null) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootRT, screenPos, null, out localPos);

        float w   = _rt.rect.width;
        float h   = _rt.rect.height;
        Rect  b   = rootRT.rect;
        float pad = screenPadding;
        localPos.x = Mathf.Clamp(localPos.x, b.xMin + pad, b.xMax - w - pad);
        localPos.y = Mathf.Clamp(localPos.y, b.yMin + h + pad, b.yMax - pad);

        _rt.anchorMin        = new Vector2(0.5f, 0.5f);
        _rt.anchorMax        = new Vector2(0.5f, 0.5f);
        _rt.pivot            = new Vector2(0f, 1f);
        _rt.anchoredPosition = localPos;
        transform.SetAsLastSibling();
    }

    // ボタンクリック後に Action を実行し、必ず Hide する
    private void InvokeAndHide(Action action)
    {
        action?.Invoke();
        Hide();
    }

    private void SetButton(Button btn, bool visible, Action onClick)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(visible);
        btn.onClick.RemoveAllListeners();
        if (visible && onClick != null)
            btn.onClick.AddListener(() => InvokeAndHide(onClick));
    }

    // ── ユーティリティ ───────────────────────────────────────────────
    /// <summary>スクリーン座標がこのメニュー矩形内にあるか。</summary>
    public bool IsPointerInside(Vector2 screenPosition)
    {
        if (_rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_rt, screenPosition, null);
    }
}
