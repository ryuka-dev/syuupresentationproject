using UnityEngine;

/// <summary>
/// 敵読条プログレス表示（デバッグ用第一版）。
/// WorldHealthBar と同じ OnGUI + WorldToScreenPoint 方式。
///
/// 修正ポイント：
///   GUIStyle は OnGUI 内でのみ有効なため、Start() ではなく EnsureStyles() で
///   遅延初期化する。テクスチャは Texture2D.whiteTexture + GUI.color を使用し
///   毎フレーム生成を回避する。
/// </summary>
public class EnemyCastBarUI : MonoBehaviour
{
    [Header("ワールド空間オフセット（敵の位置からの頭上）")]
    public Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);

    [Header("バーサイズ")]
    public float barWidth  = 110f;
    public float barHeight = 10f;

    // ─── キャッシュ ──────────────────────────────────────────
    private EnemySkillController _skillController;

    // GUIStyle は OnGUI 内でのみ生成可能（Start() では GUI.skin が未準備）
    private GUIStyle _skillNameStyle;
    private GUIStyle _timeStyle;

    // ─── ライフサイクル ─────────────────────────────────────
    void Awake()
    {
        _skillController = GetComponent<EnemySkillController>();
    }

    // ─── OnGUI ──────────────────────────────────────────────
    void OnGUI()
    {
        // ── null ガード ────────────────────────────────────
        if (_skillController == null)
        {
            _skillController = GetComponent<EnemySkillController>();
            if (_skillController == null) return;
        }

        if (!_skillController.IsCasting) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        if (_skillController.CurrentCastDuration <= 0f) return;

        // ── スタイル遅延初期化 ────────────────────────────
        EnsureStyles();

        // ── スクリーン座標変換 ────────────────────────────
        Vector3 screenPos = mainCam.WorldToScreenPoint(transform.position + worldOffset);
        if (screenPos.z < 0f) return;  // カメラ背後は非表示

        float screenX = screenPos.x;
        float screenY = Screen.height - screenPos.y;  // OnGUI は Y 反転
        float halfW   = barWidth * 0.5f;

        // ── 表示テキストの準備 ────────────────────────────
        var   skill    = _skillController.CurrentSkill;
        string skillName = (skill != null && !string.IsNullOrEmpty(skill.DisplayName))
                               ? skill.DisplayName
                               : "Casting";
        float elapsed  = _skillController.CurrentCastElapsed;
        float duration = _skillController.CurrentCastDuration;
        float progress = Mathf.Clamp01(_skillController.CurrentCastProgress);
        string timeText = $"{elapsed:F1} / {duration:F1}s";

        // ── レイアウト ────────────────────────────────────
        const float lineH = 18f;
        const float gap   = 2f;

        float nameY = screenY - lineH - gap - barHeight - gap;
        float barY  = screenY - barHeight - gap;
        float timeY = screenY + gap;

        // ── スキル名 ──────────────────────────────────────
        GUI.Label(new Rect(screenX - halfW, nameY, barWidth, lineH), skillName, _skillNameStyle);

        // ── 背景バー ──────────────────────────────────────
        var prevColor = GUI.color;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        GUI.DrawTexture(new Rect(screenX - halfW, barY, barWidth, barHeight), Texture2D.whiteTexture);

        // ── 進度バー ──────────────────────────────────────
        if (progress > 0f)
        {
            GUI.color = new Color(0.95f, 0.45f, 0.10f, 0.90f);
            GUI.DrawTexture(new Rect(screenX - halfW, barY, barWidth * progress, barHeight), Texture2D.whiteTexture);
        }

        GUI.color = prevColor;  // 色をリセット

        // ── 秒数テキスト ──────────────────────────────────
        GUI.Label(new Rect(screenX - halfW, timeY, barWidth, lineH), timeText, _timeStyle);
    }

    // ─── スタイル遅延初期化 ──────────────────────────────────
    /// <summary>
    /// GUIStyle は OnGUI 内でのみ GUI.skin が有効なため、ここで遅延生成する。
    /// すでに生成済みの場合は何もしない。
    /// </summary>
    private void EnsureStyles()
    {
        if (_skillNameStyle == null)
        {
            _skillNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
            };
            _skillNameStyle.normal.textColor = Color.white;
        }

        if (_timeStyle == null)
        {
            _timeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
                fontStyle = FontStyle.Normal,
            };
            _timeStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        }
    }
}
