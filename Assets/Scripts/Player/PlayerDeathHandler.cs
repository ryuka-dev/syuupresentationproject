using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家死亡处理器 v2。
/// 死亡时用 Coroutine 强制管理死亡动画，不依赖 Animator Transition。
/// 动画播完后禁用 Animator，让角色永久保持躺地姿势。
/// RPGCameraController 死亡后仍保持可用（允许视角移动）。
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class PlayerDeathHandler : MonoBehaviour
{
    private HealthComponent       _health;
    private PlayerController      _playerController;
    private PlayerSkillController _skillController;
    private PlayerSkillManager    _skillManager;
    private PlayerTargeting       _targeting;
    private Rigidbody             _rb;
    private Animator              _animator;
    private bool                  _isDeadHandled;
    private Coroutine             _deathAnimCoroutine;

    // Animator Layer 原始权重（用于复活时恢复）
    private float[] _originalLayerWeights;

    // Animator ハッシュ
    private static readonly int SpeedHash            = Animator.StringToHash("Speed");
    private static readonly int HorizontalHash       = Animator.StringToHash("Horizontal");
    private static readonly int IsGroundedHash       = Animator.StringToHash("IsGrounded");
    private static readonly int IsJumpingHash        = Animator.StringToHash("IsJumping");
    private static readonly int IsSprintingHash      = Animator.StringToHash("IsSprinting");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsDeadHash           = Animator.StringToHash("IsDead");
    private static readonly int AttackHash           = Animator.StringToHash("Attack");
    private static readonly int RadiantRiposteHash   = Animator.StringToHash("RadiantRiposte");
    private static readonly int DeathStateFullHash   = Animator.StringToHash("Base Layer.Death");

    // HumanM@Death01 のクリップ長 = 0.7333s
    private const float DeathClipLength = 0.74f;

    private void Awake()
    {
        _health           = GetComponent<HealthComponent>();
        _playerController = GetComponent<PlayerController>();
        _skillController  = GetComponent<PlayerSkillController>();
        _skillManager     = GetComponent<PlayerSkillManager>();
        _targeting        = GetComponent<PlayerTargeting>();
        _rb               = GetComponent<Rigidbody>();
        _animator         = GetComponent<Animator>();

        if (_animator == null)
            Debug.LogWarning("[PlayerDeathHandler] Animator not found!");
        else
        {
            _originalLayerWeights = new float[_animator.layerCount];
            for (int i = 0; i < _animator.layerCount; i++)
                _originalLayerWeights[i] = _animator.GetLayerWeight(i);
        }
    }

    private void OnEnable()  => _health.OnDied += HandlePlayerDied;
    private void OnDisable() => _health.OnDied -= HandlePlayerDied;

    private void HandlePlayerDied()
    {
        if (_isDeadHandled) return;
        _isDeadHandled = true;

        Debug.Log("[DeathDebug] HandlePlayerDied called");

        // ─── 移動停止 ──────────────────────────────────────────────
        if (_playerController != null)
        {
            _playerController.StopMovementForDeath();
            _playerController.enabled = false;
            Debug.Log("[DeathDebug] PlayerController disabled");
        }

        // ─── 技能・目標停止 ───────────────────────────────────────
        if (_skillController != null) _skillController.enabled = false;
        if (_skillManager    != null) _skillManager.enabled    = false;
        if (_targeting != null) { _targeting.ClearTarget(); _targeting.enabled = false; }

        // ─── Rigidbody 停止 ──────────────────────────────────────
        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
        }
        Debug.Log("[DeathDebug] Camera remains enabled");

        // ─── 死亡アニメーション（Coroutine で強制管理）────────────
        if (_deathAnimCoroutine != null) StopCoroutine(_deathAnimCoroutine);
        _deathAnimCoroutine = StartCoroutine(DeathAnimationCoroutine());

        // ─── 生存敵を全員スポーン地点に戻す ─────────────────────
        var mgr = FindFirstObjectByType<EnemyWorldManager>();
        if (mgr != null) mgr.ForceAllLivingEnemiesReturnToSpawn();
    }

    /// <summary>
    /// 死亡アニメーション強制再生 Coroutine。
    /// 1. 現フレーム末まで待機（他の Update が終わるまで）
    /// 2. Animator.Play で Death state を強制開始
    /// 3. アニメーション時間だけ待機
    /// 4. Animator を無効化して躺地姿勢を固定
    /// </summary>
    private IEnumerator DeathAnimationCoroutine()
    {
        if (_animator == null) yield break;

        // 現フレームの Update/FixedUpdate が全部終わるまで待つ
        yield return new WaitForEndOfFrame();

        Debug.Log("[DeathAnimation] DeathAnimationCoroutine start");

        // 競合 Trigger をリセット
        _animator.ResetTrigger(AttackHash);
        _animator.ResetTrigger(RadiantRiposteHash);
        _animator.SetTrigger(IsDeadHash); // AnyState -> UpperBodyIdle も発火させる

        // FallingLoop 遷移封鎖
        _animator.SetFloat(SpeedHash,            0f);
        _animator.SetFloat(HorizontalHash,       0f);
        _animator.SetFloat(VerticalVelocityHash, 0f);
        _animator.SetBool (IsSprintingHash,      false);
        _animator.SetBool (IsJumpingHash,        false);
        _animator.SetBool (IsGroundedHash,       true);

        // UpperBody Layer を無効化（Death アニメーション上書き防止）
        for (int i = 1; i < _animator.layerCount; i++)
            _animator.SetLayerWeight(i, 0f);

        // Death state を直接再生（Transition に依存しない）
        _animator.Play(DeathStateFullHash, 0, 0f);

        // 現フレームの Animator を即時更新
        _animator.Update(0f);

        // 状態確認ログ
        var info = _animator.GetCurrentAnimatorStateInfo(0);
        bool inDeath = info.fullPathHash == DeathStateFullHash || info.IsName("Death");
        Debug.Log($"[DeathAnimation] Force Play Death");
        Debug.Log($"[DeathAnimation] In Death after Play: {inDeath}");
        Debug.Log($"[DeathAnimation] BaseLayer state hash={info.fullPathHash}, normTime={info.normalizedTime}");

        // アニメーション再生を待機（クリップ長 + 余裕）
        float elapsed = 0f;
        while (elapsed < DeathClipLength + 0.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            // UpperBody Layer を毎フレーム 0 に固定（他スクリプトが戻す場合の保険）
            for (int i = 1; i < _animator.layerCount; i++)
                _animator.SetLayerWeight(i, 0f);
            yield return null;
        }

        // アニメーション完了後：最終フレームに固定してから Animator を無効化
        if (_isDeadHandled && _animator != null)
        {
            // Death アニメーション最終フレームへシーク
            _animator.Play(DeathStateFullHash, 0, 0.999f);
            _animator.Update(0f);
            // Animator を無効化 → 最終ポーズで固定（Unity は無効化時に最終ポーズを保持）
            _animator.enabled = false;
            Debug.Log("[DeathAnimation] Animator disabled and frozen in death pose");
        }
    }

    /// <summary>
    /// 复活専用：全ての状態を復元する。
    /// </summary>
    public void ResetForRespawn()
    {
        // 死亡 Coroutine を停止
        if (_deathAnimCoroutine != null) { StopCoroutine(_deathAnimCoroutine); _deathAnimCoroutine = null; }

        _isDeadHandled = false;

        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = false;
        }

        if (_animator != null)
        {
            // Animator を再有効化
            _animator.enabled = true;
            // Trigger リセット
            _animator.ResetTrigger(IsDeadHash);
            _animator.ResetTrigger(AttackHash);
            _animator.ResetTrigger(RadiantRiposteHash);
            // Layer 権重を復元
            if (_originalLayerWeights != null)
                for (int i = 0; i < Mathf.Min(_animator.layerCount, _originalLayerWeights.Length); i++)
                    _animator.SetLayerWeight(i, _originalLayerWeights[i]);
            // Animator を初期化
            _animator.Rebind();
            _animator.Update(0f);
            Debug.Log("[DeathAnimation] Respawn reset animator");
        }

        if (_playerController != null) _playerController.enabled = true;
        if (_skillController  != null) _skillController.enabled  = true;
        if (_skillManager     != null) _skillManager.enabled     = true;
        if (_targeting        != null) _targeting.enabled        = true;

        Debug.Log("[PlayerDeathHandler] Player reset for respawn.");
    }
}
