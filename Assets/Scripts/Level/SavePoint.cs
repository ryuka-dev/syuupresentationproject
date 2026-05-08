using UnityEngine;

/// <summary>
/// 复活点。挂载在场景内的复活点对象上。
/// 玩家进入 Trigger 范围后，通知 PlayerRespawnPointTracker 更新最近复活点。
/// 同一个复活点不会重复触发日志。
/// </summary>
public class SavePoint : MonoBehaviour
{
    private bool _hasActivated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasActivated) return;

        var tracker = other.GetComponentInParent<PlayerRespawnPointTracker>();
        if (tracker == null) return;

        _hasActivated = true;
        tracker.SetRespawnPoint(transform.position, transform.rotation);
    }
}
