using UnityEngine;

/// <summary>
/// プレイヤーの戦闘ステータスを一元管理するコンポーネント。
/// 基礎値 + 装備ボーナスを合算して各種ステータスを提供する。
/// HealthComponent への自動適用は行わない。
/// </summary>
public class PlayerCombatStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseNormalAttackDamage = 20f;
    [SerializeField] private float baseMaxHealth          = 100f;

    private PlayerEquipment _playerEquipment;

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>装備なしの基礎通常攻撃ダメージ。</summary>
    public float BaseNormalAttackDamage => baseNormalAttackDamage;

    /// <summary>装備なしの基礎最大生命値。</summary>
    public float BaseMaxHealth => baseMaxHealth;

    /// <summary>現在装備中の Core から得られる攻撃力加算値。</summary>
    public float EquipmentAttackPowerBonus
    {
        get
        {
            if (_playerEquipment == null) return 0f;
            var core = _playerEquipment.EquippedCore;
            return core != null ? core.AttackPowerBonus : 0f;
        }
    }

    /// <summary>現在装備中の Core から得られる最大生命値加算値。</summary>
    public float EquipmentMaxHealthBonus
    {
        get
        {
            if (_playerEquipment == null) return 0f;
            var core = _playerEquipment.EquippedCore;
            return core != null ? core.MaxHealthBonus : 0f;
        }
    }

    /// <summary>現フレームの最終通常攻撃ダメージ（基礎 + 装備攻撃ボーナス）。</summary>
    public float CurrentNormalAttackDamage => baseNormalAttackDamage + EquipmentAttackPowerBonus;

    /// <summary>現フレームの最終最大生命値（基礎 + 装備最大生命値ボーナス）。最小値 1。</summary>
    public float CurrentMaxHealth => Mathf.Max(1f, baseMaxHealth + EquipmentMaxHealthBonus);

    // ─── Lifecycle ───────────────────────────────────────────────

    private void Awake()
    {
        // PlayerEquipment は省略可能。なくても攻撃は機能する。
        _playerEquipment = GetComponent<PlayerEquipment>();
    }
}
