using UnityEngine;

/// <summary>
/// 视锥（FOV）检测 - 从眼睛看正前方的三角形区域
/// </summary>
public class FOVDetector : MonoBehaviour
{
    [Header("视锥参数")]
    public float detectionDistance = 15f;      // 探知距离
    public float fovAngle = 60f;               // 视野角度（度）
    public Transform eyePosition;              // 眼睛位置（通常是头部）
    public FactionComponent factionComponent;

    void OnEnable()
    {
        if (eyePosition == null) eyePosition = transform;
        if (factionComponent == null) factionComponent = GetComponent<FactionComponent>();
    }

    /// <summary>
    /// 检测目标是否在视锥内
    /// </summary>
/// <summary>
    /// 检测目标是否在视锥内
    /// </summary>
    public bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;

        // 使用身体朝向而不是眼睛朝向
        Vector3 bodyForward = transform.forward;
        Vector3 bodyPosition = transform.position;
        Vector3 dirToTarget = (target.position - bodyPosition).normalized;
        float angleToTarget = Vector3.Angle(bodyForward, dirToTarget);
        float distToTarget = Vector3.Distance(bodyPosition, target.position);

        // 在视锥内 && 在探知距离内 && 该目标的阵营应该被攻击
        var targetFaction = target.GetComponent<FactionComponent>();
        if (targetFaction == null) return false;

        return angleToTarget <= fovAngle * 0.5f && 
               distToTarget <= detectionDistance && 
               factionComponent.ShouldAttack(targetFaction.faction);
    }

    void OnDrawGizmos()
    {
        if (!enabled) return;
        var eye = eyePosition ?? transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(eye.position, eye.forward * detectionDistance);
    }
}
