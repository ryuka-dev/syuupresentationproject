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
///
/// [移动控制权规则 - 第一版整理]
/// 正常移动路径：NavMeshAgent 负责（Wander / Chase / ReturnToSpawn）
/// Rigidbody 职责：碰撞 / 物理辅助 / Agent 不可用时的 fallback
/// Attack 状态：StopMovementForAttack() 统一停止 Agent + 清除 Rigidbody 残留速度，防止滑动
/// 移动控制切换：通过 StopAgentMovement / PrepareAgentDrivenMovement / StopMovementForAttack 集中管理
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

    // ─── SpawnArea コンテキスト ───────────────────────────────
    private Vector3 _spawnAreaCenter;      // SpawnArea から注入された区域中心
    private bool    _hasSpawnAreaContext;  // SetSpawnAreaContext() 済みかどうか

    private Vector3 WanderCenter => _hasSpawnAreaContext ? _spawnAreaCenter : _spawnPosition;
    private Vector3 LeashCenter  => _hasSpawnAreaContext ? _spawnAreaCenter : _spawnPosition;

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
    // ─── スキルコントローラー ────────────────────────────────
    private EnemySkillController _skillController;


    // ─── 生命周期 ────────────────────────────────────────────
    void Awake()
    {
        if (animator    == null) animator    = GetComponent<Animator>();
        if (fovDetector == null) fovDetector = GetComponent<FOVDetector>();
        myFaction = GetComponent<FactionComponent>();
        myHealth  = GetComponent<HealthComponent>();

        _spawnPosition      = transform.position;
        _spawnRotation      = transform.rotation;
        _spawnAreaCenter    = _spawnPosition;
        _hasSpawnAreaContext = false;
        _skillController    = GetComponent<EnemySkillController>();
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

    // ─── NavMeshAgent 判定 ───────────────────────────────────
    /// <summary>NavMeshAgent が移動に使用できる状態かを判定する。</summary>
    private bool CanUseAgent() =>
        _hasAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh;

    // ─── 移動制御ヘルパー ─────────────────────────────────────

    /// <summary>
    /// Rigidbody の線速度と角速度をゼロにする。
    /// 残留速度が後続の移動状態に影響しないようにするための共通クリア処理。
    /// </summary>
    private void ClearRigidbodyVelocity()
    {
        if (rb == null) return;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// NavMeshAgent を安全に停止する。
    /// Agent が null / disabled の場合は何もしない。
    /// ResetPath は Agent が NavMesh 上にある場合のみ実行する。
    /// </summary>
    private void StopAgentMovement(bool resetPath = false)
    {
        if (!_hasAgent || _agent == null || !_agent.enabled) return;
        _agent.isStopped = true;
        if (resetPath && _agent.isOnNavMesh) _agent.ResetPath();
    }

    /// <summary>
    /// Wander / Chase / ReturnToSpawn で Agent 移動に切り替える前の共通準備。
    /// Rigidbody 残留速度をクリアし、Agent が位置を引き継いでも速度干渉が起きないようにする。
    /// </summary>
    private void PrepareAgentDrivenMovement()
    {
        ClearRigidbodyVelocity();
    }

    /// <summary>
    /// Attack 状態に入る際の移動完全停止処理。
    /// Agent を停止（ResetPath あり）し、rb.isKinematic を false に戻した上で
    /// Rigidbody 線速度・角速度をクリアして敵が滑らないようにする。
    /// </summary>
    private void StopMovementForAttack()
    {
        StopAgentMovement(true);
        if (rb != null) rb.isKinematic = false;
        ClearRigidbodyVelocity();
    }

    /// <summary>
    /// Agent 使用中の状態から他の状態へ移行する際の共通停止処理。
    /// Agent を停止し、Rigidbody を非 kinematic に戻し、残留速度をクリアする。
    /// </summary>
    private void StopAgentAndRestoreRigidbody()
    {
        StopAgentMovement(true);
        if (rb != null) rb.isKinematic = false;
        ClearRigidbodyVelocity();
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
        // Wander または Chase 中に Agent が動いていた場合は停止（速度もクリア）
        if (currentState == EnemyState.Wander && CanUseAgent())
            StopAgentAndRestoreRigidbody();
        else if (currentState == EnemyState.Chase && _chasingWithAgent)
            StopAgentAndRestoreRigidbody();

        _chasingWithAgent             = false;
        _hasLastValidChaseDestination = false;  // Chase キャッシュをクリア
        _skillController?.CancelCasting("ReturnToSpawn");

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
                    _returningWithAgent = true;
                    PrepareAgentDrivenMovement();  // Rigidbody 残留速度クリア
                    rb.isKinematic      = true;
                    _agent.speed        = moveSpeed;
                    _agent.isStopped    = false;
                    _agent.SetDestination(spawnHit.position);
                }
            }
        }

        if (!_returningWithAgent)
        {
            StopAgentMovement(true);
            if (rb != null) rb.isKinematic = false;
        }

        Debug.Log($"[EnemyAI] {gameObject.name} 开始返回出生点。(Agent={_returningWithAgent})");
    }

    private float GetHorizontalDistanceFromSpawn()
    {
        Vector3 currentFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 centerFlat  = new Vector3(LeashCenter.x,         0f, LeashCenter.z);
        return Vector3.Distance(currentFlat, centerFlat);
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
                // Agent が使用不可になった場合は Rigidbody fallback に切り替え
                _returningWithAgent = false;
                StopAgentMovement(true);
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

        StopAgentMovement(true);

        if (!wasUsingAgent)
            transform.position = new Vector3(_spawnPosition.x, transform.position.y, _spawnPosition.z);
        transform.rotation = _spawnRotation;

        if (rb != null) rb.isKinematic = false;
        ClearRigidbodyVelocity();

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

            // CastAttack 読条中は Attack 状態を維持。
            // ターゲットが攻撃範囲外に出ても Chase に遷移しない。
            if (currentState == EnemyState.Attack &&
                _skillController != null && _skillController.IsCasting)
                return;

            TransitionTo(dist <= attackRange ? EnemyState.Attack : EnemyState.Chase);
            return;
        }

        if (currentState == EnemyState.Idle || currentState == EnemyState.Wander) return;
        TransitionTo(EnemyState.Idle);
    }

    void TransitionTo(EnemyState next)
    {
        if (currentState == next) return;

        // Wander または Chase で Agent が動いていた場合は停止（Rigidbody 速度もクリア）
        if (currentState == EnemyState.Wander && CanUseAgent())
            StopAgentAndRestoreRigidbody();
        else if (currentState == EnemyState.Chase && _chasingWithAgent)
            StopAgentAndRestoreRigidbody();

        // Attack から離れる場合: スキル施法をキャンセル
        if (currentState == EnemyState.Attack)
            _skillController?.CancelCasting("LeaveAttack");

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
                    PrepareAgentDrivenMovement();  // Rigidbody 残留速度クリア
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
                    PrepareAgentDrivenMovement();  // Rigidbody 残留速度クリア
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
                // 攻撃開始時に Agent 停止 + Rigidbody 残留速度クリア（スライド防止）
                // ※ _chasingWithAgent / _hasLastValidChaseDestination は上部ですでにクリア済み
                StopMovementForAttack();
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
                WanderCenter.x + circle.x,
                transform.position.y,
                WanderCenter.z + circle.y);

            float distFromCenter = Vector3.Distance(
                new Vector3(candidate.x, 0f, candidate.z),
                new Vector3(WanderCenter.x, 0f, WanderCenter.z));
            if (distFromCenter > leashRadius) continue;

            if (CanUseAgent())
            {
                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                {
                    float sampledDist = Vector3.Distance(
                        new Vector3(navHit.position.x, 0f, navHit.position.z),
                        new Vector3(WanderCenter.x, 0f, WanderCenter.z));
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
                // 追撃中も攻撃圏内なら通常攻撃を試みる（CastAttack 読条中はスキップ）
                if (_skillController == null || !_skillController.IsCasting)
                    TryNormalAttack();
                break;
            case EnemyState.Attack:
                moveDirection = Vector3.zero;
                if (!rb.isKinematic)
                    rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                FaceTarget();
                // スキルコントローラーが読条中は通常攻撃をスキップ
                if (_skillController != null && _skillController.IsCasting)
                    break;
                // 射程内にクールダウン済みスキルがあれば使用を試みる
                if (_skillController != null && currentTarget != null &&
                    _skillController.TryGetReadySkillInRange(currentTarget, out EnemySkillData readySkill) &&
                    _skillController.TryStartSkill(readySkill, currentTarget))
                    break;
                // 通常攻撃（IsAttacking リセット + TryNormalAttack に統一）
                if (animator != null)
                {
                    bool inAttackAnim = animator.GetCurrentAnimatorStateInfo(0).IsName("Attack");
                    if (inAttackAnim)
                        animator.SetBool("IsAttacking", false);
                    else
                        TryNormalAttack();
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

        // Rigidbody fallback（Agent 不可用時のみ）
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

/// <summary>
    /// 通常攻撃トリガー試行。Chase / Attack 両状態から共通利用。
    /// クールダウン / 距離 / ターゲット有効性をチェックしてから IsAttacking=true をセットする。
    /// 実際のダメージ結算は OnAttackHit() Animation Event が負担する。
    /// </summary>
    private void TryNormalAttack()
    {
        if (currentTarget == null) return;
        if (myHealth != null && myHealth.IsDead) return;
        if (animator == null) return;
        if (attackCooldownTimer > 0f) return;

        var targetHealth = currentTarget.GetComponent<HealthComponent>();
        if (targetHealth == null || targetHealth.IsDead) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > attackRange) return;

        // 攻撃アニメ中に重複トリガーしない
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack")) return;

        attackCooldownTimer = attackCooldown;
        animator.SetBool("IsAttacking", true);
    }


    // ─── Debug / 强制复位 ────────────────────────────────────
    public void ResetToSpawn()
    {
        _skillController?.CancelCasting("ResetToSpawn");
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

        if (rb != null) rb.isKinematic = false;
        ClearRigidbodyVelocity();

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
        _skillController?.CancelCasting("ForceDisengage");
        EnterReturnToSpawn();
    }

    /// <summary>
    /// EnemySpawnArea が敌人生成直後に呼び出すコンテキスト注入メソッド。
    /// 呼び出すことで Wander / Leash 判定の基準中心を areaCenter に切り替え、
    /// wanderRadius / leashRadius を SpawnArea の値で上書きし、
    /// この敵の実際の出生点 / 朝向を設定する。
    /// 呼び出さない場合は旧 EnemySpawnPoint との完全な互換性を保つ。
    /// </summary>
    /// <param name="areaCenter">SpawnArea の中心座標。Wander ランダム点選択と Leash 距離判定の基準に使用。</param>
    /// <param name="areaWanderRadius">SpawnArea 内圈半径。怪物の生成範囲 = Wander 範囲。</param>
    /// <param name="areaLeashRadius">SpawnArea 外圈半径。追跡 / 脱戦範囲。areaWanderRadius より小さい場合は自動修正。</param>
    /// <param name="spawnPosition">この敵が実際に生成された位置。ReturnToSpawn の帰還先。</param>
    /// <param name="spawnRotation">この敵が実際に生成された朝向。ReturnToSpawn 完了後に復元。</param>
    public void SetSpawnAreaContext(
        Vector3    areaCenter,
        float      areaWanderRadius,
        float      areaLeashRadius,
        Vector3    spawnPosition,
        Quaternion spawnRotation)
    {
        _hasSpawnAreaContext = true;
        _spawnAreaCenter     = areaCenter;

        wanderRadius = Mathf.Max(0f, areaWanderRadius);
        leashRadius  = Mathf.Max(wanderRadius, areaLeashRadius);

        _spawnPosition = spawnPosition;
        _spawnRotation = spawnRotation;
    }
}
