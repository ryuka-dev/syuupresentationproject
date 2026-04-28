using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家屏幕固定血条
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    public EntityStats stats;
    private Image fillImage;

    void Awake()
    {
        fillImage = GetComponentInChildren<Image>();
        if (stats == null)
            stats = GameObject.Find("Player")?.GetComponent<EntityStats>();
        if (stats != null)
        {
            stats.OnHealthChanged += UpdateBar;
            UpdateBar(stats.currentHealth, stats.maxHealth);
        }
    }

    void UpdateBar(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = current / max;
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnHealthChanged -= UpdateBar;
    }
}
