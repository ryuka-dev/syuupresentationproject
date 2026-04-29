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

    public event Action<float, float> OnHealthChanged;  // (current, max)
    public event Action              OnDied;

    public bool IsDead => currentHealth <= 0f;

void Awake()
    {
        // maxHealth 在 Inspector 中设置后 Awake 才运行，此时初始化是安全的
        currentHealth = maxHealth;
    }

public void TakeDamage(float amount)
    {
        Debug.Log($"[Health] TakeDamage({amount}) called. IsDead={IsDead} current={currentHealth}");
        if (IsDead) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"[Health] After damage: current={currentHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0f) OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
