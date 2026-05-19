using UnityEngine;

/// <summary>
/// Hikari サポートコントローラー（最小版・Step 2）
///
/// 担当技能：
///   微光治愈 / Light Mend       — 自動小回復（HP 80% 未満で発動、冷却 5 秒）
///   紧急祈愿 / Emergency Prayer  — 自動救急大回復（HP 35% 未満で発動、冷却 25 秒）
///
/// 優先ルール：Emergency Prayer > Light Mend
///   - EP 発動フレームは Light Mend をスキップ。
///   - EP 冷却中かつ HP < 80% なら Light Mend が補完する。
///
/// 飘字表示は HealthComponent.OnHealed → DamageNumberSpawner の既存イベントチェーンに委任。
/// 本スクリプト内では直接 currentHealth を操作しない。
/// </summary>
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

    [Header("紧急祈愿 / Emergency Prayer")]
    [Tooltip("false にすると Emergency Prayer を完全に無効化します。")]
    [SerializeField] private bool enableEmergencyPrayer = true;

    [Tooltip("この比率を下回ったときに EP を発動します（0〜1）。デフォルト 0.35 = HP 35% 未満で発動。")]
    [SerializeField, Range(0f, 1f)] private float emergencyPrayerHpThreshold = 0.35f;

    [Tooltip("1 回あたりの回復量（実際の回復量は上限クリップされます）。")]
    [SerializeField] private float emergencyPrayerHealAmount = 45f;

    [Tooltip("Emergency Prayer の最短発動間隔（秒）。")]
    [SerializeField] private float emergencyPrayerCooldown = 25f;

    [Header("Burden / 光負荷")]
    [Tooltip("Hikari の光負荷最大値。")]
    [SerializeField] private float maxBurden = 100f;

    [Tooltip("現在の光負荷。Inspector で確認可能。")]
    [SerializeField] private float currentBurden = 0f;

    [Tooltip("Light Mend 発動一回あたりの光負荷追加量。")]
    [SerializeField] private float lightMendBurdenGain = 5f;

    [Tooltip("Emergency Prayer 発動一回あたりの光負荷追加量。")]
    [SerializeField] private float emergencyPrayerBurdenGain = 25f;

    [Tooltip("光負荷の自然回復度（/秒）。")]
    [SerializeField] private float burdenRecoveryPerSecond = 1f;

    [Tooltip("false にすると光負荷の自然回復を停止します。")]
    [SerializeField] private bool enableBurdenRecovery = true;

    

[Header("デバッグ")]
    [SerializeField] private bool logDebugMessages = true;

    // ─── 実行時フィールド ─────────────────────────────────────────

    private float _nextLightMendTime;

    // ─── 公開読み取り専用プロパティ ──────────────────────────────────────

    public float CurrentBurden => currentBurden;
    public float MaxBurden     => maxBurden;
    public float BurdenRatio   => maxBurden > 0f ? currentBurden / maxBurden : 0f;
    public bool  IsBurdenMaxed => currentBurden >= maxBurden;

    private float _nextEmergencyPrayerTime;


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
        if (playerHealth == null) return;
        if (playerHealth.IsDead)  return;

        // 光負荷自然回復（毎フレーム処理）
        RecoverBurdenOverTime();

        float hpRatio = GetPlayerHpRatio();

        // Emergency Prayer を優先チェック
        if (hpRatio < emergencyPrayerHpThreshold)
        {
            if (TryUseEmergencyPrayer()) return;
        }

        // Light Mend をフォールバックチェック
        if (hpRatio < lightMendHpThreshold)
        {
            TryUseLightMend();
        }
    }

    // ─── 微光治愈 / Light Mend ────────────────────────────────────

    /// <summary>
    /// 毎フレーム呼び出される Light Mend の試行ロジック。
    /// 条件を満たしていれば playerHealth.Heal() を呼び出す。
    /// </summary>
private bool TryUseLightMend()
    {
        if (!enableLightMend) return false;
        if (Time.time < _nextLightMendTime) return false;

        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Light Mend 発動 — HP {playerHealth.currentHealth:F1}/{playerHealth.maxHealth:F1}" +
                      $" ({GetPlayerHpRatio() * 100f:F1}%) → Heal({lightMendHealAmount}) | Burden {currentBurden:F1}+{lightMendBurdenGain}");

        playerHealth.Heal(lightMendHealAmount, transform);
        _nextLightMendTime = Time.time + lightMendCooldown;
        AddBurden(lightMendBurdenGain, "Light Mend");
        return true;
    }

// ─── 紧急祈愿 / Emergency Prayer ──────────────────────────────

    /// <summary>
    /// Emergency Prayer の試行。発動できた場合は true を返す。
    /// </summary>
private bool TryUseEmergencyPrayer()
    {
        if (!enableEmergencyPrayer) return false;
        if (Time.time < _nextEmergencyPrayerTime) return false;

        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Emergency Prayer 発動 — HP {playerHealth.currentHealth:F1}/{playerHealth.maxHealth:F1}" +
                      $" ({GetPlayerHpRatio() * 100f:F1}%) → Heal({emergencyPrayerHealAmount}) | Burden {currentBurden:F1}+{emergencyPrayerBurdenGain}");

        playerHealth.Heal(emergencyPrayerHealAmount, transform);
        _nextEmergencyPrayerTime = Time.time + emergencyPrayerCooldown;
        AddBurden(emergencyPrayerBurdenGain, "Emergency Prayer");
        return true;
    }

    // ─── ヘルパー ─────────────────────────────────────────────────

    /// <summary>
    /// プレイヤーの現在 HP 比率を返す。maxHealth が 0 以下の場合は 1f（安全値）を返す。
    /// </summary>
    private float GetPlayerHpRatio()
    {
        return playerHealth.maxHealth > 0f
            ? playerHealth.currentHealth / playerHealth.maxHealth
            : 1f;
    }

// ─── Burden 光負荷 ────────────────────────────────────────────

    /// <summary>
    /// 光負荷を追加する。友化平とクリップする。
    /// </summary>
    private void AddBurden(float amount, string source)
    {
        if (amount <= 0f) return;
        float before = currentBurden;
        currentBurden = Mathf.Clamp(currentBurden + amount, 0f, maxBurden);
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Burden [{source}] {before:F1} → {currentBurden:F1} / {maxBurden:F1}");
    }

    /// <summary>
    /// 毎フレーム、光負荷を自然回復させる。
    /// </summary>
    private void RecoverBurdenOverTime()
    {
        if (!enableBurdenRecovery) return;
        if (burdenRecoveryPerSecond <= 0f) return;
        if (currentBurden <= 0f) return;
        currentBurden = Mathf.Max(0f, currentBurden - burdenRecoveryPerSecond * Time.deltaTime);
    }

    private void OnValidate()
    {
        maxBurden                  = Mathf.Max(1f,  maxBurden);
        currentBurden              = Mathf.Clamp(currentBurden, 0f, maxBurden);
        lightMendBurdenGain        = Mathf.Max(0f,  lightMendBurdenGain);
        emergencyPrayerBurdenGain  = Mathf.Max(0f,  emergencyPrayerBurdenGain);
        burdenRecoveryPerSecond    = Mathf.Max(0f,  burdenRecoveryPerSecond);
        lightMendHealAmount        = Mathf.Max(0f,  lightMendHealAmount);
        emergencyPrayerHealAmount  = Mathf.Max(0f,  emergencyPrayerHealAmount);
        lightMendCooldown          = Mathf.Max(0f,  lightMendCooldown);
        emergencyPrayerCooldown    = Mathf.Max(0f,  emergencyPrayerCooldown);
    }


}
