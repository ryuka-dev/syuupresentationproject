using UnityEngine;

/// <summary>
/// 守護反击 / Radiant Riposte コントローラー。
///
/// 守护共鸣 / Guard Resonance 成功時に反撃機会を取得する。
/// 入力は PlayerSkillManager が分発し、TryUseCounter(skillData) で実行する。
/// このスクリプト自身はキーボードを直接読まない。
///
/// 接続: HikariSupportController.OnGuardResonanceTriggered イベントを購読。
/// 伤害: PlayerCombatStats.BaseNormalAttackDamage * counterDamagePdu（PDU 換算）。
/// 伤害来源名: TryUseCounter に渡された PlayerSkillData.SkillName / LocalizationKey。
/// </summary>
public class PlayerGuardCounterController : MonoBehaviour
{
    // ─── Inspector フィールド ─────────────────────────────────────

    [Header("Hikari 参照（空の場合 Start() で自動検索）")]
    [SerializeField] private HikariSupportController hikariSupport;

    [Header("玩家戦闘ステータス参照（空の場合 GetComponent で自動解決）")]
    [SerializeField] private PlayerCombatStats combatStats;

    [Header("反撃パラメータ")]
    [Tooltip("反撃機会の有効時間（秒）。")]
    [SerializeField] private float counterWindowSeconds = 10f;

    [Tooltip("反撃伤害の PDU 倍率。1PDU = 20 enemy damage（BALANCE_BASELINE.md Tier 1）。")]
    [SerializeField] private float counterDamagePdu = 3f;

    // ─── 運行時状態 ──────────────────────────────────────────────

    private bool      _isReady;
    private float     _remainingWindow;
    private Transform _counterTarget;

    // ─── Unity ライフサイクル ──────────────────────────────────────

    private void Awake()
    {
        if (combatStats == null)
            combatStats = GetComponent<PlayerCombatStats>();
        if (combatStats == null)
            Debug.LogWarning("[RadiantRiposte] PlayerCombatStats not found.");
    }

    private void Start()
    {
        ResolveHikariSupport();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHikari();
    }

    private void Update()
    {
        // 有効時間カウントダウンのみ（キーボード入力は PlayerSkillManager が担当）
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

    private void HandleGuardResonanceTriggered(Transform attacker)
    {
        _isReady         = true;
        _counterTarget   = attacker;
        _remainingWindow = counterWindowSeconds;
        Debug.Log($"[RadiantRiposte] Radiant Riposte Ready — 攻击者: {(attacker != null ? attacker.name : "null")} | 有效时间: {counterWindowSeconds}s");
    }

    // ─── 公開状態プロパティ ───────────────────────────────────────

    /// <summary>反撃機会が 10 秒窓内にあるか。</summary>
    public bool IsCounterReady => _isReady;

    /// <summary>残り有効時間（秒）。IsCounterReady が false なら 0。</summary>
    public float CounterRemainingTime => _remainingWindow;

    /// <summary>最大有効時間（秒）。Inspector で設定。</summary>
    public float CounterWindowSeconds => counterWindowSeconds;

    /// <summary>
    /// 今すぐ反撃を実行できるか。
    /// IsCounterReady かつ目標が生存している場合のみ true。
    /// </summary>
    public bool CanUseCounter
    {
        get
        {
            if (!_isReady || _counterTarget == null) return false;
            var h = _counterTarget.GetComponent<HealthComponent>()
                 ?? _counterTarget.GetComponentInParent<HealthComponent>();
            return h != null && !h.IsDead;
        }
    }

    // 後方互換プロパティ（旧コードが参照している場合向け）
    /// <summary>IsCounterReady の別名。</summary>
    public bool IsReady => _isReady;
    /// <summary>CounterRemainingTime の別名。</summary>
    public float RemainingWindow => _remainingWindow;

    // ─── 公開実行メソッド ─────────────────────────────────────────

    /// <summary>
    /// PlayerSkillManager から呼ばれる反撃実行メソッド。
    /// 伤害来源名は skillData.SkillName / LocalizationKey から生成する。
    /// CanUseCounter が false なら何もせず false を返す。
    /// </summary>
    public bool TryUseCounter(PlayerSkillData skillData)
    {
        if (!CanUseCounter)
        {
            if (_isReady && _counterTarget == null)
            {
                Debug.Log("[RadiantRiposte] 攻击者が null のため反撃失败。Ready 清除。");
                ClearCounter();
            }
            else if (_isReady)
            {
                var h = _counterTarget.GetComponent<HealthComponent>()
                     ?? _counterTarget.GetComponentInParent<HealthComponent>();
                if (h != null && h.IsDead)
                {
                    Debug.Log("[RadiantRiposte] 攻击者已死亡，反撃失败。Ready 清除。");
                    ClearCounter();
                }
            }
            return false;
        }

        var targetHealth = _counterTarget.GetComponent<HealthComponent>()
                        ?? _counterTarget.GetComponentInParent<HealthComponent>();

        float basePdu = combatStats != null ? combatStats.BaseNormalAttackDamage : 20f;
        float damage  = basePdu * counterDamagePdu;

        // 伤害来源名は PlayerSkillData から生成（ハードコードなし）
        var sourceLabel = new CombatTextSourceLabel
        {
            localizationKey = skillData != null ? skillData.LocalizationKey : "skill.player.radiant_riposte.name",
            fallbackText    = skillData != null ? skillData.SkillName        : "Radiant Riposte"
        };

        targetHealth.TakeDamage(damage, transform, sourceLabel);

        Debug.Log($"[RadiantRiposte] 守護反击命中！ 目标: {targetHealth.name} | 伤害: {damage} ({counterDamagePdu} PDU) | 来源: {sourceLabel.GetDisplayText()}");

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
}
