using UnityEngine;

/// <summary>
/// 守護反击 / Radiant Riposte コントローラー。
///
/// Guard Resonance 成功時に反撃機会を取得する。
/// 入力は PlayerSkillManager が分発し、TryUseCounter(skillData) で実行する。
/// このスクリプト自身はキーボードを直接読まない。
///
/// 授権ルール:
///   Guard Resonance イベントの grantsGuardCounter == true の場合のみ Ready を更新。
///   false の場合（Stone Guard のみで Guard Resonance）は Ready を変更しない。
///
/// 死亡ガード:
///   HealthComponent.OnDied を購読して死亡時に ClearCounter する。
///   CanUseCounter / TryUseCounter で IsDead を確認する。
/// </summary>
public class PlayerGuardCounterController : MonoBehaviour
{
    // ─── Inspector フィールド ─────────────────────────────────────

    [Header("Hikari 参照（空の場合 Start() で自動検索）")]
    [SerializeField] private HikariSupportController hikariSupport;

    [Header("玩家戦闘ステータス参照（空の場合 GetComponent で自動解決）")]
    [SerializeField] private PlayerCombatStats combatStats;

    [Header("守護反击 VFX アンカー（左手骨骼 Transform を設定。空の場合は自動探索。）")]
    [SerializeField] private Transform leftHandVfxAnchor;

    [Header("反撃パラメータ")]
    [Tooltip("反撃機会の有効時間（秒）。")]
    [SerializeField] private float counterWindowSeconds = 10f;

    [Tooltip("反撃伤害の PDU 倍率。1PDU = 20 enemy damage（BALANCE_BASELINE.md Tier 1）。")]
    [SerializeField] private float counterDamagePdu = 3f;

    // ─── 運行時状態 ──────────────────────────────────────────────

    private bool             _isReady;
    private float            _remainingWindow;
    private Transform        _counterTarget;
    private HealthComponent  _playerHealth;   // 自身 HealthComponent（死亡チェック用）

    // ─── Unity ライフサイクル ──────────────────────────────────────

    private void Awake()
    {
        if (combatStats == null)
            combatStats = GetComponent<PlayerCombatStats>();
        if (combatStats == null)
            Debug.LogWarning("[RadiantRiposte] PlayerCombatStats not found.");

        _playerHealth = GetComponent<HealthComponent>();
    }

    private void Start()
    {
        // 左手ボーン自動探索（leftHandVfxAnchor が未設定の場合）
        if (leftHandVfxAnchor == null)
        {
            var wristL = transform.Find("Wrist_L");
            if (wristL == null) wristL = FindDeepChild(transform, "Wrist_L");
            if (wristL != null) { leftHandVfxAnchor = wristL; Debug.Log("[RadiantRiposte] leftHandVfxAnchor auto-resolved: " + wristL.name); }
            else Debug.Log("[RadiantRiposte] leftHandVfxAnchor not found, using player transform fallback.");
        }
        ResolveHikariSupport();
        SubscribeToPlayerDeath();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHikari();
        UnsubscribeFromPlayerDeath();
    }

    private void Update()
    {
        if (_isReady)
        {
            _remainingWindow -= Time.deltaTime;
            if (_remainingWindow <= 0f)
            {
                Debug.Log("[RadiantRiposte] 反撃機会が期限切れ（10秒）。");
                ClearCounter();
            }
        }
    }

    // ─── Guard Resonance イベントハンドラ ─────────────────────────

    /// <summary>
    /// HikariSupportController.OnGuardResonanceTriggered(attacker, grantsGuardCounter) のハンドラ。
    /// grantsGuardCounter == true の場合のみ Ready を更新・刷新する。
    /// false（Stone Guard のみ）の場合は既存の Ready 状態を変えない。
    /// </summary>
    private void HandleGuardResonanceTriggered(Transform attacker, bool grantsGuardCounter)
    {
        if (!grantsGuardCounter)
        {
            Debug.Log("[RadiantRiposte] Guard Resonance 触发（grantsGuardCounter=false）— Radiant Riposte は更新しない。");
            return;
        }

        _isReady         = true;
        _counterTarget   = attacker;
        _remainingWindow = counterWindowSeconds;
        Debug.Log($"[RadiantRiposte] Radiant Riposte Ready（Iron Bulwark 授権）— 攻击者: {(attacker != null ? attacker.name : "null")} | 有效时间: {counterWindowSeconds}s");
    }

    // ─── 死亡ハンドラ ────────────────────────────────────────────

    private void HandlePlayerDied()
    {
        if (!_isReady) return;
        Debug.Log("[RadiantRiposte] 玩家死亡 — Radiant Riposte Ready 清除。");
        ClearCounter();
    }

    // ─── 公開状態プロパティ ───────────────────────────────────────

    /// <summary>プレイヤーが生存しているか。</summary>
    private bool IsPlayerAlive => _playerHealth == null || !_playerHealth.IsDead;

    /// <summary>反撃機会が 10 秒窓内にあるか。</summary>
    public bool IsCounterReady => _isReady;

    /// <summary>残り有効時間（秒）。</summary>
    public float CounterRemainingTime => _remainingWindow;

    /// <summary>最大有効時間（秒）。</summary>
    public float CounterWindowSeconds => counterWindowSeconds;

    /// <summary>
    /// 今すぐ反撃を実行できるか。
    /// IsCounterReady かつプレイヤー生存 かつ目標が生存している場合のみ true。
    /// </summary>
    public bool CanUseCounter
    {
        get
        {
            if (!IsPlayerAlive)           return false;
            if (!_isReady || _counterTarget == null) return false;
            var h = _counterTarget.GetComponent<HealthComponent>()
                 ?? _counterTarget.GetComponentInParent<HealthComponent>();
            return h != null && !h.IsDead;
        }
    }

    // 後方互換プロパティ
    public bool IsReady => _isReady;
    public float RemainingWindow => _remainingWindow;

    // ─── 公開実行メソッド ─────────────────────────────────────────

    /// <summary>
    /// PlayerSkillManager から呼ばれる反撃実行メソッド。
    /// 伤害来源名は skillData.SkillName / LocalizationKey から生成する。
    /// CanUseCounter が false なら何もせず false を返す。
    /// </summary>
    public bool TryUseCounter(PlayerSkillData skillData)
    {
        // 死亡チェック（二重ガード）
        if (!IsPlayerAlive)
        {
            Debug.Log("[RadiantRiposte] 玩家已死亡 — 反撃不可。");
            ClearCounter();
            return false;
        }

        if (!CanUseCounter)
        {
            if (_isReady && _counterTarget == null)
            {
                Debug.Log("[RadiantRiposte] 攻击者が null のため反撃失败。Ready 清除。");
                ClearCounter();
            }
            else if (_isReady)
            {
                var hCheck = _counterTarget.GetComponent<HealthComponent>()
                          ?? _counterTarget.GetComponentInParent<HealthComponent>();
                if (hCheck != null && hCheck.IsDead)
                {
                    Debug.Log("[RadiantRiposte] 攻击者已死亡，反撃失败。Ready 清除。");
                    ClearCounter();
                }
            }
            return false;
        }

        // 距離チェック: skillData.EffectiveRange（Ranged = 20m）を超えていたら打てない。
        // Ready は消費しない。プレイヤーが近づいてから再び試みることができる。
        if (skillData != null)
        {
            float maxRange = skillData.EffectiveRange;
            if (maxRange > 0f)
            {
                float dist = Vector3.Distance(transform.position, _counterTarget.position);
                if (dist > maxRange)
                {
                    Debug.Log($"[RadiantRiposte] 攻击者が射程外 ({dist:F1}m > {maxRange}m) — 反撃失败。Ready は保持。");
                    return false; // Ready 消費しない
                }
            }
        }

        var targetHealth = _counterTarget.GetComponent<HealthComponent>()
                        ?? _counterTarget.GetComponentInParent<HealthComponent>();

        float basePdu = combatStats != null ? combatStats.BaseNormalAttackDamage : 20f;
        float damage  = basePdu * counterDamagePdu;

        var sourceLabel = new CombatTextSourceLabel
        {
            localizationKey = skillData != null ? skillData.LocalizationKey : "skill.player.radiant_riposte.name",
            fallbackText    = skillData != null ? skillData.SkillName        : "Radiant Riposte"
        };

        targetHealth.TakeDamage(damage, transform, sourceLabel);
        Debug.Log($"[RadiantRiposte] 守護反击命中！ 目标: {targetHealth.name} | 伤害: {damage} ({counterDamagePdu} PDU) | 来源: {sourceLabel.GetDisplayText()}");
        SimpleScreenFeedback.TriggerCounterFeedback(transform, leftHandVfxAnchor); // 守護反击命中フィードバック

        ClearCounter();
        return true;
    }

    // ─── Private ─────────────────────────────────────────────────

    private void ClearCounter()
    {
        _isReady         = false;
        _counterTarget   = null;
        _remainingWindow = 0f;
    }

    private void ResolveHikariSupport()
    {
        if (hikariSupport == null)
            hikariSupport = Object.FindFirstObjectByType<HikariSupportController>();

        if (hikariSupport == null)
        {
            Debug.LogWarning("[RadiantRiposte] HikariSupportController が見つかりません。");
            return;
        }

        hikariSupport.OnGuardResonanceTriggered += HandleGuardResonanceTriggered;
        Debug.Log($"[RadiantRiposte] HikariSupportController に購読完了: {hikariSupport.gameObject.name}");
    }

    private void UnsubscribeFromHikari()
    {
        if (hikariSupport != null)
            hikariSupport.OnGuardResonanceTriggered -= HandleGuardResonanceTriggered;
    }

    private void SubscribeToPlayerDeath()
    {
        if (_playerHealth != null)
            _playerHealth.OnDied += HandlePlayerDied;
    }

    private void UnsubscribeFromPlayerDeath()
    {
        if (_playerHealth != null)
            _playerHealth.OnDied -= HandlePlayerDied;
    }

    /// <summary>Transform ツリーを再帰的に探索する最小ヘルパー。</summary>
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
