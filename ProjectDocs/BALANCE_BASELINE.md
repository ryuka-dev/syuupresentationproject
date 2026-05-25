# BALANCE_BASELINE.md

最后更新：2026-05-25  
文档用途：记录第一版战斗数值基准、单位系统、Hikari 光负荷模型与遭遇预算。  
术语规则：Hikari 系统正式术语以 `GLOSSARY.md` 为准；本文件只在必要处保留英文 / 代码名作对照。  
注意：本文件不是最终平衡表，而是用于当前原型阶段验证核心玩法是否成立的测试基准。

---

# 1. 文档目的

当前项目已经进入核心玩法验证阶段：

- 玩家作为 Tank 承受伤害
- Hikari 根据玩家状态自动治疗
- Hikari 治疗会增加光负荷
- 玩家正确开减伤承受敌人读条重击时，可以触发守护共鸣 / Guard Resonance
- Hikari 光负荷处于 80%～99% 光溢出时，守护共鸣可额外触发溢光反震 / Overflow Counter

为了避免后续伤害、治疗、反伤、敌人血量、敌人数量都变成“随手填数字”，需要建立第一版统一数值基准。

本文件用于回答以下问题：

- 玩家打敌人时，一个标准伤害单位是多少？
- 敌人打玩家时，一个标准承伤单位是多少？
- Hikari 治疗多少算合理？
- Hikari 每次治疗增加多少光负荷算合理？
- 敌人数量多少算安全、危险、过量？
- Hikari 的治疗能力与敌人总 DPS 应该如何对抗？
- 玩家失败时，是否能明确归因？

---

# 2. 当前版本定位

当前版本属于：

```text
Tier 1 / 第一章 / 第一版野外战斗测试基准
```

当前基准假设：

```text
标准玩家最大 HP = 100
标准玩家普通攻击 = 20
Hikari maxBurden = 100
```

这些数值不是全游戏永久固定值，而是当前内容等级的测试基准。

后续章节、区域或敌人等级提高时，可以建立新的 Content Tier，例如：

```text
Tier 1：标准玩家 HP = 100，标准普通攻击 = 20
Tier 2：标准玩家 HP = 180，标准普通攻击 = 35
Tier 3：标准玩家 HP = 300，标准普通攻击 = 60
```

单位跟随内容等级变化，不跟随玩家当前实时属性变化。

---

# 3. 核心原则

## 3.1 单位不是玩家实时属性百分比

不要设计成：

```text
敌人攻击 = 玩家当前最大 HP 的 10%
```

否则玩家堆血量没有意义。

正确做法是：

```text
当前内容 Tier 假设玩家大约有多少 HP
敌人伤害围绕这个假设值设计
玩家实际更肉，则旧怪自然变弱
玩家实际更脆，则同一怪物更危险
```

例如 Tier 1：

```text
Tier 1 标准玩家 HP = 100
1 PHU = 10 damage
精英怪 CastAttack = 5 PHU = 50 damage
```

如果玩家实际 HP 为 150：

```text
精英怪 CastAttack 仍然是 50 damage
玩家损失 50 / 150 = 33%
```

这是成长带来的优势。

---

# 4. Content Tier / 内容等级

Content Tier 是一个区域、章节、敌人等级或战斗阶段的数值基准。

每个 Tier 至少定义：

```text
标准玩家 HP
标准玩家普通攻击
1 PDU 的实际伤害
1 PHU 的实际伤害
Hikari 治疗技能的实际数值
敌人 HP / DPS / 技能伤害
遭遇预算
```

当前只定义 Tier 1。

---

# 5. PDU / Player Damage Unit

## 5.1 定义

```text
PDU = Player Damage Unit
```

PDU 用于描述：

* 玩家对敌人的普通攻击
* 玩家技能伤害
* 溢光反震 / Overflow Counter 反伤
* 敌人 HP
* Boss HP
* 输出窗口收益
* 战斗时长

当前 Tier 1 定义：

```text
1 PDU = 玩家无装备普通攻击一次
1 PDU = 20 enemy damage
```

## 5.2 当前 Tier 1 基准

```text
玩家普通攻击 = 1 PDU = 20 damage
Radiant Riposte / 守护反击 = 3 PDU = 60 damage
溢光反震 = 1.5 PDU = 30 damage
普通怪 HP = 5 PDU = 100 HP
精英怪 HP = 15～25 PDU = 300～500 HP
小 Boss HP = 40～80 PDU = 800～1600 HP
```

---

# 6. PHU / Player Health Unit

## 6.1 定义

```text
PHU = Player Health Unit
```

PHU 用于描述：

* 敌人对玩家造成的伤害
* 地板伤害
* Boss 大招伤害
* Hikari 治疗量
* 玩家生命容量

PHU 不是玩家当前最大 HP 的百分比，而是当前内容 Tier 的固定承伤单位。

当前 Tier 1 定义：

```text
标准玩家 HP = 100
1 PHU = 10 player damage
标准玩家 HP = 10 PHU
```

## 6.2 当前 Tier 1 基准

```text
普通小怪普通攻击 = 1 PHU = 10 damage
精英怪普通攻击 = 1.5 PHU = 15 damage
精英怪 CastAttack / 读条重击 = 5 PHU = 50 damage
小 Boss 大招 = 6～8 PHU = 60～80 damage
```

---

# 7. BU / Burden Unit

## 7.1 定义

```text
BU = Burden Unit
```

BU 用于描述 Hikari 光负荷。

当前定义：

```text
1 BU = 5 Burden
maxBurden = 100 = 20 BU
```

## 7.2 当前关键阈值

```text
稳定导光：0～15.8 BU = 0%～79%
光溢出：16～19.8 BU = 80%～99%
导光封锁：20 BU = 100%
导光恢复阈值：12 BU = 60%
```

## 7.3 当前光负荷行为

```text
微光治愈 / Light Mend = +1 BU = +5 Burden
紧急祈愿 / Emergency Prayer = +5 BU = +25 Burden
守护共鸣 / Guard Resonance = -2 BU = -10 Burden
自然恢复速度 = 0.2 BU/s = 1 Burden/s
```

---

# 8. 玩家基础数值基准

当前 Tier 1：

```text
玩家基础 HP = 100 = 10 PHU
玩家普通攻击 = 20 = 1 PDU
```

当前建议：

```text
玩家基础 HP 不宜太低，否则 Hikari 治疗和敌人读条技能很难调。
玩家普通攻击应作为所有玩家输出技能的最小参照单位。
```

## 8.1 玩家技能标准距离

当前第一版技能距离统一使用 `PlayerSkillRangeType`：

| 类型 | 用途 | 距离 |
|---|---|---:|
| Self | 自身释放技能 | 0m |
| Melee | 一般近战攻击距离 | 3m |
| Area | 一般玩家 AOE 范围 | 5m |
| Ranged | 一般远距离攻击 | 20m |
| Custom | 个别技能特殊范围 | customRange |

当前玩家技能对应：

```text
Basic Attack = Melee = 3m
Iron Bulwark = Self = 0m
Stone Guard = Self = 0m
Area Attack = Area = 5m
Radiant Riposte = Ranged = 20m
```

说明：

```text
技能执行器应优先读取 PlayerSkillData.EffectiveRange。
不要在不同脚本里分散写死 3m / 5m / 20m。
```

---

# 9. 玩家技能基准

## 9.1 Basic Attack / BasicMeleeAttack

```text
类型：BasicMeleeAttack
距离：Melee = 3m
伤害：1 PDU = 20 enemy damage（Tier 1 无装备基准）
冷却：与 Area Attack 共享基础攻击冷却，当前由 PlayerBasicAttackController.basicAttackRecast 控制
```

说明：

```text
Basic Attack 是所有玩家输出技能的最小参照单位。
当前已注册为 PlayerSkillData，由 PlayerSkillManager 分发，PlayerBasicAttackController 执行。
```

## 9.2 Area Attack / BasicAreaAttack

```text
类型：BasicAreaAttack
距离：Area = 5m
伤害倍率：0.4 × 当前普通攻击最终伤害
冷却：与 Basic Attack 共享基础攻击冷却
```

说明：

```text
Area Attack 是基础攻击变体，不是独立爆发技能。
按 1 或 4 成功后，1 / 4 都应显示同一个共享冷却。
```

## 9.3 Iron Bulwark

当前定位：

```text
短窗口强减伤技能。
用于承受 CastAttack，并授予 Radiant Riposte / 守护反击机会。
```

当前基准：

```text
类型：DamageReduction
距离：Self = 0m
grantsGuardCounter = true
当前持续时间、冷却、减伤倍率以 Skill_IronBulwark.asset Inspector 为准。
```

设计目标：

```text
玩家不开减伤吃读条重击会明显危险。
玩家正确开 Iron Bulwark 后，伤害明显下降，Hikari 触发守护共鸣，并获得 10 秒 Radiant Riposte 窗口。
```

## 9.4 Stone Guard

当前定位：

```text
持续压力用减伤 + 接 Hikari 治疗窗口。
```

当前基准：

```text
类型：DamageReduction
距离：Self = 0m
grantsGuardCounter = false
healingReceivedMultiplier = 1.5
当前持续时间、冷却、减伤倍率以 Skill_StoneGuard.asset Inspector 为准。
```

设计目标：

```text
Stone Guard 可以触发守护共鸣并降低光负荷，但不授予 Radiant Riposte。
Stone Guard Active 期间被 Hikari 治疗时，应明显提高治疗收益。
```

## 9.5 Radiant Riposte / 守护反击

当前定位：

```text
成功处理 CastAttack 后获得的手动反击奖励。
```

当前基准：

```text
类型：GuardCounter
距离：Ranged = 20m
伤害：3 PDU = 60 enemy damage
窗口：Guard Resonance 授权后 10 秒
冷却：无普通 cooldown；限制来自 Guard Resonance 授权窗口
```

设计目标：

```text
玩家不是“亮了就按”，而是正确用 Iron Bulwark 承受关键攻击后，获得一次明确输出奖励。
```

---

# 10. Hikari 治疗模型

Hikari 的治疗不是单纯看治疗量，而要同时看：

```text
治疗量 PHU
光负荷代价 BU
冷却时间
触发条件
是否受光溢出时可控治疗效率下降影响
是否被导光封锁禁止
是否依赖自然恢复速度
```

---

# 11. Hikari 当前技能基准

## 11.1 微光治愈 / Light Mend

```text
治疗量：1.5 PHU = 15 HP
光负荷：+1 BU = +5 Burden
冷却：5 秒
触发条件：玩家 HP < 80%
定位：小治疗，常规维持
```

效率：

```text
治疗效率 = 1.5 PHU / 1 BU = 1.5 PHU per BU
理论 HPS = 1.5 PHU / 5s = 0.3 PHU/s
```

说明：

```text
微光治愈是相对高效的小治疗。
它不应该完全覆盖敌人持续 DPS。
它的作用是缓解压力，而不是让玩家站桩不死。
```

---

## 11.2 紧急祈愿 / Emergency Prayer

```text
治疗量：4.5 PHU = 45 HP
光负荷：+5 BU = +25 Burden
冷却：25 秒
触发条件：玩家 HP < 35%
定位：救命治疗，强但昂贵
```

效率：

```text
治疗效率 = 4.5 PHU / 5 BU = 0.9 PHU per BU
理论 HPS = 4.5 PHU / 25s = 0.18 PHU/s
```

说明：

```text
紧急祈愿的效率低于微光治愈。
它的价值不在持续治疗，而在救场。
频繁触发紧急祈愿代表玩家承伤过高，会快速推高 Hikari 光负荷。
```

---

## 11.3 守护共鸣 / Guard Resonance

```text
治疗量：0 PHU
光负荷变化：-2 BU = -10 Burden
内置冷却：3 秒
触发条件：
- 玩家开启 DamageReduction 技能
- 玩家受到敌人 CastAttack / 读条重击
- 伤害来源不是普通攻击
```

定位：

```text
正确 Tank 行为带来的 Hikari 光负荷释放。
```

说明：

```text
守护共鸣不治疗玩家。
它不应该被普通小伤害触发。
它用于奖励玩家正确处理关键攻击。
一次守护共鸣抵消两次微光治愈的光负荷。
一次守护共鸣相当于 10 秒自然恢复量。
```

---

## 11.4 溢光反震 / Overflow Counter

```text
伤害量：1.5 PDU = 30 enemy damage
光负荷变化：0 BU
触发条件：
- 守护共鸣成功
- 玩家开启 DamageReduction
- 玩家受到敌人 CastAttack / 读条重击
- Hikari 光负荷处于 80%～99% 光溢出
```

定位：

```text
光溢出危险收益。
```

说明：

```text
溢光反震是让 80%～99% 光溢出区间变成“可压线收益”的关键机制。
它比玩家普通攻击强，但不能代替玩家主要输出。
100% 导光封锁时不触发溢光反震。

实现备注：
当前代码或旧文档中可能仍保留 Light Counter 命名，正式术语统一为溢光反震 / Overflow Counter。
```

---

# 12. Hikari 自然恢复基准

当前自然恢复：

```text
1 Burden/s = 0.2 BU/s
```

换算：

```text
1 BU 需要 5 秒自然恢复
微光治愈 +1 BU，大约 5 秒自然恢复抵消
紧急祈愿 +5 BU，大约 25 秒自然恢复抵消
守护共鸣 -2 BU，相当于 10 秒自然恢复
```

## 12.1 普通野外战斗

普通野外战斗建议允许自然恢复。

目的：

```text
避免玩家每场小怪后都进入不可恢复的恶化状态。
允许玩家在低压战斗中逐渐恢复 Hikari 状态。
```

## 12.2 精英 / Boss 战

精英或 Boss 战可以考虑：

```text
降低自然恢复速度
或关闭自然恢复
或只允许通过守护共鸣降低 BU
```

目的：

```text
让玩家正确开减伤承受关键攻击变得更重要。
避免长战斗中自然恢复过强，导致 Hikari 光负荷压力消失。
```

---

# 13. 敌人 DPS 压力模型

敌人压力不只来自单次伤害，还来自：

```text
敌人数量 × 单只敌人 DPS × 敌人存活时间
```

真正要平衡的是：

```text
敌人总 DPS
vs
玩家 HP
vs
Hikari HPS
vs
Hikari BU 增长
vs
玩家减伤
vs
玩家击杀速度
vs
守护共鸣降 BU 能力
```

Hikari 的治疗能力与敌人 DPS 是对抗关系。

目标不是：

```text
Hikari HPS >= 敌人总 DPS
```

而是：

```text
玩家正确操作后，实际承伤压力可以被 Hikari 勉强维持。
玩家错误操作时，Hikari 会救场但光负荷快速恶化。
玩家过量拉怪时，Hikari 无法长期兜底。
```

---

# 14. Enemy DPS Pressure / EDP

第一版可以不用写进代码，但文档中用它辅助设计。

```text
EDP = Enemy DPS Pressure
表示敌人每秒对玩家造成多少 PHU 压力
```

Tier 1 普通小怪示例：

```text
普通攻击伤害：1 PHU
攻击间隔：2 秒
DPS = 0.5 PHU/s
```

则：

```text
1 只普通小怪 = 0.5 PHU/s
2 只普通小怪 = 1.0 PHU/s
3 只普通小怪 = 1.5 PHU/s
4 只普通小怪 = 2.0 PHU/s
```

---

# 15. 敌人类型基准

## 15.1 普通小怪

```text
Threat = 1
HP = 5 PDU = 100 HP
普通攻击 = 1 PHU = 10 damage
攻击间隔建议 = 2.0～2.5 秒
DPS = 0.4～0.5 PHU/s
```

定位：

```text
基础压力来源。
主要用于练习普通攻击、目标选择、掉落循环。
```

---

## 15.2 精英怪 / SkeletonBossEnemy_Variant

```text
Threat = 3
HP = 15～25 PDU = 300～500 HP
普通攻击 = 1.5 PHU = 15 damage
基础 DPS = 0.8～1.0 PHU/s
CastAttack = 5 PHU = 50 damage
CastAttack 冷却建议 = 10～12 秒
CastAttack 读条建议 = 1.5～2.5 秒
```

定位：

```text
第一版核心玩法验证敌人。
用于测试 DamageReduction、守护共鸣、溢光反震。
```

设计目标：

```text
不开减伤吃 CastAttack 会明显危险。
开减伤吃 CastAttack 会明显舒服。
Hikari 光溢出时，正确处理 CastAttack 可以触发溢光反震。
```


## 15.3 Boss AoE 技能基准

### CircleAoE / Boss Shockwave

```text
技能资产：Assets/Skills/Enemy/SK_CircleAoE_BossShockwave.asset
类型：CircleAoE
伤害：30 player damage = 3 PHU
读条：2.5 秒
冷却：12 秒
半径：5m
处理方式：远离 Boss 到范围外
```

说明：

```text
读条期间显示圆形地面提示。
读条结束提示消失后，只按 XZ 平面距离判定一次伤害。
不触发 Guard Resonance / Radiant Riposte。
```

### DonutAoE / Moon Ring / 月环

```text
技能资产：Assets/Skills/Enemy/SK_DonutAoE_MoonRing.asset
类型：DonutAoE
伤害：35 player damage = 3.5 PHU
读条：3.0 秒
冷却：14 秒
内圈安全半径：2.8m
外圈半径：7.0m
处理方式：贴近 Boss 到内圈安全区，或离开外圈之外
```

说明：

```text
提示使用 DonutAoETelegraphController 程序化生成真环形 Mesh。
只渲染伤害环区域，内圈安全区不渲染。
读条结束后按 inner < distance <= outer 判定一次伤害。
不触发 Guard Resonance / Radiant Riposte。
```

---

# 16. Encounter Budget / 遭遇预算

不能只设计单只怪物，还必须设计玩家通常会同时面对多少怪。

第一版使用 Threat Point 估算遭遇强度。

```text
普通小怪 = 1 Threat
精英怪 = 3 Threat
小 Boss = 8～10 Threat
```

---

# 17. Tier 1 遭遇强度分级

## 17.1 安全遭遇

```text
Threat 1～2
```

例：

```text
1～2 只普通小怪
```

目标体验：

```text
玩家可以正常打。
Hikari 偶尔治疗。
Burden 基本可控。
不开减伤也不一定马上死亡。
```

---

## 17.2 标准遭遇

```text
Threat 3
```

例：

```text
3 只普通小怪
1 只精英怪
```

目标体验：

```text
玩家开始感受到压力。
需要尽快击杀。
Hikari 会明显参与治疗。
如果玩家长期硬吃伤害，Burden 会累积。
```

---

## 17.3 危险遭遇

```text
Threat 4～5
```

例：

```text
4～5 只普通小怪
1 只精英怪 + 1～2 只普通小怪
```

目标体验：

```text
必须使用减伤。
必须优先击杀目标。
Hikari 会频繁治疗。
紧急祈愿可能触发。
光负荷明显上升。
玩家可以赢，但不能乱打。
```

---

## 17.4 过量遭遇 / 接近必死

```text
Threat 6+
```

例：

```text
6 只以上普通小怪
2 只精英怪
1 只精英怪 + 3 只以上普通小怪
```

目标体验：

```text
玩家错误处理时高概率死亡。
Hikari 很快进入光溢出甚至导光封锁。
玩家如果硬拉，是主动选择高风险。
```

---

# 18. TTK / Time To Kill

TTK 用于衡量敌人存活多久。

```text
TTK = 敌人 HP / 玩家有效 DPS
```

当前简化估算：

```text
普通怪 HP = 5 PDU
玩家普通攻击 = 1 PDU
如果攻击节奏约 1.5 秒一次
纯普通攻击击杀时间 ≈ 7.5 秒
```

多怪示例：

```text
3 只普通怪总 HP = 15 PDU
纯普通攻击击杀时间 ≈ 22.5 秒
```

这意味着：

```text
3 只普通怪在 20 秒以上的时间内持续对玩家输出。
这会显著增加 Hikari 治疗压力。
```

因此遭遇平衡必须同时看：

```text
敌人数量
敌人 HP
敌人 DPS
玩家输出
玩家减伤
Hikari HPS
BU 增长速度
BU 自然恢复速度
守护共鸣触发机会
```

---

# 19. Hikari HPS vs Enemy DPS

当前 Tier 1 Hikari 理论持续治疗能力：

```text
微光治愈 / Light Mend = 0.3 PHU/s
紧急祈愿 / Emergency Prayer = 0.18 PHU/s，但它是救急技能，不应视为稳定 HPS
```

普通小怪压力示例：

```text
1 只普通小怪 ≈ 0.4～0.5 PHU/s
2 只普通小怪 ≈ 0.8～1.0 PHU/s
3 只普通小怪 ≈ 1.2～1.5 PHU/s
```

这意味着：

```text
Hikari 不应该能完全覆盖敌人总 DPS。
玩家不能站着不动靠 Hikari 无限奶。
玩家必须通过击杀、减伤、处理读条来降低实际压力。
```

目标关系：

```text
低压战斗：
敌人 DPS 稍高于 Hikari 持续 HPS，但玩家能通过击杀快速结束。

标准战斗：
Hikari 会明显介入，光负荷会累积，但玩家正确操作可以稳定获胜。

危险战斗：
Hikari 会频繁治疗，紧急祈愿可能触发，光负荷快速上升。

过量战斗：
Hikari 无法长期兜底，玩家需要撤退或接受高死亡风险。
```

---

# 20. 高 PHU / 低 BU 技能限制原则

如果某个 Hikari 技能具有：

```text
高治疗 PHU
低光负荷 BU
```

它会非常强，必须增加其他限制。

可用限制方式：

```text
长冷却
低 HP 才触发
高 Burden 才触发
每场战斗次数限制
触发后短时间治疗封锁
需要玩家先触发守护共鸣才解锁
消耗复苏之光 / Revival Light
触发后直接进入光溢出
只在剧情 / 特殊状态可用
```

设计 Hikari 技能时不能只写：

```text
healAmount = 60
burdenCost = 5
```

必须同时记录：

```text
治疗量 PHU
光负荷 BU
冷却
触发条件
状态限制
副作用
```

---

# 21. 当前已确认数值

```text
Tier 1 标准玩家 HP = 100
Tier 1 标准玩家普通攻击 = 20
1 PDU = 20 enemy damage
1 PHU = 10 player damage
1 BU = 5 Burden
maxBurden = 100 = 20 BU

微光治愈 / Light Mend = 15 HP = 1.5 PHU
微光治愈光负荷 = +5 Burden = +1 BU

紧急祈愿 / Emergency Prayer = 45 HP = 4.5 PHU
紧急祈愿光负荷 = +25 Burden = +5 BU

守护共鸣 / Guard Resonance = -10 Burden = -2 BU

溢光反震 / Overflow Counter = 30 enemy damage = 1.5 PDU
溢光反震触发区间 = 80%～99% 光负荷 / Burden
导光封锁 / Overload = 100 Burden = 20 BU
导光恢复阈值 = 60 Burden = 12 BU

玩家技能标准距离：Self = 0m, Melee = 3m, Area = 5m, Ranged = 20m
Basic Attack = 1 PDU = 20 enemy damage, range = 3m
Area Attack = 0.4 × 当前普通攻击最终伤害, range = 5m
Radiant Riposte = 3 PDU = 60 enemy damage, range = 20m, window = 10s

CircleAoE / Boss Shockwave = 30 player damage, castTime = 2.5s, radius = 5m
DonutAoE / Moon Ring = 35 player damage, castTime = 3.0s, inner = 2.8m, outer = 7.0m
```

---

# 22. 当前未确认数值

以下数值需要以 Unity Inspector / Asset 实际内容为准：

```text
SkeletonBossEnemy_Variant 当前 HP
SkeletonBossEnemy_Variant 当前普通攻击伤害
SkeletonBossEnemy_Variant 当前 CastAttack 伤害
SkeletonBossEnemy_Variant 当前 CastAttack 冷却
SkeletonBossEnemy_Variant 当前 CastAttack 读条时间

Stone Guard 当前具体参数
AttackPowerMultiplier 测试技能 asset 路径与具体倍率
普通 SkeletonEnemy 当前 HP
普通 SkeletonEnemy 当前普通攻击伤害
普通 SkeletonEnemy 当前攻击间隔
```

未确认数值不要猜，应在后续通过读取对应 Prefab / ScriptableObject / Inspector 后补充。

---

# 23. 后续调参原则

## 23.1 不直接填写孤立 damage

不要这样做：

```text
这个技能伤害填 37
这个敌人攻击填 23
```

应该先决定：

```text
这个技能相当于多少 PDU
这个敌人攻击相当于多少 PHU
这个治疗相当于多少 PHU
这个治疗代价相当于多少 BU
```

再换算成实际数值。

---

## 23.2 敌人 HP 用 PDU

```text
普通怪几次攻击能打死？
精英怪战斗持续多久？
溢光反震对战斗时长影响多少？
玩家技能连携是否明显缩短 TTK？
```

这些都用 PDU 衡量。

---

## 23.3 敌人伤害用 PHU

```text
玩家几下会死？
不开减伤是否危险？
开减伤是否明显有效？
Hikari 是否会被迫治疗？
```

这些都用 PHU 衡量。

---

## 23.4 Hikari 光负荷用 BU

```text
一次治疗增加多少压力？
一次守护共鸣抵消多少压力？
自然恢复速度是否过强？
Boss 战是否应该关闭自然恢复？
```

这些都用 BU 衡量。

---

## 23.5 遭遇难度看总压力

不要只看单只怪。

必须看：

```text
敌人数量
敌人总 DPS
敌人总 HP
玩家击杀速度
Hikari 治疗频率
Burden 累积速度
守护共鸣触发机会
```

---

# 24. 第一版验收用测试场景

## 24.1 普通小怪测试

目标：

```text
1～2 只普通小怪时，玩家可以稳定获胜。
3 只普通小怪时，Hikari 明显参与治疗。
4～5 只普通小怪时，玩家需要认真操作。
6 只以上普通小怪时，错误处理高概率死亡。
```

---

## 24.2 精英怪测试

目标：

```text
SkeletonBossEnemy_Variant 能稳定释放 CastAttack。
玩家不开减伤吃 CastAttack 会明显危险。
玩家开 Iron Bulwark 吃 CastAttack 后伤害明显降低。
守护共鸣稳定触发。
```

---

## 24.3 Hikari 光溢出测试

目标：

```text
光负荷 < 80%：
守护共鸣可触发，但溢光反震不触发。

光负荷 80%～99%：
守护共鸣成功后溢光反震触发，造成 30 反伤。

光负荷 = 100%：
守护共鸣可降低光负荷，但不触发溢光反震。
```

---

## 24.4 过量拉怪测试

目标：

```text
玩家同时承受过多普通怪攻击时，Hikari 无法无限兜底。
光负荷会快速上升。
紧急祈愿会显著增加压力。
玩家需要通过击杀、减伤或撤退解决问题。
```

---

# 25. 当前结论

第一版平衡的核心不是追求最终数值完美，而是验证以下体验是否成立：

```text
玩家正确操作：
能承受危险攻击，降低 Hikari 光负荷，获得 Radiant Riposte 等处理奖励，并在光溢出时获得额外反击收益。

玩家错误操作：
Hikari 会救场，但光负荷会恶化。

玩家过量拉怪：
Hikari 无法长期兜底，战斗会变得危险。

玩家成长：
旧敌人不会自动变强，玩家会明显变得更安全、更强。

Hikari 系统：
不是普通奶量系统，而是治疗 PHU、光负荷 BU、敌人 DPS、玩家减伤与溢光反震收益之间的压力对抗系统。
```