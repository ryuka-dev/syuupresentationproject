using UnityEngine;

/// <summary>
/// 敌人有限状态机
/// Idle ──(进入视野)──→ Chase ──(进入攻击范围)──→ Attack
///   ↑                    │                            │
///   └──(丢失目标)─────────┘◄──(离开攻击范围,视野内)───┘
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FOVDetector))]
[RequireComponent(typeof(EntityStats))]
public class EnemyStateMachine : MonoBehaviour
{
    public enum State { Idle, Chase, Attack }

    [Header("移动")]
    public float moveSpeed      = 3.5f;
    public float rotationSpeed  = 5f;

    [Header("攻击")]
    public float attackRange    = 1.5f;
    public float attackDamage   = 10f;
    public float attackCooldown = 1.5f;

    [Header("调试")]
    public State currentState = State.Idle;

    // 内部引用
    private Rigidbody   rb;
    private FOVDetector fov;
    private Animator    anim;
    private EntityStats stats;
    private Transform   currentTarget;
    private float       lastAttackTime;
    private Vector3     moveDirection;

    void Start()
    {
        rb    = GetComponent<Rigidbody>();
        fov   = GetComponent<FOVDetector>();
        anim  = GetComponent<Animator>();
        stats = GetComponent<EntityStats>();

        stats.OnDeath += HandleDeath;
    }

    void Update()
    {
        if (stats.IsDead) return;

        // 扫描目标
        ScanForTarget();

        // 驱动状态转换
        switch (currentState)
        {
            case State.Idle:   UpdateIdle();   break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
        }
    }

    void FixedUpdate()
    {
        if (stats.IsDead || currentState != State.Chase) return;

        // 水平移动
        rb.linearVelocity = new Vector3(
            moveDirection.x * moveSpeed,
            rb.linearVelocity.y,
            moveDirection.z * moveSpeed);

        // 水平转向
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Vector3 lookDir = moveDirection;
            lookDir.y = 0;
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(lookDir),
                rotationSpeed * Time.deltaTime);
        }
    }

    // ── 状态扫描 ──────────────────────────────────────────────
    void ScanForTarget()
    {
        currentTarget = null;
        foreach (var fc in FindObjectsOfType<FactionComponent>())
        {
            if (fov.CanSeeTarget(fc.transform))
            {
                currentTarget = fc.transform;
                break;
            }
        }
    }

    // ── Idle ──────────────────────────────────────────────────
    void UpdateIdle()
    {
        SetVelocityZero();
        SetAnimSpeed(0);

        if (currentTarget != null)
            TransitionTo(State.Chase);
    }

    // ── Chase ─────────────────────────────────────────────────
    void UpdateChase()
    {
        if (currentTarget == null) { TransitionTo(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist <= attackRange)
        {
            SetVelocityZero();
            TransitionTo(State.Attack);
            return;
        }

        moveDirection = (currentTarget.position - transform.position).normalized;
        SetAnimSpeed(1);
    }

    // ── Attack ────────────────────────────────────────────────
    void UpdateAttack()
    {
        if (currentTarget == null) { TransitionTo(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        // 目标跑远 → 继续追
        if (dist > attackRange)
        {
            TransitionTo(currentTarget != null ? State.Chase : State.Idle);
            return;
        }

        // 面向目标
        Vector3 lookDir = (currentTarget.position - transform.position);
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(lookDir),
                rotationSpeed * Time.deltaTime);

        SetAnimSpeed(0);

        // 攻击冷却
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            PerformAttack();
        }
    }

    // ── 攻击逻辑 ──────────────────────────────────────────────
    void PerformAttack()
    {
        if (anim != null)
            anim.SetTrigger("Attack");

        if (currentTarget == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange)
        {
            var targetStats = currentTarget.GetComponent<EntityStats>();
            targetStats?.TakeDamage(attackDamage);
        }
    }

    // ── 工具方法 ──────────────────────────────────────────────
    void TransitionTo(State next)
    {
        currentState = next;
        if (next == State.Idle || next == State.Attack)
            SetVelocityZero();
    }

    void SetVelocityZero()
    {
        if (rb != null)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        moveDirection = Vector3.zero;
    }

    void SetAnimSpeed(float speed)
    {
        if (anim != null)
            anim.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    }

    void HandleDeath()
    {
        currentState = State.Idle;
        SetVelocityZero();
        if (anim != null) anim.SetTrigger("Death");
        GetComponent<Collider>().enabled = false;
        rb.isKinematic = true;
        Destroy(gameObject, 3f);
    }
}
