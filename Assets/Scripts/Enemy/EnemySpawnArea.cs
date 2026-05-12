using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 区域刷怪组件 第一版。
/// 挂载在场景 GameObject 上，在 spawnRadius 内的 NavMesh 随机点生成敌人，
/// 并通过 EnemyAI.SetSpawnAreaContext 注入区域中心 / 游荡范围 / 追逐脱战范围。
/// 死亡后经 respawnInterval 延迟补怪，同时存活数量不超过 maxAliveCount。
/// </summary>
public class EnemySpawnArea : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("可以生成的敌人 Prefab 列表（第一版随机选取，不支持权重）")]
    [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();
    [Tooltip("区域内同时存活敌人上限")]
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
    }

    // ─── 生成管理 ──────────────────────────────────────────────
    /// <summary>清理 _aliveEnemies 中已销毁（null）或已死亡的条目。</summary>
    private void CleanupAliveList()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject e = _aliveEnemies[i];
            if (e == null)
            {
                _aliveEnemies.RemoveAt(i);
                continue;
            }
            HealthComponent h = e.GetComponent<HealthComponent>();
            if (h != null && h.IsDead)
                _aliveEnemies.RemoveAt(i);
        }
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
    /// 在区域内随机 NavMesh 点生成一只敌人，注入 SpawnArea 上下文，并订阅死亡事件。
    /// 成功返回 true，失败返回 false。
    /// </summary>
    private bool TrySpawnOneEnemy()
    {
        if (_aliveEnemies.Count >= maxAliveCount) return false;

        // ── 选择有效 prefab ────────────────────────────────────
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: enemyPrefabs が設定されていません。");
            return false;
        }

        var validPrefabs = new List<GameObject>(enemyPrefabs.Count);
        foreach (GameObject p in enemyPrefabs)
            if (p != null) validPrefabs.Add(p);

        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: 有効な enemyPrefab がありません（全て null）。");
            return false;
        }

        GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

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
            // lambda でキャプチャするためローカル変数に保持
            GameObject capturedEnemy = enemy;
            health.OnDied += () => HandleEnemyDied(capturedEnemy);
        }
        else
        {
            Debug.LogWarning($"[EnemySpawnArea] {gameObject.name}: {enemy.name} に HealthComponent がありません。死亡を検知できません。");
        }

        _aliveEnemies.Add(enemy);
        Debug.Log($"[EnemySpawnArea] {gameObject.name}: {enemy.name} を {spawnPosition} に生成。(alive={_aliveEnemies.Count}/{maxAliveCount})");
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
        // 全試行失敗
        position = transform.position;
        return false;
    }

    // ─── 死亡 / 補充 ────────────────────────────────────────────
    /// <summary>敌人死亡时回调：从 aliveEnemies 移除，并启动补怪协程。</summary>
    private void HandleEnemyDied(GameObject enemy)
    {
        _aliveEnemies.Remove(enemy);
        Debug.Log($"[EnemySpawnArea] {gameObject.name}: {enemy.name} が死亡。(alive={_aliveEnemies.Count}/{maxAliveCount})");

        // SpawnArea が有効なら補怪タイマーを開始
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
