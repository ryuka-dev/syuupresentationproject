using UnityEngine;

/// <summary>
/// 玩家技能输入槽位。
/// PlayerSkillManager 会根据槽位映射到 New Input System 的对应按键。
/// 不直接依赖旧 UnityEngine.Input 或 KeyCode。
/// </summary>
public enum PlayerSkillInputSlot
{
    None,
    Slot1,
    Slot2,
    Slot3,
    Slot4,
    Slot5,
    Slot6,
    Slot7,
    Slot8,
    Slot9,
}

/// <summary>
/// 玩家技能效果类型。
/// 第一版只支持 Iron Bulwark 的 DamageReduction。
/// 后续添加新类型时在此 enum 扩展。
/// </summary>
public enum PlayerSkillEffectType
{
    None,
    DamageReduction,
    AttackPowerMultiplier,
    AreaDamage,
}

/// <summary>
/// 玩家技能视觉表现类型。
/// DefenseRing 对应 Iron Bulwark 的脚下防御光环。
/// 第一版只存数据，不接入视觉逻辑。
/// </summary>
public enum PlayerSkillVisualType
{
    None,
    DefenseRing,
}

/// <summary>
/// 玩家技能静态数据 ScriptableObject。
/// 只保存技能参数，不持有运行时状态，不引用 Player 或 Scene 对象。
/// 通过 Create > Game > Player Skill Data 创建资产。
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerSkillData", menuName = "Game/Player Skill Data")]
public class PlayerSkillData : ScriptableObject
{
    [Header("基本信息")]
    [SerializeField] private string skillId   = "new_skill";
    [SerializeField] private string skillName = "New Skill";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("输入设置")]
    [SerializeField] private PlayerSkillInputSlot inputSlot = PlayerSkillInputSlot.None;
    [SerializeField] private string keyLabel = "";

    [Header("时间参数")]
    [SerializeField] private float cooldown = 1f;
    [SerializeField] private float duration = 0f;

    [Header("技能效果")]
    [SerializeField] private PlayerSkillEffectType effectType            = PlayerSkillEffectType.None;
    [SerializeField] private float                 damageTakenMultiplier = 1f;
    [SerializeField] private float                 attackPowerMultiplier = 1f;

        [Header("AOE 伤害参数（EffectType = AreaDamage 时有效）")]
    [SerializeField] private float areaRadius            = 3f;
    [SerializeField] private float areaDamageMultiplier  = 0.8f;
    [Header("治疗参数")]
    [SerializeField, Min(0f)]
    private float healingReceivedMultiplier = 1f;


[Header("视觉表现")]
    [SerializeField] private PlayerSkillVisualType visualType = PlayerSkillVisualType.None;

    // ─── 公开只读属性 ─────────────────────────────────────────────

    public string                SkillId               => skillId;
    public string                SkillName             => skillName;
    public string                Description           => description;
    public Sprite                Icon                  => icon;
    public PlayerSkillInputSlot  InputSlot             => inputSlot;
    public string                KeyLabel              => keyLabel;
    public float                 Cooldown              => cooldown;
    public float                 Duration              => duration;
    public PlayerSkillEffectType EffectType            => effectType;
    public float                 DamageTakenMultiplier => damageTakenMultiplier;
    public float                 AttackPowerMultiplier => attackPowerMultiplier;
        public float                 AreaRadius            => areaRadius;
    public float                 AreaDamageMultiplier  => areaDamageMultiplier;
public float                 HealingReceivedMultiplier => healingReceivedMultiplier;
public PlayerSkillVisualType VisualType            => visualType;

    // ─── OnValidate ───────────────────────────────────────────────

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(skillId))
            Debug.LogWarning($"[PlayerSkillData] {name}: skillId is empty.");

        if (string.IsNullOrEmpty(skillName))
            Debug.LogWarning($"[PlayerSkillData] {name}: skillName is empty.");

        if (cooldown < 0f) cooldown = 0f;
        if (duration < 0f) duration = 0f;

        damageTakenMultiplier = Mathf.Clamp(damageTakenMultiplier, 0f, 1f);
        if (attackPowerMultiplier < 0f) attackPowerMultiplier = 0f;
        if (areaRadius           < 0f) areaRadius           = 0f;
        if (areaDamageMultiplier < 0f) areaDamageMultiplier = 0f;
        if (healingReceivedMultiplier < 0f) healingReceivedMultiplier = 0f;

    }
}
