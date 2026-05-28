# PROJECT_STATE

最后更新：2026-05-28

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
- **当前开发阶段**：早期原型 — 野外战斗 / 刷怪 / 掉落 / 固定格子背包与装备交互 / 玩家技能系统 / Hikari 光负荷 / 茶道具 / 金币钱包 / 茶商店 UI 原型与 Tier 1 数值基准闭环验证
- **项目类型**：3D RPG 动作游戏原型（单人 Tank 保护型）

### 核心玩法一句话

玩家扮演 Tank 型主角，使用减伤 / 反击技能承受敌人压力，通过刷怪掉落装备提升数值，并保护支援角色 Hikari 不因过度治疗产生光负荷过载。

---

## 3. 当前可用核心闭环

```text
玩家移动 / 自动前进（R）/ 鼠标左右键场景输入
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
→ 敌人死亡 → 掉落 ItemDrop / GoldPickup → 按 E 拾取
→ PlayerInventory 加入背包；PlayerWallet 增加 Gold
→ B 打开正式 InventoryCanvas 固定 54 格背包 / EquipmentWindow
→ 背包物品支持左键短按常驻抓取、长按临时抓取、移动到空格 / 交换、右键取消、背包外丢弃二次确认
→ Hover 物品显示 ItemDetailWindow Tooltip；右键菜单按 slot 执行 Equip / Unequip / Use Tea
→ 使用 Tea 道具触发 PlayerTeaBuffController，影响 ItemData 掉率 / Material 额外掉落
→ TeaShopCanvas 茶商店 UI 原型可展示分类 / 商品 / 详情 / 购买 / 试饮 / 赠送；临时 T 键入口已改为 New Input System，可用于打开 / 关闭茶商店。
→ PlayerCombatStats 汇总攻击力与最大生命值
→ HealthComponent 自动应用新上限
→ 刷怪点延迟刷新（EnemySpawnPoint / EnemySpawnArea）
→ 继续战斗
```

---

## 4. 当前已完成系统

### Player / Camera / Movement
- WASD FF14 Legacy-like 相机基准移动，Shift 支持八方向跑步
- `MouseInputGate` 统一判断鼠标按下瞬间属于 UI 还是场景；UI 起始鼠标输入不触发移动 / 相机 / 目标选择
- 左键或右键在场景中按住均可拖动视角；场景左键 + 右键双键前进
- R 键自动前进 v1：右键转向，左键自由视角，左键松开后相机 yaw 回正；WASD 任意方向输入或重新形成的场景双键会打断自动前进
- 持续按住 W/A/S/D 或鼠标双键时按 R，可由自动前进接管当前输入，直到方向输入 / 双键松开后恢复打断规则
- 移动时 Player 朝实际方向转身，无输入不被相机强制转身
- 跳跃动画循环修复，落地后可直接进入 RunForward / Sprint
- `RPGCameraController` 只控制相机，不直接旋转 Player

### Targeting
- 鼠标左键 Raycast 选目标，Tab 从屏幕左到右循环选敌
- 鼠标左键选目标只响应 `MouseInputGate.LeftWorldPressedThisFrame`，点击 UI 不会选中背后敌人
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
- `ItemData` ScriptableObject：Material / Equipment / Tea，含 attackPowerBonus / maxHealthBonus / Icon；Tea 可引用 `TeaBuffData`
- `PlayerInventory`：固定 slot 背包（当前运行时最低 54 格，`null` 表示空格）；Equipment 独立 stack，Material / Tea 等非 Equipment 优先合并，并通过 OnInventoryChanged 驱动正式 UI 刷新
- `PlayerInventory` 支持按 slot 的 Move / Swap / RemoveOneAt / RemoveStackAt；右键 Equip / Use Tea 与丢弃确认必须作用于被点击的 slot，不按 itemId 删除第一个同名物品
- `PlayerEquipment`：Core / Armor / Accessory 三槽（主角武器固定不入装备系统）；背包满时卸装会失败并保留装备，不允许装备消失
- `EnemyDropper`：多条目概率 ItemData 掉落 + Terrain 贴地 Raycast 生成；另支持独立 Gold Drop 配置生成 `GoldPickup`
- `PickupItem`：按 E 拾取 ItemData；AddItem 失败（背包满且无法堆叠）时地上物品保留，不销毁
- `PlayerWallet` 管理 Gold；金币不是 `ItemData`，不进入背包，不受茶 Buff 影响

### Tea / Economy / Shop
- 茶道具系统 v1 已可用：`TeaBuffData` + `PlayerTeaBuffController`，背包右键 Use 茶后应用当前茶 Buff，已有茶状态会被新茶覆盖
- 当前 Tea Buff：非 100% ItemData 掉落概率倍率、Material 成功掉落后概率额外 +1；已实测通过
- 金币钱包与金币掉落 v1 已可用：`GoldPickup` 按 E 拾取后增加 `PlayerWallet.Gold`；`SkeletonEnemy_Variant` 已配置金币掉落并实测通过
- `TeaShopCanvas` 正式茶商店 UI v1 已创建：分类 / 商品格 / 分页 / 详情 / 购买数量 / 购买 / 试饮 / 赠送 / 金币显示；暂未接 NPC

### Formal Inventory / Equipment UI (v1)
- `InventoryCanvas` 已作为正式 Canvas UI 接入，挂在 `UI` 根对象下；B 打开/关闭，Esc 关闭
- `InventoryWindow` 使用程序生成的 RPG 格子背包，`visibleSlotCount` 可在 Inspector 配置（当前测试为 54 格）；PlayerInventory 会在运行时保证容量不低于 UI 格子数
- 背包格子支持左键短按常驻抓取、左键长按临时抓取（当前阈值约 0.10s）、鼠标跟随图标、放到空格 / 与物品交换、右键任意位置取消、无效区域取消
- 常驻抓取时点击背包外 / 非 UI 区域会打开丢弃二次确认；确认后按 source slot 删除，取消则物品留在原 slot；确认窗口可通过 Inspector 绑定正式 UI，未绑定时使用 runtime fallback
- `EquipmentWindow` 显示 Core / Armor / Accessory 三槽装备栏，与背包格子共用图标 / Tooltip 交互风格；背包满时卸装被阻止并保留装备
- `ItemDetailWindow` 是纯 Hover Tooltip：不挡鼠标 Raycast，自动高度，按目标左右侧定位并 Clamp 到屏幕内
- `InventoryContextMenu` 是右键操作菜单：背包 Equipment 可 Equip，背包 Tea 可 Use，已装备槽可 Unequip；按 slot 操作，执行菜单项、点击外部、拖动窗口或关闭背包时隐藏；右键菜单第一次打开被吞的问题已修复
- `InventoryCanvas` 的 Canvas sortingOrder = 1000，用于压住 SkillCanvas / LevelUI；内部窗口置顶用 SetAsLastSibling

### Damage Number / UI Feedback
- `DamageNumberSpawner` / `DamageNumberPopup`：伤害与治疗飘字
- `CombatTextSourceLabel`：伤害可携带来源名（用于 Radiant Riposte 飘字副文本）
- Stone Guard `healingReceivedMultiplier = 1.5`，治疗飘字下显示 `GUARD HEAL`

### Debug UI
- F1 OnGUI Debug 面板（`SkeletonDebugUI`），挂在 `SkeletonSpawnerManager`
- 左侧：骷髅召唤 / 玩家操作 / 敌人调试 / 装备操作 / 战斗属性
- 右侧：背包 Debug 窗口
- 独立：装备状态窗口 / Hikari Debug 窗口（动态高度）
- 左侧 Debug 已显示当前 Tea Buff 状态与 Wallet Gold

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
| `MouseInputGate` | 统一记录鼠标左/右键按下瞬间属于 UI 还是场景，供移动、相机与目标选择共用 |
| `RPGCameraController` | 相机跟随、左键/右键场景拖动、自动前进自由视角回正，不旋转 Player |
| `PlayerTargeting` | 鼠标左键 / Tab 目标选择，提供 CurrentTarget；UI 起始左键不选中场景目标 |
| `PlayerSkillManager` | 统一技能输入、RuntimeState、分发到执行器 |
| `PlayerBasicAttackController` | Slot1/4 执行与共享基础攻击冷却 |
| `PlayerGuardCounterController` | Slot5 Radiant Riposte 执行与 10 秒窗口管理 |
| `PlayerStatusEffectController` | 减伤 / 攻击倍率 / 治疗倍率统一修正 |
| `PlayerInventory` | 固定 slot 运行时库存（当前最低 54 格，null=空格），stack 规则、slot Move/Swap/Remove 与 OnInventoryChanged 事件 |
| `PlayerEquipment` | Core / Armor / Accessory 三槽装备容器 |
| `PlayerCombatStats` | 三槽属性汇总，装备变化自动应用最大生命值 |
| `InventoryCanvasUI` | 正式背包 / 装备 UI 总控，负责格子刷新、Tooltip、右键菜单、slot 移动 / 丢弃确认与 Equip/Unequip 调用 |
| `InventoryGridSlotUI` / `EquipmentSlotUI` | 背包格子与装备槽显示、Hover、左键抓取、右键事件 |
| `ItemDetailPanelUI` / `InventoryContextMenuUI` | 物品 Tooltip 与右键操作菜单（菜单显示 / 隐藏不应依赖 Awake 调用 Hide） |
| `TeaShopCanvasUI` | 正式茶商店 UI 总控：分类、分页、详情、购买、试饮、赠送、金币显示 |
| `GoldPickup` | 金币地面拾取物，按 E 加入 PlayerWallet |
| `PlayerWallet` | Gold 钱包，提供 AddGold / CanSpendGold / TrySpendGold |
| `PlayerTeaBuffController` | 当前茶 Buff、剩余时间与掉落修正查询 |
| `TeaBuffData` | 茶 Buff 静态数据（效果类型、数值、持续时间） |
| `DraggableUIWindow` / `UIWindowBringToFront` | 背包窗口拖动与窗口置顶 |
| `HikariSupportController` | 自动治疗、光负荷、Guard Resonance、溢光反震 |
| `EnemyAI` | FSM + NavMeshAgent + 仇恨系统 |
| `EnemySkillController` | 敌人技能配置与执行（CastAttack / AoE） |
| `EnemyDropper` | ItemData 概率掉落与 GoldPickup 独立金币掉落 |
| `EnemySpawnPoint` | 单怪刷新点 |
| `EnemySpawnArea` | 区域多怪加权随机刷新 |
| `HealthComponent` | 通用血量，触发 OnDamaged / OnHealed 事件 |
| `SkeletonDebugUI` | F1 Runtime Debug Console |

详细脚本架构见 `ARCHITECTURE_REFERENCE.md`。

---

## 6. 当前已知问题 / 未确认事项

- `TeaShopCanvas` 购买 / 试饮 / 赠送逻辑尚未完成 Play Mode 实测；当前未接 NPC，T 键只是临时测试入口。
- `TeaShopCanvas` 试饮 / 赠送冷却和 affinity 为运行时状态，未存档。
- `PlayerWallet` / `PlayerInventory` / Tea Buff / TeaShop 商品与冷却均未持久化。
- 死亡复活正式 UI 尚未制作。
- 正式背包 / 装备 UI v1 已可用，但库存 / 装备 / 背包格子位置不持久化，尚未实现 `ItemDatabase` / SaveData / Load。
- 背包 / 装备仍以 `ItemData` 表示，不支持 `ItemInstance` / 随机词条。
- 丢弃确认窗口代码已支持 Inspector 绑定正式 UI；当前场景是否已创建并绑定正式 `DiscardConfirmPanel` 未确认，未绑定时使用 runtime fallback。
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

1. **最推荐：创建并绑定正式 DiscardConfirmPanel UI**：在 `InventoryCanvas` 下创建可编辑的丢弃二次确认窗口，并绑定 `InventoryCanvasUI` 的 discardConfirmPanel / MessageText / ConfirmButton / CancelButton 字段，替代 runtime fallback 的临时样式。
2. **背包交互回归测试**：重点测试短按 / 长按抓取、移动到空格、交换、右键取消、右键菜单 Equip / Use Tea、背包满时拾取失败与卸装失败保护、丢弃确认只删除 source slot。
3. **TeaShop UI Play Mode 验收**：测试分类 / 分页 / 商品详情 / 购买扣金币入背包 / 试饮直接应用茶 Buff / 赠送扣金币与冷却。
4. **茶商 NPC 接入 v1**：移除或保留为开发专用的 T 键入口，由场景中的茶商交互打开 `TeaShopCanvas`。
5. **Inventory / Wallet / Tea Save/Load v1**：建立 `ItemDatabase(itemId → ItemData)`，保存背包 slot / 装备 / Gold / 当前 Tea Buff（如需要）。
