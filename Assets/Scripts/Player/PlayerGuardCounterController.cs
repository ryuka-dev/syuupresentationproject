using UnityEngine;

/// <summary>
/// 守護反击 / Radiant Riposte コントローラー。
/// Guard Resonance 成功時に Combat Momentum（戦闘勢能）を取得する。
/// </summary>
public class PlayerGuardCounterController : MonoBehaviour
{
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
    [Tooltip("戦闘勢能（Combat Momentum）の最大値。")]
    [SerializeField] private int maxCombatMomentum = 3;
    [Tooltip("反撃伤害の PDU 倍率。1PDU = 20 enemy damage（BALANCE_BASELINE.md Tier 1）。")]
    [SerializeField] private float counterDamagePdu = 3f;
    [Tooltip("反撃時の前方短距離前压量（メートル）。デフォルト 0.75f。")]
    [SerializeField] private float lungeDistance = 0.75f;
    [Tooltip("前压の所要時間（秒）。デフォルト 0.10f。")]
    [SerializeField] private float lungeDuration = 0.10f;
    [Tooltip("前压後の攻击者との最小距離（メートル）。これより近づかない。デフォルト 1.2f。")]
    [SerializeField] private float minDistanceToTargetAfterLunge = 1.2f;

    [Header("リポステ被動 — 冷却返还")]
    [Tooltip("有効時、Radiant Riposte 成功後に指定技能の冷却を少なくする。")]
    [SerializeField] private bool enableRiposteCooldownRefundPassive = true;
    [Tooltip("減少する冷却秒数。")]
    [SerializeField] private float riposteCooldownRefundSeconds = 1f;
    [Tooltip("冷却を少なくする対象技能（PlayerSkillData）。未割り当て時はスキップ。")]
    [SerializeField] private PlayerSkillData riposteCooldownRefundTargetSkill;

    // ─── 運行時状態 ──────────────────────────────────────────────

    private int              _currentCombatMomentum;
    private Transform        _counterTarget;
    private HealthComponent  _playerHealth;
    private Rigidbody        _rb;
    private Coroutine        _lungeCoroutine;
    private PlayerSkillManager _skillManager;

    private bool IsPlayerAlive => _playerHealth == null || !_playerHealth.IsDead;

    // ─── Unity ライフサイクル ──────────────────────────────────────

    private void Awake()
    {
        if (combatAnimation == null)
            combatAnimation = GetComponent<PlayerCombatAnimationController>() ?? GetComponentInChildren<PlayerCombatAnimationController>();
        if (combatFacing == null) combatFacing = GetComponent<PlayerCombatFacingController>();
        if (combatStats == null) combatStats = GetComponent<PlayerCombatStats>();
        if (combatStats == null) Debug.LogWarning("[RadiantRiposte] PlayerCombatStats not found.");

        _playerHealth  = GetComponent<HealthComponent>();
        _rb            = GetComponent<Rigidbody>();
        _skillManager  = GetComponent<PlayerSkillManager>();
    }

    private void Start()
    {
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

    private void HandleGuardResonanceTriggered(Transform attacker, bool grantsGuardCounter)
    {
        if (!grantsGuardCounter)
        {
            Debug.Log("[CombatMomentum] Guard Resonance 触发（grantsGuardCounter=false）— Combat Momentum は変更しない。");
            return;
        }
        AddCombatMomentum(1);
        _counterTarget = attacker;
    }

    private void HandlePlayerDied()
    {
        if (_currentCombatMomentum <= 0) return;
        Debug.Log("[CombatMomentum] 玩家死亡 — Combat Momentum 清除。");
        ClearCounter();
    }

    // ─── Combat Momentum 公開 API ─────────────────────────────────

    public int  CurrentCombatMomentum => _currentCombatMomentum;
    public int  MaxCombatMomentum     => maxCombatMomentum;
    public bool HasCombatMomentum     => _currentCombatMomentum > 0;

    public void AddCombatMomentum(int amount)
    {
        int prev = _currentCombatMomentum;
        _currentCombatMomentum = Mathf.Min(_currentCombatMomentum + amount, maxCombatMomentum);
        if (_currentCombatMomentum > prev)
            Debug.Log($"[CombatMomentum] +{_currentCombatMomentum - prev}: {_currentCombatMomentum}/{maxCombatMomentum}");
        else
            Debug.Log($"[CombatMomentum] 已满: {_currentCombatMomentum}/{maxCombatMomentum}");
    }

    public bool TrySpendCombatMomentum(int amount)
    {
        if (_currentCombatMomentum < amount) return false;
        _currentCombatMomentum -= amount;
        Debug.Log($"[CombatMomentum] 消耗 -{amount}: {_currentCombatMomentum}/{maxCombatMomentum}");
        return true;
    }

    // ─── 後方互換プロパティ ────────────────────────────────────────
    public bool  IsCounterReady       => HasCombatMomentum;
    public bool  IsReady              => HasCombatMomentum;
    public float CounterRemainingTime => 0f;
    public float RemainingWindow      => 0f;
    public float CounterWindowSeconds => 0f;

    public bool CanUseCounter
    {
        get
        {
            if (!IsPlayerAlive) return false;
            if (_currentCombatMomentum <= 0 || _counterTarget == null) return false;
            var h = _counterTarget.GetComponent<HealthComponent>()
                 ?? _counterTarget.GetComponentInParent<HealthComponent>();
            return h != null && !h.IsDead;
        }
    }

    // ─── 公開実行メソッド ─────────────────────────────────────────

    public bool TryUseCounter(PlayerSkillData skillData)
    {
        if (!IsPlayerAlive)
        {
            Debug.Log("[CombatMomentum] 玩家已死亡 — 反撃不可。");
            ClearCounter();
            return false;
        }

        if (_currentCombatMomentum <= 0)
        {
            Debug.Log("[CombatMomentum] 为 0，无法释放 Radiant Riposte。");
            return false;
        }

        if (_counterTarget == null)
        {
            Debug.Log("[CombatMomentum] 攻击者为 null，无法释放。Combat Momentum 保持。");
            return false;
        }

        var hCheck = _counterTarget.GetComponent<HealthComponent>()
                  ?? _counterTarget.GetComponentInParent<HealthComponent>();
        if (hCheck == null || hCheck.IsDead)
        {
            Debug.Log("[CombatMomentum] 攻击者死亡，Combat Momentum 保持。");
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
                    Debug.Log($"[CombatMomentum] 攻击者射程外 ({dist:F1}m > {maxRange}m) — Combat Momentum 保持。");
                    return false;
                }
            }
        }

        if (!TrySpendCombatMomentum(1)) return false;

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
        Debug.Log($"[CombatMomentum] 守護反击命中！ 目标: {targetHealth.name} | 伤害: {damage} ({counterDamagePdu} PDU) | 剩余势能: {_currentCombatMomentum}/{maxCombatMomentum}");
        SimpleScreenFeedback.TriggerCounterFeedback(transform, leftHandVfxAnchor);
        TryStartLunge(_counterTarget);
        TryApplyRiposteCooldownRefund();

        return true;
    }

    // ─── Private ─────────────────────────────────────────────────

    private void TryApplyRiposteCooldownRefund()
    {
        if (!enableRiposteCooldownRefundPassive) return;

        if (_skillManager == null)
        {
            Debug.LogWarning("[RipostePassive] PlayerSkillManager not found — passive disabled.");
            enableRiposteCooldownRefundPassive = false;
            return;
        }

        if (riposteCooldownRefundTargetSkill == null)
        {
            Debug.Log("[RipostePassive] cooldown refund target skill is not assigned");
            return;
        }

        bool reduced = _skillManager.ReduceCooldown(riposteCooldownRefundTargetSkill, riposteCooldownRefundSeconds);
        if (reduced)
            Debug.Log($"[RipostePassive] {riposteCooldownRefundTargetSkill.SkillName} cooldown -{riposteCooldownRefundSeconds:F1}s");
        else
            Debug.Log("[RipostePassive] target skill runtime state not found");
    }

    private void ClearCounter()
    {
        _currentCombatMomentum = 0;
        _counterTarget         = null;
    }

    private void TryStartLunge(Transform target)
    {
        if (_rb == null || target == null) return;
        if (_lungeCoroutine != null) StopCoroutine(_lungeCoroutine);
        _lungeCoroutine = StartCoroutine(LungeCoroutine(target));
    }

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
