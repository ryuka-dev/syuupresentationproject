using System.Collections.Generic;
using System.Linq;
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

    [Header("仇恨")]
    [SerializeField] private float sightHateAmount      = 10f;
    [SerializeField] private float damageHateMultiplier = 1f;
    [SerializeField] private float disengageDistance = 15f;
    [SerializeField] private float disengageDelay    = 3f;
    private float disengageTimer = 0f;


    // 状态
    public EnemyState currentState { get; private set; } = EnemyState.Idle;

    private Transform        currentTarget;
    private Vector3          moveDirection = Vector3.zero;
    private FactionComponent myFaction;
    private HealthComponent  myHealth;
    private float            attackCooldownTimer = 0f;

    // 仇恨列表：key=目标 Transform，value=仇恨值
    private readonly Dictionary<Transform, float> hateTable = new Dictionary<Transform, float>();

    // 扫描频率控制
    private float scanTimer = 0f;
    private const float scanInterval = 0.2f;

    // ─── 生命周期 ────────────────────────────────────────────
    void Awake()
    {
        if (animator    == null) animator    = GetComponent<Animator>();
        if (fovDetector == null) fovDetector = GetComponent<FOVDetector>();
        myFaction = GetComponent<FactionComponent>();
        myHealth  = GetComponent<HealthComponent>();
    }

    void OnEnable()
    {
        if (myHealth != null) myHealth.OnDamaged += HandleDamaged;
    }

    void OnDisable()
    {
        if (myHealth != null) myHealth.OnDamaged -= HandleDamaged;
    }

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            ScanForTarget();
            scanTimer = scanInterval;
        }
        UpdateDisengage();
        UpdateState();
    }

/// <summary>
    /// 距离脱战逗计时：如果当前目标持续远于 disengageDistance，超时后清除该目标。
    /// </summary>
    void UpdateDisengage()
    {
        if (currentTarget == null)
        {
            disengageTimer = 0f;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > disengageDistance)
        {
            disengageTimer += Time.deltaTime;
            if (disengageTimer >= disengageDelay)
            {
                // 超过脱战时间，从仇恨列表移除该目标并重新选择
                hateTable.Remove(currentTarget);
                disengageTimer = 0f;
                SelectHighestHateTarget();
            }
        }
        else
        {
            disengageTimer = 0f;
        }
    }


    // ─── 仇恨系统 ────────────────────────────────────────────
    /// <summary>目标是否有效：存在、有 HealthComponent、未死亡、阵营敌对。</summary>
    private bool IsValidTarget(Transform t)
    {
        if (t == null) return false;
        var h = t.GetComponent<HealthComponent>();
        if (h == null || h.IsDead) return false;
        if (myFaction == null) return false;
        var fc = t.GetComponent<FactionComponent>();
        if (fc == null) return false;
        return myFaction.ShouldAttack(fc.faction);
    }

    /// <summary>统一入口：对指定目标增加仇恨值，然后重新选中目标。</summary>
    private void AddHate(Transform target, float amount)
    {
        if (!IsValidTarget(target)) return;

        if (hateTable.ContainsKey(target))
            hateTable[target] += amount;
        else
            hateTable[target] = amount;

        SelectHighestHateTarget();
    }

    /// <summary>清除已无效的仇恨记录。</summary>
    private void RemoveInvalidHateTargets()
    {
        var invalid = hateTable.Keys.Where(t => !IsValidTarget(t)).ToList();
        foreach (var t in invalid)
            hateTable.Remove(t);
    }

    /// <summary>选择仇恨值最高的有效目标作为当前追击目标。</summary>
    private void SelectHighestHateTarget()
    {
        RemoveInvalidHateTargets();

        if (hateTable.Count == 0)
        {
            currentTarget = null;
            return;
        }

        currentTarget = hateTable.OrderByDescending(kv => kv.Value).First().Key;
    }

    // ─── 目标扫描 ────────────────────────────────────────────
    void ScanForTarget()
    {
        // 先确保当前目标为列表中最高有效目标
        SelectHighestHateTarget();

        // 如果已有仇恨目标，不需要重新 FOV 扫描
        if (currentTarget != null) return;

        // 仇恨列表为空时，才通过 FOV 寻找新目标
        if (fovDetector == null) return;
        foreach (var fc in FindObjectsOfType<FactionComponent>())
        {
            if (fc.gameObject == gameObject) continue;
            if (myFaction != null && !myFaction.ShouldAttack(fc.faction)) continue;
            if (fovDetector.CanSeeTarget(fc.transform))
            {
                // 只有不在列表中的目标才加基础仇恨，防止每帧重复叠加
                if (!hateTable.ContainsKey(fc.transform))
                    AddHate(fc.transform, sightHateAmount);
                break;
            }
        }
    }

    /// <summary>自身受击时由 HealthComponent.OnDamaged 回调。</summary>
    void HandleDamaged(float amount, Transform attacker)
    {
        // 攻击来源直接计入仇恨，不局限于是否已有目标
        AddHate(attacker, amount * damageHateMultiplier);
    }

    // ─── 状态机 ────────────────────────────────────────────
    void UpdateState()
    {
        if (currentTarget == null)
        {
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
        switch (next)
        {
            case EnemyState.Idle:
            case EnemyState.Chase:
                animator?.SetBool("IsAttacking", false);
                break;
            case EnemyState.Attack:
                attackCooldownTimer = 0f;
                animator?.SetBool("IsAttacking", false);
                break;
        }
    }

    /// <summary>由攻击动画第 20 帧的 Animation Event 调用。</summary>
    public void OnAttackHit()
    {
        if (!enabled || currentTarget == null) return;
        var health = currentTarget.GetComponent<HealthComponent>();
        if (health == null) return;
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange * 1.2f)
            health.TakeDamage(attackDamage);
    }

    // ─── 物理更新 ────────────────────────────────────────────
    void FixedUpdate()
    {
        if (rb == null) return;
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.fixedDeltaTime;

        switch (currentState)
        {
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
                if (animator != null)
                {
                    bool inAttackAnim = animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
                    if (inAttackAnim)
                        animator.SetBool("IsAttacking", false);
                    else if (attackCooldownTimer <= 0f)
                    {
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
        if (dist <= stoppingDistance)
        {
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
