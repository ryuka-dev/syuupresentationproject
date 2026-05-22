using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家基础攻击执行器。
/// 负责单体普通攻击（键 1）和 AOE 普通攻击（键 4）的所有执行逻辑。
///
/// 职责：
///   - 管理基础攻击共享冷却 basicAttackRecast
///   - 执行单体普通攻击（TrySingleTargetAttack）
///   - 执行 AOE 普通攻击（TryAreaBasicAttack）
///   - 计算普通攻击最终伤害（含装备加成 + 攻击强化修正）
///   - AOE 范围敌人搜索 + 重复命中去重
///   - 调用 HealthComponent.TakeDamage(...)
///   - 触发攻击动画 Trigger
///
/// 不做的事：
///   - 不读取键盘输入（由 PlayerSkillController 调用）
///   - 不管理技能栏 UI
///   - 不管理技能冷却（PlayerSkillManager 的 SkillData 冷却）
/// </summary>
public class PlayerBasicAttackController : MonoBehaviour
{
    [Header("单体普通攻击")]
    [Tooltip("PlayerCombatStats 缺失时的伤害回退值。")]
    [SerializeField] private float fallbackNormalAttackDamage = 20f;
    [Tooltip("单体普通攻击有效射程（米）。")]
    [SerializeField] private float normalAttackRange          = 2.0f;

    [Header("基础攻击共享冷却")]
    [Tooltip("单体普通攻击和 AOE 普通攻击共享的固定冷却时间（秒）。")]
    [SerializeField] private float basicAttackRecast = 1.0f;

    [Header("AOE 普通攻击（键 4）")]
    [Tooltip("AOE 普通攻击的搜索半径（米）。")]
    [SerializeField] private float areaBasicAttackRadius           = 3f;
    [Tooltip("AOE 每个目标受到的伤害 = 普通攻击最终伤害 × 此倍率。")]
    [SerializeField] private float areaBasicAttackDamageMultiplier = 0.4f;

    // ─── 内部引用 ─────────────────────────────────────────────────
    private PlayerTargeting              _targeting;
    private PlayerCombatStats            _combatStats;
    private PlayerStatusEffectController _statusEffectController;
    private FactionComponent             _selfFaction;
    private HealthComponent              _selfHealth;
    private Animator                     _animator;

    // 共有冷却：次に基礎攻撃が使用できる Time.time（絶対時刻）
    private float _nextBasicAttackAllowedTime;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        _targeting              = GetComponent<PlayerTargeting>();
        _combatStats            = GetComponent<PlayerCombatStats>();
        _statusEffectController = GetComponent<PlayerStatusEffectController>();
        _selfFaction            = GetComponent<FactionComponent>();
        _selfHealth             = GetComponent<HealthComponent>();
        _animator               = GetComponent<Animator>();

        if (_targeting == null)
            Debug.LogWarning("[PlayerBasicAttackController] PlayerTargeting not found.");
        if (_selfFaction == null)
            Debug.LogWarning("[PlayerBasicAttackController] FactionComponent not found.");
        if (_animator == null)
            Debug.LogWarning("[PlayerBasicAttackController] Animator not found.");
    }

    // ─── 公开 API（由 PlayerSkillController 调用） ────────────────

    /// <summary>基础攻击冷却是否可用。</summary>
    public bool IsBasicAttackReady() => Time.time >= _nextBasicAttackAllowedTime;

    /// <summary>
    /// 尝试对当前目标执行单体普通攻击。
    /// 无目标 / 冷却中 / 目标无效时返回 false 且不消耗冷却。
    /// 攻击成功后开始冷却并返回 true。
    /// </summary>
    public bool TrySingleTargetAttack()
    {
        // 冷却中
        if (!IsBasicAttackReady())
        {
            Debug.Log($"[PlayerBasicAttackController] Normal attack on cooldown ({(_nextBasicAttackAllowedTime - Time.time):F1}s remaining).");
            return false;
        }

        // 无目标
        if (_targeting == null || _targeting.CurrentTarget == null)
        {
            Debug.Log("[PlayerBasicAttackController] No target selected.");
            return false;
        }

        Transform target = _targeting.CurrentTarget;
        if (target == null)
        {
            Debug.Log("[PlayerBasicAttackController] Target no longer exists.");
            return false;
        }

        // HealthComponent 检查
        var health = target.GetComponentInChildren<HealthComponent>()
                  ?? target.GetComponent<HealthComponent>();
        if (health == null)
        {
            Debug.LogWarning($"[PlayerBasicAttackController] {target.name} has no HealthComponent.");
            return false;
        }

        // 目标已死亡
        if (health.IsDead)
        {
            Debug.Log($"[PlayerBasicAttackController] {target.name} is already dead.");
            return false;
        }

        // 敌对判断
        var targetFaction = target.GetComponent<FactionComponent>();
        if (targetFaction == null)
        {
            Debug.LogWarning($"[PlayerBasicAttackController] {target.name} has no FactionComponent.");
            return false;
        }
        if (_selfFaction != null && !_selfFaction.ShouldAttack(targetFaction.faction))
        {
            Debug.Log($"[PlayerBasicAttackController] {target.name} is not hostile.");
            return false;
        }

        // 距离检查
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > normalAttackRange)
        {
            Debug.Log($"[PlayerBasicAttackController] Target out of range ({dist:F1} / {normalAttackRange}m).");
            return false;
        }

        // 伤害结算
        float finalDamage = CalculateNormalAttackDamage();
        StartBasicAttackRecast();
        health.TakeDamage(finalDamage, transform);
        Debug.Log($"[PlayerBasicAttackController] Normal attack hit: {target.name}, damage={finalDamage:F1}");

        TriggerAttackAnimation();
        return true;
    }

    /// <summary>
    /// 尝试对玩家周围 areaBasicAttackRadius 范围内所有敌对目标执行 AOE 普通攻击。
    /// 冷却中时返回 false。
    /// 即使范围内无敌人也视为挥空并消耗冷却（AOE 普通攻击是动作，不是条件技能）。
    /// </summary>
    public bool TryAreaBasicAttack()
    {
        // 冷却中
        if (!IsBasicAttackReady())
        {
            Debug.Log($"[PlayerBasicAttackController] AOE attack on cooldown ({(_nextBasicAttackAllowedTime - Time.time):F1}s remaining).");
            return false;
        }

        float normalDamage = CalculateNormalAttackDamage();
        float aoeDamage    = normalDamage * areaBasicAttackDamageMultiplier;

        var hits    = Physics.OverlapSphere(transform.position, areaBasicAttackRadius);
        var damaged = new HashSet<HealthComponent>();
        int hitCount = 0;

        foreach (var col in hits)
        {
            if (col == null) continue;

            var health = col.GetComponentInParent<HealthComponent>();
            if (health == null) continue;

            // 同一敌人多 Collider 去重
            if (!damaged.Add(health)) continue;

            // 跳过自己
            if (health.gameObject == gameObject) continue;

            // 跳过死亡目标
            if (health.IsDead) continue;

            // 敌对判断
            var targetFaction = col.GetComponentInParent<FactionComponent>();
            if (targetFaction == null) continue;
            if (_selfFaction == null) continue;
            if (!_selfFaction.ShouldAttack(targetFaction.faction)) continue;

            health.TakeDamage(aoeDamage, transform);
            hitCount++;
            Debug.Log($"[PlayerBasicAttackController] AOE hit: {health.gameObject.name}, damage={aoeDamage:F1}");
        }

        // 挥空也消耗冷却
        StartBasicAttackRecast();
        Debug.Log($"[PlayerBasicAttackController] AOE attack executed. radius={areaBasicAttackRadius}, normalDmg={normalDamage:F1}, aoeDmg={aoeDamage:F1}, targets hit={hitCount}");

        TriggerAttackAnimation();
        return true;
    }

    // ─── 内部实现 ─────────────────────────────────────────────────

    private void StartBasicAttackRecast()
    {
        _nextBasicAttackAllowedTime = Time.time + basicAttackRecast;
    }

    /// <summary>
    /// 计算普通攻击最终伤害。
    /// 优先使用 PlayerCombatStats.CurrentNormalAttackDamage，
    /// 再由 PlayerStatusEffectController.ModifyOutgoingNormalAttackDamage 应用攻击强化修正。
    /// </summary>
    private float CalculateNormalAttackDamage()
    {
        float damage = _combatStats != null
            ? _combatStats.CurrentNormalAttackDamage
            : fallbackNormalAttackDamage;

        if (_statusEffectController != null)
            damage = _statusEffectController.ModifyOutgoingNormalAttackDamage(damage);

        return damage;
    }

    /// <summary>触发攻击动画 Trigger（玩家死亡时跳过）。</summary>
    private void TriggerAttackAnimation()
    {
        if (_animator != null && (_selfHealth == null || !_selfHealth.IsDead))
            _animator.SetTrigger("Attack");
    }
}
