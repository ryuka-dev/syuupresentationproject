using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// マウスボタンの「押下開始が UI 上か、ワールド（シーン）上か」を判定する門番。
///
/// 重要原則：
///   ボタンを「押した瞬間」の UI 命中を記録し、その押下期間中は状態を維持する。
///   毎フレーム現在位置が UI 上かを判定しない。
///
/// [DefaultExecutionOrder(-100)] により他の MonoBehaviour より早く Update が走る。
/// これにより LeftWorldPressedThisFrame 等が同フレーム内で正確に読み取れる。
/// </summary>
[DefaultExecutionOrder(-100)]
public class MouseInputGate : MonoBehaviour
{
    // ── 共有 RaycastResult バッファ（GC 削減） ──────────────────────
    private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private static          PointerEventData    _pointerEventData;

    // ── 内部状態 ────────────────────────────────────────────────────
    private bool _leftStartedOverUI;
    private bool _rightStartedOverUI;

    // ── 公開プロパティ ──────────────────────────────────────────────
    public bool LeftStartedOverUI  => _leftStartedOverUI;
    public bool RightStartedOverUI => _rightStartedOverUI;

    public bool LeftWorldHeld
    {
        get
        {
            var m = Mouse.current;
            return m != null && m.leftButton.isPressed && !_leftStartedOverUI;
        }
    }

    public bool RightWorldHeld
    {
        get
        {
            var m = Mouse.current;
            return m != null && m.rightButton.isPressed && !_rightStartedOverUI;
        }
    }

    public bool AnyWorldMouseHeld    => LeftWorldHeld || RightWorldHeld;
    public bool BothWorldButtonsHeld => LeftWorldHeld && RightWorldHeld;

    public bool LeftWorldPressedThisFrame  { get; private set; }
    public bool RightWorldPressedThisFrame { get; private set; }
    public bool AnyWorldMousePressedThisFrame => LeftWorldPressedThisFrame || RightWorldPressedThisFrame;

    // ── Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        _pointerEventData = null;
    }

    private void Update()
    {
        LeftWorldPressedThisFrame  = false;
        RightWorldPressedThisFrame = false;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _leftStartedOverUI = IsPointerOverUI(mouse.position.ReadValue());
            if (!_leftStartedOverUI)
                LeftWorldPressedThisFrame = true;
        }
        if (mouse.leftButton.wasReleasedThisFrame)
            _leftStartedOverUI = false;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            _rightStartedOverUI = IsPointerOverUI(mouse.position.ReadValue());
            if (!_rightStartedOverUI)
                RightWorldPressedThisFrame = true;
        }
        if (mouse.rightButton.wasReleasedThisFrame)
            _rightStartedOverUI = false;
    }

    private static bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        if (_pointerEventData == null || _pointerEventData.currentInputModule == null)
            _pointerEventData = new PointerEventData(EventSystem.current);

        _pointerEventData.position = screenPos;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);
        return _raycastResults.Count > 0;
    }
}
