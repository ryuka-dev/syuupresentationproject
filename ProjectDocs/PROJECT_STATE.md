# PROJECT_STATE

最后更新：2026-05-25

---

## 1. 文档入口

| 文档 | 用途 |
|---|---|
| 本文件 | 当前项目状态摘要（首先阅读） |
| `DEV_RULES.md` | 开发硬规则，修改代码前必读 |
| `GAME_DESIGN_NOTES.md` | 长期设计方向，不等同于当前实现 |
| `GLOSSARY.md` | 术语统一，Hikari / 技能 / 数值命名以此为准 |
| `BALANCE_BASELINE.md` | Tier 1 数值基准，PDU / PHU / BU |
| `ARCHITECTURE_REFERENCE.md` | 详细脚本架构与调用关系 |
| `DEBUG_GUIDE.md` | F1 Debug UI 与测试流程 |
| `CHANGELOG_ARCHIVE.md` | 历史变更归档 |

---

## 2. Project Overview

- **Unity 版本**：6000.4.3f1 (Unity 6)
- **主要渲染管线**：URP 17.4.0
- **主要包**：New Input System 1.19.0 / AI Navigation 2.0.12 / Unity MCP (GitHub)
- **当前主场景**：`Assets/Scenes/SampleScene.unity`
- **当前开发阶段**：早期原型 — 野外战斗 / 刷怪 / 掉落 / 背包 / 装备数值 / 玩家技能系统 / Hikari 光负荷与 Tier 1 数值基准闭环验证
- **项目类型**：3D RPG 动作游戏原型（单人 Tank 保护型）

### 核心玩法一句话

玩家扮演 Tank 型主角，使用减伤 / 反击技能承受敌人压力，通过刷怪掉落装备提升数值，并保护支援角色 Hikari 不因过度治疗产生光负荷过载。

---

## 3. 当前可用核心闭环

```text
玩家移动
→ Tab / 鼠标左键选中目标（头顶倒三角指示器）
→ 1 键 Basic Attack（3m 单体）/ 4 键 Area Attack（5m AOE）
→ 2 键 Iron Bulwark / 3 键 Stone Guard（减伤技能）
→ Canvas 技能栏显示 Active / Cooldown / Ready
→ 敌人 Boss 释放读条重击 / CircleAoE / DonutAoE
→ 玩家使用减伤技能承受 CastAttack → 触发 Guard Resonance → 降低 Hikari 光负荷
→ Iron Bulwark 授权后 10 秒内按 5 键 Radiant Riposte 反击 attacker
→ Hikari 根据玩家 HP 自动 Light Mend / Emergency Prayer 治疗
→ Hikari 光负荷管理（稳定导光 / 光溢出 / 导光封锁 / 导光恢复）
→ 溢光反震（光溢出区间触发 Guard Resonance 时对攻击者反伤 30）
→ 敌人死亡 → 掉落 ItemDrop → 按 E 拾取
→ PlayerInventory 加入背包
→ F1 右侧背包 Debug 面板验证库存
→ 从背包装备 Core / Armor / Accessory
→ PlayerCombatStats 汇总攻击力与最大生命值
→ HealthComponent 自动应用新上限
→ 刷怪点延迟刷新（EnemySpawnPoint / EnemySpawnArea）
→ 继续战斗
```

---

## 4. 当前已完成系统

### Player / Camera / Movement
- WASD FF14 Legacy-like 相机基准移动，Shift 支持八方向跑步
- 右键旋转相机，左键 + 右键双键前进
- 移动时 Player 朝实际方向转身，无输入不被相机强制转身
- 跳跃动画循环修复，落地后可直接进入 RunForward / Sprint
- `RPGCameraController` 只控制相机，不直接旋转 Player

### Targeting
- 鼠标左键 Raycast 选目标，Tab 从屏幕左到右循环选敌
- `TargetSelectionIndicator` 在目标头顶显示倒三角，已修正频闪

### Player Skill System (v0.2)
- `PlayerSkillManager` 统一输入、RuntimeState、技能栏顺序
- Slot1/4 由 `PlayerBasicAttackController` 执行并共享基础攻击冷却（默认 1.0s）
- Slot5 Radiant Riposte 由 `PlayerGuardCounterController` 执行
- `PlayerStatusEffectController` 统一处理减伤 / 攻击倍率 / 治疗倍率
- `SkillCanvas` Canvas 技能栏第一版，动态生成技能格，锚定右下

当前注册技能（1~5）：Basic Attack / Iron Bulwark / Stone Guard / Area Attack / Radiant Riposte

### Hikari Support
- `HikariSupportController`（临时 Cube 测试对象）自动 Light Mend / Emergency Prayer
- 光负荷（Burden）规则：稳定导光 / 光溢出（80%）/ 导光封锁（100%）/ 导光恢复（60%）
- Guard Resonance 降低光负荷（只识别 CastAttack，不识别普通攻击）
- 溢光反震（光溢出区间 + Guard Resonance 成功 → 对 attacker 造成 30 伤害）
- 正式 Hikari 模型 / Prefab / Animator / 跟随 AI 未制作

### Enemy AI / Spawn / Skill
- FSM（Idle / Wander / Chase / Attack / ReturnToSpawn）+ 仇恨系统
- NavMeshAgent 主导移动，Rigidbody 保留碰撞 / fallback
- EnemySpawnPoint（单怪点）/ EnemySpawnArea（区域多怪加权随机）
- 敌人技能：CastAttack 读条重击 / CircleAoE / DonutAoE（月环）
- CastAttack 读条开始后不被玩家拉开距离取消

### Health / Combat Stats
- `HealthComponent` 含 TakeDamage / Heal / SetMaxHealth / OnDamaged / OnHealed
- `PlayerCombatStats` 汇总 Core+Armor+Accessory 攻击力与最大生命值
- `PlayerStatusEffectController` 修正玩家减伤 / 攻击输出 / 治疗接收倍率

### Item / Drop / Inventory / Equipment
- `ItemData` ScriptableObject：Material / Equipment，含 attackPowerBonus / maxHealthBonus
- `PlayerInventory`：Equipment 独立 stack，Material 合并 stack
- `PlayerEquipment`：Core / Armor / Accessory 三槽（主角武器固定不入装备系统）
- `EnemyDropper`：多条目概率掉落 + Terrain 贴地 Raycast 生成

### Damage Number / UI Feedback
- `DamageNumberSpawner` / `DamageNumberPopup`：伤害与治疗飘字
- `CombatTextSourceLabel`：伤害可携带来源名（用于 Radiant Riposte 飘字副文本）
- Stone Guard `healingReceivedMultiplier = 1.5`，治疗飘字下显示 `GUARD HEAL`

### Debug UI
- F1 OnGUI Debug 面板（`SkeletonDebugUI`），挂在 `SkeletonSpawnerManager`
- 左侧：骷髅召唤 / 玩家操作 / 敌人调试 / 装备操作 / 战斗属性
- 右侧：背包 Debug 窗口
- 独立：装备状态窗口 / Hikari Debug 窗口（动态高度）

### Respawn / SavePoint
- `PlayerRespawnPointTracker`：记录最近复活点
- `SavePoint`：Trigger 进入后更新复活点
- 玩家死亡时所有活敌人 ReturnToSpawn

### Terrain / NavMesh
- 主地面改为 Unity 默认 Terrain（旧 Ground 系列对象已删除）
- `NavMeshSurface_World`：已 Bake，NavMeshData 嵌入 SampleScene.unity
- EnemyDropper 贴地 Raycast 已实装

---

## 5. 当前架构总览

| 脚本 | 职责简述 |
|---|---|
| `PlayerController` | 玩家输入移动、朝向、动画参数 |
| `RPGCameraController` | 相机跟随与右键旋转，不旋转 Player |
| `PlayerTargeting` | 鼠标左键 / Tab 目标选择，提供 CurrentTarget |
| `PlayerSkillManager` | 统一技能输入、RuntimeState、分发到执行器 |
| `PlayerBasicAttackController` | Slot1/4 执行与共享基础攻击冷却 |
| `PlayerGuardCounterController` | Slot5 Radiant Riposte 执行与 10 秒窗口管理 |
| `PlayerStatusEffectController` | 减伤 / 攻击倍率 / 治疗倍率统一修正 |
| `PlayerEquipment` | Core / Armor / Accessory 三槽装备容器 |
| `PlayerCombatStats` | 三槽属性汇总，装备变化自动应用最大生命值 |
| `HikariSupportController` | 自动治疗、光负荷、Guard Resonance、溢光反震 |
| `EnemyAI` | FSM + NavMeshAgent + 仇恨系统 |
| `EnemySkillController` | 敌人技能配置与执行（CastAttack / AoE） |
| `EnemySpawnPoint` | 单怪刷新点 |
| `EnemySpawnArea` | 区域多怪加权随机刷新 |
| `HealthComponent` | 通用血量，触发 OnDamaged / OnHealed 事件 |
| `SkeletonDebugUI` | F1 Runtime Debug Console |

详细脚本架构见 `ARCHITECTURE_REFERENCE.md`。

---

## 6. 当前已知问题 / 未确认事项

- 正式背包 UI / 装备 UI / 死亡复活 UI 尚未制作。
- 背包 / 装备仍是运行时原型：库存不持久化，装备仍以 `ItemData` 表示，不支持 `ItemInstance` / 随机词条。
- 掉落系统仍使用 Prefab 上的简单 `EnemyDropper.drops`，尚未实现正式 DropTable ScriptableObject。
- Hikari 正式模型 / Prefab / Animator / 跟随 AI 尚未制作。
- 正式 Hikari UI 未制作；当前 Hikari Debug 只是 OnGUI 窗口。
- `GUARD HEAL` 仍是硬编码文本，未迁移到 `CombatTextSourceLabel` / 本地化 key。
- Debug UI（F1）仍是 OnGUI，正式发布前应隐藏。
- 伤害飘字使用 Instantiate / Destroy，未实现对象池。
- `SkeletonDebugUI` 职责较多，可后续拆分为独立 Panel。
- 玩家技能系统 v0.2 仍是原型，无正式 Buff 优先级 / 覆盖规则 / 状态列表。
- `PlayerTargeting` Tab 候选收集使用 `FindObjectsByType<HealthComponent>`，敌人数量多时有性能隐患。
- Ground / Ground (1) 系列旧地块：已确认删除。
- 敌人 ↔ 玩家碰撞已通过 `EnemyPlayerCollisionIgnore` 处理，敌人间碰撞推动尚未整理。
- NavMesh 当前使用 `layerMask = ~0`，后续建议指定 Ground / Terrain Layer。
- 当前测试地形接近平面，复杂地形下寻路 / 读条 / ItemDrop 贴地表现尚未充分验证。
- 鼠标灵敏度只支持 Inspector 调整，未实现设置菜单。
- 部分 Inspector 数值以场景 / asset 当前保存值为准，代码中 default 值仅作参考。

---

## 7. 推荐下一步

1. **整理 EnemyAI / NavMesh**：实测 Wander / Chase / ReturnToSpawn 稳定性，整理 NavMeshSurface LayerMask，考虑独立保存 NavMeshData。
2. **玩家技能系统小步扩展**：在现有 v0.2 框架上新增一个不同类型技能（打断 / 瞬发伤害），或整理通用技能视觉系统。
3. **最小打断技能 v0.1**：Boss 读条时玩家可调用 `EnemySkillController.InterruptCurrentCast()`，扩展第三种战斗判断维度。
4. **正式 Hikari UI**：制作第一版正式 Hikari 光负荷 UI（Canvas + TMP），替换 OnGUI Debug 窗口。
5. **正式背包 / 装备 UI**：第一版 Canvas 背包界面，支持查看库存与一键装备。
