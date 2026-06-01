using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// PlayerSkillRuntimeState
// PlayerSkillManager.cs 内の内部クラス。
// 1 つの PlayerSkillData に対する実行時状態を保持する。
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class PlayerSkillRuntimeState
{
    [SerializeField] private PlayerSkillData skillData;
    [SerializeField] private float           activeRemainingTime;
    [SerializeField] private float           cooldownRemainingTime;

    // ─── 公开只读属性 ─────────────────────────────────────────────

    public PlayerSkillData SkillData             => skillData;
    public string          SkillId               => skillData != null ? skillData.SkillId : "";
    public bool            IsActive              => activeRemainingTime   > 0f;
    public bool            IsOnCooldown          => cooldownRemainingTime > 0f;
    public bool            IsReady               => skillData != null && !IsActive && !IsOnCooldown;
    public float           ActiveRemainingTime   => activeRemainingTime;
    public float           CooldownRemainingTime => cooldownRemainingTime;

    /// <summary>冷却の残り割合（0 = 冷却完了 / 1 = 冷却開始直後）。</summary>
    public float CooldownNormalized
    {
        get
        {
            if (skillData == null || skillData.Cooldown <= 0f) return 0f;
            return Mathf.Clamp01(cooldownRemainingTime / skillData.Cooldown);
        }
    }

    /// <summary>持続時間の残り割合（0 = 終了 / 1 = 開始直後）。</summary>
    public float ActiveNormalized
    {
        get
        {
            if (skillData == null || skillData.Duration <= 0f) return 0f;
            return Mathf.Clamp01(activeRemainingTime / skillData.Duration);
        }
    }

    // ─── 初期化 ───────────────────────────────────────────────────

    public void Initialize(PlayerSkillData data)
    {
        skillData             = data;
        activeRemainingTime   = 0f;
        cooldownRemainingTime = 0f;
    }

    // ─── 毎フレーム更新 ───────────────────────────────────────────

    public void Tick(float deltaTime)
    {
        if (activeRemainingTime   > 0f) activeRemainingTime   = Mathf.Max(0f, activeRemainingTime   - deltaTime);
        if (cooldownRemainingTime > 0f) cooldownRemainingTime = Mathf.Max(0f, cooldownRemainingTime - deltaTime);
    }

    // ─── 激活 ─────────────────────────────────────────────────────

    /// <summary>
    /// 技能を発動する。
    /// 成功すると active / cooldown タイマーをセットして true を返す。
    /// 既に active / cooldown 中、または skillData null の場合は false を返す。
    /// duration = 0 でも発動し、即座に cooldown へ移行する（瞬発技能対応）。
    /// </summary>
    public bool TryActivate()
    {
        if (skillData == null)    return false;
        if (IsActive)             return false;
        if (IsOnCooldown)         return false;

        activeRemainingTime   = skillData.Duration;
        cooldownRemainingTime = skillData.Cooldown;
        return true;
    }

    // ─── 検索ヘルパー ─────────────────────────────────────────────

    public bool MatchesSkillId(string id)              => skillData != null && skillData.SkillId == id;
    public bool MatchesInputSlot(PlayerSkillInputSlot slot) => skillData != null && skillData.InputSlot == slot;

    /// <summary>
    /// 冒撤リマインを指定秒数少なくする。最小 0。冒撤中でない場合は何もしない。
    /// </summary>
    public void ReduceCooldown(float seconds)
    {
        if (cooldownRemainingTime <= 0f) return;
        cooldownRemainingTime = Mathf.Max(0f, cooldownRemainingTime - seconds);
    }

}

// ─────────────────────────────────────────────────────────────────────────────
// PlayerSkillManager
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 玩家技能统一管理器 — v0.1。
/// 持有 PlayerSkillData 数组，根据 PlayerSkillInputSlot 监听 New Input System 按键，
/// 并管理每个技能的激活状态、持续时间、冷却时间。
///
/// 本版本只做并行运行时状态管理，不执行实际技能效果。
/// 现有 Iron Bulwark 逻辑仍由 PlayerMitigationController 负责，本脚本不干预。
/// 下一步 UI / StatusEffect 系统可通过公开 API 读取运行时状态。
/// </summary>
public class PlayerSkillManager : MonoBehaviour
{
    [Header("技能数据")]
    [SerializeField] private PlayerSkillData[] skills;

    [Header("装备被动技能（临时构筑入口，未来由技能解锁系统控制）")]
    [SerializeField] private PlayerSkillData[] equippedPassiveSkills;


    [Header("调试")]
    [SerializeField] private bool logSkillActivation = true;

    // 运行时状态列表（Inspector 中只读可见，方便调试）
    [SerializeField]
    private List<PlayerSkillRuntimeState> runtimeStates = new List<PlayerSkillRuntimeState>();


    // 最後に対応キーを押した技能の RuntimeState（冷却中でも更新される）
    private PlayerSkillRuntimeState lastPressedSkillState;
    private PlayerGuardCounterController _guardCounterController;
    private PlayerBuffController             _buffController;
    private PlayerStatusEffectController     _statusEffectController;
    private PlayerBasicAttackController      _basicAttackController;
    private HealthComponent                  _playerHealth;

    /// <summary>最後にキーを押した技能の RuntimeState。一度も押していなければ null。</summary>

    // ─── 技能成功发动事件 ────────────────────────────────────────

    /// <summary>
    /// 技能が成功して発動した直後に発火するイベント。
    /// 冷却中・Already Active 時の入力では発火しない。
    /// </summary>
    public event Action<PlayerSkillRuntimeState> OnSkillActivated;

    public PlayerSkillRuntimeState LastPressedSkillState => lastPressedSkillState;

    // ─── 公开查询 API ─────────────────────────────────────────────

    /// <summary>すべての技能の実行時状態リスト（読み取り専用）。</summary>
    public IReadOnlyList<PlayerSkillRuntimeState> RuntimeStates => runtimeStates;

    /// <summary>skillId で実行時状態を返す。見つからなければ null。</summary>
    public PlayerSkillRuntimeState GetStateBySkillId(string skillId)
    {
        foreach (var s in runtimeStates)
            if (s.MatchesSkillId(skillId)) return s;
        return null;
    }

    /// <summary>InputSlot で実行時状態を返す。見つからなければ null。</summary>
    public PlayerSkillRuntimeState GetStateByInputSlot(PlayerSkillInputSlot slot)
    {
        foreach (var s in runtimeStates)
            if (s.MatchesInputSlot(slot)) return s;
        return null;
    }

    /// <summary>skillId で技能を外部から発動する。成功すれば true。</summary>
    public bool TryActivateSkillById(string skillId)
    {
        var state = GetStateBySkillId(skillId);
        if (state == null) return false;
        return TryActivateSkill(state);
    }

    /// <summary>InputSlot で技能を外部から発動する。成功すれば true。</summary>
    public bool TryActivateSkillByInputSlot(PlayerSkillInputSlot slot)
    {
        var state = GetStateByInputSlot(slot);
        if (state == null) return false;
        return TryActivateSkill(state);
    }

    /// <summary>
    /// 指定した PlayerSkillData の現在冒撤を指定秒数少なくする。
    /// skillData が null / seconds ≤ 0 / RuntimeState が見つからない場合は false 。
    /// skillData.Cooldown の周期属性自体は変更しない。
    /// </summary>
    public bool ReduceCooldown(PlayerSkillData skillData, float seconds)
    {
        if (skillData == null || seconds <= 0f) return false;
        var state = GetStateBySkillId(skillData.SkillId);
        if (state == null) return false;
        state.ReduceCooldown(seconds);
        return true;
    }


    /// <summary>装備中の被动技能配列（読み取り専用）。</summary>

    /// <summary>
    /// 技能が現在リソース条件を満たしているかを判定する。
    /// UI および入力弱出期の共通クエリ。
    /// - skillData == null → false
    /// - CombatMomentumCost &lt;= 0 → true（コストなし）
    /// - _guardCounterController == null → false
    /// - CurrentCombatMomentum &gt;= CombatMomentumCost → true
    /// - それ以外 → false
    /// </summary>
    public bool IsSkillResourceUsable(PlayerSkillData skillData)
    {
        if (skillData == null) return false;
        if (skillData.CombatMomentumCost <= 0) return true;
        if (_guardCounterController == null) return false;
        return _guardCounterController.CurrentCombatMomentum >= skillData.CombatMomentumCost;
    }

    public PlayerSkillData[] EquippedPassiveSkills => equippedPassiveSkills;

    /// <summary>
    /// 被动技能のトリガー入口。
    /// PlayerGuardCounterController などが成功イベント後に呼び出す。
    /// </summary>
    public void NotifyPassiveTrigger(PlayerPassiveTriggerType triggerType)
    {
        if (triggerType == PlayerPassiveTriggerType.None) return;
        if (equippedPassiveSkills == null) return;

        foreach (var passive in equippedPassiveSkills)
        {
            if (passive == null) continue;
            if (!passive.IsPassive) continue;
            if (passive.PassiveTriggerType != triggerType) continue;

            switch (passive.PassiveEffectType)
            {
                case PlayerPassiveEffectType.ReduceCooldown:
                    if (passive.PassiveTargetSkill != null && passive.PassiveValue > 0f)
                    {
                        bool ok = ReduceCooldown(passive.PassiveTargetSkill, passive.PassiveValue);
                        if (ok)
                            Debug.Log($"[PassiveSkill] {passive.SkillName}: {passive.PassiveTargetSkill.SkillName} cooldown -{passive.PassiveValue:F1}s");
                        else
                            Debug.Log($"[PassiveSkill] {passive.SkillName}: target skill runtime state not found");
                    }
                    else
                    {
                        Debug.Log($"[PassiveSkill] {passive.SkillName}: PassiveTargetSkill または PassiveValue が未設定。");
                    }
                    break;

                case PlayerPassiveEffectType.AddNextSkillDamageMultiplier:
                    // 未実装—将来の構築システム用
                    Debug.Log($"[PassiveSkill] {passive.SkillName}: AddNextSkillDamageMultiplier は未実装。");
                    break;

                default:
                    break;
            }
        }
    }

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        BuildRuntimeStates();
        _guardCounterController = GetComponent<PlayerGuardCounterController>();
        _buffController         = GetComponent<PlayerBuffController>();
        _statusEffectController  = GetComponent<PlayerStatusEffectController>();
        _basicAttackController  = GetComponent<PlayerBasicAttackController>();
        _playerHealth = GetComponent<HealthComponent>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var state in runtimeStates)
            state.Tick(dt);

        HandleSkillInput();
    }

    // ─── 初期化 ───────────────────────────────────────────────────

    private void BuildRuntimeStates()
    {
        runtimeStates.Clear();

        if (skills == null || skills.Length == 0)
        {
            Debug.LogWarning("[PlayerSkillManager] skills 数组为空，没有任何技能被注册。");
            return;
        }

        var registeredIds = new HashSet<string>();

        foreach (var skill in skills)
        {
            if (skill == null)
            {
                Debug.LogWarning("[PlayerSkillManager] skills 数组中存在 null 元素，已跳过。");
                continue;
            }

            if (registeredIds.Contains(skill.SkillId))
            {
                Debug.LogWarning($"[PlayerSkillManager] 重复的 skillId: \"{skill.SkillId}\"，已跳过。");
                continue;
            }

            var state = new PlayerSkillRuntimeState();
            state.Initialize(skill);
            runtimeStates.Add(state);
            registeredIds.Add(skill.SkillId);
        }

        Debug.Log($"[PlayerSkillManager] 注册了 {runtimeStates.Count} 个技能。");
    }

    // ─── 入力処理 ─────────────────────────────────────────────────

private void HandleSkillInput()
    {
        // 死亡時はスキル入力を無視する（全スキル共通）
        if (_playerHealth != null && _playerHealth.IsDead) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        foreach (var state in runtimeStates)
        {
            if (state.SkillData == null) continue;

            if (WasInputSlotPressed(state.SkillData.InputSlot))
            {
                // 冷却中でも「最後に押した技能」として記録する
                lastPressedSkillState = state;
                // EffectType に応じて実行を振り分ける
                if (state.SkillData.EffectType == PlayerSkillEffectType.BasicMeleeAttack)
                {
                    _basicAttackController?.TryExecuteBasicMeleeAttack(state.SkillData);
                }
                else if (state.SkillData.EffectType == PlayerSkillEffectType.BasicAreaAttack)
                {
                    _basicAttackController?.TryExecuteBasicAreaAttack(state.SkillData);
                }
                else if (state.SkillData.EffectType == PlayerSkillEffectType.GuardCounter)
                {
                    if (_guardCounterController != null)
                        _guardCounterController.TryUseCounter(state.SkillData);
                }
                else if (state.SkillData.EffectType == PlayerSkillEffectType.NextSkillDamageBoost)
                {
                    TryActivateNextSkillDamageBoost(state);
                }
                else if (state.SkillData.EffectType == PlayerSkillEffectType.NextIncomingDamageReduction)
                {
                    TryActivateNextIncomingDamageReduction(state);
                }
                else if (state.SkillData.EffectType == PlayerSkillEffectType.DamageShield)
                {
                    TryActivateDamageShield(state);
                }
                else
                {
                    TryActivateSkill(state);
                }
            }
        }
    }

    private void TryActivateDamageShield(PlayerSkillRuntimeState state)
    {
        if (state == null || state.SkillData == null) return;
        Debug.Log($"[GuardConversion] TryActivateDamageShield called: {state.SkillData.SkillName}");

        int cost = state.SkillData.CombatMomentumCost;
        if (cost > 0)
        {
            if (_guardCounterController == null || !_guardCounterController.TrySpendCombatMomentum(cost))
            {
                Debug.Log($"[GuardConversion] Combat Momentum 不足（{state.SkillData.SkillName} には {cost} 点必要）。");
                return;
            }
        }

        if (_statusEffectController == null)
        {
            Debug.LogWarning("[GuardConversion] PlayerStatusEffectController not found。");
            return;
        }

        float dur = state.SkillData.Duration > 0f ? state.SkillData.Duration : 6f;
        _statusEffectController.SetGuardConversionShield(
            state.SkillData.ShieldAmount,
            state.SkillData.CombatMomentumRefundOnShieldBreak,
            state.SkillData,
            dur);
        state.TryActivate();
        OnSkillActivated?.Invoke(state);
        if (logSkillActivation)
            Debug.Log($"[PlayerSkillManager] Activated: {state.SkillData.SkillName}");
    }

    private void TryActivateNextIncomingDamageReduction(PlayerSkillRuntimeState state)
    {
        if (state == null || state.SkillData == null) return;
        Debug.Log($"[GuardConversion] TryActivateNextIncomingDamageReduction called: {state.SkillData.SkillName}");

        int cost = state.SkillData.CombatMomentumCost;
        if (cost > 0)
        {
            if (_guardCounterController == null || !_guardCounterController.TrySpendCombatMomentum(cost))
            {
                Debug.Log($"[GuardConversion] Combat Momentum 不足（{state.SkillData.SkillName} には {cost} 点必要）。");
                return;
            }
        }

        if (_statusEffectController == null)
        {
            Debug.LogWarning("[GuardConversion] PlayerStatusEffectController not found。");
            return;
        }

        float dur = state.SkillData.Duration > 0f ? state.SkillData.Duration : 6f;
        _statusEffectController.SetNextIncomingDamageReduction(
            state.SkillData.NextIncomingDamageTakenMultiplier,
            state.SkillData,
            dur);
        state.TryActivate();
        OnSkillActivated?.Invoke(state);
        if (logSkillActivation)
            Debug.Log($"[PlayerSkillManager] Activated: {state.SkillData.SkillName}");
    }

    private void TryActivateNextSkillDamageBoost(PlayerSkillRuntimeState state)
    {
        if (state == null || state.SkillData == null) return;
        Debug.Log($"[MomentumFocus] TryActivateNextSkillDamageBoost called: {state.SkillData.SkillName}");


        int cost = state.SkillData.CombatMomentumCost;
        if (cost > 0)
        {
            if (_guardCounterController == null || !_guardCounterController.TrySpendCombatMomentum(cost))
            {
                Debug.Log($"[MomentumFocus] Combat Momentum 不足（{state.SkillData.SkillName} には {cost} 点必要）。");
                return;
            }
        }

        if (_statusEffectController == null)
        {
            Debug.LogWarning("[MomentumFocus] PlayerStatusEffectController not found。");
            return;
        }

        float buffDuration = state.SkillData.Duration > 0f ? state.SkillData.Duration : 10f;
        _statusEffectController.SetNextSkillDamageBoost(
            state.SkillData.NextSkillDamageMultiplier,
            state.SkillData,
            buffDuration);
        state.TryActivate();
        OnSkillActivated?.Invoke(state);
        if (logSkillActivation)
            Debug.Log($"[PlayerSkillManager] Activated: {state.SkillData.SkillName}");
    }

    /// <summary>
    /// この技能が Buff UI に持続表示すべきかどうかを判断する。
    /// 瞬発技能（Basic Attack, GuardCounter, NextSkillDamageBoost）は除外。
    /// </summary>
    private static bool IsTimedSkillBuff(PlayerSkillData skill)
    {
        if (skill == null || skill.Duration <= 0f) return false;
        if (skill.IsPassive) return false;
        var et = skill.EffectType;
        return et == PlayerSkillEffectType.DamageReduction
            || et == PlayerSkillEffectType.AttackPowerMultiplier;
    }

    private bool TryActivateSkill(PlayerSkillRuntimeState state)
    {
        if (state == null || state.SkillData == null) return false;

        if (state.TryActivate())
        {
            OnSkillActivated?.Invoke(state);
            if (logSkillActivation)
            if (logSkillActivation)
                Debug.Log($"[PlayerSkillManager] Activated skill: {state.SkillData.SkillName} ({state.SkillData.SkillId})");

            // 持続型 Buff を Buff UI に追加
            bool isTimedBuff = IsTimedSkillBuff(state.SkillData);
            Debug.Log($"[SkillBuffDisplay] TryActivateSkill success: {state.SkillData.SkillName} | IsTimedSkillBuff={isTimedBuff}");
            if (isTimedBuff)
            {
                if (_buffController != null)
                {
                    _buffController.AddOrOverwriteSkillBuff(state.SkillData);
                    Debug.Log($"[SkillBuffDisplay] Add skill buff: {state.SkillData.SkillName} {state.SkillData.Duration:F1}s");
                }
                else
                {
                    Debug.Log("[SkillBuffDisplay] skip: PlayerBuffController not found");
                }
            }
        }

        // 失敗理由を出力（按键时のみ呼ばれるので毎フレーム刷屏にはならない）
        if (logSkillActivation)
        {
            if (state.IsActive)
                Debug.Log($"[PlayerSkillManager] {state.SkillData.SkillName}: 技能正在生效中（{state.ActiveRemainingTime:F1}s 剩余）。");
            else if (state.IsOnCooldown)
                Debug.Log($"[PlayerSkillManager] {state.SkillData.SkillName}: 冷却中（{state.CooldownRemainingTime:F1}s 剩余）。");
        }
        return false;
    }

    // ─── InputSlot → New Input System 按键 映射 ───────────────────

    private bool WasInputSlotPressed(PlayerSkillInputSlot slot)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;

        switch (slot)
        {
            case PlayerSkillInputSlot.Slot1: return kb.digit1Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot2: return kb.digit2Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot3: return kb.digit3Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot4: return kb.digit4Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot5: return kb.digit5Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot6: return kb.digit6Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot7: return kb.digit7Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot8: return kb.digit8Key.wasPressedThisFrame;
            case PlayerSkillInputSlot.Slot9: return kb.digit9Key.wasPressedThisFrame;
            default:                         return false;
        }
    }
}
