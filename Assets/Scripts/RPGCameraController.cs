using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 传统 RPG 摄像机。
///   左/右键ドラッグ → 镜頭回転（MouseInputGate.AnyWorldMouseHeld ベース）
///   滚轮            → 推拉
///   自动前进 + 左键自由镜头終了後 → yaw 慢速回正
/// </summary>
public class RPGCameraController : MonoBehaviour
{
    [Header("目标")]
    public Transform target;
    public Vector3   targetOffset = new Vector3(0f, 1.0f, 0f);
    /// <summary>シェイクなど外部からの加算オフセット。毎フレーム残存値をゼロに近づける。</summary>
    [System.NonSerialized] public Vector3 shakeOffset = Vector3.zero;


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
    [SerializeField] private MouseInputGate     mouseInputGate;
    [SerializeField] private PlayerController   playerController;

    // ── 内部状態 ────────────────────────────────────────────────────
    private float   _yaw;
    private float   _pitch = 30f;
    private Vector2 _dragStartMousePos;
    private bool    _wasDragging;
    private bool    _gateWarned;

    // ─────────────────────────────────────────────────────────────
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
        {
            Debug.LogWarning("[RPGCameraController] MouseInputGate not found. Camera drag disabled.");
            _gateWarned = true;
        }

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
            Debug.LogWarning("[RPGCameraController] PlayerController not found. Auto-forward camera return disabled.");
    }

    // ─────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // MouseInputGate がない場合は一切動かさない
        bool anyWorldHeld = mouseInputGate != null && mouseInputGate.AnyWorldMouseHeld;

        // ── ドラッグ開始 → Cursor 隠す ─────────────────────────────
        bool dragStartThisFrame = mouseInputGate != null &&
            mouseInputGate.AnyWorldMousePressedThisFrame && !_wasDragging;

        if (dragStartThisFrame)
        {
            _dragStartMousePos = mouse.position.ReadValue();
            Cursor.visible     = false;
            Cursor.lockState   = CursorLockMode.Locked;
        }

        // ── ドラッグ中：マウスデルタでカメラ回転 ───────────────────
        if (anyWorldHeld)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * rotationSpeed * Time.deltaTime;
            _pitch -= delta.y * rotationSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        // ── ドラッグ終了 → Cursor 復元 ─────────────────────────────
        if (_wasDragging && !anyWorldHeld)
        {
            Cursor.lockState = CursorLockMode.None;
            mouse.WarpCursorPosition(_dragStartMousePos);
            Cursor.visible   = true;
        }
        _wasDragging = anyWorldHeld;

        // ── カメラ yaw 回正（自动前进 + 自由镜头終了後） ─────────────
        HandleCameraReturn();

        // ── スクロール ────────────────────────────────────────────
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.01f, minDistance, maxDistance);

        // ── カメラ位置更新 ────────────────────────────────────────
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    lookAt   = target.position + targetOffset;
        Vector3    desired  = lookAt - rotation * Vector3.forward * distance;

        transform.position = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime) + shakeOffset;
        shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, 20f * Time.deltaTime); // 自然減衰
        transform.LookAt(lookAt);
    }

    // ─────────────────────────────────────────────────────────────
    private void HandleCameraReturn()
    {
        if (playerController == null) return;
        if (!playerController.AutoForwardCameraReturnActive) return;

        // ドラッグ中・自由镜头中は回正しない
        if (mouseInputGate != null && mouseInputGate.AnyWorldMouseHeld) return;

        // 目標 yaw：locked forward の水平方向から計算
        Vector3 lockedFwd = playerController.AutoForwardLockedForward;
        if (lockedFwd.sqrMagnitude < 0.001f) lockedFwd = target != null
            ? Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized
            : Vector3.forward;

        float targetYaw = Mathf.Atan2(lockedFwd.x, lockedFwd.z) * Mathf.Rad2Deg;
        float speed     = playerController.AutoForwardCameraReturnYawSpeed;

        _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, speed * Time.deltaTime);

        // 差が 0.5 度以内に収まったら完了通知
        if (Mathf.Abs(Mathf.DeltaAngle(_yaw, targetYaw)) < 0.5f)
        {
            _yaw = targetYaw;
            playerController.NotifyCameraReturnComplete();
        }
    }

    // ─────────────────────────────────────────────────────────────
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
