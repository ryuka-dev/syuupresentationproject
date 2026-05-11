using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Idle, Chase, Attack, ReturnToSpawn, Wander }

/// <summary>
/// 敌人 AI - 有限状态机（Idle / Wander / Chase / Attack / ReturnToSpawn）
/// Wander / Chase / ReturnToSpawn は NavMeshAgent を優先使用。不可の場合は Rigidbody fallback。
/// Chase 中は目標位置が一時的に不可達でも Agent Chase を維持し、最後の有効 destination を追い続ける。
/// 攻击触发：attackCooldownTimer 到 0 时设 IsAttacking=true 一帧，动画开始后立刻清除。
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
    [SerializeField] private float returnToSpawnStopDistance   = 0.5f;
    [Tooltip("出生点の NavMesh サンプリング最大距離（m）")]
    [SerializeField] private float returnNavMeshSampleDistance = 3f;

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

    [Header("NavMesh Chase")]
    [Tooltip("Chase 中に Agent destination を更新する間隔（秒）")]
    [SerializeField] private float chaseDestinationUpdateInterval = 0.2f;
    [Tooltip("目標位置の NavMesh サンプリング最大距離（m）")]
    [SerializeField] private float chaseNavMeshSampleDistance     = 2f;

    // 状态
    public EnemyState currentState { get; private set; } = EnemyState.Idle;

    private Transform        currentTarget;
    private Vector3          moveDirection = Vector3.zero;
    private FactionComponent myFaction;
    private HealthComponent  myHealth;
    private float            attackCooldownTimer = 0f;

    // 仇恨列表
    private readonly Dictionary<Transform, float> hateTable = new Dictionary<Transform, float>();

    // 扫描频率控制
    private float scanTimer = 0f;
    private const float scanInterval = 0.2f;

    // ─── 出生点 ──────────────────────────────────────────────
    private Vector3    _spawnPosition;
    private Quaternion _spawnRotation;

    // ─── 游荡状态内部変量 ────────────────────────────────────
    private Vector3 _wanderTarget;
    private float   _idleTimer = 0f;

    // ─── NavMeshAgent ────────────────────────────────────────
    private NavMeshAgent _agent;
    private bool         _hasAgent;
    private NavMeshPath  _wanderPath;              // Wander 用経路検証
    private NavMeshPath  _returnPath;              // ReturnToSpawn 用経路検証
    private NavMeshPath  _chasePath;               // Chase 用経路検証
    private bool         _returningWithAgent;      // ReturnToSpawn で Agent 使用中
    private bool         _chasingWithAgent;        // Chase で Agent 使用中
    private float        _nextChaseDestinationUpdateTime; // 次回 destination 更新時刻

    // ─── Chase destination キャッシュ ────────────────────────
    /// <summary>最後に成功した Chase Agent の目標点。目標が一時的に不可達の場合に使用する。</summary>
    private Vector3 _lastValidChaseDestination;
    /// <summary>_lastValidChaseDestination が有効かどうかのフラグ。</summary>
    private bool    _hasLastValidChaseDestination;

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

        _agent    = GetComponent<NavMeshAgent>();
        _hasAgent = _agent != null;
        if (_hasAgent)
        {
            _wanderPath      = new NavMeshPath();
            _returnPath      = new NavMeshPath();
            _chasePath       = new NavMeshPath();
            _agent.isStopped = true;
        }

        SetupIdleTimer();

        if (wanderRadius > 0f && wanderRadius > leashRadius)
            Debug.LogWarning($"[EnemyAI] {gameObject.name}: wanderRadius({wanderRadius}) が leashRadius({leashRadius}) を超えています。");
    }

    // ─── NavMeshAgent ヘルパー ───────────────────────────────
    /// <summary>NavMeshAgent が移動に使用できる状態かを判定する。</summary>
    private bool CanUseAgent() =>
        _hasAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh;

    /// <summary>
    /// Agent の移動を停止し Rigidbody を非 kinematic に戻す。
    /// Agent 使用中の状態から他の状態へ移行するときに呼ぶ。
    /// </summary>
    private void StopAgentAndRestoreRigidbody()
    {
        if (_hasAgent && _agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }
        if (rb != null) rb.isKinematic = false;
    }

    void Update()
    {
        if (currentState == EnemyState.ReturnToSpawn) return;

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

        if (currentState == EnemyState.Idle || currentState == EnemyState.Wander)
            UpdateIdleWanderCycle();
    }

    // ─── 脱战计时 ────────────────────────────────────────────
    void UpdateDisengage()
    {
        if (currentTarget == null) { disengageTimer = 0f; return; }

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
        else { disengageTimer = 0f; }
    }

    // ─── 返回出生点 ──────────────────────────────────────────
    private void EnterReturnToSpawn()
    {
        // Wander または Chase 中に Agent が動いていた場合は停止
        if (currentState == EnemyState.Wander && CanUseAgent())
            StopAgentAndRestoreRigidbody();
        else if (currentState == EnemyState.Chase && _chasingWithAgent)
            StopAgentAndRestoreRigidbody();

        _chasingWithAgent             = false;
        _hasLastValidChaseDestination = false;  // Chase キャッシュをクリア

        hateTable.Clear();
        currentTarget       = null;
        disengageTimer      = 0f;
        attackCooldownTimer = 0f;
        currentState        = EnemyState.ReturnToSpawn;
        animator?.SetBool("IsAttacking", false);

        // Agent で帰還できるか判定
        _returningWithAgent = false;
        if (CanUseAgent())
        {
            if (NavMesh.SamplePosition(_spawnPosition, out NavMeshHit spawnHit,
                                       returnNavMeshSampleDistance, NavMesh.AllAreas))
            {
                _agent.CalculatePath(spawnHit.position, _returnPath);
                if (_returnPath.status == NavMeshPathStatus.PathComplete)
                {
                    _returningWithAgent  = true;
                    rb.isKinematic       = true;
                    _agent.speed         = moveSpeed;
                    _agent.isStopped     = false;
                    _agent.SetDestination(spawnHit.position);
                }
            }
        }

        if (!_returningWithAgent)
        {
            if (_hasAgent && _agent != null && _agent.enabled)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            if (rb != null) rb.isKinematic = false;
        }

        Debug.Log($"[EnemyAI] {gameObject.name} 开始返回出生点。(Agent={_returningWithAgent})");
    }

    private float GetHorizontalDistanceFromSpawn()
    {
        Vector3 currentFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 spawnFlat   = new Vector3(_spawnPosition.x,     0f, _spawnPosition.z);
        return Vector3.Distance(currentFlat, spawnFlat);
    }

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

    private void HandleReturnToSpawn()
    {
        if (!enabled) return;

        if (_returningWithAgent)
        {
            if (CanUseAgent())
            {
                Vector3 vel = _agent.velocity;
                moveDirection = vel.sqrMagnitude > 0.01f
                    ? new Vector3(vel.x, 0f, vel.z).normalized
                    : Vector3.zero;

                if (!_agent.pathPending && _agent.remainingDistance <= returnToSpawnStopDistance)
                    FinishReturnToSpawn();
                return;
            }
            else
            {
                _returningWithAgent = false;
                if (_hasAgent && _agent != null && _agent.enabled)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                if (rb != null) rb.isKinematic = false;
            }
        }

        Vector3 currentFlat    = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 spawnFlat      = new Vector3(_spawnPosition.x,     0f, _spawnPosition.z);
        float   horizontalDist = Vector3.Distance(currentFlat, spawnFlat);

        if (horizontalDist > returnToSpawnStopDistance)
        {
            moveDirection = new Vector3(
                _spawnPosition.x - transform.position.x, 0f,
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
            FinishReturnToSpawn();
        }
    }

    private void FinishReturnToSpawn()
    {
        bool wasUsingAgent  = _returningWithAgent;
        _returningWithAgent = false;

        if (_hasAgent && _agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        if (!wasUsingAgent)
            transform.position = new Vector3(_spawnPosition.x, transform.position.y, _spawnPosition.z);
        transform.rotation = _spawnRotation;

        if (rb != null)
        {
            rb.isKinematic    = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (myHealth != null) myHealth.RestoreFullHealth();

        currentState        = EnemyState.Idle;
        attackCooldownTimer = 0f;
        moveDirection       = Vector3.zero;
        SetupIdleTimer();
        animator?.SetBool("IsAttacking", false);
        animator?.SetFloat("Speed", 0f);
        Debug.Log($"[EnemyAI] {gameObject.name} 已回到出生点，重置完成。");
    }

    // ─── 仇恨系统 ────────────────────────────────────────────
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

    private void AddHate(Transform target, float amount)
    {
        if (!IsValidTarget(target)) return;
        if (hateTable.ContainsKey(target)) hateTable[target] += amount;
        else hateTable[target] = amount;
        SelectHighestHateTarget();
    }

    private void RemoveInvalidHateTargets()
    {
        var invalid = hateTable.Keys.Where(t => !IsValidTarget(t)).ToList();
        foreach (var t in invalid) hateTable.Remove(t);
    }

    private void SelectHighestHateTarget()
    {
        RemoveInvalidHateTargets();
        if (hateTable.Count == 0) { currentTarget = null; return; }
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

    void HandleDamaged(float amount, Transform attacker)
    {
        AddHate(attacker, amount * damageHateMultiplier);
    }

    // ─── 状态机 ──────────────────────────────────────────────
    void UpdateState()
    {
        if (currentState == EnemyState.ReturnToSpawn) return;

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            TransitionTo(dist <= attackRange ? EnemyState.Attack : EnemyState.Chase);
            return;
        }

        if (currentState == EnemyState.Idle || currentState == EnemyState.Wander) return;
        TransitionTo(EnemyState.Idle);
    }

    void TransitionTo(EnemyState next)
    {
        if (currentState == next) return;

        // Wander または Chase で Agent が動いていた場合は停止
        if (currentState == EnemyState.Wander && CanUseAgent())
            StopAgentAndRestoreRigidbody();
        else if (currentState == EnemyState.Chase && _chasingWithAgent)
            StopAgentAndRestoreRigidbody();

        // Chase から離れる場合: フラグとキャッシュをクリア
        if (currentState == EnemyState.Chase)
        {
            _chasingWithAgent             = false;
            _hasLastValidChaseDestination = false;
        }

        currentState = next;
        switch (next)
        {
            case EnemyState.Idle:
                animator?.SetBool("IsAttacking", false);
                SetupIdleTimer();
                break;

            case EnemyState.Wander:
                animator?.SetBool("IsAttacking", false);
                if (CanUseAgent())
                {
                    rb.isKinematic   = true;
                    _agent.speed     = wanderMoveSpeed;
                    _agent.isStopped = false;
                    _agent.SetDestination(_wanderTarget);
                }
                break;

            case EnemyState.Chase:
                animator?.SetBool("IsAttacking", false);
                _chasingWithAgent               = false;
                _nextChaseDestinationUpdateTime = 0f;
                if (CanUseAgent())
                {
                    _chasingWithAgent = true;
                    rb.isKinematic    = true;
                    _agent.speed      = moveSpeed;
                    _agent.isStopped  = false;
                    _agent.ResetPath();
                    // 最初の destination 設定を試みる（失敗しても Agent Chase は継続）
                    TryUpdateAgentChaseDestination();
                }
                else
                {
                    rb.isKinematic = false;
                }
                break;

            case EnemyState.ReturnToSpawn:
                animator?.SetBool("IsAttacking", false);
                break;

            case EnemyState.Attack:
                attackCooldownTimer = 0f;
                animator?.SetBool("IsAttacking", false);
                // Chase Agent を Attack 開始時に停止（敵がスライドしないように）
                if (_chasingWithAgent)
                {
                    _chasingWithAgent             = false;
                    _hasLastValidChaseDestination = false;
                    if (_hasAgent && _agent != null && _agent.enabled)
                    {
                        _agent.isStopped = true;
                        _agent.ResetPath();
                    }
                    if (rb != null) rb.isKinematic = false;
                }
                break;
        }
    }

    // ─── Chase NavMeshAgent ─────────────────────────────────
    /// <summary>
    /// 現在のターゲット位置付近の NavMesh 点を取得し、経路が PathComplete なら destination を設定する。
    /// 成功時: _lastValidChaseDestination を更新して true を返す。
    /// 失敗時: false を返す。Agent Chase は継続（Rigidbody fallback はしない）。
    /// </summary>
    private bool TryUpdateAgentChaseDestination()
    {
        if (currentTarget == null) return false;
        if (!CanUseAgent()) return false;

        if (NavMesh.SamplePosition(currentTarget.position, out NavMeshHit targetHit,
                                   chaseNavMeshSampleDistance, NavMesh.AllAreas))
        {
            _agent.CalculatePath(targetHit.position, _chasePath);
            if (_chasePath.status == NavMeshPathStatus.PathComplete)
            {
                _agent.SetDestination(targetHit.position);
                _lastValidChaseDestination    = targetHit.position;
                _hasLastValidChaseDestination = true;
                return true;
            }
        }
        // 失敗: Agent Chase は継続。_lastValidChaseDestination は保持する。
        return false;
    }

    // ─── Idle / Wander サイクル ──────────────────────────────
    private void SetupIdleTimer()
    {
        _idleTimer = Random.Range(minIdleTime, maxIdleTime);
    }

    private void UpdateIdleWanderCycle()
    {
        if (currentTarget != null) return;

        if (currentState == EnemyState.Idle)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                if (wanderRadius <= 0f) { SetupIdleTimer(); return; }

                if (TryPickWanderPoint(out Vector3 point))
                {
                    _wanderTarget = point;
                    TransitionTo(EnemyState.Wander);
                }
                else
                {
                    SetupIdleTimer();
                }
            }
        }
        else if (currentState == EnemyState.Wander)
        {
            bool arrived;

            if (_hasAgent && _agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh)
                {
                    arrived = !_agent.pathPending &&
                              _agent.remainingDistance <= wanderPointReachDistance;
                }
                else
                {
                    StopAgentAndRestoreRigidbody();
                    arrived = true;
                }
            }
            else
            {
                Vector3 flatPos    = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatTarget = new Vector3(_wanderTarget.x,      0f, _wanderTarget.z);
                arrived = Vector3.Distance(flatPos, flatTarget) <= wanderPointReachDistance;
            }

            if (arrived) TransitionTo(EnemyState.Idle);
        }
    }

    private bool TryPickWanderPoint(out Vector3 point)
    {
        point = Vector3.zero;
        for (int i = 0; i < 10; i++)
        {
            Vector2 circle    = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = new Vector3(
                _spawnPosition.x + circle.x,
                transform.position.y,
                _spawnPosition.z + circle.y);

            float distFromSpawn = Vector3.Distance(
                new Vector3(candidate.x, 0f, candidate.z),
                new Vector3(_spawnPosition.x, 0f, _spawnPosition.z));
            if (distFromSpawn > leashRadius) continue;

            if (CanUseAgent())
            {
                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                {
                    float sampledDist = Vector3.Distance(
                        new Vector3(navHit.position.x, 0f, navHit.position.z),
                        new Vector3(_spawnPosition.x, 0f, _spawnPosition.z));
                    if (sampledDist > leashRadius) continue;

                    _agent.CalculatePath(navHit.position, _wanderPath);
                    if (_wanderPath.status == NavMeshPathStatus.PathComplete)
                    {
                        point = navHit.position;
                        return true;
                    }
                }
            }
            else
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
                moveDirection = Vector3.zero;
                if (!rb.isKinematic)
                    rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                break;
            case EnemyState.Wander:
                HandleWanderMovement();
                break;
            case EnemyState.Chase:
                HandleChaseMovement();
                break;
            case EnemyState.Attack:
                moveDirection = Vector3.zero;
                if (!rb.isKinematic)
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

    private void HandleWanderMovement()
    {
        if (_hasAgent && _agent != null && _agent.enabled)
        {
            if (_agent.isOnNavMesh)
            {
                Vector3 vel = _agent.velocity;
                moveDirection = vel.sqrMagnitude > 0.01f
                    ? new Vector3(vel.x, 0f, vel.z).normalized
                    : Vector3.zero;
                return;
            }
            else { StopAgentAndRestoreRigidbody(); }
        }

        Vector3 flatPos    = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(_wanderTarget.x,      0f, _wanderTarget.z);
        float   dist       = Vector3.Distance(flatPos, flatTarget);

        if (dist > wanderPointReachDistance)
        {
            moveDirection = new Vector3(
                _wanderTarget.x - transform.position.x, 0f,
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
            moveDirection     = Vector3.zero;
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    /// <summary>
    /// Chase 状態の移動処理。
    /// Agent が使用可能なら Agent で追跡し続ける。
    /// 目標位置が一時的に不可達でも Rigidbody fallback はしない。
    /// Agent 自体が使用不可になった場合のみ Rigidbody fallback。
    /// </summary>
    private void HandleChaseMovement()
    {
        if (currentTarget == null) return;

        // ── Agent 使用パス ──────────────────────────────────
        if (_chasingWithAgent)
        {
            if (!CanUseAgent())
            {
                // Agent 自体が使用不可の場合のみ Rigidbody fallback
                _chasingWithAgent             = false;
                _hasLastValidChaseDestination = false;
                StopAgentAndRestoreRigidbody();
                ChaseTargetRigidbody();
                return;
            }

            // 定期的に destination を更新
            if (Time.time >= _nextChaseDestinationUpdateTime)
            {
                _nextChaseDestinationUpdateTime = Time.time + chaseDestinationUpdateInterval;
                bool updated = TryUpdateAgentChaseDestination();

                if (!updated)
                {
                    // 目標位置が一時的に不可達: Rigidbody fallback しない
                    // Agent に既存パスがあればそのまま続行。
                    // パスがない場合は最後の有効 destination を再セット。
                    if (!_agent.hasPath && _hasLastValidChaseDestination)
                        _agent.SetDestination(_lastValidChaseDestination);
                    // パスも last valid もない場合はその場で次の更新を待つ（エラーなし）
                }
            }

            // Agent velocity を moveDirection に反映（Animator Speed 用）
            Vector3 vel = _agent.velocity;
            moveDirection = vel.sqrMagnitude > 0.01f
                ? new Vector3(vel.x, 0f, vel.z).normalized
                : Vector3.zero;
            return;
        }

        // ── Rigidbody fallback パス ─────────────────────────
        ChaseTargetRigidbody();
    }

    /// <summary>旧来の Rigidbody による直線追跡。Chase fallback として使用。</summary>
    private void ChaseTargetRigidbody()
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
    public void ResetToSpawn()
    {
        hateTable.Clear();
        currentTarget                 = null;
        disengageTimer                = 0f;
        _returningWithAgent           = false;
        _chasingWithAgent             = false;
        _hasLastValidChaseDestination = false;

        if (_hasAgent && _agent != null && _agent.enabled)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            if (_agent.isOnNavMesh)
                _agent.Warp(_spawnPosition);
            else
                transform.position = _spawnPosition;
        }
        else
        {
            transform.position = _spawnPosition;
        }
        transform.rotation = _spawnRotation;

        if (rb != null)
        {
            rb.isKinematic    = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (myHealth != null) myHealth.RestoreFullHealth();

        currentState        = EnemyState.Idle;
        attackCooldownTimer = 0f;
        moveDirection       = Vector3.zero;
        SetupIdleTimer();
        animator?.SetBool("IsAttacking", false);
        animator?.SetFloat("Speed", 0f);
    }

    public void ForceDisengageAndReturnToSpawn()
    {
        if (!enabled) return;
        if (myHealth != null && myHealth.IsDead) return;
        if (currentState == EnemyState.ReturnToSpawn) return;

        Debug.Log($"[EnemyAI] {gameObject.name} 外部指令により強制脱戦、出生点へ帰還。");
        EnterReturnToSpawn();
    }
}
