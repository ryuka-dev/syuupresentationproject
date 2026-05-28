using UnityEngine;

/// <summary>
/// 玩家战斗朝向辅助层 v0.3 — 技能执行前自动面向目标的统一入口。
///
/// Use this before player combat actions that should face a target at execution time.
///
/// 新增: LockedFacingRotation — PlayerController が技能朝向ロック中にこの Rotation を使う。
/// </summary>
public class PlayerCombatFacingController : MonoBehaviour
{
    [Header("朝向ロック時間（秒）")]
    [SerializeField] private float faceLockDuration = 0.30f;

    private const float MinDirectionSqr = 0.0001f;

    // ─── 朝向ロック ──────────────────────────────────────────────
    private float     _faceLockUntil;

    /// <summary>ロック中は true。PlayerController は rotation 覆盖を行わない。</summary>
    public bool       IsFacingLocked         => Time.time < _faceLockUntil;
    /// <summary>最後に FaceTarget() で設定した戦闘朝向。ロック中 PlayerController がこれを使う。</summary>
    public Quaternion LockedFacingRotation   { get; private set; }

    // ─── Singleton ───────────────────────────────────────────────
    private static PlayerCombatFacingController _instance;

    private void Awake() { _instance = this; }

    // ─── 公開 API ────────────────────────────────────────────────

    /// <summary>
    /// プレイヤーを target 方向に瞬間回転させ、faceLockDuration 秒間ロックする。
    /// ロック中は PlayerController.FixedUpdate がこの Rotation を強制保持する。
    /// </summary>
    public bool FaceTarget(Transform target)
    {
        if (target == null)                       return false;
        if (!target.gameObject.activeInHierarchy) return false;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < MinDirectionSqr) return false;

        LockedFacingRotation = Quaternion.LookRotation(dir);
        transform.rotation   = LockedFacingRotation;
        _faceLockUntil       = Time.time + faceLockDuration;
        return true;
    }

    public bool FaceTarget(GameObject target)
    {
        return target != null && FaceTarget(target.transform);
    }

    public static bool StaticFaceTarget(Transform target)
    {
        return _instance != null && _instance.FaceTarget(target);
    }
}
