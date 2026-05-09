using System.Collections;
using UnityEngine;

/// <summary>
/// 正式野怪刷新点（第一版）。
/// 一个刷怪点管理一只敌人。敌人死亡后延迟刷新，刷新出的敌人自动注册到 LevelObjectiveManager。
/// 挂载在场景中的空 GameObject 上，将 enemyPrefab 指定为骷髅 Prefab。
/// </summary>
public class EnemySpawnPoint : MonoBehaviour
{
    [Header("生成设置")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float      respawnDelay = 10f;
    [SerializeField] private bool       spawnOnStart = true;

    // ── 运行时状态 ────────────────────────────────────────
    private GameObject    _currentEnemy;
    private HealthComponent _currentEnemyHealth;
    private System.Action   _currentEnemyDiedHandler;
    private bool            _isRespawning;

    // ── 生命周期 ─────────────────────────────────────────
    private void Start()
    {
        if (spawnOnStart)
            SpawnEnemy();
    }

    private void OnDisable()
    {
        // 無効化時に OnDied 購読が残らないようにクリア
        if (_currentEnemyHealth != null && _currentEnemyDiedHandler != null)
        {
            _currentEnemyHealth.OnDied -= _currentEnemyDiedHandler;
            _currentEnemyDiedHandler    = null;
        }
    }

    // ── 生成 ─────────────────────────────────────────────
    /// <summary>
    /// 刷怪点の位置/朝向で敌人を生成する。
    /// EnemyAI.Awake() が Instantiate 直後に位置を記録するため、
    /// 生成後に位置を変えてはいけない。
    /// </summary>
    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"[EnemySpawnPoint] {gameObject.name}: enemyPrefab が設定されていません。");
            return;
        }
        if (_currentEnemy != null) return;  // 既に生存中

        _currentEnemy = Instantiate(enemyPrefab, transform.position, transform.rotation);

        _currentEnemyHealth = _currentEnemy.GetComponent<HealthComponent>();
        if (_currentEnemyHealth != null)
        {
            _currentEnemyDiedHandler = HandleCurrentEnemyDied;
            _currentEnemyHealth.OnDied += _currentEnemyDiedHandler;
        }

        // LevelObjectiveManager に登録
        var levelManager = FindFirstObjectByType<LevelObjectiveManager>();
        if (levelManager != null && _currentEnemyHealth != null)
            levelManager.RegisterEnemy(_currentEnemyHealth);

        Debug.Log($"[EnemySpawnPoint] {gameObject.name}: 敌人生成完毕 ({_currentEnemy.name})。");
    }

    // ── 死亡処理 ──────────────────────────────────────────
    /// <summary>
    /// 管理中の敌人が死亡した時に呼ばれる。
    /// 実際の Destroy は EnemyDeathHandler が行うため、ここでは参照クリアと刷新カウントダウンだけ行う。
    /// </summary>
    private void HandleCurrentEnemyDied()
    {
        // 购読を解除してから参照をクリア
        if (_currentEnemyHealth != null && _currentEnemyDiedHandler != null)
        {
            _currentEnemyHealth.OnDied -= _currentEnemyDiedHandler;
            _currentEnemyDiedHandler    = null;
        }
        _currentEnemyHealth = null;
        _currentEnemy       = null;

        if (!_isRespawning)
            StartCoroutine(RespawnAfterDelay());
    }

    // ── 刷新待ち ─────────────────────────────────────────
    private IEnumerator RespawnAfterDelay()
    {
        _isRespawning = true;
        Debug.Log($"[EnemySpawnPoint] {gameObject.name}: {respawnDelay} 秒後に刷新します。");
        yield return new WaitForSeconds(respawnDelay);
        _isRespawning = false;
        SpawnEnemy();
    }
}
