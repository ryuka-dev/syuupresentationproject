using UnityEngine;

/// <summary>
/// 敌人死亡时在原地生成固定掉落物，并将 ItemData 注入到 PickupItem 中。
/// 挂载在敌人 Prefab 上，与 HealthComponent 配合使用。
/// </summary>
public class EnemyDropper : MonoBehaviour
{
    [Header("掉落设置")]
    [SerializeField] private ItemData dropItem;
    [SerializeField] private PickupItem pickupPrefab;
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.2f, 0f);

    private HealthComponent _health;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();

        if (_health == null)
        {
            Debug.LogWarning("[EnemyDropper] HealthComponent not found on this GameObject.");
        }
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        if (dropItem == null)
        {
            Debug.LogWarning("[EnemyDropper] dropItem is not assigned. No item will drop.");
            return;
        }

        if (pickupPrefab == null)
        {
            Debug.LogWarning("[EnemyDropper] pickupPrefab is not assigned. No item will drop.");
            return;
        }

        Vector3 spawnPosition = transform.position + dropOffset;
        PickupItem dropped = Instantiate(pickupPrefab, spawnPosition, Quaternion.identity);
        dropped.SetItemData(dropItem);
    }
}
