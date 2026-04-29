using UnityEngine;

public enum EnemyState { Idle, Chase, Attack }

/// <summary>
/// 敌人 AI - 有限状态机
/// 成熟方案：每帧检查 Animator 状态，动画伤害由 Animation Event 在第20帧触发
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("检测")]
    public FOVDetector fovDetector;

    [Header("移动")]
    public float moveSpeed        = 3.5f;
    public float stoppingDistance = 1.2f;
    public float rotationSpeed    = 6f;
    public Rigidbody rb;

    [Header("攻击")]
    public float attackRange  = 1.5f;
    public float attackDamage = 10f;
    [Tooltip("两次攻击之间的冷却时间（秒）")]
    public float attackCooldown = 1.5f;


    [Header("动画")]
    public Animator animator;

    // 状态
    public EnemyState currentState { get; private set; } = EnemyState.Idle;

    private Transform currentTarget;
    private Vector3   moveDirection = Vector3.zero;
    private HealthComponent targetHealth;
    private float attackCooldownTimer = 0f;


    // ─── 生命周期 ────────────────────────────────────────────
    void Awake()
    {
        if (animator    == null) animator    = GetComponent<Animator>();
        if (fovDetector == null) fovDetector = GetComponent<FOVDetector>();
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        ScanForTarget();
        UpdateState();
    }

    // ─── 目标扫描 ────────────────────────────────────────────
    void ScanForTarget()
    {
        if (fovDetector == null) return;

        currentTarget = null;
        targetHealth  = null;

        foreach (var fc in FindObjectsOfType<FactionComponent>()) {
            if (fovDetector.CanSeeTarget(fc.transform)) {
                currentTarget = fc.transform;
                targetHealth  = fc.GetComponent<HealthComponent>();
                break;
            }
        }
    }

    // ─── 状态机 ──────────────────────────────────────────────
    void UpdateState()
    {
        if (currentTarget == null) {
            TransitionTo(EnemyState.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        TransitionTo(dist <= attackRange ? EnemyState.Attack : EnemyState.Chase);
    }

    void TransitionTo(EnemyState next)
    {
        if (currentState == next) return;
        currentState = next;

        switch (next) {
            case EnemyState.Idle:
            case EnemyState.Chase:
                animator?.SetBool("IsAttacking", false);
                break;

            case EnemyState.Attack:
                // 进入攻击状态，立即触发动画
                animator?.SetBool("IsAttacking", true);
                break;
        }
    }

    /// <summary>
    /// 由动画第20帧的 Animation Event 调用 - 伤害触发点
    /// </summary>
public void OnAttackHit()
    {
        Debug.Log($"[EnemyAI] OnAttackHit called. target={currentTarget?.name} targetHealth={targetHealth}");
        if (targetHealth == null || currentTarget == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        Debug.Log($"[EnemyAI] dist={dist:F2} attackRange*1.2={attackRange * 1.2f:F2}");
        if (dist <= attackRange * 1.2f)
            targetHealth.TakeDamage(attackDamage);
    }

    // ─── 物理移动 ────────────────────────────────────────────
void FixedUpdate()
    {
        if (rb == null) return;

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.fixedDeltaTime;

        switch (currentState) {
            case EnemyState.Idle:
                moveDirection = Vector3.zero;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                break;

            case EnemyState.Chase:
                ChaseTarget();
                break;

            case EnemyState.Attack:
                if (animator != null) {
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    bool animPlaying = state.IsName("Attack");

                    if (!animPlaying) {
                        // 动画已结束，必须先重置 IsAttacking
                        // 否则 Animator 会立刻再次进入 Attack，冷却无效
                        animator.SetBool("IsAttacking", false);

                        if (attackCooldownTimer <= 0f) {
                            // 冷却到期，触发下一次攻击
                            attackCooldownTimer = attackCooldown;
                            animator.SetBool("IsAttacking", true);
                        }
                    }
                }
                moveDirection = Vector3.zero;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                FaceTarget();
                break;
        }

        animator?.SetFloat("Speed", moveDirection.magnitude, 0.1f, Time.deltaTime);
    }

    void ChaseTarget()
    {
        if (currentTarget == null) return;
        moveDirection = (currentTarget.position - transform.position).normalized;
        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed);
        FaceTarget();
    }

    void FaceTarget()
    {
        if (currentTarget == null) return;
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                rotationSpeed * Time.deltaTime);
    }
}
