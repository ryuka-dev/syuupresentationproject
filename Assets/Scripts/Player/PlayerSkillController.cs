using UnityEngine;

/// <summary>
/// 玩家技能 / 动作输入调度器（レガシー）。
///
/// 注意: 1 / 4 の入力分発は PlayerSkillManager に移行済み。
/// このスクリプトは PlayerDeathHandler が disabled 状態を管理するために残す。
/// </summary>
public class PlayerSkillController : MonoBehaviour
{
    // 1 / 4 の入力は PlayerSkillManager.HandleSkillInput() が管理する。
    // 二重トリガーを防ぐため、このクラスでは何もしない。
}
