using UnityEngine;
using System;

/// <summary>
/// 通用血量组件 - 玩家和敌人都使用
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("血量")]
    public float maxHealth = 100f;
    public float currentHealth { get; private set; }

    public event Action<float, float>     OnHealthChanged;  // (current, max)
    public event Action<float, Transform> OnDamaged;        // (最终伤害值, 攻击来源)
    public event Action                   OnDied;
    public event Action<float, Transform> OnHealed;         // (actualHealAmount, healer)


    public bool IsDead => currentHealth <= 0f;

    // PlayerMitigationController 已移除，伤害修正由 PlayerStatusEffectController 统一负责。
    private PlayerStatusEffectController _statusEffectController;

    void Awake()
    {
        currentHealth           = maxHealth;
        _statusEffectController = GetComponent<PlayerStatusEffectController>();
    }

    /// <summary>旧接口，保持向后兼容。</summary>
    public void TakeDamage(float amount)
    {
        TakeDamage(amount, null);
    }

    /// <summary>带攻击来源的伤害接口。attacker 可为 null。</summary>
    public void TakeDamage(float amount, Transform attacker)
    {
        Debug.Log($"[Health] TakeDamage({amount}) called. IsDead={IsDead} current={currentHealth}");
        if (IsDead) return;

        // 统一减伤修正入口：玩家侧有 PlayerMitigationController 时会应用减伤倍率；敌人侧直接返回原值。
        float finalDamage = ApplyIncomingDamageModifiers(amount);

        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        Debug.Log($"[Health] After damage: current={currentHealth} (finalDamage={finalDamage:F1})");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke(finalDamage, attacker);
        if (currentHealth <= 0f) OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        Heal(amount, null);
    }

    /// <summary>带治疗来源的统一治疗入口。healer 可为 null。</summary>
    public void Heal(float amount, Transform healer)
    {
        if (amount <= 0f)
        {
            Debug.LogWarning($"[Health] Heal called with amount <= 0 ({amount}), ignored.");
            return;
        }
        if (IsDead) return;
        if (currentHealth >= maxHealth) return;
        float modifiedAmount = ApplyIncomingHealingModifiers(amount);
        if (modifiedAmount <= 0f) return;


        float oldHealth   = currentHealth;
        currentHealth     = Mathf.Min(maxHealth, currentHealth + modifiedAmount);
        float actualHeal  = currentHealth - oldHealth;

        if (actualHeal > 0f)
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHealed?.Invoke(actualHeal, healer);
        }
    }

    /// <summary>
    /// 复活专用接口：将生命值恢复至最大值，并触发 OnHealthChanged 以刷新 UI 血条。
    /// 不触发 OnDied / OnDamaged。若 HealthComponent 内无额外死亡 bool，
        /// 调用后 IsDead 自然变为 false（currentHealth > 0）。
    /// </summary>
public void RestoreFullHealth()
    {
        float oldHealth  = currentHealth;
        currentHealth    = maxHealth;
        Debug.Log($"[Health] RestoreFullHealth() called. currentHealth={currentHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        float actualHeal = currentHealth - oldHealth;
        if (actualHeal > 0f)
            OnHealed?.Invoke(actualHeal, null);
    }

    /// <summary>
    /// 最大生命値を変更する。
    /// keepCurrentRatio=true の場合は現在 HP を旧 maxHealth との比率で再計算する。
    /// keepCurrentRatio=false の場合は現在 HP をそのまま保持し、超過分のみ切り捨てる。
    /// IsDead 状態・OnDied・復活処理には一切関与しない。
    /// </summary>
    public void SetMaxHealth(float newMaxHealth, bool keepCurrentRatio = false)
    {
        if (newMaxHealth < 1f) newMaxHealth = 1f;

        float oldMax     = maxHealth;
        float oldCurrent = currentHealth;

        if (keepCurrentRatio)
        {
            float ratio   = oldMax > 0f ? oldCurrent / oldMax : 1f;
            maxHealth     = newMaxHealth;
            currentHealth = Mathf.Max(0f, newMaxHealth * ratio);
        }
        else
        {
            maxHealth     = newMaxHealth;
            currentHealth = Mathf.Min(oldCurrent, newMaxHealth);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"[Health] SetMaxHealth: oldMax={oldMax}, newMax={maxHealth}, oldCurrent={oldCurrent}, newCurrent={currentHealth}");
    }

    // ─── Private ─────────────────────────────────────────────────

    /// <summary>
    /// 对即将受到的伤害应用所有输入端减伤修正。
    /// 当前只支持 PlayerMitigationController。
    /// 敌人没有此组件时直接返回原始伤害值，不影响敌人逻辑。
    /// </summary>
    /// <summary>
    /// 受ける治療量に対して、PlayerStatusEffectController に基づく治療倒率修正を適用する。
    /// PlayerStatusEffectController が存在しない場合（敵人等）は原値をそのまま返す。
    /// </summary>
    private float ApplyIncomingHealingModifiers(float healing)
    {
        if (_statusEffectController == null)
            return healing;
        return _statusEffectController.ModifyIncomingHealing(healing);
    }

        private float ApplyIncomingDamageModifiers(float damage)
    {
        if (_statusEffectController != null)
            return _statusEffectController.ModifyIncomingDamage(damage);
        return damage;
    }
}
