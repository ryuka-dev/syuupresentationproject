using System.Collections;
using UnityEngine;

/// <summary>
/// 守護反击 / Radiant Riposte 命中時の画面フィードバック v0.2
///
/// 変更内容:
///   - カメラシェイク: RPGCameraController.shakeOffset を加算方式に変更。
///     LateUpdate が毎フレーム position を上書きしてもシェイクが反映される。
///   - 全画面フラッシュ: 廃止（ユーザー要望により）。
///   - 左手局部光効: Wrist_L ボーン付近に Point Light を短時間生成・淡出。
///     左手ボーン未バインド時は playerTransform 左前方へ fallback。
///
/// 使用: SimpleScreenFeedback.TriggerCounterFeedback(transform, leftHandAnchor);
/// </summary>
public class SimpleScreenFeedback : MonoBehaviour
{
    [Header("カメラシェイク")]
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeStrength = 0.08f;

    [Header("左手局部光効")]
    [SerializeField] private float lightRange     = 1.4f;
    [SerializeField] private float lightIntensity = 0.9f;
    [SerializeField] private float lightDuration  = 0.20f;
    [SerializeField] private Color lightColor     = new Color(1f, 0.95f, 0.6f);  // 淡黄白

    // ─── ランタイム参照 ──────────────────────────────────────────
    private RPGCameraController _camCtrl;
    private bool                _initialized;

    // ─── Singleton ───────────────────────────────────────────────
    private static SimpleScreenFeedback _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    // ─── 公開 API ────────────────────────────────────────────────

    /// <summary>
    /// 守護反击命中時に呼び出す。
    /// playerRoot: プレイヤー Transform（fallback 用）
    /// leftHandAnchor: 左手骨骼 Transform（null の場合は fallback）
    /// </summary>
    public void TriggerFeedback(UnityEngine.Transform playerRoot, UnityEngine.Transform leftHandAnchor)
    {
        EnsureInitialized();
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine());
        StartCoroutine(LocalLightRoutine(playerRoot, leftHandAnchor));
    }

    // ─── 静的アクセスヘルパー ────────────────────────────────────

    /// <summary>
    /// 外部から呼び出す静的ヘルパー。
    /// playerRoot: プレイヤー Transform / leftHandAnchor: 左手（null 可）
    /// </summary>
    public static void TriggerCounterFeedback(UnityEngine.Transform playerRoot, UnityEngine.Transform leftHandAnchor = null)
    {
        try
        {
            if (_instance == null)
            {
                var go = new GameObject("_SimpleScreenFeedback");
                _instance = go.AddComponent<SimpleScreenFeedback>();
            }
            _instance.TriggerFeedback(playerRoot, leftHandAnchor);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[SimpleScreenFeedback] フィードバック失敗（戦闘結算に影響なし）: " + ex.Message);
        }
    }

    // ─── 後方互換（旧 Trigger() 呼び出し用）────────────────────
    public static void Trigger()
    {
        TriggerCounterFeedback(null, null);
    }

    // ─── 初期化 ──────────────────────────────────────────────────

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _camCtrl = UnityEngine.Object.FindFirstObjectByType<RPGCameraController>();
        _initialized = true;
    }

    // ─── シェイク（RPGCameraController.shakeOffset 加算方式）────
    private IEnumerator ShakeRoutine()
    {
        if (_camCtrl == null) yield break;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float t = 1f - elapsed / shakeDuration;
            _camCtrl.shakeOffset = new Vector3(
                Random.Range(-shakeStrength, shakeStrength) * t,
                Random.Range(-shakeStrength, shakeStrength) * t,
                0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _camCtrl.shakeOffset = Vector3.zero;
    }

    // ─── 局部 Point Light ────────────────────────────────────────
    private IEnumerator LocalLightRoutine(UnityEngine.Transform playerRoot, UnityEngine.Transform leftHandAnchor)
    {
        // 生成位置の決定
        Vector3 spawnPos;
        string anchorLabel;
        if (leftHandAnchor != null)
        {
            spawnPos   = leftHandAnchor.position;
            anchorLabel = leftHandAnchor.name;
        }
        else if (playerRoot != null)
        {
            // Fallback: プレイヤー左前方・腰高さ付近
            spawnPos = playerRoot.position
                     + playerRoot.right   * -0.35f
                     + playerRoot.forward *  0.35f
                     + Vector3.up         *  1.0f;
            anchorLabel = "player_left_front_fallback";
        }
        else
        {
            yield break;
        }

        // Point Light 生成
        var lightGO = new GameObject("_RiposteLightFX");
        lightGO.transform.position = spawnPos;

        var light = lightGO.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = lightColor;
        light.range     = lightRange;
        light.intensity = lightIntensity;
        light.shadows   = LightShadows.None;

        // 淡出
        float elapsed = 0f;
        while (elapsed < lightDuration)
        {
            light.intensity = lightIntensity * (1f - elapsed / lightDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(lightGO);
    }
}
