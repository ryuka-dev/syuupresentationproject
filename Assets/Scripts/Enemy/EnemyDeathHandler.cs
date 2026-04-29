using System.Collections;
using UnityEngine;

/// <summary>
/// 敌人死亡处理器。挂载在骷髅敌人上。
/// 监听 HealthComponent.OnDied，死亡时停止 AI、播放死亡动画，动画结束后销毁对象。
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyDeathHandler : MonoBehaviour
{
    [Tooltip("死亡动画结束后到销毁的延迟（秒），与 clip 时长对齐，默认覆盖 root|death 的 1.4s）")]
    public float destroyDelay = 1.6f;

    private HealthComponent  _health;
    private EnemyAI          _enemyAI;
    private Animator         _animator;
    private Rigidbody        _rb;
    private Collider         _collider;
    private bool             _isDying;

    private void Awake()
    {
        _health   = GetComponent<HealthComponent>();
        _enemyAI  = GetComponent<EnemyAI>();
        _animator = GetComponent<Animator>();
        _rb       = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
    }

    private void OnEnable()  => _health.OnDied += HandleDeath;
    private void OnDisable() => _health.OnDied -= HandleDeath;

    private void HandleDeath()
    {
        if (_isDying) return;
        _isDying = true;

        // 停止 AI
        if (_enemyAI != null) _enemyAI.enabled = false;

        // 停止移动
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic    = true;
        }

        // 清除攻击动画，防止死亡瞬间仍在攻击
        if (_animator != null)
        {
            _animator.SetBool("IsAttacking", false);
            _animator.SetTrigger("IsDead");
        }

        // 禁用 Collider，防止继续阻挡/被攻击
        if (_collider != null) _collider.enabled = false;

        StartCoroutine(DestroyAfterDelay());
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}
