using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// \u73a9\u5bb6 HP + \u62a4\u76fe\u6761 HUD UI\u3002
/// HP \u6761\u306e\u5c71\u306b\u62a4\u76fe\u6bb5\u3092\u88ab\u305b\u3066\u8868\u793a\u3059\u308b\u3002
/// </summary>
public class PlayerHealthShieldBarUI : MonoBehaviour
{
    [Header("\u30c7\u30fc\u30bf\u30bd\u30fc\u30b9")]
    [SerializeField] private HealthComponent                 healthComponent;
    [SerializeField] private PlayerStatusEffectController   statusEffectController;

    [Header("UI \u53c2\u7167")]
    [SerializeField] private RectTransform healthFill;     // HP \u3092\u8868\u3059 Image
    [SerializeField] private RectTransform shieldFill;     // \u62a4\u76fe\u3092\u8868\u3059 Image
    [SerializeField] private TMP_Text      healthValueText;
    [SerializeField] private TMP_Text      shieldValueText;
    [SerializeField] private RectTransform barBackground;  // \u5168\u5e45\u80cc\u666f\uff08\u5e45\u3092 barWidth \u3068\u3057\u3066\u4f7f\u7528\uff09

    private float _barWidth;

    private void Awake()
    {
        // \u8eab\u8fd1\u306a\u30b3\u30f3\u30dd\u30fc\u30cd\u30f3\u30c8\u3092\u81ea\u52d5\u89e3\u6c7a
        if (healthComponent == null)
            healthComponent = FindFirstObjectByType<HealthComponent>();
        if (statusEffectController == null)
            statusEffectController = FindFirstObjectByType<PlayerStatusEffectController>();
    }

    private void Start()
    {
        if (barBackground != null)
            _barWidth = barBackground.rect.width;
        else if (healthFill != null)
            _barWidth = 400f; // fallback
    }

    private void Update()
    {
        if (healthComponent == null) return;

        float maxHP     = healthComponent.MaxHealth;
        float currentHP = healthComponent.CurrentHealth;

        if (maxHP <= 0f) return;

        float healthRatio = Mathf.Clamp01(currentHP / maxHP);

        // \u62a4\u76fe\u91cf\u53d6\u5f97
        float shieldAmount = (statusEffectController != null && statusEffectController.HasGuardConversionShield)
            ? statusEffectController.GuardConversionShieldRemaining
            : 0f;
        float shieldRatio = Mathf.Clamp01(shieldAmount / maxHP);

        // Shield \u304c HP \u6761\u306e\u7bc4\u56f2\u3092\u8d85\u3048\u308b\u5834\u5408\u306f\u8aad\u308a\u88c5\u308b
        // Shield Fill は常に左端から開始，HP Fill の上層に覆いながる
        float displayShieldRatio = Mathf.Clamp01(shieldRatio); // 最大 100%

        // \u5e45 = sizeDelta.x \u3067\u5236\u5fa1\uff08anchorMin.x == anchorMax.x == 0 \u3067\u5de6\u5bc4\u308a\u524d\u63d0\uff09
        if (healthFill != null)
        {
            var sd = healthFill.sizeDelta;
            sd.x = healthRatio * _barWidth;
            healthFill.sizeDelta = sd;
        if (shieldFill != null)
        {
            var shieldSD  = shieldFill.sizeDelta;
            shieldSD.x    = displayShieldRatio * _barWidth;
            shieldFill.sizeDelta = shieldSD;
            // 常に左端から開始
            var pos = shieldFill.anchoredPosition;
            pos.x   = 0f;
            shieldFill.anchoredPosition = pos;
            shieldFill.gameObject.SetActive(shieldAmount > 0.01f);
        }
            shieldFill.gameObject.SetActive(shieldAmount > 0.01f);
        }

        // HP \u30c6\u30ad\u30b9\u30c8
        if (healthValueText != null)
            healthValueText.text = Mathf.CeilToInt(currentHP).ToString();

        // \u62a4\u76fe\u30c6\u30ad\u30b9\u30c8
        if (shieldValueText != null)
        {
            if (shieldAmount > 0.01f)
            {
                shieldValueText.text = Mathf.CeilToInt(shieldAmount).ToString();
                shieldValueText.gameObject.SetActive(true);
            }
            else
            {
                shieldValueText.text = string.Empty;
                shieldValueText.gameObject.SetActive(false);
            }
        }
    }
}
