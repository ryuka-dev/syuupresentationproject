using UnityEngine;

/// <summary>
/// プレイヤーの戦闘ステータスを一元管理するコンポーネント。
/// 基礎値 + 装備ボーナスを合算して各種ステータスを提供する。
/// PlayerEquipment.OnEquipmentChanged を監視し、装備変化時に
/// HealthComponent へ自動的に最大生命値を適用する。
/// </summary>
public class PlayerCombatStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseNormalAttackDamage = 20f;
    [SerializeField] private float baseMaxHealth          = 100f;

    private PlayerEquipment  _playerEquipment;
    private HealthComponent  _healthComponent;

    // ─── Public API ──────────────────────────────────────────────

    public float BaseNormalAttackDamage => baseNormalAttackDamage;
    public float BaseMaxHealth          => baseMaxHealth;

    public float EquipmentAttackPowerBonus
    {
        get
        {
            if (_playerEquipment == null) return 0f;
            var core = _playerEquipment.EquippedCore;
            return core != null ? core.AttackPowerBonus : 0f;
        }
    }

    public float EquipmentMaxHealthBonus
    {
        get
        {
            if (_playerEquipment == null) return 0f;
            var core = _playerEquipment.EquippedCore;
            return core != null ? core.MaxHealthBonus : 0f;
        }
    }

    public float CurrentNormalAttackDamage => baseNormalAttackDamage + EquipmentAttackPowerBonus;
    public float CurrentMaxHealth          => Mathf.Max(1f, baseMaxHealth + EquipmentMaxHealthBonus);

    // ─── Public Methods ──────────────────────────────────────────

    /// <summary>
    /// CurrentMaxHealth を HealthComponent に適用する。
    /// keepCurrentRatio=false: 現在 HP を保持し、超過分のみ切り捨て。
    /// keepCurrentRatio=true : 旧 maxHealth との比率で現在 HP を再計算。
    /// </summary>
    public void ApplyCurrentMaxHealth(bool keepCurrentRatio = false)
    {
        if (_healthComponent == null)
        {
            Debug.LogWarning("[PlayerCombatStats] ApplyCurrentMaxHealth: HealthComponent not found.");
            return;
        }
        float newMax        = CurrentMaxHealth;
        float beforeMax     = _healthComponent.maxHealth;
        float beforeCurrent = _healthComponent.currentHealth;
        _healthComponent.SetMaxHealth(newMax, keepCurrentRatio);
        Debug.Log($"[PlayerCombatStats] ApplyCurrentMaxHealth: keepRatio={keepCurrentRatio}, maxHealth {beforeMax}->{_healthComponent.maxHealth}, currentHealth {beforeCurrent}->{_healthComponent.currentHealth}");
    }

    // ─── Lifecycle ───────────────────────────────────────────────

    private void Awake()
    {
        _playerEquipment = GetComponent<PlayerEquipment>();
        _healthComponent = GetComponent<HealthComponent>();
    }

    private void OnEnable()
    {
        if (_playerEquipment != null)
            _playerEquipment.OnEquipmentChanged += HandleEquipmentChanged;
    }

    private void OnDisable()
    {
        if (_playerEquipment != null)
            _playerEquipment.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    // ─── Private ─────────────────────────────────────────────────

    private void HandleEquipmentChanged()
    {
        ApplyCurrentMaxHealth(keepCurrentRatio: false);
    }
}
