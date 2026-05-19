using UnityEngine;

/// <summary>
/// Hikari サポートコントローラー（最小版・Step 1）
///
/// 担当技能：
///   微光治愈 / Light Mend  — 自動小回復
///   プレイヤー HP が一定比率を下回ると、一定クールダウンで自動的に Heal() を呼び出す。
///
/// 飘字表示は HealthComponent.OnHealed → DamageNumberSpawner の既存イベントチェーンに委任。
/// 本スクリプト内では直接 currentHealth を操作しない。
/// </summary>
public class HikariSupportController : MonoBehaviour
{
    // ─── Inspector フィールド ─────────────────────────────────────

    [Header("プレイヤー参照")]
    [Tooltip("手動でアサインしない場合、Start() で PlayerTag から自動検索します。")]
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private string playerTag = "Player";

    [Header("微光治愈 / Light Mend")]
    [Tooltip("false にすると Light Mend を完全に無効化します。")]
    [SerializeField] private bool enableLightMend = true;

    [Tooltip("この比率を下回ったときに治療を発動します（0〜1）。デフォルト 0.8 = HP 80% 未満で発動。")]
    [SerializeField, Range(0f, 1f)] private float lightMendHpThreshold = 0.8f;

    [Tooltip("1 回あたりの回復量（実際の回復量は上限クリップされます）。")]
    [SerializeField] private float lightMendHealAmount = 15f;

    [Tooltip("Light Mend の最短発動間隔（秒）。")]
    [SerializeField] private float lightMendCooldown = 5f;

    [Header("デバッグ")]
    [SerializeField] private bool logDebugMessages = true;

    // ─── 実行時フィールド ─────────────────────────────────────────

    private float _nextLightMendTime;

    // ─── Unity ライフサイクル ──────────────────────────────────────

    private void Start()
    {
        if (playerHealth != null) return;   // Inspector でアサイン済みならそのまま使う

        var playerGO = GameObject.FindGameObjectWithTag(playerTag);
        if (playerGO == null)
        {
            Debug.LogWarning($"[HikariSupport] Tag '{playerTag}' のオブジェクトが見つかりません。" +
                             " playerHealth を Inspector でアサインするか、Player タグを確認してください。");
            return;
        }

        playerHealth = playerGO.GetComponent<HealthComponent>();
        if (playerHealth == null)
            Debug.LogWarning($"[HikariSupport] '{playerGO.name}' に HealthComponent が見つかりません。");
        else if (logDebugMessages)
            Debug.Log($"[HikariSupport] playerHealth を自動解決しました: {playerGO.name}");
    }

    private void Update()
    {
        TryLightMend();
    }

    // ─── 微光治愈 / Light Mend ────────────────────────────────────

    /// <summary>
    /// 毎フレーム呼び出される Light Mend の試行ロジック。
    /// 条件を満たしていれば playerHealth.Heal() を呼び出す。
    /// </summary>
    private void TryLightMend()
    {
        if (!enableLightMend)  return;
        if (playerHealth == null) return;
        if (playerHealth.IsDead)  return;
        if (Time.time < _nextLightMendTime) return;

        // HP 比率チェック（maxHealth が 0 以下の場合は除算ガード）
        float hpRatio = playerHealth.maxHealth > 0f
            ? playerHealth.currentHealth / playerHealth.maxHealth
            : 1f;

        if (hpRatio >= lightMendHpThreshold) return;   // 閾値以上なら治療不要

        // 治療実行
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Light Mend 発動 — HP {playerHealth.currentHealth:F1}/{playerHealth.maxHealth:F1}" +
                      $" ({hpRatio * 100f:F1}%) → Heal({lightMendHealAmount})");

        playerHealth.Heal(lightMendHealAmount, transform);
        _nextLightMendTime = Time.time + lightMendCooldown;
    }
}
