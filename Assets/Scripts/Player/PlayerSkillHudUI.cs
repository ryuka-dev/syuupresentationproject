using UnityEngine;

/// <summary>
/// 玩家技能战斗 HUD — 旧 OnGUI 版本（已迁移到 PlayerSkillCanvasUI 正式 Canvas UI）。
/// 本脚本已改为读取 PlayerSkillManager，移除了对旧 PlayerMitigationController 的依赖。
/// 如不再需要此 OnGUI HUD，可将 showHud 设为 false 或直接 Remove Component。
/// </summary>
public class PlayerSkillHudUI : MonoBehaviour
{
    [Header("HUD 显示控制")]
    [SerializeField] private bool showHud = false;   // 已有正式 Canvas UI，默认关闭

    [Header("技能信息")]
    [SerializeField] private string skillName = "Iron Bulwark";
    [SerializeField] private string keyLabel  = "2";
    [SerializeField] private string skillId   = "iron_bulwark";

    [Header("HUD 位置 / 尺寸")]
    [SerializeField] private Vector2 panelSize    = new Vector2(220f, 120f);
    [SerializeField] private float   bottomOffset = 40f;
    [SerializeField] private float   rightOffset  = 40f;

    private PlayerSkillManager _skillManager;

    private GUIStyle _titleStyle;
    private GUIStyle _statusReadyStyle;
    private GUIStyle _statusActiveStyle;
    private GUIStyle _statusCooldownStyle;
    private GUIStyle _infoStyle;
    private bool     _stylesInitialized;

    private void Awake()
    {
        _skillManager = GetComponent<PlayerSkillManager>();
        if (_skillManager == null)
            _skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (_skillManager == null)
            Debug.LogWarning("[PlayerSkillHudUI] PlayerSkillManager not found. HUD will show NOT FOUND.");
    }

    private void OnGUI()
    {
        if (!showHud) return;

        EnsureStyles();

        float x = Screen.width  - panelSize.x - rightOffset;
        float y = Screen.height - panelSize.y - bottomOffset;

        GUI.Box(new Rect(x, y, panelSize.x, panelSize.y), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 6f, y + 6f, panelSize.x - 12f, panelSize.y - 12f));

        GUILayout.Label($"{skillName}  [{keyLabel}]", _titleStyle);

        var state = _skillManager != null ? _skillManager.GetStateBySkillId(skillId) : null;

        if (state == null)
        {
            GUILayout.Label("Status: NOT FOUND", _infoStyle);
        }
        else if (state.IsActive)
        {
            GUILayout.Label("Status: ACTIVE", _statusActiveStyle);
            GUILayout.Label($"Duration: {state.ActiveRemainingTime:F2} s", _infoStyle);
            if (state.SkillData != null)
                GUILayout.Label($"Damage Taken: {state.SkillData.DamageTakenMultiplier:P0}", _infoStyle);
        }
        else if (state.IsOnCooldown)
        {
            GUILayout.Label("Status: COOLDOWN", _statusCooldownStyle);
            GUILayout.Label($"Cooldown: {state.CooldownRemainingTime:F2} s", _infoStyle);
            if (state.SkillData != null)
                GUILayout.Label($"Damage Taken: {state.SkillData.DamageTakenMultiplier:P0}", _infoStyle);
        }
        else
        {
            GUILayout.Label("Status: READY", _statusReadyStyle);
            if (state.SkillData != null)
                GUILayout.Label($"Damage Taken: {state.SkillData.DamageTakenMultiplier:P0}", _infoStyle);
        }

        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, fontSize = 13,
            normal    = { textColor = Color.white }
        };
        _statusReadyStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, fontSize = 12,
            normal    = { textColor = new Color(0.4f, 1f, 0.4f) }
        };
        _statusActiveStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, fontSize = 12,
            normal    = { textColor = new Color(0.4f, 0.8f, 1f) }
        };
        _statusCooldownStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, fontSize = 12,
            normal    = { textColor = new Color(1f, 0.75f, 0.25f) }
        };
        _infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal   = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
    }
}
