using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 正式 Canvas 技能格 UI — 通用版。
/// PlayerSkillBarCanvasUI から Initialize() で初期化されるか、
/// 旧来通り Inspector で skillId を設定して Start() で自動検索する。
/// どちらの使い方でも動作する。
/// </summary>
public class PlayerSkillCanvasUI : MonoBehaviour
{
    [Header("技能数据（自动初始化时无需手动填写）")]
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

    // PlayerSkillBarCanvasUI から直接 state を渡された場合はこちらを優先
    private PlayerSkillRuntimeState _runtimeState;

    // ─── 公开初始化（PlayerSkillBarCanvasUI から呼ばれる） ─────────

    /// <summary>
    /// 技能栏スクリプトから呼ばれる初期化メソッド。
    /// state と manager を設定し、静的テキスト・アイコンを更新する。
    /// </summary>
    public void Initialize(PlayerSkillManager manager, PlayerSkillRuntimeState state)
    {
        skillManager   = manager;
        _runtimeState  = state;

        if (state == null || state.SkillData == null)
        {
            ApplyNotFoundState();
            return;
        }

        var data = state.SkillData;
        skillId   = data.SkillId;
        skillName = data.SkillName;
        keyLabel  = data.KeyLabel;

        if (skillNameText != null) skillNameText.text = data.SkillName;
        if (keyText       != null) keyText.text       = data.KeyLabel;
        if (iconImage     != null && data.Icon != null) iconImage.sprite = data.Icon;

        ApplyReadyState();
    }

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Start()
    {
        // Initialize() が呼ばれていない場合（旧来の手動バインド）のみ自動検索
        if (_runtimeState != null) return;

        if (skillManager == null)
            skillManager = GetComponentInParent<PlayerSkillManager>();
        if (skillManager == null)
            skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (skillManager == null)
            Debug.LogWarning("[PlayerSkillCanvasUI] PlayerSkillManager not found. UI will show NOT FOUND.");

        if (keyText       != null) keyText.text      = keyLabel;
        if (skillNameText != null) skillNameText.text = skillName;

        ApplyReadyState();
    }

private void Update()
    {
        PlayerSkillRuntimeState state;
        if (_runtimeState != null)
            state = _runtimeState;
        else if (skillManager != null)
            state = skillManager.GetStateBySkillId(skillId);
        else
            return; // skillManager 未設定なら何もしない

        if (state == null) { ApplyNotFoundState(); return; }

        if (state.IsActive)
            ApplyActiveState(state.ActiveRemainingTime);
        else if (state.IsOnCooldown)
            ApplyCooldownState(state.CooldownRemainingTime, state.CooldownNormalized);
        else
            ApplyReadyState();
    }

    // ─── 状態更新 ─────────────────────────────────────────────────

    private void ApplyReadyState()
    {
        SetOverlayActive(false);
        if (cooldownText != null) cooldownText.text = string.Empty;
        SetIconBrightness(1f);
    }

    private void ApplyActiveState(float remaining)
    {
        SetOverlayActive(false);
        if (cooldownText != null) cooldownText.text = remaining.ToString("F1");
        SetIconBrightness(1f);
    }

    private void ApplyCooldownState(float remaining, float fillRatio)
    {
        SetOverlayActive(true, fillRatio);
        if (cooldownText != null) cooldownText.text = remaining.ToString("F1");
        SetIconBrightness(0.45f);
    }

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
