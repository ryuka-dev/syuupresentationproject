using UnityEngine;

/// <summary>
/// 场景中全体敌人的全局命令入口（第一版）。
/// 职责：对所有活着且 AI 启用的敌人下达统一指令。
/// 今后可以在此扩展：注册缓存、刷新管理、掉落触发等。
/// 挂载在场景中名为 EnemyWorldManager 的空 GameObject 上。
/// </summary>
public class EnemyWorldManager : MonoBehaviour
{
    /// <summary>
    /// 命令场景中所有活着且 AI 启用的敌人脱战并走回出生点。
    /// 玩家死亡时由 PlayerDeathHandler 调用。
    /// </summary>
    public void ForceAllLivingEnemiesReturnToSpawn()
    {
        var allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var enemy in allEnemies)
        {
            enemy.ForceDisengageAndReturnToSpawn();
            count++;
        }

        Debug.Log($"[EnemyWorldManager] 命令了 {count} 个敌人返回出生点。");
    }
}
