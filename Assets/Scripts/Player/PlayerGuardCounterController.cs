using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 守護反击 / Radiant Riposte コントローラー（第一版最小实现）。
///
/// 守护共鸣 / Guard Resonance 成功時に反撃機会を取得し、
/// 10秒以内に5キーを押すことで攻撃者に 3PDU のダメージを与える。
///
/// 接続: HikariSupportController.OnGuardResonanceTriggered イベントを購読。
/// 入力: New Input System の Keyboard.current.digit5Key。
/// 伤害: PlayerCombatStats.BaseNormalAttackDamage * counterDamagePdu（PDU 換算）。
/// </summary>
public class PlayerGuardCounterController : MonoBehaviour
{
    // ─── Inspector フィールド ─────────────────────────────────────

    [Header("Hikari 参照（空の場合 Start() で自動検索）")]
    [SerializeField] private HikariSupportController hikariSupport;

    [Header("玩家戦闘ステータス参照（空の場合 GetComponent で自動解決）")]
    [SerializeField] private PlayerCombatStats combatStats;

    [Header("反撃パラメータ")]
    [Tooltip("反撃機会の有効時間（秒）。この時間内に5キーで反撃できる。")]
    [SerializeField] private float counterWindowSeconds = 10f;

    [Tooltip("反撃伤害の PDU 倍率。1PDU = 20 enemy damage（BALANCE_BASELINE.md Tier 1 定義）。")]
    [SerializeField] private float counterDamagePdu = 3f;

    [Header("表示名（ローカライズ対応構造）")]
    [Tooltip("将来的なローカライズ向けキー。現状は fallbackText を使用。")]
    [SerializeField] private string counterLocalizationKey = "skill.player.radiant_riposte.name";
    [Tooltip("現在の表示テキスト（フォールバック）。")]
    [SerializeField] private string counterFallbackName = "Radiant Riposte";

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
            Debug.LogWarning("[RadiantRiposte] PlayerCombatStats not found. Damage calculation will fall back to 0.");
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
        // 有効時間カウントダウン
        if (_isReady)
        {
            _remainingWindow -= Time.deltaTime;
            if (_remainingWindow <= 0f)
            {
                Debug.Log("[RadiantRiposte] 反撃機会が期限切れ（10秒）。");
                ClearCounter();
            }
        }

        // 5キー入力チェック
        if (Keyboard.current != null && Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            TryExecuteCounter();
        }
    }

    // ─── Guard Resonance イベントハンドラ ─────────────────────────

    private void HandleGuardResonanceTriggered(Transform attacker)
    {
        // Guard Resonance 成功 → 反撃機会を取得（または刷新）
        _isReady         = true;
        _counterTarget   = attacker;
        _remainingWindow = counterWindowSeconds;
        Debug.Log($"[RadiantRiposte] Radiant Riposte Ready — 攻击者: {(attacker != null ? attacker.name : "null")} | 有效时间: {counterWindowSeconds}s");
    }

    // ─── 反撃実行 ────────────────────────────────────────────────

    private void TryExecuteCounter()
    {
        if (!_isReady)
        {
            // Ready でない場合はサイレントに無視（過剰なログ抑制）
            return;
        }

        // 攻撃者の検証
        if (_counterTarget == null)
        {
            Debug.Log("[RadiantRiposte] 攻击者が null のため反撃失败。Ready 清除。");
            ClearCounter();
            return;
        }

        var targetHealth = _counterTarget.GetComponent<HealthComponent>();
        if (targetHealth == null)
            targetHealth = _counterTarget.GetComponentInParent<HealthComponent>();

        if (targetHealth == null)
        {
            Debug.Log("[RadiantRiposte] 攻击者に HealthComponent がないため反撃失败。Ready 清除。");
            ClearCounter();
            return;
        }

        if (targetHealth.IsDead)
        {
            Debug.Log("[RadiantRiposte] 攻击者已死亡，反撃失败。Ready 清除。");
            ClearCounter();
            return;
        }

        // 伤害计算: BALANCE_BASELINE.md Tier 1: 1 PDU = 20 enemy damage
        // combatStats.BaseNormalAttackDamage は Tier 1 無装備 20 = 1 PDU に相当
        float basePdu  = combatStats != null ? combatStats.BaseNormalAttackDamage : 20f;
        float damage   = basePdu * counterDamagePdu;

        // 来源ラベル生成（ローカライズ対応構造）
        var sourceLabel = new CombatTextSourceLabel
        {
            localizationKey = counterLocalizationKey,
            fallbackText    = counterFallbackName
        };

        // 伤害を与える
        targetHealth.TakeDamage(damage, transform, sourceLabel);

        Debug.Log($"[RadiantRiposte] 守護反击 / Radiant Riposte 命中！ 目标: {targetHealth.name} | 伤害: {damage} ({counterDamagePdu} PDU) | 来源: {sourceLabel.GetDisplayText()}");

        // 反撃機会消費
        ClearCounter();
    }

    // ─── ヘルパー ─────────────────────────────────────────────────

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
            Debug.LogWarning("[RadiantRiposte] HikariSupportController が見つかりません。Guard Resonance イベントを購読できません。");
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

    // ─── 公開プロパティ（Debug / UI 拡張向け） ────────────────────

    /// <summary>反撃機会が有効かどうか。</summary>
    public bool IsReady => _isReady;

    /// <summary>残り有効時間（秒）。</summary>
    public float RemainingWindow => _remainingWindow;
}
