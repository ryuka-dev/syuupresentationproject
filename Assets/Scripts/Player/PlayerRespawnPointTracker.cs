using UnityEngine;

/// <summary>
/// 玩家复活点记录器。挂载在 Player 上。
/// 记录当前最近复活点的位置和朝向。
/// 初始值为 Awake 时玩家自身的位置和朝向。
/// </summary>
public class PlayerRespawnPointTracker : MonoBehaviour
{
    public Vector3    CurrentRespawnPosition { get; private set; }
    public Quaternion CurrentRespawnRotation { get; private set; }

    private void Awake()
    {
        CurrentRespawnPosition = transform.position;
        CurrentRespawnRotation = transform.rotation;
        Debug.Log($"[PlayerRespawnPointTracker] Initialized at {CurrentRespawnPosition}");
    }

    /// <summary>
    /// 更新最近复活点。由 SavePoint 在玩家进入触发范围时调用。
    /// </summary>
    public void SetRespawnPoint(Vector3 position, Quaternion rotation)
    {
        CurrentRespawnPosition = position;
        CurrentRespawnRotation = rotation;
        Debug.Log($"[PlayerRespawnPointTracker] Respawn point updated: {position}");
    }
}
