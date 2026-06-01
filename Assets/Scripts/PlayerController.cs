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
    private PlayerCombatFacingController _facingCtrl;

    [Header("摄像机参考")]
    public Transform cameraTransform;

    [Header("Input")]
    [SerializeField] private MouseInputGate mouseInputGate;

    [Header("自动前进")]
    [SerializeField] private bool  autoForwardActive;
    [SerializeField] private float autoForwardCameraReturnYawSpeed = 180f;

    // ── 自动前进状態（RPGCameraController から参照） ──────────────
    public bool    AutoForwardActive              => autoForwardActive;
    public bool    AutoForwardFreeLookActive      => _autoForwardFreeLookActive;
    public bool    AutoForwardCameraReturnActive  => _autoForwardCameraReturnActive;
    public Vector3 AutoForwardLockedForward       => _autoForwardLockedForward;
    public float   AutoForwardCameraReturnYawSpeed => autoForwardCameraReturnYawSpeed;

    // ── 自动前进内部状態 ──────────────────────────────────────────
    private bool    _autoForwardFreeLookActive;
    private bool    _autoForwardCameraReturnActive;
    private Vector3 _autoForwardLockedForward;
    private bool    _mouseGateWarned;
    private bool    _suppressBothMouseBreakUntilReleased;
    private bool    _suppressDirectionalBreakUntilReleased;

    // ── Rigidbody / Animator ──────────────────────────────────────
    private Rigidbody rb;
    private Animator  anim;
    private bool      isGrounded;

    // ── ジャンプ状態管理 ──────────────────────────────────────────
    private bool _clearIsJumpingNextFrame;
    private bool _jumpConsumed;
    private int  _groundedFrameCount;
    private const int GroundedFrameThreshold = 2;

    // ── 死亡ガード ────────────────────────────────────────────────
    private HealthComponent _health;
    private bool            _deathGuardLogged;

    private bool IsPlayerDead() => _health != null && _health.IsDead;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        _facingCtrl   = GetComponent<PlayerCombatFacingController>();
        rb            = GetComponent<Rigidbody>();
        anim          = GetComponent<Animator>();
        _health       = GetComponent<HealthComponent>();
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
        // ■ 死亡ガード：死亡中は Update 全処理をスキップ
        if (IsPlayerDead())
        {
            if (!_deathGuardLogged)
            {
                Debug.Log("[DeathDebug] PlayerController dead guard active, movement blocked");
                _deathGuardLogged = true;
            }
            return;
        }
        _deathGuardLogged = false;

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

        bool hasDirectionalInput = HasManualDirectionalInput(keyboard);

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
            if (autoForwardActive)
            {
                if (mouseInputGate != null && mouseInputGate.BothWorldButtonsHeld)
                    _suppressBothMouseBreakUntilReleased = true;
                if (hasDirectionalInput)
                    _suppressDirectionalBreakUntilReleased = true;
            }
            else
            {
                _autoForwardFreeLookActive             = false;
                _autoForwardCameraReturnActive         = false;
                _suppressBothMouseBreakUntilReleased   = false;
                _suppressDirectionalBreakUntilReleased = false;
            }
        }

        if (!hasDirectionalInput)
            _suppressDirectionalBreakUntilReleased = false;
        if (mouseInputGate == null || !mouseInputGate.BothWorldButtonsHeld)
            _suppressBothMouseBreakUntilReleased = false;

        if (autoForwardActive && hasDirectionalInput && !_suppressDirectionalBreakUntilReleased)
        {
            autoForwardActive                      = false;
            _autoForwardFreeLookActive             = false;
            _autoForwardCameraReturnActive         = false;
            _suppressBothMouseBreakUntilReleased   = false;
            _suppressDirectionalBreakUntilReleased = false;
        }

        if (autoForwardActive
            && mouseInputGate != null
            && mouseInputGate.BothWorldButtonsHeld
            && !_suppressBothMouseBreakUntilReleased)
        {
            autoForwardActive                      = false;
            _autoForwardFreeLookActive             = false;
            _autoForwardCameraReturnActive         = false;
            _suppressBothMouseBreakUntilReleased   = false;
            _suppressDirectionalBreakUntilReleased = false;
        }

        // ── 自由镜头 状態更新 ──────────────────────────────────────
        if (mouseInputGate != null && autoForwardActive)
        {
            bool leftHeld  = mouseInputGate.LeftWorldHeld;
            bool rightHeld = mouseInputGate.RightWorldHeld;

            if (rightHeld && (_autoForwardFreeLookActive || _autoForwardCameraReturnActive))
            {
                _autoForwardFreeLookActive     = false;
                _autoForwardCameraReturnActive = false;
            }

            if (leftHeld && !rightHeld)
            {
                if (!_autoForwardFreeLookActive)
                {
                    _autoForwardLockedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                    if (_autoForwardLockedForward.sqrMagnitude < 0.001f && cameraTransform != null)
                        _autoForwardLockedForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                    _autoForwardLockedForward      = _autoForwardLockedForward.normalized;
                    _autoForwardFreeLookActive     = true;
                    _autoForwardCameraReturnActive = false;
                }
            }

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
        // ■ 死亡ガード：死亡中は移動を完全停止
        if (IsPlayerDead())
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (anim != null)
            {
                anim.SetFloat("Speed",      0f, 0.05f, Time.fixedDeltaTime);
                anim.SetBool ("IsSprinting", false);
            }
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed) v += 1f;

        // ── 双键前進 / 自动前进 ────────────────────────────────────
        if (mouseInputGate != null)
        {
            if (mouseInputGate.BothWorldButtonsHeld)
                v = Mathf.Max(v, 1f);
            if (autoForwardActive)
                v = Mathf.Max(v, 1f);
        }
        else if (autoForwardActive)
        {
            v = Mathf.Max(v, 1f);
        }

        bool  hasMoveInput = (h != 0f || v != 0f);
        bool  isSprinting  = hasMoveInput && keyboard.leftShiftKey.isPressed;
        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        Vector3 dir = Vector3.zero;
        if (hasMoveInput)
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            bool rightHeld   = mouseInputGate != null && mouseInputGate.RightWorldHeld;
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

        if (_facingCtrl != null && _facingCtrl.IsFacingLocked)
        {
            transform.rotation = _facingCtrl.LockedFacingRotation;
        }
        else if (dir.sqrMagnitude > 0.001f)
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
    /// <summary>
    /// 死亡時に PlayerDeathHandler から呼ばれる。
    /// 自動前進・入力フラグをクリアし、Rigidbody と Animator を停止する。
    /// </summary>
    public void StopMovementForDeath()
    {
        Debug.Log("[DeathDebug] StopMovementForDeath called");
        autoForwardActive                      = false;
        _autoForwardFreeLookActive             = false;
        _autoForwardCameraReturnActive         = false;
        _suppressBothMouseBreakUntilReleased   = false;
        _suppressDirectionalBreakUntilReleased = false;
        _jumpConsumed                          = true;

        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (anim != null)
        {
            anim.SetFloat("Speed",      0f);
            anim.SetFloat("Horizontal", 0f);
            anim.SetBool ("IsSprinting", false);
        }
    }

    // ─────────────────────────────────────────────────────────────
    /// <summary>RPGCameraController から呼ばれる：カメラ回正完了通知</summary>
    public void NotifyCameraReturnComplete()
    {
        _autoForwardCameraReturnActive = false;
    }

    // ─────────────────────────────────────────────────────────────
    private bool HasManualDirectionalInput(Keyboard keyboard)
    {
        if (keyboard == null) return false;
        return keyboard.wKey.isPressed
            || keyboard.aKey.isPressed
            || keyboard.sKey.isPressed
            || keyboard.dKey.isPressed;
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
