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
    public event Action<float, Transform> OnDamaged;        // (伤害值, 攻击来源)
    public event Action                   OnDied;

    public bool IsDead => currentHealth <= 0f;

    void Awake()
    {
        currentHealth = maxHealth;
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
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"[Health] After damage: current={currentHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke(amount, attacker);
        if (currentHealth <= 0f) OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// 复活专用接口：将生命值恢复至最大值，并触发 OnHealthChanged 以刷新 UI 血条。
    /// 不触发 OnDied / OnDamaged。若 HealthComponent 内无额外死亡 bool，
    /// 调用后 IsDead 自然变为 false（currentHealth > 0）。
    /// </summary>
    public void RestoreFullHealth()
    {
        currentHealth = maxHealth;
        Debug.Log($"[Health] RestoreFullHealth() called. currentHealth={currentHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// 最大生命值を変更する。
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
}
