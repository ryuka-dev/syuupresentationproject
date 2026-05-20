# BalanceTables

这些 CSV 用于第一版 Tier 1 平衡设计。

- content_tiers.csv：章节/区域/内容等级基准
- enemy_balance.csv：敌人 HP、攻击、DPS、技能、Threat
- hikari_skills.csv：Hikari 治疗、BU、HPS、效率、反击
- player_skills.csv：玩家普通攻击、减伤、强化技能
- encounter_budget.csv：安全/标准/危险/过量遭遇预算
- encounter_tests.csv：实际 Play Mode 测试记录表

说明：
- PDU：玩家对敌人的输出单位。Tier 1 中 1 PDU = 20 enemy damage。
- PHU：敌人对玩家的承伤单位。Tier 1 中 1 PHU = 10 player damage。
- BU：Hikari 光负荷单位。1 BU = 5 Burden。
- “未确认”表示需要之后从 Unity Inspector / Prefab / ScriptableObject 中核对，不能猜。
