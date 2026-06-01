using UnityEngine;

/// <summary>
/// 玩家死亡处理器。挂载在 Player 对象上。
/// 死亡时禁用移动/攻击/目标选择输入，停止 Rigidbody，播放死亡动画。
/// RPGCameraController は死亡後も有効に保ち、カメラ回転を許可する。
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

    private void Awake()
    {
        _health           = GetComponent<HealthComponent>();
        _playerController = GetComponent<PlayerController>();
        _skillController  = GetComponent<PlayerSkillController>();
        _skillManager     = GetComponent<PlayerSkillManager>();
        _targeting        = GetComponent<PlayerTargeting>();
        _rb               = GetComponent<Rigidbody>();
        _animator         = GetComponent<Animator>();

        if (_playerController == null)
            Debug.LogWarning("[PlayerDeathHandler] PlayerController not found! Movement cannot be disabled.");
        if (_targeting == null)
            Debug.LogWarning("[PlayerDeathHandler] PlayerTargeting not found.");
        if (_rb == null)
            Debug.LogWarning("[PlayerDeathHandler] Rigidbody not found.");
        if (_animator == null)
            Debug.LogWarning("[PlayerDeathHandler] Animator not found. Death animation will not play.");
    }

    private void OnEnable()  => _health.OnDied += HandlePlayerDied;
    private void OnDisable() => _health.OnDied -= HandlePlayerDied;

    private void HandlePlayerDied()
    {
        if (_isDeadHandled) return;
        _isDeadHandled = true;

        Debug.Log("[DeathDebug] HandlePlayerDied called");
        Debug.Log($"[DeathDebug] PlayerController found: {_playerController != null}");

        // ─── 移動停止（先に Stop を呼び、その後 enabled = false で確実に封鎖） ──
        if (_playerController != null)
        {
            Debug.Log($"[DeathDebug] PlayerController enabled before disable: {_playerController.enabled}");
            _playerController.StopMovementForDeath();
            _playerController.enabled = false;
        }
        else
        {
            Debug.LogWarning("[DeathDebug] PlayerController is null — cannot stop movement!");
        }

        // ─── 技能入力停止 ─────────────────────────────────────────
        if (_skillController != null) _skillController.enabled = false;
        if (_skillManager    != null) _skillManager.enabled    = false;

        // ─── 目標選択停止 ────────────────────────────────────────
        if (_targeting != null)
        {
            _targeting.ClearTarget();
            _targeting.enabled = false;
        }

        // ─── RPGCameraController は無効化しない（死亡後も視角移動を許可） ─
        Debug.Log("[DeathDebug] Camera remains enabled");

        // ─── Rigidbody 停止 ──────────────────────────────────────
        if (_rb != null)
        {
            Debug.Log($"[DeathDebug] Rigidbody before: isKinematic={_rb.isKinematic}, velocity={_rb.linearVelocity}");
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
            Debug.Log($"[DeathDebug] Rigidbody after: isKinematic={_rb.isKinematic}");
        }

        // ─── 死亡アニメーション ──────────────────────────────────
        if (_animator != null) _animator.SetTrigger("IsDead");

        Debug.Log("[PlayerDeathHandler] Player died. Controls disabled. Camera remains active.");

        // ─── 生存敵を全員スポーン地点に戻す ─────────────────────
        var enemyWorldManager = FindFirstObjectByType<EnemyWorldManager>();
        if (enemyWorldManager != null)
            enemyWorldManager.ForceAllLivingEnemiesReturnToSpawn();
        else
            Debug.LogWarning("[PlayerDeathHandler] EnemyWorldManager not found. Enemy disengage skipped.");
    }

    /// <summary>
    /// 复活専用：PlayerDeathHandler が死亡時に変更した状態を復元する。
    /// HP / 転送 / UI は別スクリプトが担当する。
    /// </summary>
    public void ResetForRespawn()
    {
        _isDeadHandled = false;

        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = false;
        }

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }

        if (_playerController != null)
            _playerController.enabled = true;

        if (_skillController != null)
            _skillController.enabled = true;

        if (_skillManager != null)
            _skillManager.enabled = true;

        if (_targeting != null)
            _targeting.enabled = true;

        // RPGCameraController は死亡時に無効化していないため、ここで有効化する必要なし

        Debug.Log("[PlayerDeathHandler] Player reset for respawn.");
    }
}
