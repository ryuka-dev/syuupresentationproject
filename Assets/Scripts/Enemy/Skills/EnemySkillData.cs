using UnityEngine;

/// <summary>
/// 敵スキルの種類。
/// 現段階では None と CastAttack のみ定義。
/// </summary>
public enum EnemySkillType
{
    None,
    CastAttack,
}

/// <summary>
/// 敵スキルデータ ScriptableObject。
/// スキルのパラメータを保持するだけで、実行ロジックは持たない。
/// EnemyAI / 特定の敵オブジェクトを参照しない。
/// </summary>
[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "RPG/Enemy Skill Data")]
public class EnemySkillData : ScriptableObject
{
    [SerializeField] private string        _skillId;
    [SerializeField] private string        _displayName;
    [SerializeField] private EnemySkillType _skillType = EnemySkillType.None;

    [Header("戦闘パラメータ")]
    [SerializeField] private float _damage   = 10f;
    [SerializeField] private float _castTime = 1f;
    [SerializeField] private float _cooldown = 5f;
    [SerializeField] private float _range    = 2f;

    // ─── 読み取り専用プロパティ ──────────────────────────────
    public string         SkillId     => _skillId;
    public string         DisplayName => _displayName;
    public EnemySkillType SkillType   => _skillType;
    public float          Damage      => _damage;
    public float          CastTime    => _castTime;
    public float          Cooldown    => _cooldown;
    public float          Range       => _range;

    // ─── バリデーション ─────────────────────────────────────
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_skillId))
            Debug.LogWarning($"[EnemySkillData] {name}: skillId が空です。後で設定してください。");

        _damage   = Mathf.Max(0f,    _damage);
        _castTime = Mathf.Max(0f,    _castTime);
        _cooldown = Mathf.Max(0f,    _cooldown);
        _range    = Mathf.Max(0.1f,  _range);
    }
}
