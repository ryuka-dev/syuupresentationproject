using UnityEngine;

/// <summary>
/// 玩家技能战斗 HUD — OnGUI 版。
/// 显示 PlayerSkillManager.LastPressedSkillState（最后一次按下的技能）的详细信息。
/// showHud = true にすると右下に表示される。デフォルトは true（Canvas UI とは別位置）。
/// 不修改技能状态，只读取显示。
/// </summary>
public class PlayerSkillHudUI : MonoBehaviour
{
    [Header("HUD 显示控制")]
    [SerializeField] private bool showHud = true;

    [Header("HUD 位置 / 尺寸")]
    [SerializeField] private Vector2 panelSize    = new Vector2(240f, 160f);
    [SerializeField] private float   bottomOffset = 280f;   // Canvas 技能栏より上に表示
    [SerializeField] private float   rightOffset  = 40f;

    private PlayerSkillManager _skillManager;

    private GUIStyle _titleStyle;
    private GUIStyle _statusReadyStyle;
    private GUIStyle _statusActiveStyle;
    private GUIStyle _statusCooldownStyle;
    private GUIStyle _infoStyle;
    private GUIStyle _headerStyle;
    private bool     _stylesInitialized;

    private void Awake()
    {
        _skillManager = GetComponent<PlayerSkillManager>();
        if (_skillManager == null)
            _skillManager = FindFirstObjectByType<PlayerSkillManager>();
        if (_skillManager == null)
            Debug.LogWarning("[PlayerSkillHudUI] PlayerSkillManager not found.");
    }

    private void OnGUI()
    {
        if (!showHud) return;

        EnsureStyles();

        float x = Screen.width  - panelSize.x - rightOffset;
        float y = Screen.height - panelSize.y - bottomOffset;

        GUI.Box(new Rect(x, y, panelSize.x, panelSize.y), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 6f, y + 6f, panelSize.x - 12f, panelSize.y - 12f));

        GUILayout.Label("Last Pressed Skill", _headerStyle);

        if (_skillManager == null)
        {
            GUILayout.Label("PlayerSkillManager not found", _infoStyle);
            GUILayout.EndArea();
            return;
        }

        var state = _skillManager.LastPressedSkillState;

        if (state == null)
        {
            GUILayout.Label("No skill pressed yet", _infoStyle);
            GUILayout.EndArea();
            return;
        }

        var data = state.SkillData;
        string name_    = data != null ? data.SkillName             : "Unknown";
        string key_     = data != null ? data.KeyLabel              : "?";
        string id_      = data != null ? data.SkillId               : "?";
        string effect_  = data != null ? data.EffectType.ToString() : "?";

        GUILayout.Label($"{name_}  [{key_}]", _titleStyle);
        GUILayout.Label($"Id: {id_}", _infoStyle);
        GUILayout.Label($"Effect: {effect_}", _infoStyle);

        if (state.IsActive)
        {
            GUILayout.Label("Status: ACTIVE", _statusActiveStyle);
            GUILayout.Label($"Active Remaining: {state.ActiveRemainingTime:F2} s", _infoStyle);
        }
        else if (state.IsOnCooldown)
        {
            GUILayout.Label("Status: COOLDOWN", _statusCooldownStyle);
            GUILayout.Label($"Cooldown Remaining: {state.CooldownRemainingTime:F2} s", _infoStyle);
        }
        else
        {
            GUILayout.Label("Status: READY", _statusReadyStyle);
        }

        if (data != null && data.EffectType == PlayerSkillEffectType.DamageReduction)
            GUILayout.Label($"Damage Taken: {data.DamageTakenMultiplier * 100f:F0}%", _infoStyle);

        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, fontSize = 11,
            normal    = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };
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
