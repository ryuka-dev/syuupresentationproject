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

    [Header("调试")]
    [SerializeField] private bool logSkillActivation = true;

    // 运行时状态列表（Inspector 中只读可见，方便调试）
    [SerializeField]
    private List<PlayerSkillRuntimeState> runtimeStates = new List<PlayerSkillRuntimeState>();


    // 最後に対応キーを押した技能の RuntimeState（冷却中でも更新される）
    private PlayerSkillRuntimeState lastPressedSkillState;

    /// <summary>最後にキーを押した技能の RuntimeState。一度も押していなければ null。</summary>
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

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        BuildRuntimeStates();
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
        var kb = Keyboard.current;
        if (kb == null) return;

        foreach (var state in runtimeStates)
        {
            if (state.SkillData == null) continue;

            if (WasInputSlotPressed(state.SkillData.InputSlot))
            {
                // 冷却中でも「最後に押した技能」として記録する
                lastPressedSkillState = state;
                TryActivateSkill(state);
            }
        }
    }

    private bool TryActivateSkill(PlayerSkillRuntimeState state)
    {
        if (state == null || state.SkillData == null) return false;

        if (state.TryActivate())
        {
            if (logSkillActivation)
                Debug.Log($"[PlayerSkillManager] Activated skill: {state.SkillData.SkillName} ({state.SkillData.SkillId})");
            return true;
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
