using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人死亡时生成掉落物，并将 ItemData 注入到 PickupItem 中。
/// drops リストが設定されている場合は各エントリを確率判定で生成する。
/// drops が空の場合は旧来の dropItem を 100% 掉落 (fallback)。
/// </summary>
public class EnemyDropper : MonoBehaviour
{
    // ─── 新: 複数掉落条目 ──────────────────────────────────────
    [System.Serializable]
    public class DropEntry
    {
        public ItemData item;
        [Range(0f, 1f)] public float dropChance = 1f;
        public Vector3 offset;
    }

    [Header("掉落列表（新）")]
    [SerializeField] private List<DropEntry> drops = new List<DropEntry>();

    // ─── 旧: 単一掉落（fallback 兼容） ─────────────────────────
    [Header("单物品掉落（旧 fallback，drops 为空时使用）")]
    [SerializeField] private ItemData dropItem;

    // ─── 共通 ──────────────────────────────────────────────────
    [Header("拾取物 Prefab")]
    [SerializeField] private PickupItem pickupPrefab;
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.2f, 0f);

    private HealthComponent _health;

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        if (_health == null)
            Debug.LogWarning("[EnemyDropper] HealthComponent not found on this GameObject.");
    }

    private void OnEnable()
    {
        if (_health != null) _health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (pickupPrefab == null)
        {
            Debug.LogWarning("[EnemyDropper] pickupPrefab is not assigned. No item will drop.");
            return;
        }

        // ─── drops リストが設定されている場合 ──────────────────
        if (drops != null && drops.Count > 0)
        {
            foreach (var entry in drops)
            {
                if (entry.item == null)
                {
                    Debug.LogWarning("[EnemyDropper] DropEntry has null item, skipping.");
                    continue;
                }
                if (Random.value <= entry.dropChance)
                {
                    Vector3 pos = transform.position + dropOffset + entry.offset;
                    PickupItem dropped = Instantiate(pickupPrefab, pos, Quaternion.identity);
                    dropped.SetItemData(entry.item);
                    Debug.Log($"[EnemyDropper] Dropped: {entry.item.ItemName} (chance={entry.dropChance:P0})");
                }
            }
            return;
        }

        // ─── fallback: 旧来の単一 dropItem ─────────────────────
        if (dropItem == null)
        {
            Debug.LogWarning("[EnemyDropper] dropItem is not assigned and drops list is empty. No item will drop.");
            return;
        }

        Vector3 spawnPos = transform.position + dropOffset;
        PickupItem legacy = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
        legacy.SetItemData(dropItem);
        Debug.Log($"[EnemyDropper] Dropped (legacy): {dropItem.ItemName}");
    }
}
