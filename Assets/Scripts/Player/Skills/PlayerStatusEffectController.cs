using UnityEngine;

/// <summary>
/// 玩家状态效果控制器 — v0.1 DamageReduction 专用。
/// 从 PlayerSkillManager 的 RuntimeStates 中读取处于 Active 状态的技能，
/// 将 EffectType == DamageReduction 的技能的 DamageTakenMultiplier 乘算到伤害上。
///
/// HealthComponent 在扣血前调用 ModifyIncomingDamage(float)。
/// 如果此脚本不存在，HealthComponent 将 fallback 到旧 PlayerMitigationController。
///
/// 不处理输入，不管理冷却，不更新 UI，不播放特效，只负责伤害修正计算。
/// 不影响敌人（敌人没有此组件，HealthComponent 会安全跳过）。
/// </summary>
public class PlayerStatusEffectController : MonoBehaviour
{
    [Header("技能管理器引用（留空时自动查找）")]
    [SerializeField] private PlayerSkillManager skillManager;

    [Header("调试")]
    [SerializeField] private bool logDamageModification = true;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        ResolveSkillManager();
    }

    // ─── 公开方法 ─────────────────────────────────────────────────

    /// <summary>
    /// 对即将受到的伤害应用所有 Active 减伤状态效果。
    /// 由 HealthComponent.ApplyIncomingDamageModifiers 调用。
    /// damage &lt;= 0 / skillManager null / 无 Active DamageReduction 时返回原值。
    /// 多个减伤效果同时存在时采用乘算（第一版规则）。
    /// </summary>
    public float ModifyIncomingDamage(float damage)
    {
        if (damage <= 0f)           return damage;
        if (skillManager == null)   return damage;

        float finalDamage = damage;
        var   states      = skillManager.RuntimeStates;

        foreach (var state in states)
        {
            if (state == null)            continue;
            if (state.SkillData == null)  continue;
            if (!state.IsActive)          continue;
            if (state.SkillData.EffectType != PlayerSkillEffectType.DamageReduction) continue;

            finalDamage *= state.SkillData.DamageTakenMultiplier;
        }

        if (logDamageModification && !Mathf.Approximately(finalDamage, damage))
            Debug.Log($"[PlayerStatusEffectController] Damage modified: original={damage:F1}, final={finalDamage:F1}");

        return finalDamage;
    }

/// <summary>
    /// 玩家普通攻击输出伤害的倍率修正。
    /// 遍历所有 Active 且 EffectType == AttackPowerMultiplier 的技能，乘算叠加。
    /// 由 PlayerSkillController 在普通攻击结算时调用。
    /// skillManager null / RuntimeStates 为空 / 无匹配技能时返回原始伤害。
    /// </summary>
    public float ModifyOutgoingNormalAttackDamage(float baseDamage)
    {
        if (baseDamage <= 0f)        return baseDamage;
        if (skillManager == null)    return baseDamage;

        float finalDamage = baseDamage;
        var   states      = skillManager.RuntimeStates;
        if (states == null) return baseDamage;

        foreach (var state in states)
        {
            if (state == null)           continue;
            if (state.SkillData == null) continue;
            if (!state.IsActive)         continue;
            if (state.SkillData.EffectType != PlayerSkillEffectType.AttackPowerMultiplier) continue;

            finalDamage *= state.SkillData.AttackPowerMultiplier;
        }

        if (logDamageModification && !Mathf.Approximately(finalDamage, baseDamage))
            Debug.Log($"[PlayerStatusEffectController] Outgoing damage modified: base={baseDamage:F1}, final={finalDamage:F1}");

        return finalDamage;
    }



    /// <summary>
    /// Active 状态の技能に基づき、受ける治療量を修正する。
    /// 全 Active 技能の HealingReceivedMultiplier を乗算叠算で適用する。
    /// baseHealing &lt;= 0 または skillManager が null の場合は原値を返す。
    /// デフォルト値 1f の技能は実質的に影響を与えない。
    /// </summary>
    public float ModifyIncomingHealing(float baseHealing)
    {
        if (baseHealing <= 0f)       return 0f;
        if (skillManager == null)    return baseHealing;

        float result = baseHealing;
        var   states = skillManager.RuntimeStates;
        if (states == null) return baseHealing;

        foreach (var state in states)
        {
            if (state == null)           continue;
            if (state.SkillData == null) continue;
            if (!state.IsActive)         continue;

            result *= state.SkillData.HealingReceivedMultiplier;
        }

        if (logDamageModification && !Mathf.Approximately(result, baseHealing))
            Debug.Log($"[PlayerStatusEffectController] Healing modified: base={baseHealing:F1}, final={result:F1}");

        return result;
    }

    // ─── Private ─────────────────────────────────────────────────

    private void ResolveSkillManager()
    {
        if (skillManager != null) return;

        skillManager = GetComponent<PlayerSkillManager>();
        if (skillManager != null) return;

        skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (skillManager == null)
            Debug.LogWarning("[PlayerStatusEffectController] PlayerSkillManager not found. Damage modification disabled.");
    }
}
