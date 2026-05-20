using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵スキルコントローラー。
/// 敵 GameObject に任意でアタッチし、Inspector からスキルリストを設定する。
///
/// 設計方針：
///   - skills リストが空の敵は何もしない（エラーにならない）。
///   - null 要素が含まれていても安全にスキップする。
///   - EnemyAI の FSM 状態を直接変更しない。
///   - 移動制御は EnemyAI 側の責任。このクラスは読条・冷却・ダメージ結算のみ担当。
/// </summary>
public class EnemySkillController : MonoBehaviour
{
    [Header("スキルリスト（Inspector で設定）")]
    [SerializeField] private List<EnemySkillData> _skills = new List<EnemySkillData>();

    // ─── クールダウン管理 ────────────────────────────────────
    private readonly Dictionary<EnemySkillData, float> _nextAvailableTime
        = new Dictionary<EnemySkillData, float>();

    // ─── 施法状態（内部） ────────────────────────────────────
    private Coroutine      _currentCastCoroutine;
    private EnemySkillData _currentSkill;
    private Transform      _currentCastTarget;
    private float          _currentCastElapsed;
    private float          _currentCastDuration;

    // ─── キャッシュ ──────────────────────────────────────────
    private EnemyAI         _enemyAI;
    private HealthComponent _healthComponent;

    // ─── 公開プロパティ（読み取り専用） ──────────────────────

    /// <summary>現在読条中かどうか。</summary>
    public bool IsCasting { get; private set; }

    /// <summary>現在読条中のスキル。読条中以外は null。</summary>
    public EnemySkillData CurrentSkill => _currentSkill;

    /// <summary>現在の読条経過時間（秒）。</summary>
    public float CurrentCastElapsed => _currentCastElapsed;

    /// <summary>現在の読条総時間（秒）。</summary>
    public float CurrentCastDuration => _currentCastDuration;

    /// <summary>読条残り時間（秒）。</summary>
    public float CurrentCastRemaining => Mathf.Max(0f, _currentCastDuration - _currentCastElapsed);

    /// <summary>読条進度 0〜1。0=開始直後、1=完了。</summary>
    public float CurrentCastProgress =>
        _currentCastDuration > 0f
            ? Mathf.Clamp01(_currentCastElapsed / _currentCastDuration)
            : (_currentCastDuration <= 0f && IsCasting ? 1f : 0f);

    /// <summary>設定されたスキルリスト（読み取り専用）。</summary>
    public IReadOnlyList<EnemySkillData> Skills => _skills;

    /// <summary>1つ以上のスキルが設定されているか。</summary>
    public bool HasAnySkill => _skills != null && _skills.Count > 0;

    // ─── ライフサイクル ─────────────────────────────────────
    void Awake()
    {
        _enemyAI         = GetComponent<EnemyAI>();
        _healthComponent = GetComponent<HealthComponent>();
    }

    /// <summary>
    /// コンポーネントが disable されたとき（GameObject 非破棄の無効化）に施法を中断する。
    /// GameObject 破棄時は Unity がコルーチンを自動停止するため CleanupCast のみ呼ぶ。
    /// </summary>
    void OnDisable()
    {
        if (IsCasting)
        {
            if (_currentCastCoroutine != null)
            {
                StopCoroutine(_currentCastCoroutine);
                _currentCastCoroutine = null;
            }
            CleanupCast();
        }
    }

    // ─── スキル使用可否 ─────────────────────────────────────

    public bool CanUseSkill(EnemySkillData skill)
    {
        if (skill == null) return false;
        if (IsCasting)     return false;
        return IsSkillReady(skill);
    }

    public bool IsSkillReady(EnemySkillData skill)
    {
        if (skill == null) return false;
        if (!_nextAvailableTime.TryGetValue(skill, out float nextTime))
            return true;
        return Time.time >= nextTime;
    }

    // ─── クールダウン操作 ────────────────────────────────────

    public void StartCooldown(EnemySkillData skill)
    {
        if (skill == null) return;
        _nextAvailableTime[skill] = Time.time + skill.Cooldown;
    }

    // ─── スキル選択 ─────────────────────────────────────────

    public bool TryGetReadySkillInRange(Transform target, out EnemySkillData skill)
    {
        skill = null;
        if (target == null)                         return false;
        if (_skills == null || _skills.Count == 0) return false;

        float distToTarget = Vector3.Distance(transform.position, target.position);
        foreach (var s in _skills)
        {
            if (s == null)              continue;
            if (!IsSkillReady(s))       continue;
            if (distToTarget > s.Range) continue;
            skill = s;
            return true;
        }
        return false;
    }

    // ─── スキル実行 ─────────────────────────────────────────

    /// <summary>
    /// 指定スキルの実行を試みる。
    /// 成功時: 読条コルーチンを開始し true を返す。
    /// </summary>
    public bool TryStartSkill(EnemySkillData skill, Transform target)
    {
        if (skill == null)                                return false;
        if (target == null)                               return false;
        if (IsCasting)                                    return false;
        if (!CanUseSkill(skill))                          return false;
        if (skill.SkillType != EnemySkillType.CastAttack) return false;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > skill.Range) return false;

        _currentCastCoroutine = StartCoroutine(CastAttackRoutine(skill, target));
        return true;
    }

    /// <summary>
    /// 読条重攻撃コルーチン。
    /// フレームごとに経過時間を加算し、UI が CurrentCastProgress を読み取れるようにする。
    /// 読条完了後にターゲット再検証を行い、命中条件を満たせば TakeDamage を呼ぶ。
    /// </summary>
private IEnumerator CastAttackRoutine(EnemySkillData skill, Transform target)
    {
        IsCasting             = true;
        _currentSkill         = skill;
        _currentCastTarget    = target;
        _currentCastElapsed   = 0f;
        _currentCastDuration  = skill.CastTime;

        Debug.Log($"[EnemySkillController] {gameObject.name}: 読条開始 [{skill.DisplayName}] castTime={skill.CastTime}s");

        // 読条中：距離はチェックしない。caster 死亡または target 消失時のみ中断。
        while (_currentCastElapsed < _currentCastDuration)
        {
            // caster 自身が死亡したら中断
            if (_healthComponent != null && _healthComponent.IsDead)
            {
                Debug.Log($"[EnemySkillController] {gameObject.name}: 施法中断（caster 死亡）");
                CleanupCast();
                yield break;
            }
            // target が消えたら中断
            if (target == null)
            {
                Debug.Log($"[EnemySkillController] {gameObject.name}: 施法中断（target が null）");
                CleanupCast();
                yield break;
            }
            _currentCastElapsed += Time.deltaTime;
            yield return null;
        }
        _currentCastElapsed = _currentCastDuration; // 丸め誤差修正

        // 読条完了 — 距離チェックなし。target の生死と caster の生死のみ確認。
        bool hit = false;
        if (skill != null && target != null)
        {
            // caster 死亡時はダメージを与えない
            if (_healthComponent != null && _healthComponent.IsDead)
            {
                Debug.Log($"[EnemySkillController] {gameObject.name}: 読条完了時 caster 死亡 — ダメージなし");
            }
            else
            {
                var targetHealth = target.GetComponent<HealthComponent>();
                if (targetHealth != null && !targetHealth.IsDead)
                {
                    // Guard Resonance 判定のための記録を TakeDamage より前に記録
                    _lastDamageSkillData = skill;
                    _lastDamageSkillTime  = Time.time;
                    targetHealth.TakeDamage(skill.Damage, transform);
                    Debug.Log($"[EnemySkillController] {gameObject.name}: [{skill.DisplayName}] 命中！ダメージ={skill.Damage}（距離チェックなし）");
                    hit = true;
                }
            }
        }

        if (!hit)
            Debug.Log($"[EnemySkillController] {gameObject.name}: [{skill?.DisplayName}] 不命中（target 死亡 / caster 死亡 / target null）");

        StartCooldown(skill);
        CleanupCast();
    }

    // ─── キャンセル ─────────────────────────────────────────

    /// <summary>
    /// 現在施法中のスキルをキャンセルする。施法中でなければ何もしない。
    /// クールダウンは開始しない。
    /// </summary>
    public void CancelCasting(string reason)
    {
        if (!IsCasting) return;

        if (_currentCastCoroutine != null)
        {
            StopCoroutine(_currentCastCoroutine);
            _currentCastCoroutine = null;
        }

        Debug.Log($"[EnemySkillController] {gameObject.name}: 施法キャンセル [{_currentSkill?.DisplayName}] 理由={reason}");
        CleanupCast();
    }

/// <summary>
    /// 現在の読条を打断する。将来の打断技能用の最小実装。
    /// 打断時はクールダウンを開始しない。
    /// </summary>
    public void InterruptCurrentCast()
    {
        CancelCasting("Interrupted");
    }


    // ─── 最近ダメージスキル記録 ────────────────────────────────

    private EnemySkillData _lastDamageSkillData;
    private float          _lastDamageSkillTime = -999f;

    /// <summary>最近ダメージを与えたスキル。ダメージ発生外は null。</summary>
    public EnemySkillData LastDamageSkillData => _lastDamageSkillData;
    /// <summary>最近ダメージを与えた時刻（Time.time）。</summary>
    public float          LastDamageSkillTime  => _lastDamageSkillTime;

    // ─── 内部クリア ─────────────────────────────────────────
    // ─── 内部クリア ─────────────────────────────────────────
    private void CleanupCast()
    {
        IsCasting             = false;
        _currentSkill         = null;
        _currentCastTarget    = null;
        _currentCastCoroutine = null;
        _currentCastElapsed   = 0f;
        _currentCastDuration  = 0f;
    }
}
