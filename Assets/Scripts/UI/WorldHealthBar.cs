using UnityEngine;

/// <summary>
/// 世界空间血条 - 跟随角色头顶显示
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [Header("目标")]
    public HealthComponent health;
    public Transform followTarget;
    public Vector3 offset = new Vector3(0, 0.3f, 0);

    [Header("外观")]
    public float barWidth  = 80f;
    public float barHeight = 8f;
    public Color bgColor   = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color fgColor   = Color.red;

    private Camera mainCam;
    private Texture2D bgTex;
    private Texture2D fgTex;

    void Start()
    {
        mainCam = Camera.main;
        if (health == null) health = GetComponent<HealthComponent>();
        if (followTarget == null) followTarget = transform;
        bgTex = MakeTexture(bgColor);
        fgTex = MakeTexture(fgColor);
    }

    void OnGUI()
    {
        if (health == null || mainCam == null) return;
        Vector3 screenPos = mainCam.WorldToScreenPoint(followTarget.position + offset);
        if (screenPos.z < 0) return;

        float x = screenPos.x - barWidth * 0.5f;
        float y = Screen.height - screenPos.y;
        float ratio = health.currentHealth / health.maxHealth;

        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), bgTex);
        GUI.DrawTexture(new Rect(x, y, barWidth * ratio, barHeight), fgTex);
    }

    Texture2D MakeTexture(Color c)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }
}
