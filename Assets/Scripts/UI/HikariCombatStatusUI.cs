using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hikari Combat UI v0.2 — 正式战斗状态 UI（可编辑版，非 Debug UI）
///
/// 职责：
///   - 通过 SerializedField 引用场景中的 UI 组件（可在 Inspector 中调整）
///   - 从 HikariSupportController 读取 Hikari 状态 / 当前动作 / 光负荷 / 读条进度
///   - Update() 每帧刷新 UI 显示
///
/// 挂载位置：SampleScene → UI → HikariHUDCanvas → HikariPanel
/// 注意：不再使用 RuntimeInitializeOnLoadMethod 自动生成，
///       旧 HikariCombatStatusUI_Root 已移除。
/// </summary>
public class HikariCombatStatusUI : MonoBehaviour
{
    // ─── Serialized 引用（在 Inspector / Scene 中绑定）─────────────
    [Header("Hikari 状态显示")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text stateText;          // 当前状态：待机 / 光溢出 / 导光封锁
    [SerializeField] private TMP_Text actionText;         // 当前动作：-- / 治疗读条中 / 紧急治疗读条中

    [Header("读条条（可选）")]
    [SerializeField] private Image    castBarFill;        // 读条 fill（可为 null 时跳过）
    [SerializeField] private TMP_Text castValueText;      // 读条 0.8 / 1.5（可为 null）

    [Header("光负荷")]
    [SerializeField] private Image    burdenBarFill;      // 光负荷 fill
    [SerializeField] private TMP_Text burdenValueText;    // 35 / 100
    [SerializeField] private TMP_Text burdenChangeHintText; // 变化提示：-- 占位行

    // ─── 数据源 ─────────────────────────────────────────────────────
    private HikariSupportController _hikari;

    // ─── 警告去重 ────────────────────────────────────────────────────
    private bool _warnedMissingHikari;
    private bool _warnedMissingRefs;

    // ─── Unity 生命周期 ──────────────────────────────────────────────

    private void Start()
    {
        _hikari = FindFirstObjectByType<HikariSupportController>();
        if (_hikari == null)
        {
            Debug.LogWarning("[HikariCombatStatusUI] HikariSupportController が見つかりません。" +
                             " Hikari オブジェクトが Scene に存在するか確認してください。");
        }

        ValidateRefs();
    }

    private void Update()
    {
        if (_hikari == null)
        {
            _hikari = FindFirstObjectByType<HikariSupportController>();
            return;
        }
        RefreshUI();
    }

    // ─── 参照検証（起動時1回のみ警告）──────────────────────────────
    private void ValidateRefs()
    {
        if (_warnedMissingRefs) return;
        bool missing = (stateText == null || actionText == null || burdenBarFill == null || burdenValueText == null);
        if (missing)
        {
            Debug.LogWarning("[HikariCombatStatusUI] 一部の SerializedField が未バインドです。" +
                             " Inspector で stateText / actionText / burdenBarFill / burdenValueText を設定してください。");
            _warnedMissingRefs = true;
        }
    }

    // ─── UI 刷新 ────────────────────────────────────────────────────

    private void RefreshUI()
    {
        // ── Hikari 状态（光负荷阶段）──────────────────────────────
        if (stateText != null)
            stateText.text = "State: " + GetStateLabel();

        // ── Hikari 当前动作（读条类型）────────────────────────────
        if (actionText != null)
            actionText.text = "Action: " + GetActionLabel();

        // ── 读条条 ────────────────────────────────────────────────
        float castRatio = _hikari.CastRatio;
        if (castBarFill != null)
        {
            castBarFill.rectTransform.anchorMax = new Vector2(castRatio, 1f);
            castBarFill.enabled = _hikari.IsCasting;
        }
        if (castValueText != null)
        {
            if (_hikari.IsCasting)
                castValueText.text = $"Cast {_hikari.CurrentCastTime:F1} / {_hikari.CurrentCastDuration:F1}";
            else
                castValueText.text = "";
        }

        // ── 光负荷条 fill ─────────────────────────────────────────
        if (burdenBarFill != null)
        {
            float ratio = Mathf.Clamp01(_hikari.BurdenRatio);
            burdenBarFill.rectTransform.anchorMax = new Vector2(ratio, 1f);

            // 颜色：稳定导光=蓝，光溢出=橙，导光封锁=红
            if (_hikari.IsOverloaded)
                burdenBarFill.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            else if (_hikari.IsOverburdened)
                burdenBarFill.color = new Color(1.0f, 0.6f, 0.1f, 1f);
            else
                burdenBarFill.color = new Color(0.4f, 0.75f, 1.0f, 1f);
        }

        // ── 光负荷数值 ────────────────────────────────────────────
        if (burdenValueText != null)
        {
            int cur = Mathf.RoundToInt(_hikari.CurrentBurden);
            int max = Mathf.RoundToInt(_hikari.MaxBurden);
            burdenValueText.text = $"{cur} / {max}";
        }

        // ── 变化提示占位（不实现原因追踪）────────────────────────
        // TODO: 后续由 OnBurdenChanged 事件驱动，显示如 "光负荷 +12：因为 Hikari 治疗了玩家"
        // burdenChangeHintText 保持 "Hint: --"
    }
    // ─── ASCII label helpers (CJK → ASCII for TMP LiberationSans font) ─────
    private string GetStateLabel()
    {
        if (_hikari == null) return "--";
        if (_hikari.IsOverloaded)   return "Locked";
        if (_hikari.IsOverburdened) return "Overflow";
        return "Idle";
    }

    private string GetActionLabel()
    {
        if (_hikari == null) return "--";
        return _hikari.CurrentActionLabel switch
        {
            "\u6cbb\u7597\u8aad\u6761\u4e2d" => "Casting Heal",
            "\u7d27\u6025\u6cbb\u7597\u8aad\u6761\u4e2d" => "Casting Emergency",
            _ => _hikari.CurrentActionLabel == "--" ? "--" : _hikari.CurrentActionLabel,
        };
    }


}
