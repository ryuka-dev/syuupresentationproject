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
    [Tooltip("下落阶段に追加する重力倍率。大きいほど下落が速い。デフォルト 2.5。")]
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [Tooltip("上昇面の追加重力倍率。1.0 は変更なし。")]
    [SerializeField] private float riseGravityMultiplier = 1.0f;
    [Tooltip("最大下落速度。过大なクラッシュを防ぐ。デフォルト 25。")]
    [SerializeField] private float maxFallSpeed          = 25f;
    [Tooltip("Y velocity がこの値以下になったら fallGravityMultiplier に切替。0=最高点到達後。正値=上昇中でも早めに切替。")]
    [SerializeField, Range(-5f, 5f)] private float fallGravityStartVelocity = 0f;

    [Header("旋转")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("摄像机参考")]
    public Transform cameraTransform;

    [Header("Input")]
    [SerializeField] private MouseInputGate mouseInputGate;

    private Rigidbody rb;
    private Animator  anim;
    private bool      isGrounded;

    // ─── ジャンプ状態管理 ──────────────────────────────────────
    private bool _clearIsJumpingNextFrame = false;
    private bool _jumpConsumed = false;
    private int  _groundedFrameCount    = 0;
    private const int GroundedFrameThreshold = 2;

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
            mouseInputGate = GetComponentInChildren<MouseInputGate>()
                          ?? FindFirstObjectByType<MouseInputGate>();
        if (mouseInputGate == null)
            Debug.LogWarning("[PlayerController] MouseInputGate not found. Dual-button forward will be disabled.");
    }

    void Update()
    {
        // ─── 着地検出 ──────────────────────────────────────────
        bool rawGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.2f,
            Vector3.down, 0.35f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        if (rawGrounded)
            _groundedFrameCount = Mathf.Min(_groundedFrameCount + 1, GroundedFrameThreshold);
        else
            _groundedFrameCount = 0;

        isGrounded = (_groundedFrameCount >= GroundedFrameThreshold);

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

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
    }

    void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.aKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed) v += 1f;

        // LMB + RMB = move toward camera forward (MMO dual-button forward)
        // 両方ともワールド起点で押されている場合のみ前進。UI 起点は無視。
        if (mouseInputGate != null)
        {
            if (mouseInputGate.BothWorldButtonsHeld)
                v = Mathf.Max(v, 1f);
        }
        else
        {
            // fallback: MouseInputGate がない場合は旧ロジック（UI 区別なし）
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && mouse.rightButton.isPressed)
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
            if (cameraTransform != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
                dir = (camForward * v + camRight * h).normalized;
            }
            else
            {
                Debug.LogWarning("[PlayerController] cameraTransform not set.");
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
