using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyState { Idle, Chase, Attack, ReturnToSpawn, Wander }

/// <summary>
/// 敌人 AI - 有限状态机（Idle / Wander / Chase / Attack / ReturnToSpawn）
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
    [SerializeField] private float disengageDistance    = 15f;
    [SerializeField] private float disengageDelay       = 3f;
    private float disengageTimer = 0f;

    [Header("返回出生点")]
    [SerializeField] private float returnToSpawnStopDistance = 0.5f;

    [Header("活动范围")]
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private float leashRadius  = 25f;

    [Header("游荡")]
    [Tooltip("游荡移动速度（建议低于 moveSpeed）")]
    [SerializeField] private float wanderMoveSpeed          = 2f;
    [Tooltip("判定到达游荡目标点的距离阈值")]
    [SerializeField] private float wanderPointReachDistance = 0.8f;
    [Tooltip("每次待机的最短时间（秒）")]
    [SerializeField] private float minIdleTime              = 2f;
    [Tooltip("每次待机的最长时间（秒）")]
    [SerializeField] private float maxIdleTime              = 5f;

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

    // ─── 出生点 ──────────────────────────────────────────────
    private Vector3    _spawnPosition;
    private Quaternion _spawnRotation;

    // ─── 游荡状态内部变量 ────────────────────────────────────
    private Vector3 _wanderTarget;
    private float   _idleTimer = 0f;

    // ─── 生命周期 ────────────────────────────────────────────
    void Awake()
    {
        if (animator    == null) animator    = GetComponent<Animator>();
        if (fovDetector == null) fovDetector = GetComponent<FOVDetector>();
        myFaction = GetComponent<FactionComponent>();
        myHealth  = GetComponent<HealthComponent>();

        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
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

        // 初期 Idle タイマーを設定（ゲーム開始直後から Wander サイクルを動かす）
        SetupIdleTimer();

        if (wanderRadius > 0f && wanderRadius > leashRadius)
            Debug.LogWarning($"[EnemyAI] {gameObject.name}: wanderRadius({wanderRadius}) が leashRadius({leashRadius}) を超えています。wanderRadius <= leashRadius に設定してください。");
    }

    void Update()
    {
        // ReturnToSpawn 中はスキャン・脱戦・状態更新を行わない
        if (currentState == EnemyState.ReturnToSpawn) return;

        // leashRadius 超過チェック：超えたら即座に帰還
        CheckLeashRadius();
        if (currentState == EnemyState.ReturnToSpawn) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            ScanForTarget();
            scanTimer = scanInterval;
        }
        UpdateDisengage();
        UpdateState();

        // Idle / Wander サイクル更新（ターゲットがいない場合）
        if (currentState == EnemyState.Idle || currentState == EnemyState.Wander)
            UpdateIdleWanderCycle();
    }

    // ─── 脱战计时 ────────────────────────────────────────────
    /// <summary>
    /// 距离脱战计时：目标持续超出 disengageDistance 超过 disengageDelay 秒后，
    /// 进入 ReturnToSpawn 状态自行走回出生点。
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
                disengageTimer = 0f;
                EnterReturnToSpawn();
            }
        }
        else
        {
            disengageTimer = 0f;
        }
    }

    // ─── 返回出生点 ──────────────────────────────────────────
    /// <summary>
    /// 进入返回出生点状态。清空仇恨，停止追击，开始自行走回出生点。
    /// 不恢复满血，不瞬间传送。
    /// </summary>
    private void EnterReturnToSpawn()
    {
        hateTable.Clear();
        currentTarget       = null;
        disengageTimer      = 0f;
        attackCooldownTimer = 0f;
        currentState        = EnemyState.ReturnToSpawn;
        animator?.SetBool("IsAttacking", false);
        Debug.Log($"[EnemyAI] {gameObject.name} 开始返回出生点。");
    }

    /// <summary>
    /// 敵と出生中心点の XZ 水平距離を返す。
    /// Y 軸の高低差は活動範囲判定に影響させない。
    /// </summary>
    private float GetHorizontalDistanceFromSpawn()
    {
        Vector3 currentFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 spawnFlat   = new Vector3(_spawnPosition.x,     0f, _spawnPosition.z);
        return Vector3.Distance(currentFlat, spawnFlat);
    }

    /// <summary>
    /// leashRadius 超過チェック。
    /// 出生中心から leashRadius を超えた場合は即座に EnterReturnToSpawn() を呼ぶ。
    /// ReturnToSpawn 状態中および死亡時は何もしない。
    /// </summary>
    private void CheckLeashRadius()
    {
        if (currentState == EnemyState.ReturnToSpawn) return;
        if (!enabled) return;
        if (myHealth != null && myHealth.IsDead) return;

        if (GetHorizontalDistanceFromSpawn() > leashRadius)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} 超过活动边界（leashRadius={leashRadius}），开始返回出生点。");
            EnterReturnToSpawn();
        }
    }

    /// <summary>
    /// ReturnToSpawn 状态每帧处理（在 FixedUpdate 中调用）。
    /// 走向出生点，到达后精确归位、恢复满血并进入 Idle。
    /// </summary>
    private void HandleReturnToSpawn()
    {
        if (!enabled) return;

        // 水平距離のみで到達判定（Y 軸の高さ差は無視）
        Vector3 currentFlat  = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 spawnFlat    = new Vector3(_spawnPosition.x,     0f, _spawnPosition.z);
        float   horizontalDist = Vector3.Distance(currentFlat, spawnFlat);

        if (horizontalDist > returnToSpawnStopDistance)
        {
            // X/Z 方向のみで移動（Y 軸は重力に任せる）
            moveDirection = new Vector3(
                _spawnPosition.x - transform.position.x,
                0f,
                _spawnPosition.z - transform.position.z).normalized;

            rb.linearVelocity = new Vector3(
                moveDirection.x * moveSpeed,
                rb.linearVelocity.y,
                moveDirection.z * moveSpeed);

            if (moveDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.LookRotation(moveDirection),
                    rotationSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // 到達：X/Z のみ修正、Y は現在値を維持（地形に着地したまま）
            transform.position = new Vector3(
                _spawnPosition.x,
                transform.position.y,
                _spawnPosition.z);
            transform.rotation = _spawnRotation;

            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (myHealth != null)
                myHealth.RestoreFullHealth();

            currentState        = EnemyState.Idle;
            attackCooldownTimer = 0f;
            moveDirection       = Vector3.zero;
            SetupIdleTimer();   // 帰還後に Idle タイマーをリセット
            animator?.SetBool("IsAttacking", false);
            animator?.SetFloat("Speed", 0f);
            Debug.Log($"[EnemyAI] {gameObject.name} 已回到出生点，重置完成。");
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
        SelectHighestHateTarget();

        if (currentTarget != null) return;

        if (fovDetector == null) return;
        foreach (var fc in FindObjectsOfType<FactionComponent>())
        {
            if (fc.gameObject == gameObject) continue;
            if (myFaction != null && !myFaction.ShouldAttack(fc.faction)) continue;
            if (fovDetector.CanSeeTarget(fc.transform))
            {
                if (!hateTable.ContainsKey(fc.transform))
                    AddHate(fc.transform, sightHateAmount);
                break;
            }
        }
    }

    /// <summary>自身受击时由 HealthComponent.OnDamaged 回调。</summary>
    void HandleDamaged(float amount, Transform attacker)
    {
        AddHate(attacker, amount * damageHateMultiplier);
    }

    // ─── 状态机 ────────────────────────────────────────────
    void UpdateState()
    {
        if (currentState == EnemyState.ReturnToSpawn) return;

        // 有効なターゲットがいる場合は Chase / Attack へ
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            TransitionTo(dist <= attackRange ? EnemyState.Attack : EnemyState.Chase);
            return;
        }

        // ターゲットなし：Idle / Wander サイクルは UpdateIdleWanderCycle() に任せる
        if (currentState == EnemyState.Idle || currentState == EnemyState.Wander) return;

        // Chase / Attack 中にターゲットを失った → Idle へ戻して Wander サイクルを再開
        TransitionTo(EnemyState.Idle);
    }

    void TransitionTo(EnemyState next)
    {
        if (currentState == next) return;
        currentState = next;
        switch (next)
        {
            case EnemyState.Idle:
                animator?.SetBool("IsAttacking", false);
                SetupIdleTimer();   // Idle 遷移時に毎回タイマーをリセット
                break;
            case EnemyState.Wander:
            case EnemyState.Chase:
            case EnemyState.ReturnToSpawn:
                animator?.SetBool("IsAttacking", false);
                break;
            case EnemyState.Attack:
                attackCooldownTimer = 0f;
                animator?.SetBool("IsAttacking", false);
                break;
        }
    }

    // ─── Idle / Wander サイクル ──────────────────────────────
    /// <summary>Idle タイマーをランダム時間でリセットする。</summary>
    private void SetupIdleTimer()
    {
        _idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    /// <summary>
    /// Idle / Wander サイクルを管理する。Update() の末尾から毎フレーム呼ばれる。
    /// ターゲットがいない場合にのみ有効。
    /// </summary>
    private void UpdateIdleWanderCycle()
    {
        // ターゲットが出現した場合は UpdateState() が遷移させるので何もしない
        if (currentTarget != null) return;

        if (currentState == EnemyState.Idle)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                // wanderRadius <= 0 の場合はゲームまま待機（エラーなし）
                if (wanderRadius <= 0f)
                {
                    SetupIdleTimer();
                    return;
                }

                // 游荡目标点を選択して Wander へ遷移
                if (TryPickWanderPoint(out Vector3 point))
                {
                    _wanderTarget = point;
                    TransitionTo(EnemyState.Wander);
                }
                else
                {
                    // 有効な点が見つからない場合はタイマーをリセットして次回再試行
                    SetupIdleTimer();
                }
            }
        }
        else if (currentState == EnemyState.Wander)
        {
            // 目标点に到達したら Idle へ戻す
            Vector3 flatPos    = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatTarget = new Vector3(_wanderTarget.x,      0f, _wanderTarget.z);
            if (Vector3.Distance(flatPos, flatTarget) <= wanderPointReachDistance)
            {
                TransitionTo(EnemyState.Idle);
            }
        }
    }

    /// <summary>
    /// _spawnPosition 周囲の XZ 平面でランダムな游荡目标点を選ぶ。
    /// leashRadius 内に収まる点を最大 10 回試行する。
    /// </summary>
    private bool TryPickWanderPoint(out Vector3 point)
    {
        point = Vector3.zero;
        for (int i = 0; i < 10; i++)
        {
            Vector2 circle    = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = new Vector3(
                _spawnPosition.x + circle.x,
                transform.position.y,   // Y は現在位置を維持（重力に任せる）
                _spawnPosition.z + circle.y);

            // leashRadius チェック（XZ のみ）
            float distFromSpawn = Vector3.Distance(
                new Vector3(candidate.x, 0f, candidate.z),
                new Vector3(_spawnPosition.x, 0f, _spawnPosition.z));

            if (distFromSpawn <= leashRadius)
            {
                point = candidate;
                return true;
            }
        }
        return false;
    }

    /// <summary>由攻击动画第 20 帧的 Animation Event 调用。方法名不可改。</summary>
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
                moveDirection     = Vector3.zero;
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                break;
            case EnemyState.Wander:
                HandleWanderMovement();
                break;
            case EnemyState.Chase:
                ChaseTarget();
                break;
            case EnemyState.Attack:
                moveDirection     = Vector3.zero;
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
            case EnemyState.ReturnToSpawn:
                HandleReturnToSpawn();
                break;
        }
        animator?.SetFloat("Speed", moveDirection.magnitude, 0.1f, Time.fixedDeltaTime);
    }

    /// <summary>Wander 状態の移動処理。XZ のみ制御し Y は重力に任せる。</summary>
    private void HandleWanderMovement()
    {
        Vector3 flatPos    = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(_wanderTarget.x,      0f, _wanderTarget.z);
        float   dist       = Vector3.Distance(flatPos, flatTarget);

        if (dist > wanderPointReachDistance)
        {
            moveDirection = new Vector3(
                _wanderTarget.x - transform.position.x,
                0f,
                _wanderTarget.z - transform.position.z).normalized;

            rb.linearVelocity = new Vector3(
                moveDirection.x * wanderMoveSpeed,
                rb.linearVelocity.y,
                moveDirection.z * wanderMoveSpeed);

            if (moveDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.LookRotation(moveDirection),
                    rotationSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // 到達済み（UpdateIdleWanderCycle が次フレームで Idle へ遷移させる）
            moveDirection     = Vector3.zero;
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    void ChaseTarget()
    {
        if (currentTarget == null) return;
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= stoppingDistance)
        {
            moveDirection     = Vector3.zero;
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

    // ─── Debug / 强制复位 ────────────────────────────────────
    /// <summary>
    /// 强制瞬间重置敌人到出生点。Debug 菜单 / 强制复位专用接口。
    /// 走回出生点的正式流程请使用 EnterReturnToSpawn()。
    /// </summary>
    public void ResetToSpawn()
    {
        hateTable.Clear();
        currentTarget  = null;
        disengageTimer = 0f;

        transform.position = _spawnPosition;
        transform.rotation = _spawnRotation;

        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (myHealth != null)
            myHealth.RestoreFullHealth();

        currentState        = EnemyState.Idle;
        attackCooldownTimer = 0f;
        moveDirection       = Vector3.zero;
        SetupIdleTimer();   // リセット後に Idle タイマーを再設定
        animator?.SetBool("IsAttacking", false);
        animator?.SetFloat("Speed", 0f);
    }

    /// <summary>
    /// 外部から敌人を強制脱戦させ、出生点へ帰還させる。
    /// プレイヤー死亡時など、EnemyWorldManager から呼び出す想定。
    /// 瞬間移動なし。歩いて帰る正式帰還フローを使用する。
    /// </summary>
    public void ForceDisengageAndReturnToSpawn()
    {
        if (!enabled) return;
        if (myHealth != null && myHealth.IsDead) return;
        if (currentState == EnemyState.ReturnToSpawn) return;

        Debug.Log($"[EnemyAI] {gameObject.name} 外部指令により強制脱戦、出生点へ帰還。");
        EnterReturnToSpawn();
    }
}
