using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家目标选择系统（New Input System）
/// 挂载在 Player 对象上。
/// </summary>
public class PlayerTargeting : MonoBehaviour
{
    public Transform CurrentTarget { get; private set; }

    private FactionComponent _selfFaction;

    private void Awake()
    {
        _selfFaction = GetComponent<FactionComponent>();
        if (_selfFaction == null)
        {
            Debug.LogWarning("[PlayerTargeting] Player 上找不到 FactionComponent，敌对判断将失效。");
        }
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        if (Camera.main == null)
        {
            Debug.LogWarning("[PlayerTargeting] Camera.main 为空，无法发射射线。");
            return;
        }

        Vector2 mousePos = mouse.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var health  = hit.collider.GetComponentInParent<HealthComponent>();
        var faction = hit.collider.GetComponentInParent<FactionComponent>();

        if (health == null || faction == null)
        {
            Debug.Log("[PlayerTargeting] 点击目标无 HealthComponent 或 FactionComponent，忽略。");
            return;
        }

        bool isHostile = _selfFaction != null && _selfFaction.ShouldAttack(faction.faction);
        if (!isHostile)
        {
            Debug.Log($"[PlayerTargeting] {faction.gameObject.name} 不是敌对目标，忽略。");
            return;
        }

        CurrentTarget = faction.transform;
        Debug.Log($"[PlayerTargeting] 当前目标：{CurrentTarget.name}");
    }


public void ClearTarget()
    {
        CurrentTarget = null;
        Debug.Log("[PlayerTargeting] CurrentTarget cleared.");
    }
}
