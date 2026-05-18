using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 正式 Canvas 技能格 UI — Iron Bulwark 第一版。
/// 每帧读取 PlayerMitigationController 状态，更新图标遮罩和倒计时文本。
/// 不处理输入，不启动技能，只负责 UI 显示。
/// 挂载到场景中 IronBulwarkSlot GameObject 上。
/// Inspector 中绑定各子对象引用；如果 mitigationController 未绑定，
/// 则在 Start 中自动搜索（仅一次，不每帧 Find）。
/// </summary>
public class PlayerSkillCanvasUI : MonoBehaviour
{
    [Header("技能数据（只读配置）")]
    [SerializeField] private string skillName = "Iron Bulwark";
    [SerializeField] private string keyLabel  = "2";

    [Header("运行时绑定 — 减伤控制器")]
    [SerializeField] private PlayerMitigationController mitigationController;

    [Header("UI 元素绑定")]
    [SerializeField] private Image            iconImage;
    [SerializeField] private Image            cooldownOverlay;
    [SerializeField] private TextMeshProUGUI  cooldownText;
    [SerializeField] private TextMeshProUGUI  keyText;
    [SerializeField] private TextMeshProUGUI  skillNameText;

    // 冷却计算用：记录上次激活开始时的最大冷却时长（用于 fillAmount 计算）
    private float _maxCooldown = 12f;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Start()
    {
        // Inspector 未绑定时自动查找（仅一次）
        if (mitigationController == null)
        {
            mitigationController = FindFirstObjectByType<PlayerMitigationController>();
            if (mitigationController == null)
                Debug.LogWarning("[PlayerSkillCanvasUI] PlayerMitigationController not found. UI will show safe fallback state.");
        }

        // 初始化静态文本
        if (keyText       != null) keyText.text       = keyLabel;
        if (skillNameText != null) skillNameText.text  = skillName;

        // 初始状态：READY
        ApplyReadyState();
    }

    private void Update()
    {
        if (mitigationController == null)
        {
            ApplyNotFoundState();
            return;
        }

        if (mitigationController.IsMitigationActive)
        {
            // 追踪最大冷却值（active 期间 cooldown 已经被设置为最大值）
            if (mitigationController.CooldownRemainingTime > _maxCooldown)
                _maxCooldown = mitigationController.CooldownRemainingTime;

            ApplyActiveState(mitigationController.MitigationRemainingTime);
        }
        else if (mitigationController.CooldownRemainingTime > 0f)
        {
            float remaining = mitigationController.CooldownRemainingTime;
            float fill      = _maxCooldown > 0f ? remaining / _maxCooldown : 0f;
            ApplyCooldownState(remaining, fill);
        }
        else
        {
            _maxCooldown = 12f; // 冷却结束后重置（下次重新追踪）
            ApplyReadyState();
        }
    }

    // ─── 状态更新方法 ─────────────────────────────────────────────

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

    // ─── 辅助方法 ─────────────────────────────────────────────────

    private void SetOverlayActive(bool active, float fillAmount = 1f)
    {
        if (cooldownOverlay == null) return;
        cooldownOverlay.enabled     = active;
        cooldownOverlay.fillAmount  = Mathf.Clamp01(fillAmount);
    }

    private void SetIconBrightness(float brightness)
    {
        if (iconImage == null) return;
        float b = Mathf.Clamp01(brightness);
        iconImage.color = new Color(b, b, b, 1f);
    }
}
