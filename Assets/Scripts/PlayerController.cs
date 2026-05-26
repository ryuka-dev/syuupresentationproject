using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed   = 5f;
    public float sprintSpeed = 10f;

    [Header("跳跃")]
    public float jumpForce = 6f;

    [Header("Jump Tuning")]
    [SerializeField] private float fallGravityMultiplier    = 2.5f;
    [SerializeField] private float riseGravityMultiplier    = 1.0f;
    [SerializeField] private float maxFallSpeed             = 25f;
    [SerializeField, Range(-5f, 5f)] private float fallGravityStartVelocity = 0f;

    [Header("旋转")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("摄像机参考")]
    public Transform cameraTransform;

    [Header("Input")]
    [SerializeField] private MouseInputGate mouseInputGate;

    [Header("自动前进")]
    [SerializeField] private bool  autoForwardActive;                          // Inspector 可读（调试用）
    [SerializeField] private float autoForwardCameraReturnYawSpeed = 180f;    // yaw 回正速度（度/秒）

    // ── 自动前进状態（RPGCameraController から参照） ──────────────
    /// <summary>自动前进が有効か</summary>
    public bool  AutoForwardActive          => autoForwardActive;
    /// <summary>左键自由镜头中か</summary>
    public bool  AutoForwardFreeLookActive  => _autoForwardFreeLookActive;
    /// <summary>カメラ yaw 回正処理中か</summary>
    public bool  AutoForwardCameraReturnActive => _autoForwardCameraReturnActive;
    /// <summary>自由镜头進入時に固定した前進方向（水平 normalized）</summary>
    public Vector3 AutoForwardLockedForward => _autoForwardLockedForward;
    /// <summary>カメラ回正速度（度/秒）を RPGCameraController が読み取る用</summary>
    public float AutoForwardCameraReturnYawSpeed => autoForwardCameraReturnYawSpeed;

    // ── 自动前进内部状態 ──────────────────────────────────────────
    private bool    _autoForwardFreeLookActive;    // 左键自由镜头中
    private bool    _autoForwardCameraReturnActive; // カメラ回正中
    private Vector3 _autoForwardLockedForward;     // 自由镜头進入時の前進方向
    private bool    _mouseGateWarned;              // Warning 一回だけ

    // ── Rigidbody / Animator ──────────────────────────────────────
    private Rigidbody rb;
    private Animator  anim;
    private bool      isGrounded;

    // ── ジャンプ状態管理 ──────────────────────────────────────────
    private bool _clearIsJumpingNextFrame;
    private bool _jumpConsumed;
    private int  _groundedFrameCount;
    private const int GroundedFrameThreshold = 2;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        rb   = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        rb.freezeRotation = true;
        rb.isKinematic    = false;
        if (anim != null) anim.applyRootMotion = false;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (mouseInputGate == null)
            mouseInputGate = GetComponent<MouseInputGate>()
                          ?? FindFirstObjectByType<MouseInputGate>();
        if (mouseInputGate == null)
        {
            Debug.LogWarning("[PlayerController] MouseInputGate not found. Mouse-based forward/free-look disabled.");
            _mouseGateWarned = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        // ── 着地検出 ───────────────────────────────────────────────
        bool rawGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.2f,
            Vector3.down, 0.35f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        _groundedFrameCount = rawGrounded
            ? Mathf.Min(_groundedFrameCount + 1, GroundedFrameThreshold)
            : 0;
        isGrounded = (_groundedFrameCount >= GroundedFrameThreshold);

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // ── IsJumping 1フレームクリア ──────────────────────────────
        if (_clearIsJumpingNextFrame)
        {
            if (anim != null) anim.SetBool("IsJumping", false);
            _clearIsJumpingNextFrame = false;
        }

        if (isGrounded)
        {
            _jumpConsumed = false;
            if (anim != null) anim.SetBool("IsJumping", false);
        }

        // ── ジャンプ ───────────────────────────────────────────────
        if (keyboard.spaceKey.wasPressedThisFrame && isGrounded && !_jumpConsumed)
        {
            _jumpConsumed            = true;
            _clearIsJumpingNextFrame = true;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            if (anim != null) anim.SetBool("IsJumping", true);
        }

        if (anim != null)
        {
            anim.SetBool ("IsGrounded",      isGrounded);
            anim.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        }

        // ── 自动前进 Toggle（R キー） ──────────────────────────────
        if (keyboard.rKey.wasPressedThisFrame)
        {
            autoForwardActive = !autoForwardActive;
            if (!autoForwardActive)
            {
                // 自动前进 OFF 時に自由镜头 / 回正も全てキャンセル
                _autoForwardFreeLookActive     = false;
                _autoForwardCameraReturnActive = false;
            }
        }

        // ── W 押下で自动前進をキャンセル ──────────────────────────
        // wasPressedThisFrame なので吞み込まない。当フレームの W 入力は FixedUpdate で正常に読まれる。
        if (autoForwardActive && keyboard.wKey.wasPressedThisFrame)
        {
            autoForwardActive              = false;
            _autoForwardFreeLookActive     = false;
            _autoForwardCameraReturnActive = false;
        }

        // ── 左右键双键で自动前進をキャンセル ─────────────────────
        // BothWorldButtonsHeld は UI 起点を除外済み。当フレームの双键前進は FixedUpdate で正常に読まれる。
        if (autoForwardActive && mouseInputGate != null && mouseInputGate.BothWorldButtonsHeld)
        {
            autoForwardActive              = false;
            _autoForwardFreeLookActive     = false;
            _autoForwardCameraReturnActive = false;
        }

        // ── 自由镜头 状態更新（mouseInputGate がある場合のみ） ───────
        if (mouseInputGate != null && autoForwardActive)
        {
            bool leftHeld  = mouseInputGate.LeftWorldHeld;
            bool rightHeld = mouseInputGate.RightWorldHeld;

            // 右键優先：FreeLook 中 or CameraReturn 中どちらでも右键が来たら locked forward を解放
            // BothWorldButtonsHeld の場合は上の打断ブロックで autoForwardActive=false になっているので
            // ここへ来るのは「右键単独」または「左键自由視角中に右键追加で双键にならないケース」は
            // 発生しないため、このブランチは常に「右键単独接管」を意味する。
            if (rightHeld && (_autoForwardFreeLookActive || _autoForwardCameraReturnActive))
            {
                _autoForwardFreeLookActive     = false;
                _autoForwardCameraReturnActive = false;
                // autoForwardActive はそのまま維持（右键単独は打断しない）
            }

            // 左键のみ押されているとき（右键なし）→ 自由镜头モード
            if (leftHeld && !rightHeld)
            {
                if (!_autoForwardFreeLookActive)
                {
                    // 自由镜头 進入：プレイヤーの現在の水平 forward を記録
                    _autoForwardLockedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                    if (_autoForwardLockedForward.sqrMagnitude < 0.001f && cameraTransform != null)
                        _autoForwardLockedForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                    _autoForwardLockedForward      = _autoForwardLockedForward.normalized;
                    _autoForwardFreeLookActive     = true;
                    _autoForwardCameraReturnActive = false; // 回正を中断
                }
            }

            // 左键が離れた → 自由镜头 終了、回正開始
            if (!leftHeld && _autoForwardFreeLookActive)
            {
                _autoForwardFreeLookActive     = false;
                _autoForwardCameraReturnActive = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed) v += 1f;

        // ── 双键前進 / 自动前进（MouseInputGate 必須、fallback なし） ─
        if (mouseInputGate != null)
        {
            if (mouseInputGate.BothWorldButtonsHeld)
                v = Mathf.Max(v, 1f);
            if (autoForwardActive)
                v = Mathf.Max(v, 1f);
        }
        else if (autoForwardActive)
        {
            // MouseInputGate なしでも自动前进の基本 W 相当だけは動かす
            v = Mathf.Max(v, 1f);
        }

        bool  hasMoveInput = (h != 0f || v != 0f);
        bool  isSprinting  = hasMoveInput && keyboard.leftShiftKey.isPressed;
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        // ── 移動方向の決定 ────────────────────────────────────────
        Vector3 dir = Vector3.zero;
        if (hasMoveInput)
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            // 自动前进 + 自由镜头中 or カメラ回正中 かつ右键なし：locked forward を使う
            // _autoForwardCameraReturnActive も含めることで、松手後に相機がまだ回正中でも
            // 角色が相機の自由視角方向に転向しないようにする。
            bool rightHeld = mouseInputGate != null && mouseInputGate.RightWorldHeld;
            bool useLockedFwd = autoForwardActive
                && (_autoForwardFreeLookActive || _autoForwardCameraReturnActive)
                && !rightHeld
                && _autoForwardLockedForward.sqrMagnitude > 0.001f;
            if (useLockedFwd)
            {
                Vector3 lockedFwd = _autoForwardLockedForward;
                Vector3 camRight  = cameraTransform != null
                    ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
                    : transform.right;
                dir = (lockedFwd * v + camRight * h).normalized;
            }
            else if (cameraTransform != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
                dir = (camForward * v + camRight * h).normalized;
            }
            else
            {
                dir = (transform.forward * v + transform.right * h).normalized;
            }
        }

        rb.linearVelocity = new Vector3(dir.x * currentSpeed, rb.linearVelocity.y, dir.z * currentSpeed);
        ApplyJumpGravityTuning();

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }

        if (anim != null)
        {
            float targetAnimSpeed = hasMoveInput ? (isSprinting ? 1f : 0.5f) : 0f;
            anim.SetFloat("Speed",      targetAnimSpeed, 0.1f, Time.fixedDeltaTime);
            anim.SetFloat("Horizontal", 0f,              0.1f, Time.fixedDeltaTime);
            anim.SetBool ("IsSprinting", isSprinting);
        }
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>RPGCameraController から呼ばれる：カメラ回正完了通知</summary>
    public void NotifyCameraReturnComplete()
    {
        _autoForwardCameraReturnActive = false;
    }

    // ─────────────────────────────────────────────────────────────
    private void ApplyJumpGravityTuning()
    {
        if (isGrounded) return;

        Vector3 vel      = rb.linearVelocity;
        float multiplier = vel.y <= fallGravityStartVelocity
            ? fallGravityMultiplier
            : riseGravityMultiplier;

        if (multiplier > 1f)
            vel.y += Physics.gravity.y * (multiplier - 1f) * Time.fixedDeltaTime;

        vel.y = Mathf.Max(vel.y, -maxFallSpeed);
        rb.linearVelocity = vel;
    }
}
