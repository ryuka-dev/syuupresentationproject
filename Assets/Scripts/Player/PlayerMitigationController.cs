using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家减伤控制器 — 第一版最小可用实现。
/// 按键盘 2 开启短时间减伤。减伤期间 HealthComponent.TakeDamage 会调用
/// ModifyIncomingDamage() 将伤害乘以 damageTakenMultiplier。
/// 减伤结束后自动恢复；冷却期间不可再次开启。
/// 不影响敌人（敌人没有此组件，HealthComponent 会安全跳过）。
/// </summary>
public class PlayerMitigationController : MonoBehaviour
{
    [Header("减伤参数")]
    [SerializeField] private float damageTakenMultiplier = 0.5f;  // 减伤期间受到伤害的比例
    [SerializeField] private float duration              = 4f;    // 减伤持续时间（秒）
    [SerializeField] private float cooldown              = 12f;   // 冷却时间（秒）

    // 运行时计时器
    private float _mitigationTimer;
    private float _cooldownTimer;

    // ─── 公开只读属性 ─────────────────────────────────────────────

    /// <summary>当前是否处于减伤状态。</summary>
    public bool IsMitigationActive => _mitigationTimer > 0f;

    /// <summary>减伤剩余时间（秒）。不处于减伤时为 0。</summary>
    public float MitigationRemainingTime => _mitigationTimer;

    /// <summary>冷却剩余时间（秒）。冷却结束时为 0。</summary>
    public float CooldownRemainingTime => _cooldownTimer;

    /// <summary>减伤期间受到伤害的比例（0 ~ 1）。</summary>
    public float DamageTakenMultiplier => damageTakenMultiplier;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Update()
    {
        // 更新计时器
        if (_mitigationTimer > 0f)
            _mitigationTimer = Mathf.Max(0f, _mitigationTimer - Time.deltaTime);

        if (_cooldownTimer > 0f)
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - Time.deltaTime);

        // 检测按键 2（New Input System）
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit2Key.wasPressedThisFrame)
            TryActivateMitigation();
    }

    private void OnValidate()
    {
        damageTakenMultiplier = Mathf.Clamp(damageTakenMultiplier, 0f, 1f);
        duration              = Mathf.Max(0.1f, duration);
        cooldown              = Mathf.Max(0f, cooldown);
    }

    // ─── 公开方法 ─────────────────────────────────────────────────

    /// <summary>
    /// 尝试激活减伤。
    /// 冷却中或已激活时返回 false。
    /// 成功时返回 true，并启动计时器。
    /// </summary>
    public bool TryActivateMitigation()
    {
        if (_cooldownTimer > 0f)
        {
            Debug.Log($"[Mitigation] 冷却中，还需 {_cooldownTimer:F1}s 才能再次使用。");
            return false;
        }

        if (IsMitigationActive)
        {
            Debug.Log($"[Mitigation] 减伤已经处于激活中，剩余 {_mitigationTimer:F1}s。");
            return false;
        }

        _mitigationTimer = duration;
        _cooldownTimer   = cooldown;
        Debug.Log($"[Mitigation] 减伤已激活！持续时间={duration}s，伤害倍率={damageTakenMultiplier:P0}，冷却={cooldown}s");
        return true;
    }

    /// <summary>
    /// 对即将受到的伤害应用减伤修正。
    /// 由 HealthComponent.ApplyIncomingDamageModifiers 调用。
    /// 非激活状态下返回原值；激活状态下返回 damage * damageTakenMultiplier。
    /// </summary>
    public float ModifyIncomingDamage(float damage)
    {
        if (damage <= 0f) return damage;
        if (!IsMitigationActive) return damage;

        float finalDamage = damage * damageTakenMultiplier;
        Debug.Log($"[Mitigation] 减伤生效：原始伤害={damage:F1}，最终伤害={finalDamage:F1}（倍率={damageTakenMultiplier:P0}，剩余={_mitigationTimer:F1}s）");
        return finalDamage;
    }
}
