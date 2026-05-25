# DEBUG_GUIDE

最后更新：2026-05-25

F1 Debug UI 的使用方法与测试流程。本文件记录开发 / 调试用途，不是正式 UI 说明。

---

## 1. F1 Debug UI 总览

- 脚本：SkeletonDebugUI.cs
- 路径：Assets/Scripts/Spawner/SkeletonDebugUI.cs
- 挂载对象：SampleScene 中的 SkeletonSpawnerManager GameObject
- 实现方式：OnGUI() / IMGUI（不在 Hierarchy 的 Canvas 对象中）
- 按 F1 显示 / 隐藏 Debug 面板
- 这些按钮不是正式游戏 UI，不应用于正式发布版本
- 正式背包 UI / 装备 UI / 死亡复活 UI 应使用 Canvas + TMP + Button 或 UI Toolkit

---

## 2. 左侧 Debug 面板功能

### Skeleton Spawner
- Spawn 1：在 SpawnPoint 附近生成 1 个 SkeletonEnemy_Variant
- Spawn 5：生成 5 个
- Clear All：清除所有由 SkeletonSpawner 生成的敌人

注意：Debug 召唤骷髅已改用 SkeletonEnemy_Variant，不是旧 SkeletonEnemy.prefab。

### 玩家操作
- 恢复玩家满血：调用 HealthComponent.RestoreFullHealth()
- 复活玩家测试：调用 PlayerDeathHandler.ResetForRespawn() 并恢复满血
- 复活到最近存档点：传送到 PlayerRespawnPointTracker 记录的位置并复活

### 敌人调试
- 显示当前目标：输出 PlayerTargeting.CurrentTarget 的名称与信息
- 重置当前目标敌人：对当前目标调用 EnemyAI.ResetToSpawn()

### 装备调试
- 显示当前 Core：输出 PlayerEquipment.EquippedCore 信息
- 装备测试 Core：直接装备测试用 Core 资产（不从背包装）
- 强制清空 Core（Debug）：调用 UnequipCore()，不把装备放回背包（纯调试用）
- 卸下 Core 到背包：调用 UnequipCore() 并把结果放入 PlayerInventory
- 装备背包中的第一个 Core：FindFirstEquipmentBySlot(Core) → EquipCore → 从背包移除 → 旧 Core 回背包
- 装备背包中的第一个 Armor：同上，Armor 槽
- 卸下 Armor 到背包：UnequipArmor() → AddItem
- 装备背包中的第一个 Accessory：同上，Accessory 槽
- 卸下 Accessory 到背包：UnequipAccessory() → AddItem

### 战斗属性调试（只读显示）
- Base Normal Attack Damage
- Equipment Attack Bonus（Core + Armor + Accessory 总和）
- Current Normal Attack Damage（= Base + Equipment Bonus）
- Equipment Max Health Bonus
- Base Max Health
- Current Max Health
- 玩家减伤状态：读取 PlayerSkillManager.GetStateBySkillId(skillId: iron_bulwark)
  - 显示：Skill Source / Skill Id / Mitigation Active / Active Remaining / Cooldown Remaining / Damage Taken Multiplier
  - 找不到 PlayerSkillManager 或对应 state 时显示 PlayerSkillManager state not found

---

## 3. 右侧背包 Debug 窗口

位置：屏幕右侧，不与左侧 Debug 按钮重叠
宽度：Mathf.Clamp(Screen.width * 0.28f, 320f, 460f)

显示内容：
- --- 背包调试 ---
- ItemCount（所有 stack 的数量总和）
- StackCount（stack 总数）
- 每个 ItemStack：物品名 / itemId / Count / ItemType
  - Equipment 时额外显示 EquipmentSlotType
  - AttackPowerBonus > 0 时显示 ATK Bonus
  - MaxHealthBonus > 0 时显示 Max HP Bonus
- 背包为空时显示「背包为空」

常用用途：
- 验证骨头数量是否正确合并
- 验证 Core 是否从背包移除后进入装备槽
- 验证卸下 Core 后是否回到背包
- 验证替换 Core 时旧 Core 是否回到背包

---

## 4. 装备状态 Debug 窗口

位置：屏幕左上，左侧主面板右侧
宽度：310f，高度动态计算（不低于 240f）

显示内容：
- --- 装备状态 ---
- Core：当前装备名 / 未装备
- Armor：当前装备名 / 未装备
- Accessory：当前装备名 / 未装备
- --- 战斗属性汇总 ---
- Equipment ATK Bonus
- Equipment Max HP Bonus
- Current Normal Attack
- Current Max Health

缺少 PlayerEquipment 或 PlayerCombatStats 时显示提示，不崩溃。

---

## 5. Hikari Debug 窗口

位置：屏幕左上，装备状态窗口下方
高度：动态计算（通过 BuildHikariDebugLines() + GUIStyle.CalcHeight()）
找不到 HikariSupportController 时窗口自动缩小，只显示「未找到」

显示内容：
- Hikari 支援组件：已找到 / 未找到
- 光负荷、光负荷比例、光负荷已满
- 光溢出状态（80%~99%）、高负荷阈值、高负荷治疗倍率
- 导光封锁状态（100%）、过载恢复阈值（60%）、可控治疗可用
- 守护共鸣状态、减负量、冷却、剩余冷却、触发条件（读条重击 / CastAttack）
- 溢光反震说明
- 自然下降状态与下降速度

按钮：
- 增加光负荷 25
- 重置光负荷
- 开启自然下降 / 关闭自然下降

注意：如果新增 Hikari Debug 显示行，应通过 BuildHikariDebugLines() 更新文本，
不要单独修改绘制代码，避免高度计算与绘制内容不同步。

---

## 6. 常用测试流程

### 测试掉落 / 拾取 / 背包
1. F1 → Spawn 1～5 骷髅
2. 击杀骷髅（骨头 100% 掉落，守护核心 20% 掉落）
3. 靠近掉落物，按 E 拾取
4. F1 右侧背包 Debug 窗口确认：骨头数量合并，Core 独立 stack
   （可临时将 SkeletonEnemy_Variant EnemyDropper 的 Core 掉率改为 1.0 快速测试）

### 测试装备 Core
1. 背包中有 Core 后，F1 → 装备背包中的第一个 Core
2. 装备状态窗口确认 Core 栏从「未装备」变为装备名
3. 战斗属性汇总确认攻击力 / 最大生命值变化
4. F1 → 卸下 Core 到背包 → 背包窗口确认 Core 回到背包
5. 替换测试：背包有 2 个 Core → 装备第一个 → 装备第二个 → 旧 Core 回背包

### 测试 Hikari 光负荷
1. F1 Hikari Debug → 确认光负荷初始值
2. 让玩家 HP 降到 80% 以下，触发 Light Mend，观察光负荷增加
3. F1 Hikari Debug → 增加光负荷 25（重复点击），观察光溢出状态与治疗倍率变化
4. 光负荷达到 100%，确认导光封锁状态：治疗停摆
5. 等待自然下降到 60% 以下，确认导光恢复：治疗恢复
6. F1 Hikari Debug → 重置光负荷，恢复正常状态

### 测试 Guard Resonance / Overflow Counter
1. 通过场景中已配置的 Boss 测试对象 / EnemySpawnArea，或临时将测试生成 Prefab 指向 `SkeletonBossEnemy_Variant`，生成 Boss。
2. 按 2 键激活 Iron Bulwark，等待 Boss CastAttack 读条
3. CastAttack 命中时确认：
   - Hikari Debug 窗口光负荷下降（Guard Resonance 触发：Burden -10）
   - Slot5 Radiant Riposte 从灰色变为发光 Ready 状态
4. 10 秒内按 5 键对 Boss 发动 Radiant Riposte（须在 20m 内）
5. 确认 Boss 收到 3 PDU（Tier 1 = 60）反击伤害
6. 测试 Overflow Counter：先用增加光负荷 25 让 Burden 达到 80%~99%
   激活 Iron Bulwark 后承受 CastAttack，确认 Boss 受到 30 溢光反震伤害

### 测试玩家死亡 / 复活
1. 靠近场景中的 SavePoint 并进入 Trigger（日志确认复活点更新）
2. 让玩家死亡（被敌人打死，或调试方式手动降低 HP）
3. 确认所有活敌人开始 ReturnToSpawn
4. F1 → 复活到最近存档点 → 确认玩家传送到 SavePoint 位置并满血
5. 确认 PlayerSkillManager 技能栏恢复正常，GuardCounterController Ready 已清除
