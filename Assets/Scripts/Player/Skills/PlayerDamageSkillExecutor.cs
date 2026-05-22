using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家攻击技能效果执行层 — 第一版 AreaDamage 专用。
/// 挂载在 Player 对象上。
///
/// 职责：
///   - 订阅 PlayerSkillManager.OnSkillActivated。
///   - 只处理 effectType == AreaDamage 的技能。
///   - 以玩家位置为中心，用 Physics.OverlapSphere 搜索半径内 Collider。
///   - 过滤敌对且存活的目标，每个 HealthComponent 只命中一次。
///   - 调用 HealthComponent.TakeDamage(finalDamage, transform)。
///
/// 不做的事：
///   - 不读取键盘输入。
///   - 不管理冷却时间。
///   - 不生成技能栏 UI。
///   - 不保存技能列表（由 PlayerSkillManager 统一持有）。
///   - 不处理 DamageReduction / AttackPowerMultiplier（本次语义未整理）。
/// </summary>
public class PlayerDamageSkillExecutor : MonoBehaviour
{
    [Header("调试")]
    [SerializeField] private bool logAoeDamage = true;

    // ─── 内部引用 ─────────────────────────────────────────────────
    private PlayerSkillManager  _skillManager;
    private PlayerCombatStats   _combatStats;
    private FactionComponent    _selfFaction;

    /// <summary>AOE 基础伤害 fallback（PlayerCombatStats 缺失时使用）。</summary>
    private const float FallbackBaseDamage = 20f;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        _skillManager = GetComponent<PlayerSkillManager>();
        _combatStats  = GetComponent<PlayerCombatStats>();
        _selfFaction  = GetComponent<FactionComponent>();

        if (_skillManager == null)
            Debug.LogWarning("[PlayerDamageSkillExecutor] PlayerSkillManager not found on same GameObject.");
        if (_selfFaction == null)
            Debug.LogWarning("[PlayerDamageSkillExecutor] FactionComponent not found on same GameObject.");
    }

    private void OnEnable()
    {
        if (_skillManager != null)
            _skillManager.OnSkillActivated += HandleSkillActivated;
    }

    private void OnDisable()
    {
        if (_skillManager != null)
            _skillManager.OnSkillActivated -= HandleSkillActivated;
    }

    // ─── 事件处理 ─────────────────────────────────────────────────

    private void HandleSkillActivated(PlayerSkillRuntimeState state)
    {
        if (state == null || state.SkillData == null) return;
        if (state.SkillData.EffectType != PlayerSkillEffectType.AreaDamage) return;

        ExecuteAreaDamage(state.SkillData);
    }

    // ─── AOE 伤害执行 ─────────────────────────────────────────────

    private void ExecuteAreaDamage(PlayerSkillData skill)
    {
        float radius      = skill.AreaRadius;
        float baseDamage  = _combatStats != null
            ? _combatStats.CurrentNormalAttackDamage
            : FallbackBaseDamage;
        float finalDamage = baseDamage * skill.AreaDamageMultiplier;

        // Physics.OverlapSphere で範囲内 Collider を取得
        var hits = Physics.OverlapSphere(transform.position, radius);

        // 同一 HealthComponent への重複命中を防ぐ HashSet
        var damaged = new HashSet<HealthComponent>();
        int hitCount = 0;

        foreach (var col in hits)
        {
            if (col == null) continue;

            // HealthComponent を自身 or 親から探す
            var health = col.GetComponentInParent<HealthComponent>();
            if (health == null) continue;

            // 重複命中スキップ（同一敵の複数 Collider 対策）
            if (!damaged.Add(health)) continue;

            // 自分自身はスキップ
            if (health.gameObject == gameObject) continue;

            // 死亡済みはスキップ
            if (health.IsDead) continue;

            // FactionComponent で敵対判定
            var targetFaction = col.GetComponentInParent<FactionComponent>();
            if (targetFaction == null) continue;
            if (_selfFaction == null) continue;
            if (!_selfFaction.ShouldAttack(targetFaction.faction)) continue;

            // 伤害結算
            health.TakeDamage(finalDamage, transform);
            hitCount++;

            if (logAoeDamage)
                Debug.Log($"[PlayerDamageSkillExecutor] {skill.SkillName} hit: {health.gameObject.name}, damage={finalDamage:F1}");
        }

        if (logAoeDamage)
            Debug.Log($"[PlayerDamageSkillExecutor] {skill.SkillName} executed. radius={radius}, baseDmg={baseDamage:F1}, finalDmg={finalDamage:F1}, targets hit={hitCount}");
    }
}
