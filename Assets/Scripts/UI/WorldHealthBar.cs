using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间血条 - 敌人头顶显示
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [Header("引用")]
    public EntityStats stats;
    public Transform target;          // 跟随的目标（敌人根对象）
    public Vector3 offset = new Vector3(0, 2.4f, 0);

    private Image fillImage;
    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
        fillImage = GetComponentInChildren<Image>();
        if (stats != null)
            stats.OnHealthChanged += UpdateBar;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 跟随目标
        transform.position = target.position + offset;
        // 始终面向摄像机
        transform.forward = mainCamera.transform.forward;
    }

    void UpdateBar(float current, float max)
    {
        if (fillImage != null)
            fillImage.fillAmount = current / max;
    }

    void OnDestroy()
    {
        if (stats != null)
            stats.OnHealthChanged -= UpdateBar;
    }
}
