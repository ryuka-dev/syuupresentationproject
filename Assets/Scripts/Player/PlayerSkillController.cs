using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家技能输入控制器
/// 按数字键 1 对当前目标释放普通攻击并造成伤害。
/// 攻击成功时触发 Animator 的 Attack Trigger 播放攻击动画。
/// 最终伤害由 PlayerCombatStats.CurrentNormalAttackDamage 提供；
/// 若 PlayerCombatStats 缺失则回退使用 normalAttackDamage。
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    [Header("Normal Attack")]
    public float normalAttackDamage   = 20f;   // fallback / PlayerCombatStats 缺失时使用
    public float normalAttackRange    = 2.0f;
    public float normalAttackCooldown = 1.0f;

    private PlayerTargeting   _targeting;
    private FactionComponent  _selfFaction;
    private HealthComponent   _selfHealth;
    private Animator          _animator;
    private PlayerCombatStats              _combatStats;
    private PlayerStatusEffectController  _statusEffectController;
    private float                         _cooldownTimer;

private void Awake()
    {
        _targeting = GetComponent<PlayerTargeting>();
        if (_targeting == null)
            Debug.LogWarning("[PlayerSkillController] PlayerTargeting not found.");

        _selfFaction = GetComponent<FactionComponent>();
        if (_selfFaction == null)
            Debug.LogWarning("[PlayerSkillController] FactionComponent not found.");

        _selfHealth = GetComponent<HealthComponent>();
        _animator   = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogWarning("[PlayerSkillController] Animator not found.");

        _combatStats            = GetComponent<PlayerCombatStats>();
        _statusEffectController = GetComponent<PlayerStatusEffectController>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        var kb = Keyboard.current;
        if (kb == null || !kb.digit1Key.wasPressedThisFrame) return;

        TryNormalAttack();
    }

private void TryNormalAttack()
    {
        // 1. 无目标
        if (_targeting == null || _targeting.CurrentTarget == null)
        {
            Debug.Log("[PlayerSkillController] No target selected.");
            return;
        }

        Transform target = _targeting.CurrentTarget;

        // 2. 目标对象已被销毁
        if (target == null)
        {
            Debug.Log("[PlayerSkillController] Target no longer exists.");
            return;
        }

        // 3. 冷却中
        if (_cooldownTimer > 0f)
        {
            Debug.Log($"[PlayerSkillController] Normal Attack on cooldown ({_cooldownTimer:F1}s remaining).");
            return;
        }

        // 4. 检查 HealthComponent
        var health = target.GetComponentInChildren<HealthComponent>()
                  ?? target.GetComponent<HealthComponent>();
        if (health == null)
        {
            Debug.LogWarning($"[PlayerSkillController] {target.name} has no HealthComponent. Invalid target.");
            return;
        }

        // 5. 目标已死亡
        if (health.IsDead)
        {
            Debug.Log($"[PlayerSkillController] {target.name} is already dead.");
            return;
        }

        // 6. 检查 FactionComponent + 敌对判断
        var targetFaction = target.GetComponent<FactionComponent>();
        if (targetFaction == null)
        {
            Debug.LogWarning($"[PlayerSkillController] {target.name} has no FactionComponent. Invalid target.");
            return;
        }

        if (_selfFaction != null && !_selfFaction.ShouldAttack(targetFaction.faction))
        {
            Debug.Log($"[PlayerSkillController] Invalid target: {target.name} is not hostile.");
            return;
        }

        // 7. 距离检查
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > normalAttackRange)
        {
            Debug.Log($"[PlayerSkillController] Target out of range ({dist:F1} / {normalAttackRange}m).");
            return;
        }

        // 8. 最终伤害 — 优先从 PlayerCombatStats 读取，再由 PlayerStatusEffectController 应用 AttackPowerMultiplier
        float finalDamage;
        if (_combatStats != null)
        {
            float baseDmg  = _combatStats.BaseNormalAttackDamage;
            float eqBonus  = _combatStats.EquipmentAttackPowerBonus;
            finalDamage    = _combatStats.CurrentNormalAttackDamage;

            if (_statusEffectController != null)
                finalDamage = _statusEffectController.ModifyOutgoingNormalAttackDamage(finalDamage);

            _cooldownTimer = normalAttackCooldown;
            health.TakeDamage(finalDamage, transform);
            Debug.Log($"[PlayerSkillController] Player attack hit: base={baseDmg}, equipmentBonus={eqBonus}, final={finalDamage}");
        }
        else
        {
            finalDamage = normalAttackDamage;

            if (_statusEffectController != null)
                finalDamage = _statusEffectController.ModifyOutgoingNormalAttackDamage(finalDamage);

            _cooldownTimer = normalAttackCooldown;
            health.TakeDamage(finalDamage, transform);
            Debug.Log($"[PlayerSkillController] Player attack hit: final={finalDamage} (fallback: no PlayerCombatStats)");
        }

        // 9. 触发攻击动画（玩家未死亡时）
        if (_animator != null && (_selfHealth == null || !_selfHealth.IsDead))
        {
            _animator.SetTrigger("Attack");
        }
    }
}
