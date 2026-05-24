using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 正式 Canvas 技能格 UI — 通用版（GuardCounter 対応）。
///
/// GuardCounter 技能の場合:
///   条件未達時 → 灰色オーバーレイ（Condition Locked）
///   Ready 時   → 灰色を非表示にし、金色オーバーレイと残り時間を表示（Proc Ready）
///
/// 普通技能の場合: 従来の Ready / Active / Cooldown 表示。
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

    // GuardCounter 専用フィールド
    private bool                         _isGuardCounterSkill;
    private PlayerGuardCounterController _guardCounter;
    private Image                        _conditionLockedOverlay;   // 灰色 Condition Locked
    private Image                        _procReadyGlow;            // 金色 Proc Ready 背景
    private TextMeshProUGUI              _procRemainingText;        // 残り秒数表示

    // ─── 公开初始化（PlayerSkillBarCanvasUI から呼ばれる） ─────────

    public void Initialize(PlayerSkillManager manager, PlayerSkillRuntimeState state)
    {
        skillManager  = manager;
        _runtimeState = state;

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

        // GuardCounter 専用 UI をセットアップ
        if (data.EffectType == PlayerSkillEffectType.GuardCounter)
        {
            _isGuardCounterSkill = true;
            _guardCounter = manager != null ? manager.GetComponent<PlayerGuardCounterController>() : null;
            if (_guardCounter == null)
                _guardCounter = Object.FindFirstObjectByType<PlayerGuardCounterController>();
            CreateGuardCounterUI();
            ApplyConditionLockedUI(); // 初期は Condition Locked
        }
        else
        {
            _isGuardCounterSkill = false;
            ApplyReadyState();
        }
    }

    // ─── GuardCounter 専用 UI 動的生成 ───────────────────────────

    private void CreateGuardCounterUI()
    {
        // 既存の cooldownOverlay を念のため非表示（通常 CD と混ざらないよう）
        if (cooldownOverlay != null) cooldownOverlay.enabled = false;

        // ─ Condition Locked オーバーレイ（灰色）
        var lockedGO  = new GameObject("ConditionLockedOverlay");
        lockedGO.transform.SetParent(transform, false);
        _conditionLockedOverlay = lockedGO.AddComponent<Image>();
        _conditionLockedOverlay.color = new Color(0.15f, 0.15f, 0.15f, 0.55f);
        var lockedRT  = lockedGO.GetComponent<RectTransform>();
        lockedRT.anchorMin = Vector2.zero;
        lockedRT.anchorMax = Vector2.one;
        lockedRT.offsetMin = Vector2.zero;
        lockedRT.offsetMax = Vector2.zero;

        // ─ Proc Ready グロー（金色背景）
        var glowGO = new GameObject("ProcReadyGlow");
        glowGO.transform.SetParent(transform, false);
        _procReadyGlow = glowGO.AddComponent<Image>();
        _procReadyGlow.color = new Color(1f, 0.85f, 0.1f, 0.3f);
        var glowRT = glowGO.GetComponent<RectTransform>();
        glowRT.anchorMin = new Vector2(-0.08f, -0.08f);
        glowRT.anchorMax = new Vector2(1.08f,  1.08f);
        glowRT.offsetMin = Vector2.zero;
        glowRT.offsetMax = Vector2.zero;
        glowGO.SetActive(false);

        // ─ 残り時間テキスト
        var textGO = new GameObject("ProcRemainingText");
        textGO.transform.SetParent(transform, false);
        _procRemainingText = textGO.AddComponent<TextMeshProUGUI>();
        _procRemainingText.fontSize   = 18f;
        _procRemainingText.fontStyle  = FontStyles.Bold;
        _procRemainingText.alignment  = TextAlignmentOptions.Center;
        _procRemainingText.color      = Color.white;
        _procRemainingText.text       = string.Empty;
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0.1f);
        textRT.anchorMax = new Vector2(1f, 0.6f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
    }

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Start()
    {
        if (_runtimeState != null) return;

        if (skillManager == null)
            skillManager = GetComponentInParent<PlayerSkillManager>();
        if (skillManager == null)
            skillManager = FindFirstObjectByType<PlayerSkillManager>();

        if (keyText       != null) keyText.text       = keyLabel;
        if (skillNameText != null) skillNameText.text = skillName;

        ApplyReadyState();
    }

    private void Update()
    {
        if (_isGuardCounterSkill)
        {
            UpdateGuardCounterDisplay();
            return; // 通常の Ready/Active/Cooldown ロジックは実行しない
        }

        PlayerSkillRuntimeState state;
        if (_runtimeState != null)
            state = _runtimeState;
        else if (skillManager != null)
            state = skillManager.GetStateBySkillId(skillId);
        else
            return;

        if (state == null) { ApplyNotFoundState(); return; }

        if (state.IsActive)
            ApplyActiveState(state.ActiveRemainingTime);
        else if (state.IsOnCooldown)
            ApplyCooldownState(state.CooldownRemainingTime, state.CooldownNormalized);
        else
            ApplyReadyState();
    }

    // ─── GuardCounter 表示更新 ───────────────────────────────────

    private void UpdateGuardCounterDisplay()
    {
        if (_guardCounter == null) { ApplyConditionLockedUI(); return; }

        if (_guardCounter.IsCounterReady)
            ApplyProcReadyUI(_guardCounter.CounterRemainingTime);
        else
            ApplyConditionLockedUI();
    }

    private void ApplyConditionLockedUI()
    {
        SetIconBrightness(0.4f);
        SetOverlayActive(false); // 通常 CD オーバーレイは非表示
        if (cooldownText != null) cooldownText.text = string.Empty;

        if (_conditionLockedOverlay != null) _conditionLockedOverlay.gameObject.SetActive(true);
        if (_procReadyGlow          != null) _procReadyGlow.gameObject.SetActive(false);
        if (_procRemainingText      != null) _procRemainingText.text = string.Empty;
    }

    private void ApplyProcReadyUI(float remaining)
    {
        SetIconBrightness(1f);
        SetOverlayActive(false);
        if (cooldownText != null) cooldownText.text = string.Empty;

        if (_conditionLockedOverlay != null) _conditionLockedOverlay.gameObject.SetActive(false);

        if (_procReadyGlow != null)
        {
            _procReadyGlow.gameObject.SetActive(true);
            // 軽いパルス演出
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            var c = _procReadyGlow.color;
            c.a = Mathf.Lerp(0.2f, 0.5f, pulse);
            _procReadyGlow.color = c;
        }

        if (_procRemainingText != null)
            _procRemainingText.text = Mathf.CeilToInt(remaining).ToString();
    }

    // ─── 通常状態更新 ─────────────────────────────────────────────

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
