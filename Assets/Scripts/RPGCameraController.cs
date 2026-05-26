using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 传统 RPG 摄像机
///   左键ドラッグ → 镜頭回転（ワールド起点の場合のみ）
///   右键ドラッグ → 镜頭回転（ワールド起点の場合のみ）
///   滚轮         → 推拉距离
///   拖拽时隐藏鼠标，松开后恢复显示。
///
/// 入力帰属の判定は MouseInputGate に委譲する。
/// </summary>
public class RPGCameraController : MonoBehaviour
{
    [Header("目标")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.0f, 0f);

    [Header("距离")]
    public float distance    = 6f;
    public float minDistance = 2f;
    public float maxDistance = 14f;
    public float zoomSpeed   = 3f;

    [Header("旋转")]
    public float rotationSpeed = 200f;
    public float minPitch      = 10f;
    public float maxPitch      = 75f;

    [Header("平滑")]
    public float followSmooth = 10f;

    [Header("Input")]
    [SerializeField] private MouseInputGate mouseInputGate;

    private float _yaw;
    private float _pitch = 30f;

    // ドラッグ開始時のマウス座標（カーソル復元用）
    private Vector2 _dragStartMousePos;
    // 前フレームに AnyWorldMouseHeld だったか（On→Off 検出用）
    private bool    _wasDragging;

    void Start()
    {
        var angles = transform.eulerAngles;
        _yaw   = angles.y;
        _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        SetCursorVisible(true);

        if (mouseInputGate == null)
            mouseInputGate = FindFirstObjectByType<MouseInputGate>();
        if (mouseInputGate == null)
            Debug.LogWarning("[RPGCameraController] MouseInputGate not found. Camera drag will be disabled.");
    }

    void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // MouseInputGate が見つからない場合は安全に何もしない
        bool anyWorldHeld = mouseInputGate != null && mouseInputGate.AnyWorldMouseHeld;
        bool leftWorld    = mouseInputGate != null && mouseInputGate.LeftWorldHeld;
        bool rightWorld   = mouseInputGate != null && mouseInputGate.RightWorldHeld;

        // ─── ドラッグ開始：ワールド起点でいずれかのボタンが押された瞬間 ─
        bool dragStartThisFrame = mouseInputGate != null &&
            (mouseInputGate.LeftWorldPressedThisFrame || mouseInputGate.RightWorldPressedThisFrame)
            && !_wasDragging;

        if (dragStartThisFrame)
        {
            _dragStartMousePos = mouse.position.ReadValue();
            Cursor.visible     = false;
            Cursor.lockState   = CursorLockMode.Locked;
        }

        // ─── ドラッグ中：左または右のワールドボタンが押されていれば回転 ──
        if (anyWorldHeld)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * rotationSpeed * Time.deltaTime;
            _pitch -= delta.y * rotationSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        // ─── ドラッグ終了：前フレームは押されていたが今フレームは離れた ──
        if (_wasDragging && !anyWorldHeld)
        {
            Cursor.lockState = CursorLockMode.None;
            mouse.WarpCursorPosition(_dragStartMousePos);
            Cursor.visible   = true;
        }

        _wasDragging = anyWorldHeld;

        // ─── スクロール拡縮 ───────────────────────────────────────
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.01f, minDistance, maxDistance);

        // ─── カメラ位置更新 ───────────────────────────────────────
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    lookAt   = target.position + targetOffset;
        Vector3    desired  = lookAt - rotation * Vector3.forward * distance;

        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);
        transform.LookAt(lookAt);
    }

    void OnDisable()
    {
        _wasDragging     = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void OnDestroy()
    {
        _wasDragging     = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void SetCursorVisible(bool visible)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = visible;
    }
}
