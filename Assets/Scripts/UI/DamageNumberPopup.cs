using UnityEngine;
using TMPro;

/// <summary>
/// 伤害飘字单体。
/// 显示最终伤害数值，向上浮动并淡出，生命周期结束后自动销毁。
/// 由 DamageNumberSpawner 通过 Instantiate + Initialize() 驱动，不依赖场景中特定 Canvas。
/// 使用 TextMeshPro（世界空间文字），不使用 TextMeshProUGUI。
/// </summary>
public class DamageNumberPopup : MonoBehaviour
{
    [Header("组件引用（留空时自动查找）")]
    [SerializeField] private TextMeshPro text;

    [Header("动画参数")]
    [SerializeField] private float lifetime            = 1f;
    [SerializeField] private float floatSpeed         = 1.2f;
    [SerializeField] private Vector3 worldOffset      = Vector3.zero;

    // ─── 运行时状态 ───────────────────────────────────────────────

    private float  _timer;
    private Color  _startColor;
    private Camera _mainCamera;

    // ─── 公开初始化 ───────────────────────────────────────────────

    /// <summary>
    /// 生成后立即调用。设置伤害数值并缓存 Main Camera。
    /// damage 会四舍五入为整数显示，最小显示 1。
    /// </summary>
    public void Initialize(float damage)
    {
        Initialize(damage, Camera.main);
    }

    /// <summary>
    /// 生成后立即调用（Camera を明示的に渡すオーバーロード）。
    /// damage 会四舍五入为整数显示，最小显示 1。
    /// </summary>
    public void Initialize(float damage, Camera targetCamera)
    {
        // TextMeshPro 解決
        if (text == null)
            text = GetComponentInChildren<TextMeshPro>();

        if (text == null)
        {
            Debug.LogWarning("[DamageNumberPopup] TextMeshPro component not found. Destroying popup.");
            Destroy(gameObject);
            return;
        }

        _mainCamera = targetCamera;

        // 伤害数值格式化
        int displayValue = Mathf.Max(1, Mathf.RoundToInt(damage));
        text.text = displayValue.ToString();

        // 初始颜色缓存（用于淡出计算）
        _startColor = text.color;
        _timer      = 0f;

        // 应用世界空间偏移
        transform.position += worldOffset;
    }

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Update()
    {
        if (text == null)
        {
            Destroy(gameObject);
            return;
        }

        _timer += Time.deltaTime;

        // 向上浮动
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 淡出（线性）
        float alpha = Mathf.Clamp01(1f - (_timer / lifetime));
        Color c = _startColor;
        c.a     = alpha;
        text.color = c;

        // 始终面向摄像机
        if (_mainCamera != null)
            transform.forward = _mainCamera.transform.forward;

        // 生命周期结束时销毁
        if (_timer >= lifetime)
            Destroy(gameObject);
    }
}
