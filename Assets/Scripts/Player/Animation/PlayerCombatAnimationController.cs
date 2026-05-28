using UnityEngine;

/// <summary>
/// 玩家战斗动作播放层 — 薄封装，只负责动作请求，不负责伤害 / 技能逻辑。
///
/// 设计原则：
///   - 動画播放失败不能影响战斗结算（TryResolveAnimator 失败时直接 return）
///   - 只知道 Trigger 名称，不知道 ThirdParty 资源路径
///   - 每次动作请求前 ResetTrigger 防止累積
///
/// 扩展方式：
///   每新增一个战斗动作，添加一个对应的 public Play...() 方法即可。
///   Trigger 名称通过 SerializeField 配置，不要硬编码。
/// </summary>
public class PlayerCombatAnimationController : MonoBehaviour
{
    [Header("Animator 参照（留空時は自動解決）")]
    [SerializeField] private Animator animator;

    [Header("Trigger 名称 — Animator Controller と一致させること")]
    [SerializeField] private string radiantRiposteTrigger = "RadiantRiposte";

    // ─── 警告去重 ────────────────────────────────────────────────
    private bool _warnedMissingAnimator;

    // ─── ライフサイクル ───────────────────────────────────────────

    private void Awake()
    {
        TryResolveAnimator();
    }

    // ─── 公開 API ────────────────────────────────────────────────

    /// <summary>
    /// 守護反击 / Radiant Riposte の動作を再生する。
    /// Animator が未解決の場合は一度だけ警告して return（戦闘結算に影響なし）。
    /// </summary>
    public void PlayRadiantRiposte()
    {
        if (!TryResolveAnimator()) return;

        animator.ResetTrigger(radiantRiposteTrigger);
        animator.SetTrigger(radiantRiposteTrigger);
    }

    // ─── Private ─────────────────────────────────────────────────

    private bool TryResolveAnimator()
    {
        if (animator != null) return true;

        animator = GetComponent<Animator>();
        if (animator != null) return true;

        animator = GetComponentInChildren<Animator>();
        if (animator != null) return true;

        if (!_warnedMissingAnimator)
        {
            Debug.LogWarning("[PlayerCombatAnimationController] Animator が見つかりません。" +
                             " Inspector で Animator を設定するか、PlayerCombatAnimationController を" +
                             " Animator と同じ GameObject に追加してください。");
            _warnedMissingAnimator = true;
        }
        return false;
    }
}
