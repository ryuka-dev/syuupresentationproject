using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家技能 / 动作输入调度器。
///
/// 职责：
///   - 读取键盘输入
///   - 按 1 时调用 PlayerBasicAttackController.TrySingleTargetAttack()
///   - 按 4 时调用 PlayerBasicAttackController.TryAreaBasicAttack()
///   - 未来可在此扩展其他动作键的调度（技能、闪避、交互等）
///
/// 不做的事：
///   - 不直接计算伤害
///   - 不直接执行 AOE 搜索
///   - 不管理基础攻击冷却
///   - 不管理技能栏 UI（由 PlayerSkillManager / PlayerSkillBarCanvasUI 负责）
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    // ─── 内部引用 ─────────────────────────────────────────────────
    private PlayerBasicAttackController _basicAttack;

    // ─── Unity 生命周期 ───────────────────────────────────────────

    private void Awake()
    {
        _basicAttack = GetComponent<PlayerBasicAttackController>();
        if (_basicAttack == null)
            Debug.LogWarning("[PlayerSkillController] PlayerBasicAttackController not found on same GameObject.");
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 键 1：单体普通攻击
        if (kb.digit1Key.wasPressedThisFrame)
            _basicAttack?.TrySingleTargetAttack();

        // 键 4：AOE 普通攻击
        if (kb.digit4Key.wasPressedThisFrame)
            _basicAttack?.TryAreaBasicAttack();
    }
}
