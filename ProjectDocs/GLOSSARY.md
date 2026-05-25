# GLOSSARY.md

最后更新：2026-05-25  
文档用途：统一项目内中文 / 英文 / 代码 / UI / Debug / 设计文档中的术语，避免同一概念在不同文件中被写成不同名字。  
注意：本文件只负责“术语怎么叫、是什么意思、不要怎么叫”。具体玩法实现以 `PROJECT_STATE.md` 为准，长期设计方向以 `GAME_DESIGN_NOTES.md` 为准，数值基准以 `BALANCE_BASELINE.md` 为准。

---

# 0. 术语使用总原则

## 0.1 同一术语允许前期误解、后期反转

Hikari 相关术语需要同时支持两层理解。

前期表层理解：

- Hikari 是普通治疗角色。
- 她为了治疗玩家，榨出不该存在的力量。
- 治疗行为本身让她积累负荷。
- 光负荷像是“过度治疗造成的代价”。

后期真实含义：

- Hikari 不是普通奶妈。
- 她本来就是过量光能容器。
- 治疗不是制造负荷的根本原因。
- 治疗只是把原本被灯压住的光导出、转向、使用。
- 真正危险的是：太强的光本身无法被安全收束。

因此，术语不能过早剧透“光容器”“过量光能”“灯在封锁治疗通道”等真相。

## 0.2 前期 UI / Debug 的写法边界

前期可以写：

- Hikari 治疗会增加光负荷。
- 光负荷过高时，可控治疗效率下降。
- 光负荷达到上限时，进入导光封锁。

前期不要写：

- Hikari 体内原本就有过量光能。
- 治疗只是导出被灯压制的光。
- 导光封锁是灯为了阻止强制延命而主动封锁治疗。
- Hikari 不是变弱，而是变得过强、过危险。

这些应留给后期剧情或关键演出揭示。

## 0.3 禁止把 Hikari 写成体弱奶妈

Hikari 的危险状态不是：

- 体力不足
- 魔力枯竭
- 奶妈被榨干
- HP 快死了
- 治疗资源耗尽

Hikari 的本质是：

- 可控治疗越不稳定
- 失控光能越强
- 光越接近无法收束
- 玩家越依赖她，她越接近失控

---

# 1. Hikari 核心资源术语

## 光负荷 / Burden

正式中文名：

- 光负荷

英文 / 代码名：

- Burden
- Light Burden

代码中当前可继续使用：

- burden
- currentBurden
- maxBurden
- burdenRatio

表层含义：

- 玩家前期可以理解为：Hikari 为了治疗玩家而承受的负担。
- 治疗越频繁，光负荷越高。

真实含义：

- Hikari 体内无法安全收束的过量光，在被导出、转向、治疗、复活时产生的不稳定压力。

使用场景：

- UI 资源条
- Debug 面板
- 设计文档
- Hikari 系统说明
- 战斗状态说明

推荐用法：

- 光负荷上升
- 光负荷下降
- 当前光负荷
- Hikari 光负荷
- 光负荷达到上限

不推荐用法：

- 治疗负荷
- 治疗压力条
- Hikari MP
- Hikari 体力
- Hikari 疲劳值
- Hikari 血量
- 奶妈负担

备注：

“光负荷”这个词必须保留模糊性。它既能让前期玩家误以为是治疗代价，也能在后期被重新解释为“过量光本身造成的压力”。

## 复苏之光 / Revival Light

正式中文名：

- 复苏之光

英文 / 代码名：

- Revival Light

含义：

- Hikari 是否还能在玩家死亡后进行一次高代价复活的独立资源。

使用场景：

- 玩家死亡后的复活判断
- Boss 战受控逃课机制
- Hikari 后期剧情系统
- 高代价救场资源

不要和什么混淆：

- 光负荷 = 当前 Hikari 承受了多少不稳定压力
- 复苏之光 = Hikari 是否还能强行拒绝一次玩家死亡

推荐设计理解：

- 玩家死亡
- Hikari 消耗复苏之光复活玩家
- 光负荷大幅上升
- 玩家成功逃过一次机制
- 后续战斗变得更危险

---

# 2. Hikari 光负荷阶段术语

## 稳定导光 / Stable Channeling

正式中文名：

- 稳定导光

英文候选：

- Stable Channeling
- Stable Guidance
- Stable Light Channel

阈值：

- 0%～79%

表层含义：

- Hikari 能正常治疗。
- 光负荷处于安全范围。

真实含义：

- 灯与 Hikari 仍能稳定分流体内的光。
- 光还没有明显突破可控范围。

战斗效果：

- Hikari 普通治疗正常。
- 光负荷可控。
- 没有额外风险收益。

不推荐叫法：

- 正常状态
- 低负荷状态
- 安全奶妈状态

说明：

“正常状态”可以作为说明，但不建议作为正式状态名。

## 光溢出 / Light Overflow

正式中文名：

- 光溢出

英文候选：

- Light Overflow
- Overflow

阈值：

- 80%～99%

表层含义：

- Hikari 治疗负担过高，治疗变得不稳定。

真实含义：

- 过量光开始突破导光结构。
- 可控治疗效率下降，但失控光能变强。

战斗效果：

- 可控治疗效率下降。
- 失控光能增强。
- 玩家可以利用危险状态获得额外收益。

当前第一版收益：

- 溢光反震：光溢出状态下，守护共鸣成功触发时，对 CastAttack 攻击者造成额外失控光伤害。

不推荐叫法：

- 高负荷状态
- 过劳状态
- 虚弱状态
- 治疗衰减状态

说明：

“高负荷”可以出现在内部说明里，但不要作为正式状态名。  
这是“压线贪”的核心区间。

玩家应该觉得：

- Hikari 已经危险了。
- 但如果我正确处理重击，就能把这个危险转化成收益。

## 导光封锁 / Channel Lockdown

正式中文名：

- 导光封锁

英文候选：

- Channel Lockdown
- Light Channel Lockdown
- Overload

阈值：

- 100%

表层含义：

- Hikari 负荷达到上限，暂时无法继续普通治疗。

真实含义：

- 导光结构封锁普通治疗通道，防止继续治疗变成危险的强制延命。

战斗效果：

- Light Mend 停止。
- Emergency Prayer 停止。
- 复活不可用。
- 溢光反震不触发。
- 守护共鸣仍可用于降低光负荷。

推荐用法：

- 进入导光封锁
- 导光封锁中
- 导光封锁解除

不推荐用法：

- Hikari 没魔了
- Hikari 治疗耗尽
- Hikari 虚弱
- Hikari 倒下

和“过载”的关系：

- 正式 UI：导光封锁
- 设计说明：导光封锁 / 过载
- 代码内部：Overload 可以保留

说明：

“过载”太泛，容易让人以为只是机器过热或资源耗尽。  
“导光封锁”更适合 Hikari 的设定反转。

## 导光恢复 / Channel Recovery

正式中文名：

- 导光恢复

英文候选：

- Channel Recovery
- Light Channel Recovery

阈值：

- 光负荷下降到 60% 以下或等于 60%

表层含义：

- Hikari 从封锁状态中恢复，可以重新治疗。

真实含义：

- 光流重新回到可控范围，导光结构允许普通治疗通道重新打开。

使用场景：

- 导光恢复
- 导光封锁解除
- 恢复稳定导光

不推荐叫法：

- 过载解除
- 恢复体力
- 恢复魔力
- 缓过来了

说明：

“过载解除”可作为旧说明，但正式文档建议统一成“导光恢复”。

## 解除限制 / Limit Release

正式中文名：

- 解除限制

英文候选：

- Limit Release
- Restriction Release
- Unsealed Light

触发条件：

- 剧情 / 特殊战斗条件

表层含义：

- Hikari 暂时释放更强治疗或光能力量。

真实含义：

- Hikari 主动放弃灯或导光结构的一部分限制，让无法收束的光直接流出。

战斗效果候选：

- 救玩家
- 重创 Boss
- 逆转战局
- 造成审判光爆发
- 触发高代价剧情后果

不推荐用法：

- 大招
- 爆气
- 超级治疗

说明：

这些词可以作为开发口语，但不要作为正式设定术语。

---

# 3. Hikari 治疗技能术语

## 微光治愈 / Light Mend

正式中文名：

- 微光治愈

英文 / 代码名：

- Light Mend

当前 Tier 1 定位：

- 小治疗，常规维持。

当前 Tier 1 基准：

- 治疗量：15 HP = 1.5 PHU
- 光负荷：+5 Burden = +1 BU
- 冷却：5 秒
- 触发条件：玩家 HP < 80%

表层含义：

- Hikari 用较小的治疗维持玩家生命。

真实含义：

- Hikari 将一部分可控光导向玩家，使其表现为治疗术。

## 紧急祈愿 / Emergency Prayer

正式中文名：

- 紧急祈愿

英文 / 代码名：

- Emergency Prayer

当前 Tier 1 定位：

- 救命治疗，强但昂贵。

当前 Tier 1 基准：

- 治疗量：45 HP = 4.5 PHU
- 光负荷：+25 Burden = +5 BU
- 冷却：25 秒
- 触发条件：玩家 HP < 35%

表层含义：

- Hikari 在玩家濒危时强行使用大治疗。

真实含义：

- Hikari 更大幅度地引出光，短期救回玩家，但让自身光流更接近失控。

备注：

频繁触发紧急祈愿，代表玩家承伤过高，正在把压力转嫁给 Hikari。

---

# 4. Hikari 连携 / 反击机制术语

## 守护共鸣 / Guard Resonance

正式中文名：

- 守护共鸣

英文 / 代码名：

- Guard Resonance

含义：

- 玩家正确开启减伤承受敌人关键攻击时，Hikari 的光负荷下降。

当前触发条件：

- 玩家 DamageReduction 技能处于 Active
- 玩家受到敌人 CastAttack / 读条重击
- 伤害来源不是普通攻击
- 内置冷却可用

当前 Tier 1 效果：

- 光负荷 -10 Burden = -2 BU
- 不治疗玩家
- 不生成治疗飘字
- 不修改玩家 HP

表层含义：

- 玩家正确保护了 Hikari，所以 Hikari 压力下降。

真实含义：

- 玩家用自己的防御行为稳定了承伤瞬间的光流，让 Hikari 不必用更危险的方式救场。

不推荐叫法：

- 格挡回血
- 减伤奖励治疗
- 反击触发器
- 低负荷技能

说明：

守护共鸣的重点不是回血，而是“正确 Tank 行为释放 Hikari 压力”。

## 溢光反震 / Overflow Counter

正式中文名：

- 溢光反震

英文 / 代码名：

- Overflow Counter

当前旧名 / 代码名可能仍出现：

- Light Counter
- 高负荷守护反击

含义：

- 光溢出状态下，守护共鸣成功触发时，外泄的失控光反过来震伤攻击者。

当前触发条件：

- Hikari 光负荷处于 80%～99%
- 玩家 DamageReduction 技能处于 Active
- 玩家受到敌人 CastAttack / 读条重击
- 守护共鸣成功触发
- 攻击者有有效 HealthComponent
- 攻击者未死亡

当前 Tier 1 效果：

- 对攻击者造成 30 enemy damage = 1.5 PDU
- 不治疗玩家
- 不改变光负荷
- 100% 导光封锁时不触发

表层含义：

- Hikari 负荷过高时，玩家正确承受重击可以触发额外反击。

真实含义：

- Hikari 体内外溢的强光无法完全收束，玩家的防御行为让这股危险光能反向冲击攻击者。

不推荐叫法：

- 高负荷反击
- 高负荷守护反击
- Light Counter 的中文直译
- 治疗反伤

说明：

“高负荷反击”可以作为旧名记录，但正式名建议统一为“溢光反震”。

## 守护反击 / Radiant Riposte

正式中文名：

- 守护反击

英文 / 代码名：

- Radiant Riposte
- GuardCounter
- PlayerSkillEffectType.GuardCounter

含义：

- 玩家用指定减伤技能成功承受敌人 CastAttack 并触发守护共鸣后，获得一次限时反击机会。

当前触发 / 授权规则：

- 守护共鸣本身可以由任意 DamageReduction Active + CastAttack 命中触发。
- 守护反击的释放资格只由 `PlayerSkillData.GrantsGuardCounter == true` 的减伤技能授予。
- 当前 Iron Bulwark 授予守护反击；Stone Guard 不授予守护反击。

当前 Tier 1 效果：

- Guard Resonance 成功后获得 10 秒 Ready 窗口。
- 10 秒内按 Slot5，对触发 Guard Resonance 的 attacker 造成 3 PDU。
- 目标死亡 / 丢失 / 无 HealthComponent 时不能打出。
- 超出 Ranged 标准距离 20m 时不能打出，但 Ready 不消耗。

不推荐叫法：

- 普通反伤
- 自动反击
- 冷却好了就按的爆发技能

说明：

守护反击是“成功处理机制后的手动奖励”，不是普通 CD 伤害技能。

---

# 5. Hikari 剧情 / 世界观术语

## 光容器 / Vessel of Light

正式中文名：

- 光容器

英文候选：

- Vessel of Light
- Light Vessel

含义：

- Hikari 因幼年时被超常规光之术强行救回，体内持续承载无法完全收束的光。

使用限制：

这是后期真相词。不要在前期 UI、早期教学、普通 Debug 文案中直接使用。

推荐使用场景：

- 后期剧情
- 设定文档
- 关键 Boss 前后演出
- Hikari 真相揭示

## 灯 / Lamp

正式中文名：

- 灯

英文候选：

- Lamp
- Light Lamp
- Guiding Lamp

表层含义：

- Hikari 随身携带的施法媒介或治疗道具。

真实含义：

- 用于分流、导出、压制 Hikari 体内失控光的装置。

使用限制：

前期可以表现为普通治疗媒介。  
后期再揭示它其实是限制装置 / 导光结构。

## 导光 / Light Channeling

正式中文名：

- 导光

英文候选：

- Light Channeling
- Channeling
- Guiding Light

表层含义：

- Hikari 把治疗之光导向玩家。

真实含义：

- 灯将 Hikari 体内过量光分流、导出、压制，使其保持在可控范围。

备注：

“导光”是 Hikari 术语体系的核心词。它必须保留双重解释能力。

## 审判光 / Judgment Light

正式中文名：

- 审判光

英文候选：

- Judgment Light
- Light of Judgment

含义：

- Hikari 后期失控或解除限制时，治愈之光反转成审判、拒绝死亡、强制延命性质的光。

使用场景：

- 后期剧情
- 解除限制
- Boss 演出
- 多结局伏笔

不推荐早期使用：

不要在前期战斗系统说明里使用“审判光”。它会过早暴露 Hikari 光属性的反转方向。

## 拒绝死亡 / Rejection of Death

正式中文名：

- 拒绝死亡

英文候选：

- Rejection of Death
- Denying Death

含义：

- Hikari 的光并非单纯治疗，而是有强行维持存在、暂停死亡、扭曲延命的倾向。

使用场景：

- 复活系统
- 后期剧情
- 亡灵化结局
- 解除限制

备注：

这是 Hikari 光属性后期反转的核心概念。前期不要直接说明。

---

# 6. 玩家技能 / Tank 行为术语

## 减伤 / Damage Reduction

正式中文名：

- 减伤

英文 / 代码名：

- DamageReduction

含义：

- 玩家开启技能，降低自己受到的伤害。

当前代表技能：

- Iron Bulwark
- Stone Guard

Hikari 系统关联：

- DamageReduction Active + 承受 CastAttack → 守护共鸣 → 光负荷下降

## Iron Bulwark

当前中文候选：

- 钢铁壁垒
- 铁壁

英文 / 代码名：

- Iron Bulwark
- iron_bulwark

当前定位：

- 标准短 CD 减伤技能。

当前 Tier 1 基准：

- 受到伤害倍率：0.5
- 持续时间：4 秒
- 冷却：12 秒

备注：

如果后续 UI 需要中文正式名，推荐用“钢铁壁垒”。  
“铁壁”较短，适合 Debug 或技能栏简称。

## Stone Guard

当前中文候选：

- 石肤守护
- 石之守护

英文 / 代码名：

- Stone Guard

当前定位：

- 持续压力用减伤技能。
- 用于承接 Hikari 的治疗窗口，提高被治疗效率。

当前规则：

- 属于 DamageReduction。
- 可触发守护共鸣并降低 Hikari 光负荷。
- 当前不授予守护反击。
- 当前 `HealingReceivedMultiplier = 1.5`，Active 期间受到 Hikari 治疗时显示 `GUARD HEAL` 提示。

说明：

Stone Guard 的核心定位不是爆发反击，而是“稳住血线 + 接治疗”。

## CastAttack / 读条重击

正式中文名：

- 读条重击

英文 / 代码名：

- CastAttack
- EnemySkillType.CastAttack

含义：

- 敌人开始读条后，在读条完成时造成一次明确的关键攻击。

Hikari 系统关联：

- 普通攻击不触发守护共鸣。
- 读条重击在玩家开减伤承受时，可以触发守护共鸣。

不推荐叫法：

- 普通攻击
- 技能攻击
- 大招

说明：

“技能攻击”太泛。“读条重击”更适合第一版精英怪 / 小 Boss 测试。

## Basic Attack / BasicMeleeAttack

正式中文名：

- 基础攻击
- 普通近战攻击

英文 / 代码名：

- Basic Attack
- BasicMeleeAttack
- PlayerSkillEffectType.BasicMeleeAttack

含义：

- 玩家 Slot1 的基础近战攻击。
- 当前作为 `PlayerSkillData` 注册到 `PlayerSkillManager`，由 `PlayerBasicAttackController` 执行。

当前标准距离：

- Melee = 3m

说明：

Basic Attack 不应再作为 `PlayerSkillController` 的特判输入处理。

## Area Attack / BasicAreaAttack

正式中文名：

- 范围基础攻击
- AOE 普攻

英文 / 代码名：

- Area Attack
- BasicAreaAttack
- PlayerSkillEffectType.BasicAreaAttack

含义：

- 玩家 Slot4 的基础 AOE 攻击。
- 当前作为 `PlayerSkillData` 注册到 `PlayerSkillManager`，由 `PlayerBasicAttackController` 执行。
- 与 Basic Attack 共享基础攻击冷却。

当前标准距离：

- Area = 5m

## 技能距离类型 / PlayerSkillRangeType

英文 / 代码名：

- PlayerSkillRangeType

当前标准：

| 类型 | 含义 | 距离 |
|---|---|---:|
| Self | 自身释放 | 0m |
| Melee | 一般近战 | 3m |
| Area | 一般 AOE 范围 | 5m |
| Ranged | 一般远距离 | 20m |
| Custom | 自定义 | customRange |

说明：

技能距离应优先通过 `PlayerSkillData.EffectiveRange` 读取，避免在执行器里分散硬编码。

## 条件锁定 / Condition Locked

英文 / 代码名：

- Condition Locked

含义：

- 技能存在于技能栏中，但当前不满足使用条件。

当前代表：

- Radiant Riposte 平时显示灰色遮罩，表示未获得 GuardCounter Ready。

不要和什么混淆：

- Cooldown：技能使用后等待恢复。
- Condition Locked：技能条件未满足，不是冷却。

## 条件触发可用 / Proc Ready

英文 / 代码名：

- Proc Ready

含义：

- 通过机制处理获得的限时可用状态。

当前代表：

- Radiant Riposte 在 Guard Resonance 授权后进入 10 秒 Ready，技能格发光并显示剩余时间。

## 压线贪

正式中文名：

- 压线贪

英文候选：

- Risk Greed
- Edge Greed
- Playing the Edge

含义：

- 玩家故意让 Hikari 光负荷接近危险区间，以换取更高收益，但一旦过头就会进入导光封锁。

当前代表玩法：

- 把光负荷推到 80%～99%
- 进入光溢出
- 正确开减伤承受 CastAttack
- 触发守护共鸣
- 触发溢光反震

备注：

这是 Hikari 系统当前最核心的玩法目标之一。不要把光负荷设计成纯惩罚条。

## CircleAoE / 圆形 AoE

正式中文名：

- 圆形 AoE

英文 / 代码名：

- CircleAoE
- EnemySkillType.CircleAoE

含义：

- 以 Boss / 敌人为中心的圆形伤害范围。
- 当前第一版要求玩家远离 Boss 到范围外躲避。

当前规则：

- 有读条。
- 读条期间显示圆形范围提示。
- 读条结束后提示消失，并按 XZ 平面距离判定一次伤害。
- 当前不触发守护共鸣 / 守护反击。

## 月环 / DonutAoE / Moon Ring

正式中文名：

- 月环

英文 / 代码名：

- DonutAoE
- Moon Ring
- EnemySkillType.DonutAoE

含义：

- 以 Boss 为中心的环形 AoE。Boss 脚下内圈安全，外侧圆环为伤害区。
- 玩家需要贴近 Boss 或离开外圈来躲避，当前主要表达“贴近 Boss 安全”的 FF14 式机制语言。

当前规则：

- `distance <= innerRadius`：安全。
- `innerRadius < distance <= outerRadius`：命中。
- `distance > outerRadius`：安全。
- 提示应渲染为真正的环形，只渲染伤害区域；内圈安全区不应有颜色。
- 当前不触发守护共鸣 / 守护反击。

---

# 7. 数值单位术语

## Content Tier / 内容等级

正式中文名：

- 内容等级

英文 / 代码名：

- Content Tier
- Tier

含义：

- 一个区域、章节、敌人等级或战斗阶段使用的数值基准。

当前版本：

- Tier 1 / 第一章 / 第一版野外战斗测试基准

当前 Tier 1 基准：

- 标准玩家 HP = 100
- 标准玩家普通攻击 = 20
- Hikari maxBurden = 100

备注：

敌人伤害不应按玩家当前实时 HP 百分比计算。应先根据内容等级确定基准，再换算实际数值。

## PDU / Player Damage Unit

正式中文名：

- 玩家伤害单位

英文 / 代码名：

- PDU
- Player Damage Unit

含义：

- 用于描述玩家对敌人造成的伤害、敌人 HP、Boss HP、输出窗口收益和反击伤害。

当前 Tier 1 定义：

- 1 PDU = 玩家无装备普通攻击一次
- 1 PDU = 20 enemy damage

当前 Tier 1 示例：

- 玩家普通攻击 = 1 PDU = 20 damage
- 溢光反震 = 1.5 PDU = 30 damage
- 普通怪 HP = 5 PDU = 100 HP
- 精英怪 HP = 15～25 PDU = 300～500 HP
- 小 Boss HP = 40～80 PDU = 800～1600 HP

使用原则：

不要直接写“这个技能伤害 37”。  
应先写“这个技能伤害约 2 PDU”，再换算实际数值。

## PHU / Player Health Unit

正式中文名：

- 玩家承伤单位

英文 / 代码名：

- PHU
- Player Health Unit

含义：

- 用于描述敌人对玩家造成的伤害、地板伤害、Boss 大招伤害、Hikari 治疗量和玩家生命容量。

当前 Tier 1 定义：

- 标准玩家 HP = 100
- 1 PHU = 10 player damage
- 标准玩家 HP = 10 PHU

当前 Tier 1 示例：

- 普通小怪普通攻击 = 1 PHU = 10 damage
- 精英怪普通攻击 = 1.5 PHU = 15 damage
- 精英怪 CastAttack / 读条重击 = 5 PHU = 50 damage
- 小 Boss 大招 = 6～8 PHU = 60～80 damage

使用原则：

不要把敌人攻击设计成“玩家最大 HP 的 10%”。  
应设计成“Tier 1 普通攻击 = 1 PHU = 10 damage”。  
这样玩家通过装备提高 HP 后，旧敌人会自然变弱。

## BU / Burden Unit

正式中文名：

- 光负荷单位

英文 / 代码名：

- BU
- Burden Unit

含义：

- 用于描述 Hikari 光负荷变化。

当前定义：

- 1 BU = 5 Burden
- maxBurden = 100 = 20 BU

当前关键阈值：

- 稳定导光：0～15.8 BU = 0%～79%
- 光溢出：16～19.8 BU = 80%～99%
- 导光封锁：20 BU = 100%
- 导光恢复阈值：12 BU = 60%

当前光负荷行为：

- 微光治愈 = +1 BU = +5 Burden
- 紧急祈愿 = +5 BU = +25 Burden
- 守护共鸣 = -2 BU = -10 Burden
- 自然恢复速度 = 0.2 BU/s = 1 Burden/s

使用原则：

不要直接写“这个治疗增加 13 光负荷”。  
应先写“这个治疗增加约 2.5 BU”，再换算实际 Burden 数值。

## EDP / Enemy DPS Pressure

正式中文名：

- 敌人 DPS 压力

英文 / 代码名：

- EDP
- Enemy DPS Pressure

含义：

- 敌人每秒对玩家造成多少 PHU 压力。

示例：

- 普通小怪普通攻击 = 1 PHU
- 攻击间隔 = 2 秒
- EDP = 0.5 PHU/s

使用场景：

- 遭遇平衡
- 拉怪数量评估
- Hikari 治疗压力评估
- Boss 小怪组合设计

## TTK / Time To Kill

正式中文名：

- 击杀时间

英文 / 代码名：

- TTK
- Time To Kill

含义：

- 玩家击杀一个敌人或一组敌人需要多少时间。

计算方式：

- TTK = 敌人 HP / 玩家有效 DPS

使用场景：

- 普通怪 HP 设计
- 精英怪战斗长度
- Boss 阶段长度
- 溢光反震对战斗时长的影响

---

# 8. 文档与实现文件术语

## PROJECT_STATE.md

正式用途：

- 记录当前 Unity 项目实际完成状态。

应写内容：

- 已经实现的功能
- 当前脚本职责
- 当前 Prefab / Scene 绑定状态
- 已知问题
- 当前测试结果
- 下一步开发注意点

不应写内容：

- 大段未来设定
- 未实现玩法当作已完成
- 聊天总结
- 临时想法流水账

## GAME_DESIGN_NOTES.md

正式用途：

- 记录游戏长期设计方向、核心体验、系统规划与取舍原则。

应写内容：

- 游戏定位
- Hikari 长期设定
- 系统设计方向
- 剧情反转方向
- 长期不做什么
- Vertical Slice 范围

注意：

本文件不等于当前实现状态。当前 Unity 项目是否真的完成，必须看 `PROJECT_STATE.md`。

## BALANCE_BASELINE.md

正式用途：

- 记录第一版战斗数值基准、单位系统、Hikari 治疗压力模型与遭遇预算。

应写内容：

- PDU
- PHU
- BU
- EDP
- Content Tier
- 敌人 HP / 伤害 / DPS 基准
- Hikari 治疗与光负荷基准
- 遭遇预算
- 调参原则

注意：

本文件不是最终平衡表。它是当前原型阶段避免随手乱填数字的基准。

## GLOSSARY.md

正式用途：

- 统一项目术语。

应写内容：

- 正式中文名
- 英文名 / 代码名
- 表层含义
- 真实含义
- 使用场景
- 不推荐叫法
- 必要备注

不应写内容：

- 完整剧情
- 完整数值表
- 具体代码实现
- 长篇聊天总结

---

# 9. 术语替换表

## Hikari 状态旧词替换

| 旧说法 | 新说法 |
|---|---|
| 高负荷状态 | 光溢出 |
| 过载状态 | 导光封锁 |
| 解除过载 | 导光恢复 |
| 治疗量下降 | 可控治疗效率下降 |
| 高负荷反击 | 溢光反震 |
| 高负荷守护反击 | 溢光反震 |
| Light Counter | Overflow Counter / 溢光反震 |
| Guard Resonance | 守护共鸣 |
| Burden | 光负荷 |
| Revival Light | 复苏之光 |

## 不推荐术语总表

以下词不建议作为正式术语：

- Hikari MP
- Hikari 体力
- Hikari 血量
- 奶妈疲劳
- 治疗压力条
- 治疗资源
- 过劳
- 虚弱状态
- 高负荷反击
- 高负荷守护反击
- Hikari 治疗耗尽
- Hikari 没魔了

这些词可在聊天或临时说明中使用，但不要进入正式 UI、正式文档或长期注释。

---

# 10. 当前推荐正式术语总表

- 光负荷 / Burden
- 复苏之光 / Revival Light
- 稳定导光 / Stable Channeling
- 光溢出 / Light Overflow
- 导光封锁 / Channel Lockdown
- 导光恢复 / Channel Recovery
- 解除限制 / Limit Release
- 微光治愈 / Light Mend
- 紧急祈愿 / Emergency Prayer
- 守护共鸣 / Guard Resonance
- 溢光反震 / Overflow Counter
- 守护反击 / Radiant Riposte
- 导光 / Light Channeling
- 灯 / Lamp
- 光容器 / Vessel of Light
- 审判光 / Judgment Light
- 拒绝死亡 / Rejection of Death
- 减伤 / Damage Reduction
- 读条重击 / CastAttack
- 压线贪 / Playing the Edge
- Basic Attack / BasicMeleeAttack
- Area Attack / BasicAreaAttack
- 技能距离类型 / PlayerSkillRangeType
- 条件锁定 / Condition Locked
- 条件触发可用 / Proc Ready
- 圆形 AoE / CircleAoE
- 月环 / DonutAoE / Moon Ring
- 战斗文字来源标签 / CombatTextSourceLabel
- PDU / Player Damage Unit
- PHU / Player Health Unit
- BU / Burden Unit
- EDP / Enemy DPS Pressure
- TTK / Time To Kill
- Content Tier / 内容等级

---

# 11. 后续维护规则

1. 新增系统前，先检查是否已有术语。
2. 同一概念不要在不同文档里使用多个中文名。
3. 如果代码名暂时沿用旧词，文档中要标明“旧代码名 / 现正式名”。
4. 前期 UI 不要剧透 Hikari 的真实本质。
5. Hikari 相关术语必须避免把她写成单纯虚弱的奶妈。
6. 数值设计必须优先使用 PDU / PHU / BU，再换算实际数值。
7. `GLOSSARY.md` 只管术语，不代替 `PROJECT_STATE.md`、`GAME_DESIGN_NOTES.md` 或 `BALANCE_BASELINE.md`。
