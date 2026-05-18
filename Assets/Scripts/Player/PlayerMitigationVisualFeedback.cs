using UnityEngine;

/// <summary>
/// Iron Bulwark 減伤视觉反馈 — 第五步：改为读取 PlayerSkillManager 的运行时状态。
/// 减伤激活时在玩家脚下用 LineRenderer 显示蓝色防御光环（呼吸缩放 + 缓慢旋转）。
/// 光环显示条件：PlayerSkillManager.GetStateBySkillId(skillId).IsActive == true。
/// 视觉表现本身（LineRenderer、颜色、旋转、呼吸）完全保留，本次只改"何时显示"。
/// 不处理输入、不修改减伤逻辑、不修改 UI、不修改 HealthComponent。
/// </summary>
public class PlayerMitigationVisualFeedback : MonoBehaviour
{
    [Header("技能管理器引用（留空时自动查找）")]
    [SerializeField] private PlayerSkillManager skillManager;
    [SerializeField] private string             skillId = "iron_bulwark";

    [Header("旧引用（过渡期保留，当前不用于主逻辑）")]
    [SerializeField] private PlayerMitigationController mitigationController;

    [Header("光环形状")]
    [SerializeField] private float radius          = 1.2f;
    [SerializeField] private float yOffset         = 0.05f;
    [SerializeField] private float lineWidth       = 0.06f;
    [SerializeField] private int   segments        = 96;

    [Header("光环动画")]
    [SerializeField] private float pulseSpeed       = 3f;
    [SerializeField] private float pulseScaleAmount = 0.08f;
    [SerializeField] private float rotationSpeed    = 60f;

    [Header("颜色")]
    [SerializeField] private Color activeColor = new Color(0.25f, 0.75f, 1f, 0.75f);

    // ─── Runtime 对象 ─────────────────────────────────────────────
    private GameObject   _ringGO;
    private LineRenderer _lr;
    private Material     _runtimeMat;
    private bool         _ready;    // BuildRing() 成否のみに依存

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        ResolveSkillManager();
        if (skillManager == null)
            Debug.LogWarning("[MitigationFX] PlayerSkillManager not found. Visual feedback may not activate.");

        _ready = BuildRing();
    }

    private void OnEnable()
    {
        // 脚本 re-enable 時に正しい初期状態を保つ
        if (_ringGO != null)
            _ringGO.SetActive(ShouldShowMitigationRing());
    }

    private void OnDisable()
    {
        if (_ringGO != null) _ringGO.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_runtimeMat != null) Destroy(_runtimeMat);
    }

    private void Update()
    {
        if (!_ready) return;

        bool shouldShow = ShouldShowMitigationRing();
        if (_ringGO.activeSelf != shouldShow)
            _ringGO.SetActive(shouldShow);

        if (!shouldShow) return;

        // ── 旋転 ──
        _ringGO.transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);

        // ── 呼吸スケール ──
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScaleAmount;
        _ringGO.transform.localScale = new Vector3(pulse, 1f, pulse);
    }

    // ─── Private ─────────────────────────────────────────────────

    /// <summary>
    /// 光环を表示すべきかどうかを PlayerSkillManager 経由で判定する。
    /// PlayerSkillManager / state が見つからない場合は false（エラーなし）。
    /// </summary>
    private bool ShouldShowMitigationRing()
    {
        if (skillManager == null) return false;
        var state = skillManager.GetStateBySkillId(skillId);
        if (state == null) return false;
        return state.IsActive;
    }

    private void ResolveSkillManager()
    {
        if (skillManager != null) return;
        skillManager = GetComponent<PlayerSkillManager>();
        if (skillManager != null) return;
        skillManager = GetComponentInParent<PlayerSkillManager>();
        if (skillManager != null) return;
        skillManager = FindFirstObjectByType<PlayerSkillManager>();
    }

    /// <summary>Runtime で ring GameObject と LineRenderer を生成する。成功したら true。</summary>
    private bool BuildRing()
    {
        // ── Material ──
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("[MitigationFX] No usable Shader found. Visual feedback disabled.");
            return false;
        }

        _runtimeMat       = new Material(shader);
        _runtimeMat.name  = "MitigationRingMat_Runtime";
        _runtimeMat.color = activeColor;

        // ── Ring GameObject ──
        _ringGO = new GameObject("MitigationRing_Runtime");
        _ringGO.transform.SetParent(transform, false);
        _ringGO.transform.localPosition = Vector3.zero;
        _ringGO.transform.localScale    = Vector3.one;

        // ── LineRenderer ──
        _lr = _ringGO.AddComponent<LineRenderer>();
        _lr.useWorldSpace      = false;
        _lr.loop               = true;
        _lr.positionCount      = segments;
        _lr.startWidth         = lineWidth;
        _lr.endWidth           = lineWidth;
        _lr.startColor         = activeColor;
        _lr.endColor           = activeColor;
        _lr.material           = _runtimeMat;
        _lr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows     = false;

        // ── 頂点を XZ 平面に配置 ──
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, yOffset, Mathf.Sin(angle) * radius));
        }

        _ringGO.SetActive(false);
        return true;
    }
}
