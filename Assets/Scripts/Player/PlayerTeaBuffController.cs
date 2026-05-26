using UnityEngine;

/// <summary>
/// 管理玩家当前生效的茶 Buff。
/// 挂在 Player 对象上。
/// - 使用新茶时覆盖旧茶（不叠加）
/// - Update 倒计时，到期自动清除
/// - 不接入 PlayerSkillManager / 技能栏 / Hikari
/// </summary>
public class PlayerTeaBuffController : MonoBehaviour
{
    private TeaBuffData _activeTeaBuff;
    private float       _remainingSeconds;

    // ── 状態查询 ─────────────────────────────────────────────────────
    public bool         HasActiveTeaBuff => _activeTeaBuff != null && _remainingSeconds > 0f;
    public TeaBuffData  ActiveTeaBuff    => _activeTeaBuff;
    public float        RemainingSeconds => _remainingSeconds;

    // ── Lifecycle ────────────────────────────────────────────────────
    private void Update()
    {
        if (_activeTeaBuff == null) return;
        _remainingSeconds -= Time.deltaTime;
        if (_remainingSeconds <= 0f)
        {
            _remainingSeconds = 0f;
            Debug.Log($"[PlayerTeaBuffController] Tea buff expired: {_activeTeaBuff.DisplayName}");
            _activeTeaBuff = null;
        }
    }

    // ── 使用茶 ───────────────────────────────────────────────────────
    /// <summary>
    /// 消耗一个茶道具并应用 Buff。
    /// 成功返回 true，调用方负责从 PlayerInventory 移除物品。
    /// </summary>
    public bool TryUseTea(ItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning("[PlayerTeaBuffController] TryUseTea: itemData is null.");
            return false;
        }
        if (itemData.ItemType != ItemType.Tea)
        {
            Debug.LogWarning($"[PlayerTeaBuffController] TryUseTea: {itemData.ItemName} is not a Tea item.");
            return false;
        }
        if (itemData.TeaBuffData == null)
        {
            Debug.LogWarning($"[PlayerTeaBuffController] TryUseTea: {itemData.ItemName} has no TeaBuffData assigned.");
            return false;
        }

        _activeTeaBuff    = itemData.TeaBuffData;
        _remainingSeconds = _activeTeaBuff.DurationSeconds;
        Debug.Log($"[PlayerTeaBuffController] Tea buff applied: {_activeTeaBuff.DisplayName} ({_activeTeaBuff.EffectType}, value={_activeTeaBuff.Value}, duration={_remainingSeconds}s)");
        return true;
    }

    // ── 效果查询 ─────────────────────────────────────────────────────
    /// <summary>
    /// 返回非必定掉落（dropChance &lt; 1f）的概率倍率。
    /// 无 Buff 或效果类型不符时返回 1f（无倍率）。
    /// </summary>
    public float GetNonGuaranteedDropChanceMultiplier()
    {
        if (!HasActiveTeaBuff) return 1f;
        if (_activeTeaBuff.EffectType == TeaBuffEffectType.NonGuaranteedDropChanceMultiplier)
            return _activeTeaBuff.Value;
        return 1f;
    }

    /// <summary>
    /// 返回 Material 额外掉落一个的概率（0~1）。
    /// 无 Buff 或效果类型不符时返回 0f（不额外掉落）。
    /// </summary>
    public float GetMaterialExtraQuantityChance()
    {
        if (!HasActiveTeaBuff) return 0f;
        if (_activeTeaBuff.EffectType == TeaBuffEffectType.MaterialExtraQuantityChance)
            return _activeTeaBuff.Value;
        return 0f;
    }
}
