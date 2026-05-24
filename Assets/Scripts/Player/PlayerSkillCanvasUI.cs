using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 正式 Canvas 技能格 UI — 通用版（GuardCounter / BasicAttack 対応）。
///
/// 表示モード:
///   BasicMeleeAttack / BasicAreaAttack:
///     PlayerBasicAttackController の共有冷却を参照し、両スロットで同一冷却を表示。
///   GuardCounter:
///     Condition Locked（灰）/ Proc Ready（金）表示。
///   その他:
///     従来の Ready / Active / Cooldown 表示。
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

    // ─── 実行時キャッシュ ─────────────────────────────────────────
    private PlayerSkillRuntimeState          _runtimeState;
    private bool                             _isGuardCounterSkill;
    private bool                             _isBasicAttackSkill;
    private PlayerGuardCounterController     _guardCounter;
    private PlayerBasicAttackController      _basicAttackCtrl;

    // GuardCounter 専用 UI 子オブジェクト
    private Image           _conditionLockedOverlay;
    private Image           _procReadyGlow;
    private TextMeshProUGUI _procRemainingText;

    // ─── 公開初期化 ──────────────────────────────────────────────

    public void Initialize(PlayerSkillManager manager, PlayerSkillRuntimeState state)
    {
        skillManager  = manager;
        _runtimeState = state;

        if (state == null || state.SkillData == null) { ApplyNotFoundState(); return; }

        var data  = state.SkillData;
        skillId   = data.SkillId;
        skillName = data.SkillName;
        keyLabel  = data.KeyLabel;

        if (skillNameText != null) skillNameText.text = data.SkillName;
        if (keyText       != null) keyText.text       = data.KeyLabel;
        if (iconImage     != null && data.Icon != null) iconImage.sprite = data.Icon;

        // 種別判定
        _isGuardCounterSkill = data.EffectType == PlayerSkillEffectType.GuardCounter;
        _isBasicAttackSkill  = data.EffectType == PlayerSkillEffectType.BasicMeleeAttack
                            || data.EffectType == PlayerSkillEffectType.BasicAreaAttack;

        if (_isGuardCounterSkill)
        {
            _guardCounter = manager != null ? manager.GetComponent<PlayerGuardCounterController>() : null;
            if (_guardCounter == null) _guardCounter = Object.FindFirstObjectByType<PlayerGuardCounterController>();
            CreateGuardCounterUI();
            ApplyConditionLockedUI();
        }
        else if (_isBasicAttackSkill)
        {
            _basicAttackCtrl = manager != null ? manager.GetComponent<PlayerBasicAttackController>() : null;
            ApplyReadyState();
        }
        else
        {
            ApplyReadyState();
        }
    }

    // ─── GuardCounter 専用 UI 動的生成 ───────────────────────────

    private void CreateGuardCounterUI()
    {
        if (cooldownOverlay != null) cooldownOverlay.enabled = false;

        var lockedGO = new GameObject("ConditionLockedOverlay");
        lockedGO.transform.SetParent(transform, false);
        _conditionLockedOverlay = lockedGO.AddComponent<Image>();
        _conditionLockedOverlay.color = new Color(0.15f, 0.15f, 0.15f, 0.55f);
        var lRT = lockedGO.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;

        var glowGO = new GameObject("ProcReadyGlow");
        glowGO.transform.SetParent(transform, false);
        _procReadyGlow = glowGO.AddComponent<Image>();
        _procReadyGlow.color = new Color(1f, 0.85f, 0.1f, 0.3f);
        var gRT = glowGO.GetComponent<RectTransform>();
        gRT.anchorMin = new Vector2(-0.08f, -0.08f); gRT.anchorMax = new Vector2(1.08f, 1.08f);
        gRT.offsetMin = Vector2.zero; gRT.offsetMax = Vector2.zero;
        glowGO.SetActive(false);

        var textGO = new GameObject("ProcRemainingText");
        textGO.transform.SetParent(transform, false);
        _procRemainingText = textGO.AddComponent<TextMeshProUGUI>();
        _procRemainingText.fontSize  = 18f;
        _procRemainingText.fontStyle = FontStyles.Bold;
        _procRemainingText.alignment = TextAlignmentOptions.Center;
        _procRemainingText.color     = Color.white;
        _procRemainingText.text      = string.Empty;
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0.1f); tRT.anchorMax = new Vector2(1f, 0.6f);
        tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
    }

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Start()
    {
        if (_runtimeState != null) return;
        if (skillManager == null) skillManager = GetComponentInParent<PlayerSkillManager>();
        if (skillManager == null) skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (keyText       != null) keyText.text       = keyLabel;
        if (skillNameText != null) skillNameText.text = skillName;
        ApplyReadyState();
    }

    private void Update()
    {
        if (_isGuardCounterSkill)   { UpdateGuardCounterDisplay(); return; }
        if (_isBasicAttackSkill)    { UpdateBasicAttackDisplay();   return; }

        PlayerSkillRuntimeState state;
        if (_runtimeState != null)          state = _runtimeState;
        else if (skillManager != null)      state = skillManager.GetStateBySkillId(skillId);
        else                                return;

        if (state == null) { ApplyNotFoundState(); return; }

        if (state.IsActive)          ApplyActiveState(state.ActiveRemainingTime);
        else if (state.IsOnCooldown) ApplyCooldownState(state.CooldownRemainingTime, state.CooldownNormalized);
        else                         ApplyReadyState();
    }

    // ─── BasicAttack 表示（共有冷却） ────────────────────────────

    private void UpdateBasicAttackDisplay()
    {
        if (_basicAttackCtrl == null) { ApplyReadyState(); return; }

        float rem = _basicAttackCtrl.BasicAttackCooldownRemaining;
        if (rem > 0f)
        {
            float normalized = _basicAttackCtrl.BasicAttackCooldownDuration > 0f
                ? rem / _basicAttackCtrl.BasicAttackCooldownDuration
                : 0f;
            ApplyCooldownState(rem, normalized);
        }
        else
        {
            ApplyReadyState();
        }
    }

    // ─── GuardCounter 表示 ────────────────────────────────────────

    private void UpdateGuardCounterDisplay()
    {
        if (_guardCounter == null) { ApplyConditionLockedUI(); return; }
        if (_guardCounter.IsCounterReady) ApplyProcReadyUI(_guardCounter.CounterRemainingTime);
        else                              ApplyConditionLockedUI();
    }

    private void ApplyConditionLockedUI()
    {
        SetIconBrightness(0.4f);
        SetOverlayActive(false);
        if (cooldownText            != null) cooldownText.text = string.Empty;
        if (_conditionLockedOverlay != null) _conditionLockedOverlay.gameObject.SetActive(true);
        if (_procReadyGlow          != null) _procReadyGlow.gameObject.SetActive(false);
        if (_procRemainingText      != null) _procRemainingText.text = string.Empty;
    }

    private void ApplyProcReadyUI(float remaining)
    {
        SetIconBrightness(1f);
        SetOverlayActive(false);
        if (cooldownText            != null) cooldownText.text = string.Empty;
        if (_conditionLockedOverlay != null) _conditionLockedOverlay.gameObject.SetActive(false);
        if (_procReadyGlow != null)
        {
            _procReadyGlow.gameObject.SetActive(true);
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            var c = _procReadyGlow.color; c.a = Mathf.Lerp(0.2f, 0.5f, pulse);
            _procReadyGlow.color = c;
        }
        if (_procRemainingText != null)
            _procRemainingText.text = Mathf.CeilToInt(remaining).ToString();
    }

    // ─── 通常状態 ─────────────────────────────────────────────────

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
