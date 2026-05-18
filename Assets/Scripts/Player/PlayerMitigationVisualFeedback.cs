using UnityEngine;

/// <summary>
/// Iron Bulwark 減伤视觉反馈 — 第一版。
/// 减伤激活时在玩家脚下用 LineRenderer 显示蓝色防御光环（呼吸缩放 + 缓慢旋转）。
/// 减伤结束后隐藏光环。
/// 不处理输入、不修改减伤逻辑、不修改 UI、不修改 HealthComponent。
/// 挂载方式：把此脚本挂到 Player 上即可。
/// </summary>
public class PlayerMitigationVisualFeedback : MonoBehaviour
{
    [Header("减伤控制器引用（留空时自动查找）")]
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
    private bool         _ready;          // 所有依赖均就绪

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        ResolveController();
        if (mitigationController == null)
        {
            Debug.LogWarning("[MitigationFX] PlayerMitigationController not found. Visual feedback disabled.");
            return;
        }

        _ready = BuildRing();
    }

    private void OnEnable()
    {
        // 脚本 re-enable 时保持正确初始状态
        if (_ringGO != null && mitigationController != null)
            _ringGO.SetActive(mitigationController.IsMitigationActive);
    }

    private void OnDisable()
    {
        // 脚本禁用时隐藏光环
        if (_ringGO != null) _ringGO.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_runtimeMat != null) Destroy(_runtimeMat);
    }

    private void Update()
    {
        if (!_ready || mitigationController == null) return;

        bool active = mitigationController.IsMitigationActive;
        if (_ringGO.activeSelf != active)
            _ringGO.SetActive(active);

        if (!active) return;

        // ── 旋转 ──
        _ringGO.transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);

        // ── 呼吸缩放 ──
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScaleAmount;
        _ringGO.transform.localScale = new Vector3(pulse, 1f, pulse);
    }

    // ─── Private ─────────────────────────────────────────────────

    private void ResolveController()
    {
        if (mitigationController != null) return;
        mitigationController = GetComponent<PlayerMitigationController>();
        if (mitigationController == null)
            mitigationController = FindFirstObjectByType<PlayerMitigationController>();
    }

    /// <summary>
    /// Runtime で ring GameObject と LineRenderer を生成する。
    /// 成功したら true を返す。
    /// </summary>
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
        _runtimeMat.color = activeColor;  // Sprites/Default などに対応

        // ── Ring GameObject ──
        _ringGO = new GameObject("MitigationRing_Runtime");
        _ringGO.transform.SetParent(transform, false);
        _ringGO.transform.localPosition = Vector3.zero;
        _ringGO.transform.localScale    = Vector3.one;

        // ── LineRenderer ──
        _lr = _ringGO.AddComponent<LineRenderer>();
        _lr.useWorldSpace  = false;
        _lr.loop           = true;
        _lr.positionCount  = segments;
        _lr.startWidth     = lineWidth;
        _lr.endWidth       = lineWidth;
        _lr.startColor     = activeColor;
        _lr.endColor       = activeColor;
        _lr.material       = _runtimeMat;
        _lr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows     = false;

        // ── 頂点を XZ 平面に配置 ──
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x     = Mathf.Cos(angle) * radius;
            float z     = Mathf.Sin(angle) * radius;
            _lr.SetPosition(i, new Vector3(x, yOffset, z));
        }

        _ringGO.SetActive(false);   // デフォルト非表示
        return true;
    }
}
