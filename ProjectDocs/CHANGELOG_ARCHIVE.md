# CHANGELOG_ARCHIVE

旧 PROJECT_STATE.md 中的历史变更记录归档。
此文件为历史参考，不作为未来 AI 首读文档。
当前项目状态见 PROJECT_STATE.md。

---

## 2026-05-25（文档整理）

- 文档整理：项目根目录 4 个 Markdown 文件（PROJECT_STATE.md / GAME_DESIGN_NOTES.md / GLOSSARY.md / BALANCE_BASELINE.md）移动至 ProjectDocs/ 文件夹（git mv）。
- PROJECT_STATE.md 拆分为 6 个文档：PROJECT_STATE（状态摘要）/ ARCHITECTURE_REFERENCE（详细架构）/ DEV_RULES（开发规则）/ DEBUG_GUIDE（Debug 指南）/ CHANGELOG_ARCHIVE（历史归档）/ README.md（入口）。
- 保留旧 PROJECT_STATE.md 中仍有长期价值的架构信息，删除过时的流水账式历史摘要细节。

## 2026-05-25（功能变更）

- 玩家 1 / 4 已纳入 PlayerSkillManager：Slot1 Basic Attack 为 3m 近战，Slot4 Area Attack 为 5m AOE，实际执行仍由 PlayerBasicAttackController 负责，二者共享基础攻击冷却并在 SkillBar 同步显示。
- Radiant Riposte / 守护反击已作为 Slot5 GuardCounter 技能进入技能栏：Iron Bulwark 授予 10 秒反击窗口，Stone Guard 只触发 Guard Resonance 不授予反击；玩家死亡会清除 Ready，20m 外不消耗 Ready。
- 敌人技能系统新增 CircleAoE 与 DonutAoE / Moon Ring：二者都有读条和地面范围提示，读条结束后判定一次伤害；DonutAoE 使用真环形 Mesh 提示，只渲染伤害环区域。

---

## 2026-05-22（Hikari 术语统一）

完成了 Hikari 相关术语在 Debug UI、Console Log、Inspector Header/Tooltip、代码注释和项目文档中的全面统一，以 GLOSSARY.md 为准。

修改的文件：HikariSupportController.cs / SkeletonDebugUI.cs / GAME_DESIGN_NOTES.md / PROJECT_STATE.md

正式术语（详见 GLOSSARY.md）：
- 光负荷 / Burden
- 稳定导光 / Stable Channeling（0%~79%）
- 光溢出 / Light Overflow（80%~99%）
- 导光封锁 / Channel Lockdown（100%）
- 导光恢复 / Channel Recovery（<= 60%）
- 守护共鸣 / Guard Resonance
- 溢光反震 / Overflow Counter（光溢出时）
- 微光治愈 / Light Mend
- 紧急祈愿 / Emergency Prayer

代码层保留旧变量名：currentBurden / maxBurden / isOverloaded 等（避免 Inspector 序列化丢失）。

---

## 2026-05-20

- 溢光反震 / Overflow Counter 已落地：守护共鸣成功且光溢出（80%~99%）时，对 CastAttack 攻击者造成 30 伤害；导光封锁（100%）时不触发溢光反震，但守护共鸣仍可降低光负荷。
- Tier 1 数值基准已文档化：项目根目录新增 BALANCE_BASELINE.md 与 BalanceTables/*.csv。
- CastAttack 只在开始读条时检查 range，读条中玩家拉开距离不会取消，完成时不因距离失败。
- 玩家跳跃动画循环、落地必须先 Idle 的问题已修复，跳跃下落手感已通过额外重力第一版改善。

---

## 2026-05-19

- 治疗反馈接入通用血量系统：HealthComponent 新增 Heal() 与 OnHealed，DamageNumberSpawner 可通过 healingPopupPrefab 显示实际恢复量，DamageNumberPopup_PlayerHealth.prefab 已用于玩家治疗飘字。
- Hikari 支援原型最小闭环：HikariSupportController 支持 Light Mend、Emergency Prayer、光负荷、高负荷治疗衰减、100% 过载停摆、60% 过载恢复，以及 Guard Resonance 降低光负荷。
- Guard Resonance 触发条件收敛为「玩家 DamageReduction Active 期间承受敌人 CastAttack 伤害」；EnemySkillController 记录最近技能伤害来源，普通攻击不会触发守护共鸣。F1 Hikari Debug 已独立中文窗口显示并支持动态高度。

---

## 2026-05-18（第二次）

- 玩家技能系统新增 AttackPowerMultiplier 效果类型；PlayerStatusEffectController 可修正普通攻击输出伤害。
- 战斗反馈新增伤害飘字系统：DamageNumberPopup / DamageNumberSpawner 通过 HealthComponent.OnDamaged 显示最终实际伤害；Player 与 EnemyBase.prefab 已绑定对应 Spawner，并区分玩家打出 / 受到伤害的 Popup Prefab。
- 目标选择强化：PlayerTargeting 支持 Tab 从屏幕左到右循环选择；TargetSelectionIndicator 读取 CurrentTarget 在目标头顶显示倒三角，已修正静止目标频闪问题。

---

## 2026-05-18（第一次）

- 玩家技能系统 v0.1 落地：新增 PlayerSkillData / PlayerSkillManager / PlayerStatusEffectController / PlayerSkillCanvasUI / PlayerSkillBarCanvasUI / PlayerSkillHudUI；PlayerMitigationController.cs 已删除。
- Iron Bulwark / Stone Guard 等 DamageReduction 技能通过 PlayerSkillManager.skills 注册，Canvas 技能栏按注册顺序自动生成技能格，Active / Cooldown / Ready 显示正常，多个减伤按乘算叠加。
- SkillCanvas / SkillBar / SkillSlotTemplate 已成为正式技能栏第一版：模板隐藏，运行时生成所有技能格；技能栏锚定右下，新技能显示在最右侧，技能数量增加时整体向左扩展。

---

## 2026-05-14（第二次）

- 敌人技能系统第一版落地：新增 EnemySkillData.cs / EnemySkillController.cs / EnemyCastBarUI.cs；EnemyAI.cs 的 Attack 状态已接入技能调用，有技能时尝试释放，无技能时继续普通攻击。
- CastAttack / 读条重击第一版：指定敌人可读条、显示技能名 / 进度 / 时间，读条结束后按距离和目标状态结算伤害。
- 敌人 Prefab 工作流更新：EnemyBase.prefab 统一挂载 EnemySkillController 与 EnemyCastBarUI 且 skills 为空；SkeletonEnemy_Variant 保持无技能；SkeletonBossEnemy_Variant 覆写配置读条重击。

---

## 2026-05-14（第一次）

- PlayerController.cs 与 RPGCameraController.cs 的玩家操作整理为 FF14 Legacy-like：WASD 相机基准移动、移动时角色朝实际方向转身、右键只控制相机、左键只用于目标选择、左键 + 右键支持双键前进，Shift 支持八方向跑步。
- 玩家移动动画适配 Legacy-like：非锁定移动下任意方向只使用 Forward Walk / Forward Run，Horizontal 保持 0，Speed 表示 Idle / Walk / Run。
- RPGCameraController.cs 鼠标视角旋转改为 Mouse.delta * rotationSpeed，不再乘 Time.deltaTime。
- SkeletonDebugUI.cs 装备状态窗口高度改为按实际显示行数动态计算。

---

## 2026-05-13

- EnemyAI 移动控制权整理第一版：正常移动由 NavMeshAgent 主导，Rigidbody 主要承担碰撞 / 物理辅助，Attack 进入时停止 Agent 并清理 Rigidbody 残留速度。
- 清理：DebugManager Missing Script 已清理，旧 Ground 系列对象已删除，当前主地面以 Terrain 为准。
- PlayerEquipment.cs 从单 Core 槽扩展为三槽（Core / Armor / Accessory）。
- PlayerCombatStats.cs 汇总三槽攻击力与最大生命值。
- SkeletonDebugUI 新增 Armor / Accessory Debug 操作按钮与独立装备状态 Debug 窗口。

---

## 2026-05-12（第三次）

- EnemySpawnArea.cs 创建与升级（加权随机 SpawnEntry，单种存活上限）。
- EnemyBase.prefab 创建（从 SkeletonEnemy 复制，清理模型，新增 VisualRoot 空子对象）。
- SkeletonEnemy_Variant.prefab 与 SkeletonBossEnemy_Variant.prefab 作为真正 Prefab Variant 创建。

---

## 2026-05-12（第二次）

- EnemyAI 新增 SpawnArea 支持：_spawnAreaCenter / _hasSpawnAreaContext / WanderCenter / LeashCenter 属性 / SetSpawnAreaContext() 公开方法。
- 旧 EnemySpawnPoint 完全兼容（不调用 SetSpawnAreaContext 时行为不变）。

---

## 2026-05-12（第一次）

- EnemyAI 新增 Wander 状态，实现 Idle / Wander 混合游荡；新增游荡相关参数。
- SampleScene 新增 NavMeshSurface_World 并完成 NavMesh Bake。
- SkeletonEnemy.prefab 新增 NavMeshAgent 组件。
- Wander / Chase / ReturnToSpawn 状态改为优先使用 NavMeshAgent。
- Chase 新增目标点暂时不可达时不切 Rigidbody 的逻辑。
- EnemyDropper 新增 Ground Placement：掉落物贴地 Raycast 生成，含误判高度保护。

---

## 2026-05-11

- ItemData.cs 新增 ItemType / EquipmentSlotType / maxStack / attackPowerBonus / maxHealthBonus，OnValidate() 保护规则。
- ItemStack.cs 新增 IsFull / RemainingCapacity，AddCount() 返回实际增加数量，新增 RemoveCount()。
- PlayerInventory.cs 支持 Equipment 不合并，非装备按 itemId + maxStack 合并；新增 RemoveItem() 与 FindFirstEquipmentBySlot()。
- 新增 PlayerEquipment.cs（Core 槽）、PlayerCombatStats.cs（统一数值汇总）。
- HealthComponent.cs 新增 SetMaxHealth()。
- EnemyDropper.cs 从固定单物品掉落升级为多条目概率掉落测试版。
- SkeletonEnemy.prefab 掉落配置：骨头 100%，守护核心 20%。
- RPGCameraController.cs 增加 Cursor 管理，修复拖拽视角后鼠标永久消失问题。
- 确认 F1 Debug UI 实际挂载在 SkeletonSpawnerManager。
