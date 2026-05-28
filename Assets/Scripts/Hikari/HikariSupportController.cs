using UnityEngine;

/// <summary>
/// Hikari 支援控制器（原型版）
///
/// 负责技能：
///   微光治愈 / Light Mend       — 自动小治疗（玩家 HP < 80% 触发，冷却 5 秒，+1 BU 光负荷）
///   紧急祈愿 / Emergency Prayer  — 自动救命大治疗（玩家 HP < 35% 触发，冷却 25 秒，+5 BU 光负荷）
///
/// 优先规则：Emergency Prayer > Light Mend
///
/// 光负荷阶段（Burden / 光负荷）：
///   0%~79%   → 稳定导光：正常治疗
///   80%~99%  → 光溢出：可控治疗效率下降，溢光反震 / Overflow Counter 可触发
///   100%     → 导光封锁：Light Mend / Emergency Prayer 停止
///   ≤60%     → 导光恢复：从导光封锁中恢复
///
/// 守护共鸣 / Guard Resonance：
///   玩家 DamageReduction Active + 承受 CastAttack → 光负荷 -10（-2 BU）
///
/// 溢光反震 / Overflow Counter：
///   光溢出状态（80%~99%）下守护共鸣成功触发时，对攻击者造成失控光伤害。
///   导光封锁（100%）时不触发。
///
/// 治疗飘字通过 HealthComponent.OnHealed → DamageNumberSpawner 事件链处理。
/// 本脚本内不直接操作 currentHealth。
/// </summary>
/// <summary>現在の読条技能タイプ。</summary>
public enum HikariCastType { None, LightMend, EmergencyPrayer }

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

    
[Header("光溢出状态 / Light Overflow (80%~99%)")]
    [Tooltip("光负荷比例达到此阈值后进入光溢出状态（0~1）。默认 0.8 = 80% 以上进入光溢出。")]
    [SerializeField, Range(0f, 1f)] private float overburdenThreshold = 0.8f;

    [Tooltip("光溢出状态下，可控治疗效率下降倍率。默认 0.5 = 可控治疗效率降至 50%。")]
    [SerializeField, Range(0f, 1f)] private float overburdenHealingMultiplier = 0.5f;

    [Header("Guard Resonance / 守护共鸣")]
    [Tooltip("玩家在 DamageReduction 技能 Active 期间受伤时，降低 Hikari 光负荷。")]
    [SerializeField] private bool  guardResonanceEnabled        = true;
    [Tooltip("守护共鸣触发时减少的光负荷量。")]
    [SerializeField] private float guardResonanceBurdenReduction = 10f;
    [Tooltip("守护共鸣的最短触发间隔（秒）。")]
    [SerializeField] private float guardResonanceCooldown        = 3f;
    [Tooltip("読条重撃記録と受傷時刻の許容差（秒）。これ以内ならスキル命中とみなす。")]
    [SerializeField] private float guardResonanceSkillHitWindow  = 0.25f;

    [Header("溢光反震 / Overflow Counter")]
    [Tooltip("是否启用溢光反震 / Overflow Counter。")]
    [SerializeField] private bool lightCounterEnabled = true;
    [Tooltip("溢光反震触发的最低光负荷比例。默认 0.8 = 80%。光溢出区间（80%~99%）内触发。")]
    [SerializeField, Range(0f, 1f)] private float lightCounterMinBurdenRatio = 0.8f;
    [Tooltip("Light Counter \u767a\u52d5\u306e\u4e0a\u9650 Burden \u6bd4\u7387\uff08\u672a\u6e80\uff09\u3002\u30c7\u30d5\u30a9\u30eb\u30c8 1.0 = 100%\u672a\u6e80\uff08\u904e\u8f09\u6642\u306f\u767a\u52d5\u3057\u306a\u3044\uff09\u3002")]
    [SerializeField, Range(0f, 1f)] private float lightCounterMaxBurdenRatio = 1.0f;
    [Tooltip("溢光反震对攻击者造成的固定伤害值。")]
    [SerializeField] private float lightCounterDamage = 30f;


    
[Header("导光封锁 / Channel Lockdown (100%)")]
    [Tooltip("光负荷达到此比例时进入导光封锁（0~1）。默认 1.0 = 100% 时进入导光封锁，可控治疗停止。")]
    [SerializeField, Range(0f, 1f)] private float overloadThreshold = 1f;

    [Tooltip("导光恢复阈值：光负荷下降到此比例以下时，从导光封锁中恢复治疗能力。默认 0.6 = 60% 以下导光恢复。")]
    [SerializeField, Range(0f, 1f)] private float overloadRecoveryThreshold = 0.6f;


    


    [Header("読条 / Heal Cast")]
    [Tooltip("治療読条の時間（秒）。両技能に共用。")]
    [SerializeField] private float healCastDuration = 1.5f;

    // ─── 読条実行時フィールド ──────────────────────────────────────
    private HikariCastType _castType = HikariCastType.None;
    private float          _castStartTime;

    [Header("デバッグ")]
    [SerializeField] private bool logDebugMessages = true;

    // ─── 実行時フィールド ─────────────────────────────────────────

    private float _nextLightMendTime;

    // ─── 公開読み取り専用プロパティ ──────────────────────────────────────

    public float CurrentBurden => currentBurden;
    public float MaxBurden     => maxBurden;
    public float BurdenRatio   => maxBurden > 0f ? currentBurden / maxBurden : 0f;
    public bool  IsBurdenMaxed => currentBurden >= maxBurden;
    public bool  IsOverloaded              => _isOverloaded;
    public float OverloadThreshold         => overloadThreshold;
    public float OverloadRecoveryThreshold => overloadRecoveryThreshold;
    public bool  CanUseHealing             => !_isOverloaded;
    public bool  GuardResonanceEnabled          => guardResonanceEnabled;
    public float GuardResonanceBurdenReduction  => guardResonanceBurdenReduction;
    public float GuardResonanceCooldown         => guardResonanceCooldown;
    public float GuardResonanceCooldownRemaining => Mathf.Max(0f, _nextGuardResonanceTime - Time.time);

    /// <summary>
    /// 守护共鸣 / Guard Resonance の成功時に発火するイベント。
    /// 引数は本次 CastAttack を実行した攻撃者の Transform（null の場合あり）。
    /// 外部コントローラー（PlayerGuardCounterController など）が購読して反撃機会を管理する。
    /// </summary>
    public event System.Action<Transform, bool> OnGuardResonanceTriggered;

    
public bool  IsBurdenRecoveryEnabled  => enableBurdenRecovery;
    public float BurdenRecoveryPerSecond  => burdenRecoveryPerSecond;

    public bool  IsOverburdened             => BurdenRatio >= overburdenThreshold;
    public float OverburdenThreshold        => overburdenThreshold;
    public float OverburdenHealingMultiplier => overburdenHealingMultiplier;

    // ─── 読条公開プロパティ（UI 用）────────────────────────────────
    /// <summary>現在 Hikari が読条中か。</summary>
    public bool  IsCasting            => _castType != HikariCastType.None;
    /// <summary>読条経過時間（秒）。読条中でなければ 0。</summary>
    public float CurrentCastTime      => IsCasting ? Mathf.Min(Time.time - _castStartTime, healCastDuration) : 0f;
    /// <summary>読条全体の時間（秒）。</summary>
    public float CurrentCastDuration  => healCastDuration;
    /// <summary>読条進捗比率 0~1。読条中でなければ 0。</summary>
    public float CastRatio            => healCastDuration > 0f ? Mathf.Clamp01(CurrentCastTime / healCastDuration) : 0f;

    /// <summary>
    /// 正式 UI 用：現在の読条動作を表すラベル（UI 表示用）。
    /// 読条なし → "--"  / LightMend → "治疗读条中"  / EmergencyPrayer → "紧急治疗读条中"
    /// </summary>
    public string CurrentActionLabel
    {
        get
        {
            return _castType switch
            {
                HikariCastType.LightMend       => "Casting Heal",
                HikariCastType.EmergencyPrayer => "Casting Emergency",
                _                              => "--",
            };
        }
    }

    /// <summary>
    /// 正式 UI 用：現在の光負荷フェーズを表すラベル。
    /// IsOverloaded → "导光封锁"  / IsOverburdened → "光溢出"  / それ以外 → "待机"
    /// </summary>
    public string CurrentStateLabel
    {
        get
        {
            if (IsOverloaded)   return "Locked";
            if (IsOverburdened) return "Overflow";
            return "Idle";
        }
    }



    private float _nextEmergencyPrayerTime;
    private bool _isOverloaded;
    private float               _nextGuardResonanceTime;
    private PlayerSkillManager  _playerSkillManager;
    private bool                _subscribedToPlayerDamaged;




    // ─── Unity ライフサイクル ──────────────────────────────────────

private void Start()
    {
        if (playerHealth == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGO == null)
            {
                Debug.LogWarning($"[HikariSupport] Tag '{playerTag}' のオブジェクトが見つかりません。" +
                                 " playerHealth を Inspector でアサインするか、Player タグを確認してください。");
                return;
            }
            playerHealth = playerGO.GetComponent<HealthComponent>();
            if (playerHealth == null)
            {
                Debug.LogWarning($"[HikariSupport] '{playerGO.name}' に HealthComponent が見つかりません。");
                return;
            }
            if (logDebugMessages)
                Debug.Log($"[HikariSupport] playerHealth を自動解決しました: {playerGO.name}");

            _playerSkillManager = playerGO.GetComponent<PlayerSkillManager>();
            if (_playerSkillManager == null)
                Debug.LogWarning("[HikariSupport] PlayerSkillManager が Player に見つかりません。Guard Resonance は機能しません。");
        }
        else
        {
            // Inspector で手動アサイン済みの場合も PlayerSkillManager を追尾
            _playerSkillManager = playerHealth.GetComponent<PlayerSkillManager>();
        }

        SubscribeToPlayerDamaged();
    }

private void OnDestroy()
    {
        UnsubscribeFromPlayerDamaged();
    }

    private void SubscribeToPlayerDamaged()
    {
        if (_subscribedToPlayerDamaged || playerHealth == null) return;
        playerHealth.OnDamaged    += HandlePlayerDamaged;
        _subscribedToPlayerDamaged = true;
    }

    private void UnsubscribeFromPlayerDamaged()
    {
        if (!_subscribedToPlayerDamaged || playerHealth == null) return;
        playerHealth.OnDamaged    -= HandlePlayerDamaged;
        _subscribedToPlayerDamaged = false;
    }


private void Update()
    {
        if (playerHealth == null) return;
        if (playerHealth.IsDead)  return;

        RecoverBurdenOverTime();
        UpdateOverloadState();
        TickCast();   // 読条タイマー進行（UpdateOverloadState後、CanUseHealingチェック前）

        if (!CanUseHealing) return;

        float hpRatio = GetPlayerHpRatio();

        if (hpRatio < emergencyPrayerHpThreshold)
        {
            if (TryUseEmergencyPrayer()) return;
        }

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
        if (!enableLightMend)             return false;
        if (_isOverloaded)                return false;
        if (IsCasting)                    return false;
        if (Time.time < _nextLightMendTime) return false;

        _castType      = HikariCastType.LightMend;
        _castStartTime = Time.time;
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] 微光治愈 読条開始 | Burden {currentBurden:F1}/{maxBurden:F1}");
        return true;
    }

    /// <summary>微光治愈 読条完了時に実行する本体処理。</summary>
    private void FinishLightMend()
    {
        if (_isOverloaded || playerHealth == null || playerHealth.IsDead)
        {
            if (logDebugMessages) Debug.Log("[HikariSupport] 微光治愈 読条完了キャンセル");
            return;
        }
        float finalHeal = ApplyBurdenHealingModifier(lightMendHealAmount);
        playerHealth.Heal(finalHeal, transform);
        _nextLightMendTime = Time.time + lightMendCooldown;
        AddBurden(lightMendBurdenGain, "Light Mend");
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] 微光治愈 完了 — heal={finalHeal:F1} | Burden {currentBurden:F1}/{maxBurden:F1}");
    }

// ─── 紧急祈愿 / Emergency Prayer ──────────────────────────────

    /// <summary>
    /// Emergency Prayer の試行。発動できた場合は true を返す。
    /// </summary>
private bool TryUseEmergencyPrayer()
    {
        if (!enableEmergencyPrayer)              return false;
        if (_isOverloaded)                       return false;
        if (IsCasting)                           return false;
        if (Time.time < _nextEmergencyPrayerTime) return false;

        _castType      = HikariCastType.EmergencyPrayer;
        _castStartTime = Time.time;
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] 紧急祈愿 読条開始 | Burden {currentBurden:F1}/{maxBurden:F1}");
        return true;
    }

    /// <summary>紧急祈愿 読条完了時に実行する本体処理。</summary>
    private void FinishEmergencyPrayer()
    {
        if (_isOverloaded || playerHealth == null || playerHealth.IsDead)
        {
            if (logDebugMessages) Debug.Log("[HikariSupport] 紧急祈愿 読条完了キャンセル");
            return;
        }
        float finalHeal = ApplyBurdenHealingModifier(emergencyPrayerHealAmount);
        playerHealth.Heal(finalHeal, transform);
        _nextEmergencyPrayerTime = Time.time + emergencyPrayerCooldown;
        AddBurden(emergencyPrayerBurdenGain, "Emergency Prayer");
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] 紧急祈愿 完了 — heal={finalHeal:F1} | Burden {currentBurden:F1}/{maxBurden:F1}");
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

/// <summary>
    /// Burden 状態に応じて治療量に倍率を適用する。
    /// </summary>
    private float ApplyBurdenHealingModifier(float baseHealAmount)
    {
        if (!IsOverburdened) return baseHealAmount;
        return Mathf.Max(0f, baseHealAmount * overburdenHealingMultiplier);
    }


// ─── Burden 光負荷 ────────────────────────────────────────────

    /// <summary>
    /// 光負荷を追加する。友化平とクリップする。
    /// </summary>
private void AddBurden(float amount, string source)
    {
        if (amount <= 0f) return;
        float before  = currentBurden;
        currentBurden = Mathf.Clamp(currentBurden + amount, 0f, maxBurden);
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Burden [{source}] {before:F1} → {currentBurden:F1} / {maxBurden:F1}");
        UpdateOverloadState();
    }

// ─── Debug 専用 API ──────────────────────────────────────────

    /// <summary>Debug 用：光負荷を外部から追加する。治療は発動しない。</summary>
    public void DebugAddBurden(float amount)
    {
        AddBurden(amount, "Debug");
    }

    /// <summary>Debug 用：光負荷をゼロにリセットする。治療冷却には影響しない。</summary>
public void DebugResetBurden()
    {
        currentBurden = 0f;
        if (logDebugMessages)
            Debug.Log("[HikariSupport] Burden reset by Debug.");
        UpdateOverloadState();
    }

/// <summary>Debug用：光負荷自然回復の ON/OFF を外部から設定する。</summary>
    public void DebugSetBurdenRecoveryEnabled(bool enabled)
    {
        enableBurdenRecovery = enabled;
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Burden recovery set to: {enableBurdenRecovery}");
    }

    /// <summary>Debug用：光負荷自然回復をトグルする。</summary>
    public void DebugToggleBurdenRecovery()
    {
        DebugSetBurdenRecoveryEnabled(!enableBurdenRecovery);
    }



// ─── 読条タイク ─────────────────────────────────────────────

    /// <summary>
    /// 毎フレーム呼び出し：読条タイマーを進め、完了時に治療を実行する。
    /// 導光封锁・目標無効・プレイヤー死亡時は読条をキャンセルする。
    /// </summary>
    private void TickCast()
    {
        if (_castType == HikariCastType.None) return;

        // キャンセル条件：導光封锁 / 目標無効 / プレイヤー死亡
        if (_isOverloaded || playerHealth == null || playerHealth.IsDead)
        {
            if (logDebugMessages)
                Debug.Log($"[HikariSupport] 読条キャンセル ({_castType}) — 封锁 or 目標無効");
            _castType = HikariCastType.None;
            return;
        }

        // 読条完了チェック
        if (Time.time - _castStartTime >= healCastDuration)
        {
            var finishedType = _castType;
            _castType = HikariCastType.None;  // 先にクリア
            if      (finishedType == HikariCastType.LightMend)       FinishLightMend();
            else if (finishedType == HikariCastType.EmergencyPrayer) FinishEmergencyPrayer();
        }
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

/// <summary>
    /// 過载状態を更新する。AddBurden / RecoverBurdenOverTime / DebugResetBurden 後に呼び出す。
    /// </summary>
    private void UpdateOverloadState()
    {
        if (!_isOverloaded && BurdenRatio >= overloadThreshold)
        {
            _isOverloaded = true;
            if (logDebugMessages)
                Debug.Log("[HikariSupport] 进入导光封锁 — 可控治疗停止（Light Mend / Emergency Prayer 不触发）。");
        }
        else if (_isOverloaded && BurdenRatio <= overloadRecoveryThreshold)
        {
            _isOverloaded = false;
            if (logDebugMessages)
                Debug.Log("[HikariSupport] 导光恢复 — 光负荷降至 60% 以下，可控治疗恢复。");
        }
    }

// ─── Guard Resonance / 守护共鸣 ────────────────────────────────

private void HandlePlayerDamaged(float damage, Transform attacker)
    {
        TryTriggerGuardResonance(attacker);
    }

private bool TryTriggerGuardResonance(Transform attacker)
    {
        if (!guardResonanceEnabled)               return false;
        if (playerHealth == null)                  return false;
        if (playerHealth.IsDead)                   return false;
        if (Time.time < _nextGuardResonanceTime)   return false;
        if (!HasActiveDamageReductionSkill())      return false;
        if (!IsGuardResonanceTriggerHit(attacker)) return false;

        // Light Counter 判定は Burden 減少前に行う
        bool shouldLightCounter = ShouldTriggerLightCounter();

        ReduceBurden(guardResonanceBurdenReduction, "Guard Resonance");
        _nextGuardResonanceTime = Time.time + guardResonanceCooldown;

        if (logDebugMessages)
            Debug.Log("[HikariSupport] 守护共鸣 / Guard Resonance 触发 — 光负荷减少。");

        bool grantsGuardCounter = HasCounterGrantingDamageReductionSkill();
        OnGuardResonanceTriggered?.Invoke(attacker, grantsGuardCounter);

        if (shouldLightCounter)
            TryTriggerLightCounter(attacker);

        return true;
    }

/// <summary>
    /// Light Counter 発動条件判定。Burden 80%~99% 区間のみ true。
    /// </summary>
    private bool ShouldTriggerLightCounter()
    {
        if (!lightCounterEnabled) return false;
        if (maxBurden <= 0f) return false;
        float burdenRatio = currentBurden / maxBurden;
        if (burdenRatio < lightCounterMinBurdenRatio) return false;
        if (burdenRatio >= lightCounterMaxBurdenRatio) return false;
        return true;
    }

    /// <summary>
    /// Light Counter 実行。Guard Resonance 成功後に呼び出す。
    /// </summary>
    private void TryTriggerLightCounter(Transform attacker)
    {
        if (attacker == null) return;
        HealthComponent enemyHealth = attacker.GetComponent<HealthComponent>();
        if (enemyHealth == null)
            enemyHealth = attacker.GetComponentInParent<HealthComponent>();
        if (enemyHealth == null) return;
        if (enemyHealth.IsDead) return;

        float burdenRatioBeforeReduction = currentBurden / maxBurden;
        enemyHealth.TakeDamage(lightCounterDamage, transform);
        Debug.Log($"[Hikari] 溢光反震 / Overflow Counter 触发！对 {enemyHealth.name} 造成 {lightCounterDamage} 点失控光伤害 | 光负荷（触发前）：{burdenRatioBeforeReduction * 100f:F1}%");
    }


    /// <summary>
    /// 次の伤害が Guard Resonance トリガー条件を満たすか。
    /// 攻撃者の EnemySkillController.最近ダメージスキルが CastAttack 型であり、
    /// 時間窓内の履歴であれば true。
    /// </summary>
    private bool IsGuardResonanceTriggerHit(Transform attacker)
    {
        if (attacker == null) return false;

        var skillCtrl = attacker.GetComponentInParent<EnemySkillController>();
        if (skillCtrl == null) return false;

        var lastSkill = skillCtrl.LastDamageSkillData;
        if (lastSkill == null) return false;

        // 時間窓チェック: 古いスキル記録による誤発動を防ぎ゙る
        if (Time.time - skillCtrl.LastDamageSkillTime > guardResonanceSkillHitWindow) return false;

        // CastAttack 型のスキルのみ対応
        return lastSkill.SkillType == EnemySkillType.CastAttack;
    }

    /// <summary>
    /// 光負荷を減少させて UpdateOverloadState を呼び出す。
    /// </summary>
    private void ReduceBurden(float amount, string source)
    {
        if (amount <= 0f) return;
        float oldBurden = currentBurden;
        currentBurden   = Mathf.Max(0f, currentBurden - amount);
        UpdateOverloadState();
        if (logDebugMessages)
            Debug.Log($"[HikariSupport] Burden reduced [{source}] {oldBurden:F1} → {currentBurden:F1} / {maxBurden:F1}");
    }

    /// <summary>
    /// DamageReduction タイプの技能が 1 つ以上 Active なら true。
    /// </summary>
    private bool HasActiveDamageReductionSkill()
    {
        if (_playerSkillManager == null) return false;
        foreach (var state in _playerSkillManager.RuntimeStates)
        {
            if (state == null)             continue;
            if (!state.IsActive)           continue;
            if (state.SkillData == null)   continue;
            if (state.SkillData.EffectType == PlayerSkillEffectType.DamageReduction)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Active な DamageReduction スキルの中に GrantsGuardCounter == true のものがあれば true。
    /// Radiant Riposte の授権判定に使用する。skillId で判断しない。
    /// </summary>
    private bool HasCounterGrantingDamageReductionSkill()
    {
        if (_playerSkillManager == null) return false;
        foreach (var state in _playerSkillManager.RuntimeStates)
        {
            if (state == null)           continue;
            if (!state.IsActive)         continue;
            if (state.SkillData == null) continue;
            if (state.SkillData.EffectType == PlayerSkillEffectType.DamageReduction
                && state.SkillData.GrantsGuardCounter)
                return true;
        }
        return false;
    }



private void OnValidate()
    {
        maxBurden                    = Mathf.Max(1f,  maxBurden);
        currentBurden                = Mathf.Clamp(currentBurden, 0f, maxBurden);
        lightMendBurdenGain          = Mathf.Max(0f,  lightMendBurdenGain);
        emergencyPrayerBurdenGain    = Mathf.Max(0f,  emergencyPrayerBurdenGain);
        burdenRecoveryPerSecond      = Mathf.Max(0f,  burdenRecoveryPerSecond);
        lightMendHealAmount          = Mathf.Max(0f,  lightMendHealAmount);
        emergencyPrayerHealAmount    = Mathf.Max(0f,  emergencyPrayerHealAmount);
        lightMendCooldown            = Mathf.Max(0f,  lightMendCooldown);
        emergencyPrayerCooldown      = Mathf.Max(0f,  emergencyPrayerCooldown);
        overburdenThreshold          = Mathf.Clamp01(overburdenThreshold);
        overburdenHealingMultiplier  = Mathf.Clamp01(overburdenHealingMultiplier);
        overloadThreshold            = Mathf.Clamp01(overloadThreshold);
        overloadRecoveryThreshold    = Mathf.Clamp(overloadRecoveryThreshold, 0f, overloadThreshold);
        guardResonanceBurdenReduction = Mathf.Max(0f, guardResonanceBurdenReduction);
        guardResonanceCooldown        = Mathf.Max(0f, guardResonanceCooldown);
        guardResonanceSkillHitWindow  = Mathf.Max(0f, guardResonanceSkillHitWindow);
        lightCounterDamage           = Mathf.Max(0f, lightCounterDamage);
        lightCounterMinBurdenRatio   = Mathf.Clamp01(lightCounterMinBurdenRatio);
        lightCounterMaxBurdenRatio   = Mathf.Clamp01(lightCounterMaxBurdenRatio);

    }


}
