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

    private PlayerBuffController _buffController;


    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        ResolveSkillManager();
        _buffController = GetComponent<PlayerBuffController>();
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
        if (damage <= 0f) return damage;
        LastIncomingDamageFullyBlockedByShield = false; // 毎回リセット


        float finalDamage = damage;

        // 1. DamageReduction 持続減山（Iron Bulwark / Stone Guard 等）
        if (skillManager != null)
        {
            var states = skillManager.RuntimeStates;
            foreach (var state in states)
            {
                if (state == null || state.SkillData == null || !state.IsActive) continue;
                if (state.SkillData.EffectType != PlayerSkillEffectType.DamageReduction) continue;
                finalDamage *= state.SkillData.DamageTakenMultiplier;
            }
        }

        // 2. Guard Conversion Shield 吸收（skillManager に依存しない）
        Debug.Log($"[PlayerStatusEffectController] ModifyIncomingDamage before shield: {finalDamage:F1} (hasShield={_hasGuardConversionShield})");
        finalDamage = ApplyGuardConversionShield(finalDamage, "incoming");

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
    /// <summary>
    /// Active な全技能の HealingReceivedMultiplier を乗算した最終倍率を返す。
    /// skillManager が null / RuntimeStates が null の場合は 1f を返す。
    /// EffectType は問わない（独立パラメータのため）。
    /// </summary>
    public float GetIncomingHealingReceivedMultiplier()
    {
        if (skillManager == null) return 1f;

        float multiplier = 1f;
        var   states     = skillManager.RuntimeStates;
        if (states == null) return 1f;

        foreach (var state in states)
        {
            if (state == null)           continue;
            if (state.SkillData == null) continue;
            if (!state.IsActive)         continue;

            multiplier *= state.SkillData.HealingReceivedMultiplier;
        }
        return multiplier;
    }

    /// <summary>
    /// Active 状態の技能に基づき、受ける治療量を修正する。
    /// GetIncomingHealingReceivedMultiplier() を使って乗算叠算を適用する。
    /// baseHealing &lt;= 0 または skillManager が null の場合は原值を返す。
    /// </summary>
    public float ModifyIncomingHealing(float baseHealing)
    {
        if (baseHealing <= 0f)    return 0f;
        if (skillManager == null) return baseHealing;

        float result = baseHealing * GetIncomingHealingReceivedMultiplier();

        if (logDamageModification && !Mathf.Approximately(result, baseHealing))
            Debug.Log($"[PlayerStatusEffectController] Healing modified: base={baseHealing:F1}, final={result:F1}");

        return result;
    }


    // ─── Next Skill Damage Boost ─────────────────────────────────

    private bool  _hasNextSkillDamageBoost;
    private float _nextSkillDamageMultiplier = 1f;

    /// <summary>現在 Next Skill Damage Boost が設定されているか。</summary>
    public bool HasNextSkillDamageBoost => (_buffController != null)
        ? _buffController.HasBuff(PlayerBuffController.NEXT_DAMAGE_BOOST_ID)
        : _hasNextSkillDamageBoost;

    /// <summary>
    /// 次のプレイヤー技能ダメージ強化を設定する。
    /// multiplier &lt;= 1f の場合は 1.0 として扱う。
    /// 既にある場合は上書き（v0.1 は非スタック）。
    /// </summary>
    public void SetNextSkillDamageBoost(float multiplier)
    {
        _nextSkillDamageMultiplier = Mathf.Max(1f, multiplier);
        _hasNextSkillDamageBoost   = true;
        Debug.Log($"[NextDamageBoost] next player skill damage x{_nextSkillDamageMultiplier:F2}");
    }

    /// <summary>
    /// Buff 情報付きオーバーロード。PlayerBuffController がある場合はそちらで管理する。
    /// </summary>
    public void SetNextSkillDamageBoost(float multiplier, PlayerSkillData sourceSkill, float duration)
    {
        float effectiveDuration = (duration > 0f) ? duration : 10f;
        Sprite icon     = sourceSkill?.Icon;
        string name_str = sourceSkill?.SkillName ?? "Next Damage Boost";

        if (_buffController != null)
        {
            _buffController.AddOrOverwrite(
                PlayerBuffController.NEXT_DAMAGE_BOOST_ID,
                name_str,
                icon,
                effectiveDuration,
                Mathf.Max(1f, multiplier));
            Debug.Log($"[NextDamageBoost] next player skill damage x{Mathf.Max(1f, multiplier):F2} (via BuffController, {effectiveDuration:F0}s)");
            // fallback 内部フラグも更新（ApplyAndConsume fallback 用）
            _nextSkillDamageMultiplier = Mathf.Max(1f, multiplier);
            _hasNextSkillDamageBoost   = true;
        }
        else
        {
            // PlayerBuffController がない場合は元の単一引数メソッドに委譲
            SetNextSkillDamageBoost(multiplier);
        }
    }

    /// <summary>
    /// 次のプレイヤー技能ダメージ強化を適用して消費する。
    /// boost がない場合は damage をそのまま返す。
    /// </summary>
    public float ApplyAndConsumeNextSkillDamageBoost(float damage, string sourceLabel = null)
    {
        // BuffController 経由消費
        if (_buffController != null)
        {
            var buff = _buffController.GetBuff(PlayerBuffController.NEXT_DAMAGE_BOOST_ID);
            if (buff == null) return damage;
            float mult   = buff.Multiplier;
            float boosted = damage * mult;
            _buffController.ConsumeBuff(PlayerBuffController.NEXT_DAMAGE_BOOST_ID);
            _hasNextSkillDamageBoost   = false;
            _nextSkillDamageMultiplier = 1f;
            string label = string.IsNullOrEmpty(sourceLabel) ? "skill" : sourceLabel;
            Debug.Log($"[NextDamageBoost] consumed by {label}: {damage:F1} -> {boosted:F1} (x{mult:F2})");
            return boosted;
        }

        // fallback: BuffController なし
        if (!_hasNextSkillDamageBoost) return damage;
        float boosted2 = damage * _nextSkillDamageMultiplier;
        _hasNextSkillDamageBoost   = false;
        _nextSkillDamageMultiplier = 1f;
        string label2 = string.IsNullOrEmpty(sourceLabel) ? "skill" : sourceLabel;
        Debug.Log($"[NextDamageBoost] consumed by {label2}: {damage:F1} -> {boosted2:F1}");
        return boosted2;
    }

    // ─── Next Incoming Damage Reduction ─────────────────────────

    private const string _NIDR_ID = PlayerBuffController.NEXT_INCOMING_DAMAGE_REDUCTION_ID;

    /// <summary>Guard Conversion など次の受ダメージ剩減 Buff が設定されているか。</summary>
    public bool HasNextIncomingDamageReduction => (_buffController != null)
        ? _buffController.HasBuff(_NIDR_ID)
        : false;

    /// <summary>
    /// 次の受ダメージ剩減 Buff を設定する（PlayerBuffController 経由）。
    /// multiplier &gt;= 1f は無効として無視。0 以下は 0.01f にクランプ。
    /// </summary>
    public void SetNextIncomingDamageReduction(float damageTakenMultiplier, PlayerSkillData sourceSkill, float duration)
    {
        if (damageTakenMultiplier >= 1f) return;
        float mult = Mathf.Clamp(damageTakenMultiplier, 0.01f, 1f);
        float dur  = duration > 0f ? duration : 6f;

        string name_str = sourceSkill?.SkillName ?? "Guard Conversion";
        Sprite icon     = sourceSkill?.Icon;

        if (_buffController != null)
        {
            _buffController.AddOrOverwrite(_NIDR_ID, name_str, icon, dur, mult);
            Debug.Log($"[NextIncomingDamageReduction] Set: x{mult:F2} ({dur:F0}s) via BuffController");
        }
        else
        {
            Debug.LogWarning("[NextIncomingDamageReduction] PlayerBuffController not found。");
        }
    }

    /// <summary>
    /// 次の受ダメージ剩減 Buff を適用して消費する。
    /// Buff がない場合は原値を返す。
    /// </summary>
    public float ApplyAndConsumeNextIncomingDamageReduction(float incomingDamage, string label = null)
    {
        if (_buffController == null) return incomingDamage;
        var buff = _buffController.GetBuff(_NIDR_ID);
        if (buff == null) return incomingDamage;

        float reduced = incomingDamage * buff.Multiplier;
        _buffController.ConsumeBuff(_NIDR_ID);
        string lbl = string.IsNullOrEmpty(label) ? "damage" : label;
        Debug.Log($"[NextIncomingDamageReduction] consumed by {lbl}: {incomingDamage:F1} -> {reduced:F1} (x{buff.Multiplier:F2})");
        return reduced;
    }

    // ─── Guard Conversion Shield ─────────────────────────────────

    private const string _GCS_ID = PlayerBuffController.GUARD_CONVERSION_SHIELD_ID;

    private bool  _hasGuardConversionShield;
    private float _guardConversionShieldRemaining;
    private float _guardConversionShieldMax;
    private int   _guardConversionRefundOnBreak;
    private PlayerGuardCounterController _guardCounterCtrl;

    /// <summary>Guard Conversion 護盾が設定されているか。</summary>
    public bool  HasGuardConversionShield            => _hasGuardConversionShield;
    /// <summary>直前の ModifyIncomingDamage で护盾が全量吸收したか。HealthComponent が毎回リセットする。</summary>
    public bool  LastIncomingDamageFullyBlockedByShield { get; private set; }

    /// <summary>護盾残量。</summary>
    public float GuardConversionShieldRemaining => _guardConversionShieldRemaining;
    /// <summary>護盾最大量。</summary>
    public float GuardConversionShieldMax => _guardConversionShieldMax;

    /// <summary>
    /// Guard Conversion 護盾を設定する。
    /// shieldAmount &lt;= 0 の場合は何もしない。同名護盾は上書き（返還なし）。
    /// </summary>
    public void SetGuardConversionShield(float shieldAmount, int refundOnBreak, PlayerSkillData sourceSkill, float duration)
    {
        if (shieldAmount <= 0f) return;
        float dur = duration > 0f ? duration : 6f;

        _hasGuardConversionShield       = true;
        _guardConversionShieldRemaining = shieldAmount;
        _guardConversionShieldMax       = shieldAmount;
        _guardConversionRefundOnBreak   = refundOnBreak;

        // PlayerGuardCounterController を遅延解決
        if (_guardCounterCtrl == null)
            _guardCounterCtrl = GetComponent<PlayerGuardCounterController>();

        // Buff UI 追加
        if (_buffController != null)
        {
            _buffController.AddOrOverwrite(
                _GCS_ID,
                sourceSkill?.SkillName ?? "Guard Conversion",
                sourceSkill?.Icon,
                dur,
                1f);
        }
        Debug.Log($"[GuardConversionShield] Set shield: {shieldAmount:F1} for {dur:F1}s");
    }

    /// <summary>
    /// Guard Conversion 護盾への受伤を処理する。
    /// 護盾がない場合は incomingDamage をそのまま返す。
    /// 護盾が残っている場合は吸収し、余剰ダメージのみ返す。
    /// 護盾が割れた場合は Buff を削除し、Combat Momentum を返還する。
    /// </summary>
    public float ApplyGuardConversionShield(float incomingDamage, string label = null)
    {
        if (!_hasGuardConversionShield || incomingDamage <= 0f) return incomingDamage;

        if (incomingDamage < _guardConversionShieldRemaining)
        {
            // 護盾で完全吸収
            _guardConversionShieldRemaining -= incomingDamage;
            Debug.Log($"[GuardConversionShield] absorbed: {incomingDamage:F1}, remaining: {_guardConversionShieldRemaining:F1}");
            LastIncomingDamageFullyBlockedByShield = true;
            return 0f;
        }
        else
        {
            // 護盾を割って余剰ダメージが残る
            float overflow = incomingDamage - _guardConversionShieldRemaining;
            Debug.Log($"[GuardConversionShield] broke, absorbed: {_guardConversionShieldRemaining:F1}, overflow damage: {overflow:F1}");
            _hasGuardConversionShield       = false;
            _guardConversionShieldRemaining = 0f;
            _guardConversionShieldMax       = 0f;
            _buffController?.ConsumeBuff(_GCS_ID);
            if (_guardConversionRefundOnBreak > 0 && _guardCounterCtrl != null)
            {
                _guardCounterCtrl.AddCombatMomentum(_guardConversionRefundOnBreak);
                Debug.Log($"[GuardConversionShield] refund Combat Momentum +{_guardConversionRefundOnBreak}");
            }
            _guardConversionRefundOnBreak = 0;
            return overflow;
        }
    }

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
