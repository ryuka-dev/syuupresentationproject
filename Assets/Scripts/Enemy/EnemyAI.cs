using UnityEngine;

public enum EnemyState { Idle, Chase, Attack }

/// <summary>
/// 敌人 AI - 有限状态机（Idle / Chase / Attack）
/// 攻击触发：attackCooldownTimer 到 0 时设 IsAttacking=true 一帧，动画开始后立刻清除，
/// 依赖 Animator Controller 的 hasExitTime=0.9 完成动画后自动回 Idle，防止双触发。
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
    public float attackCooldown = 2f;

    [Header("动画")]
    public Animator animator;

    // 状态
    public EnemyState currentState { get; private set; } = EnemyState.Idle;

    private Transform       currentTarget;
    private Vector3         moveDirection = Vector3.zero;
    private FactionComponent myFaction;
    private float           attackCooldownTimer = 0f;

    // ─── 生命周期 ────────────────────────────────────────────
    void Awake()
    {
        if (animator    == null) animator    = GetComponent<Animator>();
        if (fovDetector == null) fovDetector = GetComponent<FOVDetector>();
        myFaction = GetComponent<FactionComponent>();
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

        foreach (var fc in FindObjectsOfType<FactionComponent>()) {
            // 排除自己
            if (fc.gameObject == gameObject) continue;

            // 使用阵营系统判断敌对关系
            if (myFaction != null && !myFaction.ShouldAttack(fc.faction)) continue;

            if (fovDetector.CanSeeTarget(fc.transform)) {
                currentTarget = fc.transform;
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
                // timer = 0：进入攻击范围立即允许触发第一次攻击
                attackCooldownTimer = 0f;
                animator?.SetBool("IsAttacking", false);
                break;
        }
    }

    /// <summary>
    /// 由攻击动画第 20 帧的 Animation Event 调用，负责伤害判定。
    /// </summary>
    public void OnAttackHit()
    {
        if (currentTarget == null) return;

        // 基于 currentTarget 实时获取，避免缓存失效
        var health = currentTarget.GetComponent<HealthComponent>();
        if (health == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange * 1.2f) {
            health.TakeDamage(attackDamage);
        }
    }

    // ─── 物理更新 ────────────────────────────────────────────
    void FixedUpdate()
    {
        if (rb == null) return;

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
                moveDirection = Vector3.zero;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                FaceTarget();

                if (animator != null) {
                    bool inAttackAnim = animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");

                    if (inAttackAnim) {
                        // 动画已经开始 → 立刻清除 bool，防止动画结束后因 bool=true 再次触发
                        animator.SetBool("IsAttacking", false);
                    } else if (attackCooldownTimer <= 0f) {
                        // 冷却完成且动画未在播放 → 触发一次攻击
                        // 立刻重置冷却，防止下帧在动画尚未启动时再次满足条件
                        attackCooldownTimer = attackCooldown;
                        animator.SetBool("IsAttacking", true);
                    }
                }
                break;
        }

        animator?.SetFloat("Speed", moveDirection.magnitude, 0.1f, Time.fixedDeltaTime);
    }

    void ChaseTarget()
    {
        if (currentTarget == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist <= stoppingDistance) {
            // 已到停止距离，原地朝向目标
            moveDirection = Vector3.zero;
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            FaceTarget();
            return;
        }

        moveDirection = (currentTarget.position - transform.position).normalized;
        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed);
        FaceTarget();
    }

    // FaceTarget 只在 FixedUpdate 中调用，使用 fixedDeltaTime
    void FaceTarget()
    {
        if (currentTarget == null) return;
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                rotationSpeed * Time.fixedDeltaTime);
    }
}
