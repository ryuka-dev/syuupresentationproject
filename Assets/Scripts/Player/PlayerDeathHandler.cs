using UnityEngine;

/// <summary>
/// 玩家死亡处理器。挂载在 Player 对象上。
/// 监听 HealthComponent.OnDied，死亡时禁用移动/攻击/目标选择输入、摄像机控制，停止 Rigidbody，播放死亡动画。
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class PlayerDeathHandler : MonoBehaviour
{
    private HealthComponent       _health;
    private PlayerController      _playerController;
    private PlayerSkillController _skillController;
    private PlayerTargeting       _targeting;
    private RPGCameraController   _cameraController;
    private Rigidbody             _rb;
    private Animator              _animator;
    private bool                  _isDeadHandled;

    private void Awake()
    {
        _health           = GetComponent<HealthComponent>();
        _playerController = GetComponent<PlayerController>();
        _skillController  = GetComponent<PlayerSkillController>();
        _targeting        = GetComponent<PlayerTargeting>();
        _rb               = GetComponent<Rigidbody>();
        _animator         = GetComponent<Animator>();
        _cameraController = FindFirstObjectByType<RPGCameraController>();

        if (_playerController == null)
            Debug.LogWarning("[PlayerDeathHandler] PlayerController が見つかりません。");
        if (_skillController == null)
            Debug.LogWarning("[PlayerDeathHandler] PlayerSkillController が見つかりません。");
        if (_targeting == null)
            Debug.LogWarning("[PlayerDeathHandler] PlayerTargeting が見つかりません。");
        if (_rb == null)
            Debug.LogWarning("[PlayerDeathHandler] Rigidbody が見つかりません。");
        if (_animator == null)
            Debug.LogWarning("[PlayerDeathHandler] Animator が見つかりません。死亡アニメーションは再生されません。");
        if (_cameraController == null)
            Debug.LogWarning("[PlayerDeathHandler] RPGCameraController が見つかりません。右クリック朝向制御を無効化できません。");
    }

    private void OnEnable()  => _health.OnDied += HandlePlayerDied;
    private void OnDisable() => _health.OnDied -= HandlePlayerDied;

    private void HandlePlayerDied()
    {
        if (_isDeadHandled) return;
        _isDeadHandled = true;

        // 禁用移动输入
        if (_playerController != null)
            _playerController.enabled = false;

        // 禁用攻击输入
        if (_skillController != null)
            _skillController.enabled = false;

        // 禁用目标选择
        if (_targeting != null)
        {
            _targeting.ClearTarget();
            _targeting.enabled = false;
        }

        // 禁用右クリック朝向制御（RPGCameraController を無効化）
        if (_cameraController != null)
            _cameraController.enabled = false;

        // 停止 Rigidbody（Unity 6 使用 linearVelocity）
        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
        }

        // 死亡アニメーション再生
        if (_animator != null)
            _animator.SetTrigger("IsDead");

        Debug.Log("[PlayerDeathHandler] Player died. Controls disabled.");
    }

    /// <summary>
    /// 复活专用：将 PlayerDeathHandler 在死亡时修改过的玩家状态恢复为可操作状态。
    /// 不恢复 HP、不传送玩家、不处理 UI。
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

        if (_targeting != null)
            _targeting.enabled = true;

        if (_cameraController != null)
            _cameraController.enabled = true;

        Debug.Log("[PlayerDeathHandler] Player reset for respawn.");
    }
}
