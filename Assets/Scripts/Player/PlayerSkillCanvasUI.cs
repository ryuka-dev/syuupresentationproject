using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 正式 Canvas 技能格 UI — Iron Bulwark 第一版（第三步：接入 PlayerSkillManager）。
/// 読み取り元を PlayerMitigationController から PlayerSkillManager の RuntimeState に切り替え。
/// Update では PlayerSkillManager.GetStateBySkillId(skillId) で状態を取得し UI を更新する。
/// PlayerSkillManager または skillId が見つからない場合は安全に NOT FOUND 表示（エラーなし）。
/// 不处理输入，不启动技能，不修改减伤逻辑。
/// 挂载到 IronBulwarkSlot GameObject 上。
/// </summary>
public class PlayerSkillCanvasUI : MonoBehaviour
{
    [Header("技能数据（只读配置）")]
    [SerializeField] private string skillName = "Iron Bulwark";
    [SerializeField] private string keyLabel  = "2";
    [SerializeField] private string skillId   = "iron_bulwark";

    [Header("运行时绑定 — 技能管理器（留空自动查找）")]
    [SerializeField] private PlayerSkillManager skillManager;

    [Header("UI 元素绑定")]
    [SerializeField] private Image           iconImage;
    [SerializeField] private Image           cooldownOverlay;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI skillNameText;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Start()
    {
        // PlayerSkillManager の解決（Inspector 未バインド時のみ自動検索）
        if (skillManager == null)
            skillManager = GetComponentInParent<PlayerSkillManager>();
        if (skillManager == null)
            skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (skillManager == null)
            Debug.LogWarning("[PlayerSkillCanvasUI] PlayerSkillManager not found. UI will show NOT FOUND.");

        // 静的テキストの初期化
        if (keyText       != null) keyText.text      = keyLabel;
        if (skillNameText != null) skillNameText.text = skillName;

        // 初期状態：READY
        ApplyReadyState();
    }

    private void Update()
    {
        // ── PlayerSkillManager が見つからない ─────────────────────
        if (skillManager == null)
        {
            ApplyNotFoundState();
            return;
        }

        // ── skillId に対応する RuntiemState を取得 ─────────────────
        var state = skillManager.GetStateBySkillId(skillId);
        if (state == null)
        {
            ApplyNotFoundState();
            return;
        }

        // ── 状態に応じて UI を更新 ────────────────────────────────
        if (state.IsActive)
        {
            ApplyActiveState(state.ActiveRemainingTime);
        }
        else if (state.IsOnCooldown)
        {
            ApplyCooldownState(state.CooldownRemainingTime, state.CooldownNormalized);
        }
        else
        {
            ApplyReadyState();
        }
    }

    // ─── 状態更新メソッド ─────────────────────────────────────────

    /// <summary>READY：图标正常，无遮罩，冷却文本为空。</summary>
    private void ApplyReadyState()
    {
        SetOverlayActive(false);
        if (cooldownText != null) cooldownText.text = string.Empty;
        SetIconBrightness(1f);
    }

    /// <summary>ACTIVE：显示持续时间倒计时，遮罩隐藏（减伤正在生效）。</summary>
    private void ApplyActiveState(float remaining)
    {
        SetOverlayActive(false);
        if (cooldownText != null) cooldownText.text = remaining.ToString("F1");
        SetIconBrightness(1f);
    }

    /// <summary>COOLDOWN：显示冷却倒计时，遮罩覆盖，图标变暗。</summary>
    private void ApplyCooldownState(float remaining, float fillRatio)
    {
        SetOverlayActive(true, fillRatio);
        if (cooldownText != null) cooldownText.text = remaining.ToString("F1");
        SetIconBrightness(0.45f);
    }

    /// <summary>NOT FOUND：安全显示，不报错。</summary>
    private void ApplyNotFoundState()
    {
        SetOverlayActive(true, 1f);
        if (cooldownText != null) cooldownText.text = "-";
        SetIconBrightness(0.3f);
    }

    // ─── ヘルパー ─────────────────────────────────────────────────

    private void SetOverlayActive(bool active, float fillAmount = 1f)
    {
        if (cooldownOverlay == null) return;
        cooldownOverlay.enabled    = active;
        cooldownOverlay.fillAmount = Mathf.Clamp01(fillAmount);
    }

    private void SetIconBrightness(float brightness)
    {
        if (iconImage == null) return;
        float b = Mathf.Clamp01(brightness);
        iconImage.color = new Color(b, b, b, 1f);
    }
}
