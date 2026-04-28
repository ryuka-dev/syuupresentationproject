using UnityEngine;

/// <summary>
/// 阵营定义
/// </summary>
public enum Faction
{
    Player,
    Skeleton,
    Goblin,
    Dragon
}

/// <summary>
/// 阵营组件 - 附加到任何需要识别阵营的对象上
/// </summary>
public class FactionComponent : MonoBehaviour
{
    public Faction faction = Faction.Player;

    /// <summary>
    /// 检查是否应该攻击目标
    /// </summary>
    public bool ShouldAttack(Faction targetFaction)
    {
        return (faction == Faction.Skeleton && targetFaction == Faction.Player) ||
               (faction == Faction.Player && targetFaction == Faction.Skeleton) ||
               (faction == Faction.Goblin && targetFaction == Faction.Player) ||
               (faction == Faction.Player && targetFaction == Faction.Goblin);
    }
}
