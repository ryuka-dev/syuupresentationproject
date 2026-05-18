using UnityEngine;

/// <summary>
/// 玩家技能战斗 HUD 第一版（最小可用版本）。
/// 使用 OnGUI 在屏幕右下角显示减伤技能 Iron Bulwark 的当前状态。
/// 这是战斗测试 HUD，不是最终正式 UI。
/// 挂载方式：将此脚本挂到 Player 或场景中任意 GameObject 上即可。
/// 不修改 Scene / Prefab / Animator。不依赖 Canvas / TMP / 图标资源。
/// </summary>
public class PlayerSkillHudUI : MonoBehaviour
{
    [Header("HUD 显示控制")]
    [SerializeField] private bool showHud = true;

    [Header("技能信息")]
    [SerializeField] private string skillName = "Iron Bulwark";
    [SerializeField] private string keyLabel  = "2";

    [Header("HUD 位置 / 尺寸")]
    [SerializeField] private Vector2 panelSize    = new Vector2(220f, 120f);
    [SerializeField] private float   bottomOffset = 40f;
    [SerializeField] private float   rightOffset  = 40f;

    // 缓存的减伤控制器引用
    private PlayerMitigationController _mitigationController;

    // ─── GUIStyle 缓存 ────────────────────────────────────────────
    private GUIStyle _titleStyle;
    private GUIStyle _statusReadyStyle;
    private GUIStyle _statusActiveStyle;
    private GUIStyle _statusCooldownStyle;
    private GUIStyle _infoStyle;
    private bool     _stylesInitialized;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        // 优先从当前 GameObject 获取
        _mitigationController = GetComponent<PlayerMitigationController>();

        // 当前对象上没有时，在场景中查找一次（仅 Awake 执行，不每帧 Find）
        if (_mitigationController == null)
            _mitigationController = FindFirstObjectByType<PlayerMitigationController>();

        if (_mitigationController == null)
            Debug.LogWarning("[PlayerSkillHudUI] PlayerMitigationController not found. HUD will show NOT FOUND.");
    }

    private void OnGUI()
    {
        if (!showHud) return;

        EnsureStyles();

        // 右下角位置计算
        float x = Screen.width  - panelSize.x - rightOffset;
        float y = Screen.height - panelSize.y - bottomOffset;

        GUI.Box(new Rect(x, y, panelSize.x, panelSize.y), GUIContent.none);

        GUILayout.BeginArea(new Rect(x + 6f, y + 6f, panelSize.x - 12f, panelSize.y - 12f));

        // タイトル行：技能名 + 按键
        GUILayout.Label($"{skillName}  [{keyLabel}]", _titleStyle);

        if (_mitigationController == null)
        {
            GUILayout.Label("Status: NOT FOUND", _infoStyle);
        }
        else if (_mitigationController.IsMitigationActive)
        {
            // 减伤生效中
            GUILayout.Label("Status: ACTIVE", _statusActiveStyle);
            GUILayout.Label($"Duration: {_mitigationController.MitigationRemainingTime:F2} s", _infoStyle);
            GUILayout.Label($"Damage Taken: {_mitigationController.DamageTakenMultiplier:P0}", _infoStyle);
        }
        else if (_mitigationController.CooldownRemainingTime > 0f)
        {
            // 冷却中
            GUILayout.Label("Status: COOLDOWN", _statusCooldownStyle);
            GUILayout.Label($"Cooldown: {_mitigationController.CooldownRemainingTime:F2} s", _infoStyle);
            GUILayout.Label($"Damage Taken: {_mitigationController.DamageTakenMultiplier:P0}", _infoStyle);
        }
        else
        {
            // 可用（READY）
            GUILayout.Label("Status: READY", _statusReadyStyle);
            GUILayout.Label($"Damage Taken: {_mitigationController.DamageTakenMultiplier:P0}", _infoStyle);
        }

        GUILayout.EndArea();
    }

    // ─── Private ─────────────────────────────────────────────────

    /// <summary>
    /// GUIStyle は OnGUI 内で初回のみ初期化する。
    /// Awake / Start での GUI.skin アクセスは不可。
    /// </summary>
    private void EnsureStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 13,
            normal    = { textColor = Color.white }
        };

        _statusReadyStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
            normal    = { textColor = new Color(0.4f, 1f, 0.4f) }   // 緑
        };

        _statusActiveStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
            normal    = { textColor = new Color(0.4f, 0.8f, 1f) }   // 水色
        };

        _statusCooldownStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
            normal    = { textColor = new Color(1f, 0.75f, 0.25f) } // 橙
        };

        _infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal   = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
    }
}
