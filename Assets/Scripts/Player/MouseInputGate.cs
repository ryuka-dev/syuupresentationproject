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
/// 提供する情報：
///   LeftWorldHeld / RightWorldHeld      … 押下開始がワールド側のボタンが現在押されているか
///   LeftWorldPressedThisFrame           … 今フレームにワールドで左ボタンが押された
///   RightWorldPressedThisFrame          … 今フレームにワールドで右ボタンが押された
///   AnyWorldMouseHeld                   … どちらか一方でもワールドで押されているか
///   BothWorldButtonsHeld                … 両方ともワールドで押されているか
///   LeftStartedOverUI / RightStartedOverUI … デバッグ / 外部参照用
///
/// 他システムへの依存：EventSystem（null 安全対応済み）
/// </summary>
public class MouseInputGate : MonoBehaviour
{
    // ── 共有 RaycastResult バッファ（GC 削減） ──────────────────────
    private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private static          PointerEventData    _pointerEventData;

    // ── 内部状態 ────────────────────────────────────────────────────
    private bool _leftStartedOverUI;
    private bool _rightStartedOverUI;

    // ── 公開プロパティ ──────────────────────────────────────────────
    /// <summary>左ボタンの押下開始が UI 上だったか（参照・デバッグ用）</summary>
    public bool LeftStartedOverUI  => _leftStartedOverUI;
    /// <summary>右ボタンの押下開始が UI 上だったか（参照・デバッグ用）</summary>
    public bool RightStartedOverUI => _rightStartedOverUI;

    /// <summary>ワールド起点の左ボタンが現在押されているか</summary>
    public bool LeftWorldHeld
    {
        get
        {
            var m = Mouse.current;
            return m != null && m.leftButton.isPressed && !_leftStartedOverUI;
        }
    }

    /// <summary>ワールド起点の右ボタンが現在押されているか</summary>
    public bool RightWorldHeld
    {
        get
        {
            var m = Mouse.current;
            return m != null && m.rightButton.isPressed && !_rightStartedOverUI;
        }
    }

    /// <summary>どちらか一方でもワールド起点で押されているか</summary>
    public bool AnyWorldMouseHeld  => LeftWorldHeld || RightWorldHeld;

    /// <summary>両方ともワールド起点で押されているか（双键前進用）</summary>
    public bool BothWorldButtonsHeld => LeftWorldHeld && RightWorldHeld;

    // ── 今フレームのみ有効なプロパティ（Update 終了後に自動クリア） ─
    /// <summary>今フレームにワールドで左ボタンが押された</summary>
    public bool LeftWorldPressedThisFrame  { get; private set; }
    /// <summary>今フレームにワールドで右ボタンが押された</summary>
    public bool RightWorldPressedThisFrame { get; private set; }
    /// <summary>今フレームにワールドでどちらかのボタンが押された</summary>
    public bool AnyWorldMousePressedThisFrame => LeftWorldPressedThisFrame || RightWorldPressedThisFrame;

    // ── Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        // PointerEventData は一度だけ生成（EventSystem が後で初期化される可能性があるため null 許容）
        _pointerEventData = null;
    }

    private void Update()
    {
        // ThisFrame 系は毎フレームリセット（Update の先頭でクリア）
        LeftWorldPressedThisFrame  = false;
        RightWorldPressedThisFrame = false;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // ── 左ボタン ──────────────────────────────────────────────
        if (mouse.leftButton.wasPressedThisFrame)
        {
            _leftStartedOverUI = IsPointerOverUI(mouse.position.ReadValue());
            if (!_leftStartedOverUI)
                LeftWorldPressedThisFrame = true;
        }
        if (mouse.leftButton.wasReleasedThisFrame)
            _leftStartedOverUI = false;

        // ── 右ボタン ──────────────────────────────────────────────
        if (mouse.rightButton.wasPressedThisFrame)
        {
            _rightStartedOverUI = IsPointerOverUI(mouse.position.ReadValue());
            if (!_rightStartedOverUI)
                RightWorldPressedThisFrame = true;
        }
        if (mouse.rightButton.wasReleasedThisFrame)
            _rightStartedOverUI = false;
    }

    // ── UI 命中判定（押下瞬間のみ呼ばれる） ───────────────────────
    private static bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        // PointerEventData を再利用（null or EventSystem 差し替え後は再生成）
        if (_pointerEventData == null || _pointerEventData.currentInputModule == null)
            _pointerEventData = new PointerEventData(EventSystem.current);

        _pointerEventData.position = screenPos;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);
        return _raycastResults.Count > 0;
    }
}
