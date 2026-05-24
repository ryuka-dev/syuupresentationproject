using UnityEngine;

/// <summary>
/// 敵スキルの種類。
/// </summary>
public enum EnemySkillType
{
    None,
    CastAttack,
    CircleAoE,
    DonutAoE,
}

/// <summary>
/// 敵スキルデータ ScriptableObject。
/// スキルのパラメータを保持するだけで、実行ロジックは持たない。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "RPG/Enemy Skill Data")]
public class EnemySkillData : ScriptableObject
{
    [SerializeField] private string         _skillId;
    [SerializeField] private string         _displayName;
    [SerializeField] private EnemySkillType _skillType = EnemySkillType.None;

    [Header("戦闘パラメータ")]
    [SerializeField] private float _damage   = 10f;
    [SerializeField] private float _castTime = 1f;
    [SerializeField] private float _cooldown = 5f;
    [SerializeField] private float _range    = 2f;

    [Header("Circle AoE パラメータ（SkillType = CircleAoE の場合のみ有効）")]
    [Tooltip("AoE 半径（m）。伤害判定とエフェクト半径に使用。")]
    [SerializeField, Min(0f)] private float      _aoeRadius           = 5f;
    [Tooltip("読条中に表示する地面範囲提示 Prefab。null の場合はフォールバック Cylinder を使用。")]
    [SerializeField]           private GameObject _aoeTelegraphPrefab;

    [Header("Donut AoE パラメータ（SkillType = DonutAoE の場合のみ有効）")]
    [Tooltip("Boss 脚下の安全内圆半径（m）。この内側は安全区。")]
    [SerializeField, Min(0f)] private float _aoeInnerRadius = 2.5f;
    [Tooltip("AoE 最大半径（m）。_aoeInnerRadius より必ず大きくなければならない。")]
    [SerializeField, Min(0f)] private float _aoeOuterRadius = 7f;


    // ─── 読み取り専用プロパティ ──────────────────────────────
    public string         SkillId            => _skillId;
    public string         DisplayName        => _displayName;
    public EnemySkillType SkillType          => _skillType;
    public float          Damage             => _damage;
    public float          CastTime           => _castTime;
    public float          Cooldown           => _cooldown;
    public float          Range              => _range;
    public float          AoeRadius          => _aoeRadius;
    public GameObject     AoeTelegraphPrefab => _aoeTelegraphPrefab;
    public float         AoeInnerRadius     => _aoeInnerRadius;
    public float         AoeOuterRadius     => _aoeOuterRadius;

    // ─── バリデーション ─────────────────────────────────────
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_skillId))
            Debug.LogWarning($"[EnemySkillData] {name}: skillId が空です。");

        _damage   = Mathf.Max(0f,   _damage);
        _castTime = Mathf.Max(0f,   _castTime);
        _cooldown = Mathf.Max(0f,   _cooldown);
        _range    = Mathf.Max(0.1f, _range);
        _aoeRadius = Mathf.Max(0f,  _aoeRadius);
        _aoeInnerRadius = Mathf.Max(0f,  _aoeInnerRadius);
        _aoeOuterRadius = Mathf.Max(0f,  _aoeOuterRadius);
        if (_aoeOuterRadius <= _aoeInnerRadius) _aoeOuterRadius = _aoeInnerRadius + 0.5f;
    }
}
