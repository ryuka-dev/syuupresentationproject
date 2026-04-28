using UnityEngine;

/// <summary>
/// 游戏启动时配置物理层级规则
/// Player(8) / Enemy(9) 之间保持碰撞（不穿透），但脚本每帧覆盖速度所以不会被推动
/// </summary>
public class PhysicsLayerSetup : MonoBehaviour
{
    void Awake()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer  = LayerMask.NameToLayer("Enemy");

        if (playerLayer < 0 || enemyLayer < 0) {
            Debug.LogError("PhysicsLayerSetup: Player or Enemy layer not found!");
            return;
        }

        // 确保碰撞开启（不穿透）
        Physics.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        Physics.IgnoreLayerCollision(enemyLayer,  enemyLayer,  false);

        // 注：碰撞弹力由 CharacterPhysics.physicsMaterial 控制（bounciness=0）
        // 注：推力由各自脚本每帧覆盖 velocity 来消除
    }
}
