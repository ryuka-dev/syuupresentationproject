using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人死亡时生成掉落物，并将 ItemData 注入到 PickupItem 中。
/// drops リストが設定されている場合は各エントリを確率判定で生成する。
/// drops が空の場合は旧来の dropItem を 100% 掉落 (fallback)。
/// alignDropsToGround が有効な場合は Raycast で地面を検出し、掉落物を地面に密着させる。
/// Raycast の命中点が敵自身の Collider 上端より高い場合は無効と判断し candidatePosition へ fallback する。
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

    // ─── Ground Placement ──────────────────────────────────────
    [Header("Ground Placement")]
    [Tooltip("有効にすると Raycast で地面を検出し、掉落物を地面に密着させる")]
    [SerializeField] private bool alignDropsToGround = true;
    [Tooltip("候補位置から上方にどれだけ Raycast 開始点を上げるか（m）")]
    [SerializeField] private float groundRaycastStartHeight = 5f;
    [Tooltip("Raycast の最大検出距離（m）")]
    [SerializeField] private float groundRaycastDistance = 20f;
    [Tooltip("地面ヒット点から掉落物を浮かせるオフセット（m）")]
    [SerializeField] private float groundOffset = 0.1f;
    [Tooltip("地面として判定する Layer。デフォルト（~0）は全レイヤー対象")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [Tooltip("敵自身の Collider 上端より何 m 高い命中まで有効とみなすか。斜面・地形起伏の許容誤差。")]
    [SerializeField] private float maxDropHeightAboveOwnerBounds = 0.2f;

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

    private void OnValidate()
    {
        if (groundRaycastStartHeight      < 0f)   groundRaycastStartHeight      = 0f;
        if (groundRaycastDistance         < 0.1f)  groundRaycastDistance         = 0.1f;
        if (groundOffset                  < 0f)   groundOffset                  = 0f;
        if (maxDropHeightAboveOwnerBounds < 0f)   maxDropHeightAboveOwnerBounds = 0f;
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
                    Vector3 candidate = transform.position + dropOffset + entry.offset;
                    Vector3 pos       = GetGroundedDropPosition(candidate);
                    PickupItem dropped = Instantiate(pickupPrefab, pos, Quaternion.identity);
                    dropped.SetItemData(entry.item);
                    Debug.Log($"[EnemyDropper] Dropped: {entry.item.ItemName} at {pos} (chance={entry.dropChance:P0})");
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

        Vector3 legacyCandidate = transform.position + dropOffset;
        Vector3 spawnPos        = GetGroundedDropPosition(legacyCandidate);
        PickupItem legacy = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
        legacy.SetItemData(dropItem);
        Debug.Log($"[EnemyDropper] Dropped (legacy): {dropItem.ItemName} at {spawnPos}");
    }

    /// <summary>
    /// 候補位置から上方へ Raycast し、地面を検出した場合は
    /// ヒット点の groundOffset 分上の座標を返す。
    /// ヒット点が敵自身の Collider 上端より高い場合は無効と判断し candidatePosition を返す。
    /// alignDropsToGround が false、または未命中の場合も candidatePosition をそのまま返す。
    /// </summary>
    private Vector3 GetGroundedDropPosition(Vector3 candidatePosition)
    {
        if (!alignDropsToGround)
            return candidatePosition;

        Vector3 rayStart = candidatePosition + Vector3.up * groundRaycastStartHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
                            groundRaycastDistance, groundLayerMask,
                            QueryTriggerInteraction.Ignore))
        {
            // 命中点が敵自身の Collider 上端より高い場合は頭上の物体に当たっていると判断し無効扱い
            float maxAllowedY = GetMaxAllowedDropGroundY();
            if (hit.point.y <= maxAllowedY)
            {
                return hit.point + Vector3.up * groundOffset;
            }

            // 命中点が高すぎる → 頭上物体への誤命中とみなし fallback
            return candidatePosition;
        }

        // 未命中：候補位置にそのまま生成（掉落を阻断しない）
        return candidatePosition;
    }

    /// <summary>
    /// Raycast 命中点として許容できる最大 Y 座標を返す。
    /// 敵自身（子含む）の非 Trigger Collider の bounds.max.y の最大値 + maxDropHeightAboveOwnerBounds。
    /// Collider が見つからない場合は transform.position.y + maxDropHeightAboveOwnerBounds を使用する。
    /// </summary>
    private float GetMaxAllowedDropGroundY()
    {
        float maxBoundsY = float.MinValue;
        bool  found      = false;

        // 自身および子オブジェクトの非 Trigger Collider を走査
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger) continue;
            float top = col.bounds.max.y;
            if (top > maxBoundsY)
            {
                maxBoundsY = top;
                found      = true;
            }
        }

        if (found)
            return maxBoundsY + maxDropHeightAboveOwnerBounds;

        // Collider が取得できない場合は自身の Y 座標を基準にする
        return transform.position.y + maxDropHeightAboveOwnerBounds;
    }
}
