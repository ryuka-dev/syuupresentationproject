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
    [SerializeField] private Button     useButton;
    [SerializeField] private float      screenPadding = 8f;

    private RectTransform _rt;
    private bool          _justOpened;   // Show 直後フレームは外部クリック無視
    // マウスボタンが離れるまで外部クリック検出を抑制（初回右键 / 同一クリック即 Hide 防止）
    private bool          _ignoreOutsideClickUntilPointerReleased;

    private const bool DebugRightClickTrace = false;

    // ── 状態 ────────────────────────────────────────────────────────
    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    // ── Lifecycle ───────────────────────────────────────────────────
    private void Awake()
    {
        // 引用キャッシュのみ。Hide() は呼ばない。
        // 理由：ContextMenuUI の GameObject が初期 inactive の場合、
        //       初回 Show()->SetActive(true) でここが発火し、
        //       Hide()->SetActive(false) が即座に Show を打ち消してしまうため。
        _rt = GetComponent<RectTransform>();
        // フィールドはデフォルト値（false）で十分。明示的に書いておく。
        _justOpened = false;
        _ignoreOutsideClickUntilPointerReleased = false;
    }

    private void Update()
    {
        // Show と同フレームはスキップ（フレームカウントベースの保護）
        if (_justOpened)
        {
            if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Update skip justOpened");
            _justOpened = false; return;
        }
        if (!IsOpen || Mouse.current == null) return;

        // ボタン押下中は外部クリック検出をスキップ（開いた直後の同クリックによる即 Hide を防ぐ）
        if (_ignoreOutsideClickUntilPointerReleased)
        {
            bool anyButtonHeld = Mouse.current.leftButton.isPressed
                              || Mouse.current.rightButton.isPressed;
            if (anyButtonHeld)
            {
                if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Update skip until pointer released" +
                    " leftHeld=" + Mouse.current.leftButton.isPressed +
                    " rightHeld=" + Mouse.current.rightButton.isPressed);
                return;
            }
            _ignoreOutsideClickUntilPointerReleased = false;
        }

        bool anyClick = Mouse.current.leftButton.wasPressedThisFrame
                     || Mouse.current.rightButton.wasPressedThisFrame;
        if (anyClick && !IsPointerInside(Mouse.current.position.ReadValue()))
        {
            if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Update outside-click-hide" +
                " anyClick=" + anyClick +
                " pointerInside=" + IsPointerInside(Mouse.current.position.ReadValue()) +
                " ignoreUntilReleased=" + _ignoreOutsideClickUntilPointerReleased +
                " justOpened=" + _justOpened +
                " leftPressed=" + Mouse.current.leftButton.isPressed +
                " rightPressed=" + Mouse.current.rightButton.isPressed);
            Hide();
        }
    }

    // ── Public API ──────────────────────────────────────────────────
    public void ShowForInventoryItem(ItemStack stack, RectTransform rootRT,
                                     Vector2 screenPos, Action onEquip, Action onUse = null)
    {
        if (stack?.ItemData == null)
        {
            if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Hide reason=ShowForInventoryItem stack null");
            Hide(); return;
        }
        bool isEquip = stack.ItemData.ItemType == ItemType.Equipment;
        bool isTea   = stack.ItemData.ItemType == ItemType.Tea;
        SetButton(equipButton,   isEquip, onEquip);
        SetButton(unequipButton, false,   null);
        SetButton(useButton,     isTea,   onUse);
        Show(rootRT, screenPos);
    }

    public void ShowForEquippedItem(RectTransform rootRT, Vector2 screenPos, Action onUnequip)
    {
        SetButton(equipButton,   false, null);
        SetButton(unequipButton, true,  onUnequip);
        SetButton(useButton,     false, null);
        Show(rootRT, screenPos);
    }

    public void Hide()
    {
        if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Hide reason=manual/caller" +
            " wasOpen=" + IsOpen);
        if (rootPanel) rootPanel.SetActive(false);
        _justOpened = false;
        _ignoreOutsideClickUntilPointerReleased = false;
    }

    // ── 内部 ────────────────────────────────────────────────────────
    private void Show(RectTransform rootRT, Vector2 screenPos)
    {
        if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Show enter" +
            " activeSelf=" + (rootPanel != null ? rootPanel.activeSelf.ToString() : "rootPanel_null") +
            " activeInHierarchy=" + (rootPanel != null ? rootPanel.activeInHierarchy.ToString() : "rootPanel_null") +
            " isOpenBefore=" + IsOpen +
            " screenPos=" + screenPos);
        if (rootPanel == null) return;
        rootPanel.SetActive(true);
        _justOpened = true;                              // 今フレームの外部クリック判定をスキップ
        _ignoreOutsideClickUntilPointerReleased = true; // ボタン離れるまで外部クリック検出を抑制

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

        if (DebugRightClickTrace)
        {
            var cg = GetComponent<CanvasGroup>();
            Debug.Log("[InventoryRightClickTrace] ContextMenu.Show applied" +
                " activeSelf=" + rootPanel.activeSelf +
                " activeInHierarchy=" + rootPanel.activeInHierarchy +
                " isOpenAfter=" + IsOpen +
                " alpha=" + (cg != null ? cg.alpha.ToString() : "no-CanvasGroup") +
                " anchoredPos=" + _rt.anchoredPosition +
                " siblingIdx=" + transform.GetSiblingIndex());
        }
    }

    // ボタンクリック後に Action を実行し、必ず Hide する
    private void InvokeAndHide(Action action)
    {
        if (DebugRightClickTrace) Debug.Log("[InventoryRightClickTrace] ContextMenu.Hide reason=button invoked");
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
