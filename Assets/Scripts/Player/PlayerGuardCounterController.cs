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

    [Header("戦闘アニメーション（空の場合 Awake で自動解決）")]
    [SerializeField] private PlayerCombatAnimationController combatAnimation;
    [SerializeField] private PlayerCombatFacingController combatFacing;

    [Header("反撃パラメータ")]
    [Tooltip("守护充能の最大値。")]
    [SerializeField] private int maxGuardCharge = 3;

    [Tooltip("反撃伤害の PDU 倍率。1PDU = 20 enemy damage（BALANCE_BASELINE.md Tier 1）。")]
    [SerializeField] private float counterDamagePdu = 3f;

    [Tooltip("反撃時の前方短距離前压量（メートル）。デフォルト 0.75f。")]
    [SerializeField] private float lungeDistance = 0.75f;
    [Tooltip("前压の所要時間（秒）。デフォルト 0.10f。")]
    [SerializeField] private float lungeDuration = 0.10f;
    [Tooltip("前压後の攻击者との最小距離（メートル）。これより近づかない。デフォルト 1.2f。")]
    [SerializeField] private float minDistanceToTargetAfterLunge = 1.2f;

    // ─── 運行時状態 ──────────────────────────────

    private int              _currentGuardCharge;  // 現在の Guard Charge 点数
    private Transform        _counterTarget;        // 最近の Guard Resonance の攻撃者
    private HealthComponent  _playerHealth;         // 自身 HealthComponent（死亡チェック用）
    private Rigidbody        _rb;
    private Coroutine        _lungeCoroutine;

    private bool IsPlayerAlive => _playerHealth == null || !_playerHealth.IsDead;

    private void Awake()
    {
        if (combatAnimation == null)
            combatAnimation = GetComponent<PlayerCombatAnimationController>() ?? GetComponentInChildren<PlayerCombatAnimationController>();
        if (combatFacing == null) combatFacing = GetComponent<PlayerCombatFacingController>();
        if (combatStats == null)
            combatStats = GetComponent<PlayerCombatStats>();
        if (combatStats == null)
            Debug.LogWarning("[RadiantRiposte] PlayerCombatStats not found.");

        _playerHealth = GetComponent<HealthComponent>();
        _rb = GetComponent<Rigidbody>();
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

    private void Update() { }

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
            Debug.Log("[GuardCharge] Guard Resonance 触发（grantsGuardCounter=false）— Guard Charge は変更しない。");
            return;
        }

        AddGuardCharge(1);
        _counterTarget = attacker; // 最新の攻撃者を保持
    }

    // ─── 死亡ハンドラ ────────────────────────────────────────────

    private void HandlePlayerDied()
    {
        if (_currentGuardCharge <= 0) return;
        Debug.Log("[GuardCharge] 玩家死亡 — Guard Charge 清除。");
        ClearCounter();
    }


    // ─── 公開状態プロパティ ───────────────────────────────────────

    // ─── Guard Charge 公開 API ───────────────────────────────────────────

    /// <summary>守护充能现在点数。</summary>
    public int CurrentGuardCharge => _currentGuardCharge;

    /// <summary>守护充能最大値。</summary>
    public int MaxGuardCharge => maxGuardCharge;

    /// <summary>1点以上の Guard Charge を持っているか。</summary>
    public bool HasGuardCharge => _currentGuardCharge > 0;

    /// <summary>指定点数だけ Guard Charge を増加する。最大値を超えない。</summary>
    public void AddGuardCharge(int amount)
    {
        int prev = _currentGuardCharge;
        _currentGuardCharge = Mathf.Min(_currentGuardCharge + amount, maxGuardCharge);
        if (_currentGuardCharge > prev)
            Debug.Log($"[GuardCharge] +{_currentGuardCharge - prev}: {_currentGuardCharge}/{maxGuardCharge}");
        else
            Debug.Log($"[GuardCharge] 充能已满: {_currentGuardCharge}/{maxGuardCharge}");
    }

    /// <summary>指定点数だけ Guard Charge を消費する。足りなければ false を返す。</summary>
    public bool TrySpendGuardCharge(int amount)
    {
        if (_currentGuardCharge < amount) return false;
        _currentGuardCharge -= amount;
        Debug.Log($"[GuardCharge] 消費 -{amount}: {_currentGuardCharge}/{maxGuardCharge}");
        return true;
    }

    // 後方互準プロパティ（技能欄 UI 互準）
    public bool IsCounterReady => HasGuardCharge;
    public bool IsReady => HasGuardCharge;
    public float CounterRemainingTime => 0f; // 資源制に移行したためタイマーなし
    public float RemainingWindow => 0f;
    public float CounterWindowSeconds => 0f;

    /// <summary>現在ターゲットの有効性チェック。</summary>
    public bool CanUseCounter
    {
        get
        {
            if (!IsPlayerAlive) return false;
            if (_currentGuardCharge <= 0 || _counterTarget == null) return false;
            var h = _counterTarget.GetComponent<HealthComponent>()
                 ?? _counterTarget.GetComponentInParent<HealthComponent>();
            return h != null && !h.IsDead;
        }
    }


    /// <summary>
    /// PlayerSkillManager から呼ばれる反撃実行メソッド。
    /// 伤害来源名は skillData.SkillName / LocalizationKey から生成する。
    /// CanUseCounter が false なら何もせず false を返す。
    /// </summary>
    public bool TryUseCounter(PlayerSkillData skillData)
    {
        if (!IsPlayerAlive)
        {
            Debug.Log("[GuardCharge] 玩家已死亡 — 反撃不可。");
            ClearCounter();
            return false;
        }

        if (_currentGuardCharge <= 0)
        {
            Debug.Log("[GuardCharge] Guard Charge が 0 のため反撃不可。");
            return false;
        }

        if (_counterTarget == null)
        {
            Debug.Log("[GuardCharge] 攻撃者が null のため反撃不可。Guard Charge は保持。");
            return false;
        }

        var hCheck = _counterTarget.GetComponent<HealthComponent>()
                  ?? _counterTarget.GetComponentInParent<HealthComponent>();
        if (hCheck == null || hCheck.IsDead)
        {
            Debug.Log("[GuardCharge] 攻撃者死亡 / HealthComponent なし。Guard Charge は保持。");
            return false;
        }

        if (skillData != null)
        {
            float maxRange = skillData.EffectiveRange;
            if (maxRange > 0f)
            {
                float dist = Vector3.Distance(transform.position, _counterTarget.position);
                if (dist > maxRange)
                {
                    Debug.Log($"[GuardCharge] 攻撃者が射程外 ({dist:F1}m > {maxRange}m) — Guard Charge は保持。");
                    return false;
                }
            }
        }

        // ─── 成功条件を満たした: 1 点消費して反撃実行 ───
        if (!TrySpendGuardCharge(1)) return false; // 二重ガード（通常は到達しない）

        var targetHealth = _counterTarget.GetComponent<HealthComponent>()
                        ?? _counterTarget.GetComponentInParent<HealthComponent>();

        float basePdu = combatStats != null ? combatStats.BaseNormalAttackDamage : 20f;
        float damage  = basePdu * counterDamagePdu;

        var sourceLabel = new CombatTextSourceLabel
        {
            localizationKey = skillData != null ? skillData.LocalizationKey : "skill.player.radiant_riposte.name",
            fallbackText    = skillData != null ? skillData.SkillName        : "Radiant Riposte"
        };

        combatFacing?.FaceTarget(_counterTarget);
        combatAnimation?.PlayRadiantRiposte();

        targetHealth.TakeDamage(damage, transform, sourceLabel);
        Debug.Log($"[GuardCharge] 守護反击命中！ 目标: {targetHealth.name} | 伤害: {damage} ({counterDamagePdu} PDU) | 剩余 Charge: {_currentGuardCharge}/{maxGuardCharge}");
        SimpleScreenFeedback.TriggerCounterFeedback(transform, leftHandVfxAnchor);
        TryStartLunge(_counterTarget);

        return true;
    }

    // ─── Private ─────────────────────────────────────────────────

    private void ClearCounter()
    {
        _currentGuardCharge = 0;
        _counterTarget      = null;
    }
    /// <summary>
    /// Radiant Riposte 成功時に短距離前压 Coroutine を開始する。
    /// _rb が null の場合や target が null の場合は前压せず、ダメージ/アニメ/VFX には影響しない。
    /// </summary>
    private void TryStartLunge(Transform target)
    {
        if (_rb == null || target == null) return;
        if (_lungeCoroutine != null) StopCoroutine(_lungeCoroutine);
        _lungeCoroutine = StartCoroutine(LungeCoroutine(target));
    }

    /// <summary>
    /// 攻击者方向（XZ 水平面）に短距離前压する Coroutine。
    /// MovePosition で移動するため PlayerController の linearVelocity と干渉しない。
    /// 前压後 PlayerController の移動は通常通り再開される。
    /// </summary>
    private System.Collections.IEnumerator LungeCoroutine(Transform target)
    {
        Vector3 startPos = transform.position;
        Vector3 toTarget = (target != null ? target.position : startPos + transform.forward) - startPos;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist < 0.01f) { _lungeCoroutine = null; yield break; }
        Vector3 dir = toTarget / dist;

        float actualLunge = lungeDistance;
        float remainAfterLunge = dist - actualLunge;
        if (remainAfterLunge < minDistanceToTargetAfterLunge)
            actualLunge = dist - minDistanceToTargetAfterLunge;
        if (actualLunge <= 0f) { _lungeCoroutine = null; yield break; }

        Vector3 endPos = startPos + dir * actualLunge;

        float elapsed = 0f;
        while (elapsed < lungeDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / lungeDuration);
            Vector3 next = Vector3.Lerp(startPos, endPos, t);
            next.y = transform.position.y;
            _rb.MovePosition(next);
        }
        _lungeCoroutine = null;
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
