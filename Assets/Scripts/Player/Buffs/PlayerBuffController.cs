using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerBuffRuntime
{
    public string BuffId;
    public string DisplayName;
    public UnityEngine.Sprite Icon;
    public float  RemainingTime;
    public float  Duration;
    public float  Multiplier;
    public bool HasDuration => Duration > 0f;
    public bool IsExpired   => HasDuration && RemainingTime <= 0f;
}

public class PlayerBuffController : MonoBehaviour
{
    public const string NEXT_DAMAGE_BOOST_ID = "next_skill_damage_boost";

    private readonly Dictionary<string, PlayerBuffRuntime> _buffs = new();

    private void Update()
    {
        var toRemove = new List<string>();
        foreach (var kvp in _buffs)
        {
            if (!kvp.Value.HasDuration) continue;
            kvp.Value.RemainingTime -= Time.deltaTime;
            if (kvp.Value.RemainingTime <= 0f) { kvp.Value.RemainingTime = 0f; toRemove.Add(kvp.Key); Debug.Log($"[PlayerBuff] {kvp.Value.DisplayName} expired."); }
        }
        foreach (var id in toRemove) _buffs.Remove(id);
    }

    public IReadOnlyDictionary<string, PlayerBuffRuntime> ActiveBuffs => _buffs;
    public bool HasBuff(string buffId) => _buffs.ContainsKey(buffId);
    public PlayerBuffRuntime GetBuff(string buffId) { _buffs.TryGetValue(buffId, out var b); return b; }

    public PlayerBuffRuntime AddOrOverwrite(string buffId, string displayName, UnityEngine.Sprite icon, float duration, float multiplier)
    {
        var b = new PlayerBuffRuntime { BuffId=buffId, DisplayName=displayName, Icon=icon, Duration=duration, RemainingTime=duration, Multiplier=multiplier };
        _buffs[buffId] = b;
        return b;
    }

    public bool ConsumeBuff(string buffId)
    {
        if (!_buffs.ContainsKey(buffId)) return false;
        _buffs.Remove(buffId);
        return true;
    }

    /// <summary>
    /// PlayerSkillData から直接 Buff を追加 / 上書きする便利メソッド。
    /// skillData == null または duration &lt;= 0 の場合は何もしない。
    /// </summary>
    public void AddOrOverwriteSkillBuff(PlayerSkillData skillData)
    {
        if (skillData == null || skillData.Duration <= 0f) return;
        AddOrOverwrite(
            skillData.SkillId,
            skillData.SkillName,
            skillData.Icon,
            skillData.Duration,
            1f);
    }
}
