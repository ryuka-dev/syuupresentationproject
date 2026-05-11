using UnityEngine;

/// <summary>
/// プレイヤーの戦闘ステータスを一元管理するコンポーネント。
/// 基礎値 + 装備ボーナスを合算して CurrentNormalAttackDamage を提供する。
/// Buff / 随机词条 / EntityStats などの将来拡張もここに集約する予定。
/// </summary>
public class PlayerCombatStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float baseNormalAttackDamage = 20f;

    private PlayerEquipment _playerEquipment;

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>装備なしの基礎通常攻撃ダメージ。</summary>
    public float BaseNormalAttackDamage => baseNormalAttackDamage;

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

    /// <summary>現フレームの最終通常攻撃ダメージ（基礎 + 装備ボーナス）。</summary>
    public float CurrentNormalAttackDamage => baseNormalAttackDamage + EquipmentAttackPowerBonus;

    // ─── Lifecycle ───────────────────────────────────────────────

    private void Awake()
    {
        // PlayerEquipment は省略可能。なくても攻撃は機能する。
        _playerEquipment = GetComponent<PlayerEquipment>();
    }
}
