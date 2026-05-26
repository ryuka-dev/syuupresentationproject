using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人死亡时生成掉落物。
/// - ItemData 掉落（drops リスト / レガシー単一 dropItem）
/// - Gold 掉落（GoldPickup Prefab）
/// 茶 Buff 対応：PlayerTeaBuffController から掉率倍率 / Material 額外数量概率を取得する。
/// Gold 掉落は茶 Buff の影響を受けない。
/// </summary>
public class EnemyDropper : MonoBehaviour
{
    // ─── 複数掉落条目 ──────────────────────────────────────────
    [System.Serializable]
    public class DropEntry
    {
        public ItemData item;
        [Range(0f, 1f)] public float dropChance = 1f;
        public Vector3 offset;
    }

    [Header("掉落列表（新）")]
    [SerializeField] private List<DropEntry> drops = new List<DropEntry>();

    [Header("单物品掉落（旧 fallback）")]
    [SerializeField] private ItemData dropItem;

    [Header("拾取物 Prefab")]
    [SerializeField] private PickupItem pickupPrefab;
    [SerializeField] private Vector3    dropOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Ground Placement")]
    [SerializeField] private bool      alignDropsToGround        = true;
    [SerializeField] private float     groundRaycastStartHeight  = 5f;
    [SerializeField] private float     groundRaycastDistance     = 20f;
    [SerializeField] private float     groundOffset              = 0.1f;
    [SerializeField] private LayerMask groundLayerMask           = ~0;
    [SerializeField] private float     maxDropHeightAboveOwnerBounds = 0.2f;

    [Header("Gold Drop")]
    [Tooltip("有効にすると敵死亡時に金貨を掉落する")]
    [SerializeField] private bool       dropGold;
    [SerializeField, Range(0f, 1f)] private float goldDropChance = 0f;
    [SerializeField] private int        goldMin           = 1;
    [SerializeField] private int        goldMax           = 5;
    [SerializeField] private GameObject goldPickupPrefab;
    [SerializeField] private Vector3    goldDropOffset    = Vector3.zero;

    // ── キャッシュ ──────────────────────────────────────────────
    private HealthComponent         _health;
    private PlayerTeaBuffController _teaBuffController;

    // ── Lifecycle ───────────────────────────────────────────────
    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        if (_health == null)
            Debug.LogWarning("[EnemyDropper] HealthComponent not found.");
    }

    private void OnEnable()  { if (_health != null) _health.OnDied += HandleDied; }
    private void OnDisable() { if (_health != null) _health.OnDied -= HandleDied; }

    private void OnValidate()
    {
        if (groundRaycastStartHeight      < 0f)    groundRaycastStartHeight      = 0f;
        if (groundRaycastDistance         < 0.1f)  groundRaycastDistance         = 0.1f;
        if (groundOffset                  < 0f)    groundOffset                  = 0f;
        if (maxDropHeightAboveOwnerBounds < 0f)    maxDropHeightAboveOwnerBounds = 0f;
    }

    private PlayerTeaBuffController GetTeaBuffController()
    {
        if (_teaBuffController == null)
            _teaBuffController = FindFirstObjectByType<PlayerTeaBuffController>();
        return _teaBuffController;
    }

    // ── 掉落メイン ──────────────────────────────────────────────
    private void HandleDied()
    {
        var teaBuff = GetTeaBuffController();
        float dropChanceMult      = teaBuff != null ? teaBuff.GetNonGuaranteedDropChanceMultiplier() : 1f;
        float materialExtraChance = teaBuff != null ? teaBuff.GetMaterialExtraQuantityChance()       : 0f;

        // ── ItemData drops リスト ──────────────────────────────
        if (drops != null && drops.Count > 0)
        {
            if (pickupPrefab == null)
                Debug.LogWarning("[EnemyDropper] pickupPrefab is not assigned. Item drops skipped.");
            else
            {
                foreach (var entry in drops)
                {
                    if (entry.item == null) { Debug.LogWarning("[EnemyDropper] DropEntry has null item."); continue; }

                    float finalChance = entry.dropChance < 1f
                        ? Mathf.Clamp01(entry.dropChance * dropChanceMult)
                        : entry.dropChance;

                    if (Random.value <= finalChance)
                    {
                        Vector3 candidate = transform.position + dropOffset + entry.offset;
                        Vector3 pos       = GetGroundedDropPosition(candidate);
                        SpawnDrop(entry.item, pos);

                        if (entry.item.ItemType == ItemType.Material && materialExtraChance > 0f)
                        {
                            if (Random.value < materialExtraChance)
                            {
                                Vector3 extraPos = GetGroundedDropPosition(candidate + new Vector3(0.3f, 0f, 0.3f));
                                SpawnDrop(entry.item, extraPos);
                                Debug.Log($"[EnemyDropper] Material extra drop: {entry.item.ItemName}");
                            }
                        }
                    }
                }
            }
        }
        // ── fallback: 単一 dropItem ────────────────────────────
        else if (dropItem != null)
        {
            if (pickupPrefab == null)
                Debug.LogWarning("[EnemyDropper] pickupPrefab is not assigned.");
            else
            {
                Vector3 pos = GetGroundedDropPosition(transform.position + dropOffset);
                SpawnDrop(dropItem, pos);
                Debug.Log($"[EnemyDropper] Dropped (legacy): {dropItem.ItemName}");
            }
        }

        // ── Gold Drop（アイテム掉落とは独立して判定。茶 Buff の影響を受けない） ──
        SpawnGoldDrop();
    }

    // ── ItemDrop 生成 ───────────────────────────────────────────
    private void SpawnDrop(ItemData item, Vector3 pos)
    {
        PickupItem dropped = Instantiate(pickupPrefab, pos, Quaternion.identity);
        dropped.SetItemData(item);
        Debug.Log($"[EnemyDropper] Dropped: {item.ItemName} at {pos}");
    }

    // ── GoldPickup 生成 ─────────────────────────────────────────
    private void SpawnGoldDrop()
    {
        if (!dropGold) return;
        if (goldPickupPrefab == null)
        {
            Debug.LogWarning("[EnemyDropper] goldPickupPrefab is not assigned. Gold will not drop.");
            return;
        }
        if (Random.value > goldDropChance) return;

        int min    = Mathf.Min(goldMin, goldMax);
        int max    = Mathf.Max(goldMin, goldMax);
        int amount = Mathf.Max(1, Random.Range(min, max + 1));

        Vector3 candidate = transform.position + dropOffset + goldDropOffset;
        Vector3 pos       = GetGroundedDropPosition(candidate);

        var go     = Instantiate(goldPickupPrefab, pos, Quaternion.identity);
        var pickup = go.GetComponent<GoldPickup>();
        if (pickup != null)
            pickup.SetAmount(amount);
        else
            Debug.LogWarning($"[EnemyDropper] goldPickupPrefab has no GoldPickup component.");

        Debug.Log($"[EnemyDropper] Gold dropped: {amount} at {pos}");
    }

    // ── 地面への配置 ────────────────────────────────────────────
    private Vector3 GetGroundedDropPosition(Vector3 candidatePosition)
    {
        if (!alignDropsToGround) return candidatePosition;

        Vector3 rayStart = candidatePosition + Vector3.up * groundRaycastStartHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
                            groundRaycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            float maxAllowedY = GetMaxAllowedDropGroundY();
            if (hit.point.y <= maxAllowedY)
                return hit.point + Vector3.up * groundOffset;
            return candidatePosition;
        }
        return candidatePosition;
    }

    private float GetMaxAllowedDropGroundY()
    {
        float maxBoundsY = float.MinValue;
        bool  found      = false;
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger) continue;
            float top = col.bounds.max.y;
            if (top > maxBoundsY) { maxBoundsY = top; found = true; }
        }
        return found ? maxBoundsY + maxDropHeightAboveOwnerBounds
                     : transform.position.y + maxDropHeightAboveOwnerBounds;
    }
}
