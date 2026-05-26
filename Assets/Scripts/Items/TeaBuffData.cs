using UnityEngine;

public enum TeaBuffEffectType
{
    None,
    NonGuaranteedDropChanceMultiplier,
    MaterialExtraQuantityChance
}

/// <summary>
/// 茶的 Buff 效果配置 ScriptableObject。
/// 第一版只支持单效果：非必定掉落概率倍率 / Material 额外数量概率。
/// </summary>
[CreateAssetMenu(fileName = "New TeaBuff", menuName = "RPG/Items/Tea Buff Data")]
public class TeaBuffData : ScriptableObject
{
    [SerializeField] private string buffId;
    [SerializeField] private string displayName;
    [SerializeField] private TeaBuffEffectType effectType = TeaBuffEffectType.None;
    [SerializeField] private float value = 1f;
    [SerializeField] private float durationSeconds = 600f;

    public string BuffId           => buffId;
    public string DisplayName      => displayName;
    public TeaBuffEffectType EffectType => effectType;
    public float Value             => value;
    public float DurationSeconds   => durationSeconds;
}
