using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 区域刷怪组件 第二版。支持 SpawnEntry 权重随机 / 单种最大存活数量。
/// 挂载在场景 GameObject 上，在 spawnRadius 内的 NavMesh 随机点生成敌人，
/// 并通过 EnemyAI.SetSpawnAreaContext 注入区域中心 / 游荡范围 / 追逐脱战范围。
/// 死亡后经 respawnInterval 延迟补怪，同时存活总数不超过 maxAliveCount。
/// </summary>
public class EnemySpawnArea : MonoBehaviour
{
    // ─── SpawnEntry ────────────────────────────────────────────
    [System.Serializable]
    private class SpawnEntry
    {
        [Tooltip("要生成的敌人 Prefab")]
        public GameObject enemyPrefab;
        [Tooltip("加权随机权重（0 = 不参与随机）")]
        [Min(0)] public int weight   = 1;
        [Tooltip("此 Prefab 在区域内同时存活上限（0 = 禁用此条目）")]
        [Min(0)] public int maxAlive = 999;
    }

    [Header("Spawn Entries")]
    [Tooltip("怪物生成条目列表，支持权重与单种最大存活数量")]
    [SerializeField] private List<SpawnEntry> spawnEntries = new List<SpawnEntry>();

    [Header("Spawn Area")]
    [Tooltip("区域内同时存活敌人总上限")]
    [SerializeField] private int   maxAliveCount   = 6;
    [Tooltip("内圈生成半径，等于 Wander 游荡范围")]
    [SerializeField] private float spawnRadius     = 20f;
    [Tooltip("外圈追逐 / 脱战半径，应不小于 spawnRadius")]
    [SerializeField] private float leashRadius     = 35f;
    [Tooltip("敌人死亡后多少秒补一只怪（秒）")]
    [SerializeField] private float respawnInterval = 5f;
    [Tooltip("Play Mode 开始时是否自动刷满")]
    [SerializeField] private bool  spawnOnStart    = true;

    [Header("NavMesh")]
    [Tooltip("NavMesh.SamplePosition 的最大采样距离")]
    [SerializeField] private float navMeshSampleDistance    = 3f;
    [Tooltip("随机生成点最大尝试次数（全部失败则本次放弃生成）")]
    [SerializeField] private int   maxSpawnPositionAttempts = 20;

    // 运行时活着的敌人列表
    private readonly List<GameObject> _aliveEnemies = new List<GameObject>();
    // 记录每个活着的敌人使用的原始 Prefab，用于统计单种存活数量
    private readonly Dictionary<GameObject, GameObject> _spawnedPrefabByEnemy
        = new Dictionary<GameObject, GameObject>();

    // ─── 生命周期 ─────────────────────────────────────────────
    private void Start()
    {
        if (spawnOnStart)
            FillToMaxAlive();
    }

    private void OnValidate()
    {
        maxAliveCount            = Mathf.Max(0, maxAliveCount);
        spawnRadius              = Mathf.Max(0f, spawnRadius);
        leashRadius              = Mathf.Max(spawnRadius, leashRadius);
        respawnInterval          = Mathf.Max(0f, respawnInterval);
        navMeshSampleDistance    = Mathf.Max(0.1f, navMeshSampleDistance);
        maxSpawnPositionAttempts = Mathf.Max(1, maxSpawnPositionAttempts);

        if (spawnEntries == null) return;
        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry == null) continue;
            entry.weight   = Mathf.Max(0, entry.weight);
            entry.maxAlive = Mathf.Max(0, entry.maxAlive);
        }
    }

    // ─── 生成管理 ──────────────────────────────────────────────
    /// <summary>清理 _aliveEnemies 中已销毁（null）或已死亡的条目，并同步 dictionary。</summary>
    private void CleanupAliveList()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject e = _aliveEnemies[i];
            if (e == null)
            {
                // null キーは Dictionary で使用不可のためスキップ（HandleEnemyDied で Remove 済み）
                _aliveEnemies.RemoveAt(i);
                continue;
            }
            HealthComponent h = e.GetComponent<HealthComponent>();
            if (h != null && h.IsDead)
            {
                _aliveEnemies.RemoveAt(i);
                _spawnedPrefabByEnemy.Remove(e);
            }
        }
    }

    /// <summary>统计指定 Prefab 在区域内当前存活数量。</summary>
    private int CountAliveForPrefab(GameObject prefab)
    {
        int count = 0;
        foreach (KeyValuePair<GameObject, GameObject> kv in _spawnedPrefabByEnemy)
        {
            if (kv.Key != null && kv.Value == prefab)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 从 spawnEntries 中按权重加权随机选出一个可生成的条目。
    /// 排除 weight <= 0 / maxAlive <= 0 / 已达单种存活上限的条目。
    /// 全部条目达到上限时返回 false。
    /// </summary>
    private bool TryPickSpawnEntry(out SpawnEntry selectedEntry)
    {
        selectedEntry = null;

        if (spawnEntries == null || spawnEntries.Count == 0) return false;

        var candidates  = new List<SpawnEntry>();
        int totalWeight = 0;

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry == null)             continue;
            if (entry.enemyPrefab == null) continue;
            if (entry.weight  <= 0)        continue;
            if (entry.maxAlive <= 0)       continue;
            if (CountAliveForPrefab(entry.enemyPrefab) >= entry.maxAlive) continue;

            candidates.Add(entry);
            totalWeight += entry.weight;
        }

        if (totalWeight <= 0) return false;

        int roll       = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (SpawnEntry entry in candidates)
        {
            cumulative += entry.weight;
            if (roll < cumulative)
            {
                selectedEntry = entry;
                return true;
            }
        }
        return false;
    }

    /// <summary>持续生成直到达到 maxAliveCount，或生成失败为止。</summary>
    private void FillToMaxAlive()
    {
        CleanupAliveList();
        while (_aliveEnemies.Count < maxAliveCount)
        {
            if (!TrySpawnOneEnemy())
                break;
        }
    }

    /// <summary>
    /// 从 SpawnEntry 加权随机选择 Prefab，在区域内随机 NavMesh 点生成一只敌人，
    /// 注入 SpawnArea 上下文，并订阅死亡事件。成功返回 true，失败返回 false。
    /// </summary>
    private bool TrySpawnOneEnemy()
    {
        if (_aliveEnemies.Count >= maxAliveCount) return false;

        // ── 加权随机选择 SpawnEntry ────────────────────────────
        if (!TryPickSpawnEntry(out SpawnEntry selectedEntry))
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: 生成可能な SpawnEntry がありません。");
            return false;
        }

        GameObject prefab = selectedEntry.enemyPrefab;

        // ── 生成位置を NavMesh 上で決定 ────────────────────────
        if (!TryGetRandomSpawnPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: NavMesh 上の生成位置が見つかりませんでした（試行 {maxSpawnPositionAttempts} 回）。");
            return false;
        }

        Quaternion spawnRotation = transform.rotation;

        // ── Instantiate ────────────────────────────────────────
        GameObject enemy = Instantiate(prefab, spawnPosition, spawnRotation);

        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI == null)
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: 生成した {enemy.name} に EnemyAI がありません。破棄します。");
            Destroy(enemy);
            return false;
        }

        // ── SpawnArea コンテキストを注入 ───────────────────────
        enemyAI.SetSpawnAreaContext(
            transform.position,  // areaCenter
            spawnRadius,         // areaWanderRadius
            leashRadius,         // areaLeashRadius
            spawnPosition,       // spawnPosition（ReturnToSpawn の帰還先）
            spawnRotation);      // spawnRotation（ReturnToSpawn 後の朝向）

        // ── 死亡イベントを購読 ─────────────────────────────────
        HealthComponent health = enemy.GetComponent<HealthComponent>();
        if (health != null)
        {
            GameObject capturedEnemy = enemy;
            health.OnDied += () => HandleEnemyDied(capturedEnemy);
        }
        else
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: {enemy.name} に HealthComponent がありません。死亡を検知できません。");
        }

        // ── alive list と dictionary に登録 ────────────────────
        _aliveEnemies.Add(enemy);
        _spawnedPrefabByEnemy[enemy] = prefab;

        Debug.Log($"[EnemySpawnArea] {gameObject.name}: {enemy.name} (prefab={prefab.name}, weight={selectedEntry.weight}) を {spawnPosition} に生成。(alive={_aliveEnemies.Count}/{maxAliveCount})");
        return true;
    }

    /// <summary>spawnRadius 内の NavMesh 上にランダム位置を探す。成功時は position に座標を設定して true を返す。</summary>
    private bool TryGetRandomSpawnPosition(out Vector3 position)
    {
        for (int i = 0; i < maxSpawnPositionAttempts; i++)
        {
            Vector2 offset    = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
        }
        position = transform.position;
        return false;
    }

    // ─── 死亡 / 補充 ────────────────────────────────────────────
    /// <summary>敌人死亡时回调：从 aliveEnemies 与 prefab 统计中移除，并启动补怪协程。</summary>
    private void HandleEnemyDied(GameObject enemy)
    {
        _aliveEnemies.Remove(enemy);
        _spawnedPrefabByEnemy.Remove(enemy);
        Debug.Log($"[EnemySpawnArea] {gameObject.name}: {enemy.name} が死亡。(alive={_aliveEnemies.Count}/{maxAliveCount})");

        if (gameObject.activeInHierarchy)
            StartCoroutine(RespawnAfterDelay());
    }

    /// <summary>respawnInterval 秒後に FillToMaxAlive を呼び出す。</summary>
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnInterval);
        FillToMaxAlive();
    }

    // ─── Gizmos ─────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        // 内圈：生成 / Wander 范围（绿色）
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(center, spawnRadius);

        // 外圈：追逐 / 脱战范围（橙色）
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(center, leashRadius);
    }
}
