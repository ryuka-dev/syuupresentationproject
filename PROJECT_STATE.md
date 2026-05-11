# PROJECT_STATE

最后更新：2026-05-12  
当前主要场景：`Assets/Scenes/SampleScene.unity`  
Unity 版本：6000.4.3f1 (Unity 6)

---

## 1. Project Overview

- 项目类型：3D RPG 动作游戏原型
- 当前开发阶段：早期原型 - 野外战斗 / 刷怪 / 掉落 / 背包 / 装备数值闭环验证
- 使用的主要包：
  - URP (Universal Render Pipeline) 17.4.0
  - New Input System 1.19.0
  - AI Navigation 2.0.12
  - Unity MCP (GitHub)
- 当前主要场景：`Assets/Scenes/SampleScene.unity`
- 当前地图状态：
  - 原本的主要 `Ground` 实体已不再作为主要地面使用。
  - 已添加 Unity 默认 `Terrain` 地形对象。
  - SampleScene 已新增 `NavMeshSurface_World` 并完成 NavMesh Bake。
  - 当前 NavMeshData 使用 Unity `BuildNavMesh()` 默认方式嵌入 `SampleScene.unity`，暂未另存为独立 `.asset`。
  - 截图中曾出现多个 `Ground / Ground (1) ...` 系列对象，后续需要确认是否为旧测试地块残留，避免碰撞、NavMesh、刷怪点、掉落物高度或射线检测混乱。

---

## 2. Game Concept

### 当前核心循环

当前已经从单纯战斗测试推进到第一版刷装备闭环：

```text
玩家移动
→ 鼠标左键选中敌人
→ 按 1 使用普通攻击
→ 敌人死亡
→ 任务击杀进度增加
→ 敌人生成多个 ItemDrop
→ 玩家靠近按 E 拾取
→ PlayerInventory 按物品规则加入库存
→ F1 右侧背包 Debug 面板显示当前库存
→ 从背包中装备 Core
→ PlayerEquipment 更新装备槽
→ PlayerCombatStats 重新计算攻击力 / 最大生命值
→ HealthComponent 自动应用最大生命值
→ 玩家变强
→ 刷怪点延迟刷新敌人
→ 继续战斗
```

### 长期方向

- 暗黑破坏神式刷装备循环
- FF14 式野外怪物分布 / 脱战逻辑
- 第三人称 3D RPG 战斗表现
- 长期定位为「单人 Tank 保护型动作 RPG」

玩家扮演 Tank 型主角，通过 MMO 式 GCD / oGCD 技能节奏、Boss 时间轴、AoE 机制、减伤技能与装备构筑，在战斗中承受高压攻击，并保护核心治疗角色 Hikari。

### 长期核心体验

- 玩家通过正确技能承受原本扛不住的攻击。
- 玩家保护 Hikari，使她不因过度治疗而被消耗。
- 装备掉落不仅提供数值成长，也改变技能派生、防御方式和支援连携。

---

## 3. Current Architecture

## 3.1 Enemy AI 系统

### `EnemyAI.cs`

敌人有限状态机（Idle / Wander / Chase / Attack / ReturnToSpawn）+ 仇恨系统 + NavMeshAgent 优先移动 + 野怪脱战回家逻辑。

当前 EnemyAI 已从纯 Rigidbody 直线移动，推进到“NavMeshAgent 优先，Rigidbody fallback”的过渡架构。

重要点：

- 仇恨列表：`Dictionary<Transform, float> hateTable`
- `AddHate(Transform, float)`：统一仇恨入口
- `IsValidTarget(Transform)`：非 null、有 HealthComponent、未死亡、阵营敌对
- `RemoveInvalidHateTargets()`：清除死亡 / 销毁目标
- `SelectHighestHateTarget()`：选择最高仇恨目标
- `_spawnPosition / _spawnRotation`：Awake 记录出生点与朝向
- `wanderRadius`：游荡内圈半径，以 `_spawnPosition` 为中心
- `leashRadius`：活动边界外圈，超过后 ReturnToSpawn
- `OnAttackHit()`：Animation Event 绑定，方法名不可改

#### 当前 FSM 状态

```text
Idle
→ 无目标时原地待机，等待随机 Idle 时间后尝试进入 Wander

Wander
→ 无目标时在出生点附近随机游荡
→ 优先使用 NavMeshAgent
→ Agent 不可用时 fallback 到 Rigidbody 旧逻辑

Chase
→ 有有效仇恨目标时追击
→ 优先使用 NavMeshAgent
→ 目标点临时不可达时不切 Rigidbody，不放弃 Chase
→ 继续沿旧路径 / 最后一次有效 destination 追击，并持续重试目标位置更新
→ 只有 Agent 本身不可用时 fallback 到 Rigidbody Chase

Attack
→ 进入攻击距离后停止 Agent，保留旧攻击逻辑
→ 不修改攻击伤害判定与 OnAttackHit()

ReturnToSpawn
→ 脱战 / 玩家死亡 / 外部命令时回出生点
→ 优先使用 NavMeshAgent
→ Agent 不可用或回家路径失败时 fallback 到 Rigidbody 旧逻辑
→ 到达后回满血并恢复 Idle / Wander 循环
```

#### Wander 当前实现

新增 / 使用的主要参数与状态：

- `wanderMoveSpeed`：游荡移动速度
- `wanderPointReachDistance`：到达游荡目标点的判定距离
- `minIdleTime / maxIdleTime`：每次游荡前的随机待机时间
- `_wanderTarget`：当前游荡目标点
- `_idleTimer`：Idle 待机倒计时

行为：

```text
Idle 随机等待
→ TryPickWanderPoint() 选择目标点
→ Wander 移动
→ 到达后回到 Idle
→ 循环
```

NavMeshAgent 可用时：

- 使用 `NavMesh.SamplePosition` 修正随机点到 NavMesh。
- 使用 `NavMeshAgent.CalculatePath` 检查 PathComplete。
- 通过 `agent.SetDestination(_wanderTarget)` 移动。
- 到达判断使用 Agent 路径状态 / remainingDistance。

Agent 不可用时：

- 使用旧 Rigidbody / `rb.linearVelocity` 逻辑作为 fallback。

#### Chase 当前实现

新增 / 使用的主要参数与状态：

- `chaseDestinationUpdateInterval`：Chase 中更新目标 destination 的间隔，默认 0.2 秒
- `chaseNavMeshSampleDistance`：目标位置附近 NavMesh 采样距离，默认 2f
- `_chasingWithAgent`：当前 Chase 是否由 Agent 驱动
- `_chasePath`：Chase 路径验证用 `NavMeshPath`
- `_nextChaseDestinationUpdateTime`：下次更新目标位置的时间
- `_lastValidChaseDestination`：最后一次成功设置的 Chase NavMesh 目标点
- `_hasLastValidChaseDestination`：是否存在有效 last destination

当前 Chase 规则：

```text
Agent 可用
→ 保持 Agent Chase
→ 定期尝试更新玩家附近 NavMesh destination
→ 更新成功：保存 lastValidChaseDestination
→ 更新失败：不 fallback，不 ResetPath，不放弃 Chase
→ 继续沿旧 path / lastValidChaseDestination 追击

Agent 本身不可用
→ 才 fallback 到旧 Rigidbody Chase
```

重要设计结论：

```text
NavMesh 目标点不可达 ≠ Agent 失效
路径更新失败只是“本次目标更新失败”，不是“放弃 Chase”。
```

#### ReturnToSpawn 当前实现

新增 / 使用的主要参数与状态：

- `returnNavMeshSampleDistance`：出生点附近 NavMesh 采样距离，默认 3f
- `_returnPath`：ReturnToSpawn 路径验证用 `NavMeshPath`
- `_returningWithAgent`：当前 ReturnToSpawn 是否由 Agent 驱动

当前 ReturnToSpawn 规则：

```text
EnterReturnToSpawn()
→ 清空仇恨
→ 停止攻击动画
→ 停止 Chase / Wander Agent 残留路径
→ 尝试 SamplePosition(_spawnPosition)
→ CalculatePath 检查 PathComplete
→ 成功：Agent SetDestination 回家
→ 失败：Rigidbody fallback 回家
→ 到达后 FinishReturnToSpawn()
→ RestoreFullHealth()
→ TransitionTo(Idle)
```

#### NavMeshAgent / Rigidbody 当前过渡规则

当前 SkeletonEnemy 同时存在 NavMeshAgent 与 Rigidbody。

- Wander / Chase / ReturnToSpawn：优先 Agent。
- Attack：停止 Agent，保留原攻击逻辑。
- Agent 不存在 / disabled / 不在 NavMesh 上：fallback 到 Rigidbody。
- 当前仍存在 `rb.isKinematic` 在 Agent 状态与 Rigidbody fallback 间切换的过渡结构。
- 后续建议整理为“NavMeshAgent 负责移动，Rigidbody 主要负责碰撞检测”的更稳定结构。

### `EnemyWorldManager.cs`

场景级敌人管理器第一版。

- 挂载在 SampleScene 的 `EnemyWorldManager` 空 GameObject 上。
- `ForceAllLivingEnemiesReturnToSpawn()` 会查找所有 `EnemyAI`，并调用 `ForceDisengageAndReturnToSpawn()`。
- 当前主要用于玩家死亡时，让所有活着敌人脱战并自己走回出生点。
- 后续敌人数量变多后，应改为注册缓存，不应长期依赖 Find 系列 API。

### `EnemySpawnPoint.cs`

第一版正式野外刷怪点。

- 一个 SpawnPoint 管理一个敌人。
- 字段：`enemyPrefab`、`respawnDelay`、`spawnOnStart`
- `SpawnEnemy()` 在 SpawnPoint 自身位置 / 朝向生成敌人。
- 死亡后等待 `respawnDelay` 自动刷新。
- 会注册生成敌人到 `LevelObjectiveManager`。
- Destroy 仍由 `EnemyDeathHandler` 负责。

---

## 3.2 Player 系统

### `PlayerController.cs`

玩家输入和移动控制。使用 New Input System。`applyRootMotion = false`。

### `RPGCameraController.cs`

第三人称相机跟随。右键拖拽时同步玩家朝向。

本次新增 / 修复：

- 新增 `_isCameraDragging` 状态。
- 新增 `SetCursorVisible(bool visible)`：
  - `Cursor.lockState = CursorLockMode.None`
  - `Cursor.visible = visible`
- 视角拖拽开始时隐藏鼠标。
- 视角拖拽结束时显示鼠标。
- `OnDisable()` / `OnDestroy()` 强制恢复鼠标显示。
- 不使用 `CursorLockMode.Locked`，避免光标跳到屏幕中心。
- 修复 Fullscreen Editor Play Mode / F11 全屏下，按右键或左键操作视角后鼠标永久消失的问题。

注意：

- 当前实现优先保证“松手后光标恢复显示”。
- Unity 原生 API 不擅长精确恢复 OS 鼠标坐标；如果将来需要完全回到按下前的屏幕坐标，需单独设计。

### `PlayerTargeting.cs`

鼠标左键点击，Physics.Raycast 检测目标，验证 HealthComponent + FactionComponent + 敌对关系后设为 `CurrentTarget`。提供 `ClearTarget()`，玩家死亡时会主动清空目标。

### `PlayerSkillController.cs`

按数字键 1 使用普通攻击。

当前职责：

- 读取 `PlayerTargeting.CurrentTarget`
- 验证目标有效性
- 读取 `PlayerCombatStats.CurrentNormalAttackDamage` 作为最终普通攻击伤害
- 调用 `HealthComponent.TakeDamage(finalDamage, transform)`
- 触发 `Attack` Trigger 播放攻击动画

当前逻辑变化：

- 不再直接读取 `PlayerEquipment.EquippedCore.AttackPowerBonus`。
- 不再写死 Core 装备 +10 伤害。
- 若 Player 上没有 `PlayerCombatStats`，回退使用原本 `normalAttackDamage`。

### `PlayerDeathHandler.cs`

玩家死亡处理：

- 清除当前锁定目标
- 禁用 PlayerController / PlayerSkillController / PlayerTargeting / RPGCameraController
- 清零 Rigidbody 并设置 `isKinematic = true`
- 播放死亡动画
- 调用 `EnemyWorldManager.ForceAllLivingEnemiesReturnToSpawn()`
- 复活时只恢复玩家自身，不额外重置敌人

---

## 3.3 Health & Combat Stats

### `HealthComponent.cs`

通用血量组件。

当前包含：

- `TakeDamage(float)`：向后兼容接口
- `TakeDamage(float, Transform attacker)`：带攻击来源接口
- `RestoreFullHealth()`：恢复满血并触发血条刷新
- `SetMaxHealth(float newMaxHealth, bool keepCurrentRatio = false)`：动态修改最大生命值
- `IsDead`
- 事件：
  - `OnHealthChanged(float, float)`
  - `OnDied`
  - `OnDamaged(float, Transform)`

`SetMaxHealth` 规则：

- `newMaxHealth < 1f` 时修正为 1。
- `keepCurrentRatio == true`：按旧生命比例换算当前生命值。
- `keepCurrentRatio == false`：当前生命值保持原值，但超过新上限时裁剪。
- 最后触发 `OnHealthChanged(currentHealth, maxHealth)`。
- 不处理死亡 / 复活状态。
- 不调用 `RestoreFullHealth()`。

### `PlayerCombatStats.cs`

新增的玩家战斗数值组件，挂载在 Player 上。

职责：

- 统一计算当前普通攻击伤害。
- 统一计算当前最大生命值。
- 监听装备变化。
- 装备变化时自动应用当前最大生命值到 `HealthComponent`。

字段 / 属性：

- `[SerializeField] private float baseNormalAttackDamage = 20f`
- `[SerializeField] private float baseMaxHealth = 100f`
- `BaseNormalAttackDamage`
- `EquipmentAttackPowerBonus`
- `CurrentNormalAttackDamage`
- `BaseMaxHealth`
- `EquipmentMaxHealthBonus`
- `CurrentMaxHealth`

计算规则：

```text
EquipmentAttackPowerBonus = EquippedCore?.AttackPowerBonus ?? 0
CurrentNormalAttackDamage = BaseNormalAttackDamage + EquipmentAttackPowerBonus
EquipmentMaxHealthBonus = EquippedCore?.MaxHealthBonus ?? 0
CurrentMaxHealth = Max(1, BaseMaxHealth + EquipmentMaxHealthBonus)
```

装备变化自动刷新：

```text
PlayerEquipment.OnEquipmentChanged
→ PlayerCombatStats.HandleEquipmentChanged()
→ ApplyCurrentMaxHealth(false)
→ HealthComponent.SetMaxHealth(CurrentMaxHealth, false)
```

`ApplyCurrentMaxHealth(bool keepCurrentRatio = false)`：

- 若缺少 HealthComponent，Warning 后 return。
- 调用 `HealthComponent.SetMaxHealth(CurrentMaxHealth, keepCurrentRatio)`。
- 当前自动流程使用 `keepCurrentRatio: false`。
- 装备加最大生命值时不会自动补满血。
- 卸下装备导致上限降低时，当前 HP 超过新上限会被裁剪。

---

## 3.4 Item / Drop / Inventory / Equipment 系统

### `ItemData.cs`

物品数据 ScriptableObject。

当前字段：

- `itemId`：稳定内部 ID，用于掉落表、背包、存档、多语言 key
- `itemName`：当前阶段临时显示名
- `rarity`：`ItemRarity`，Common / Rare / Epic / Legendary
- `description`
- `itemType`：`ItemType`
- `maxStack`
- `equipmentSlotType`：`EquipmentSlotType`
- `attackPowerBonus`
- `maxHealthBonus`

枚举：

```csharp
public enum ItemType
{
    Material,
    Equipment,
    Consumable,
    Currency,
    Quest,
    Cosmetic
}

public enum EquipmentSlotType
{
    None,
    Core,
    Weapon,
    Armor,
    Accessory
}
```

只读属性：

- `ItemType`
- `MaxStack`
- `EquipmentSlotType`
- `AttackPowerBonus`
- `MaxHealthBonus`

`OnValidate()` 规则：

- `maxStack < 1` → 修正为 1
- `itemType == Equipment` → `maxStack = 1`
- `itemType != Equipment` → `equipmentSlotType = None`
- `itemType != Equipment` → `attackPowerBonus = 0`
- `itemType != Equipment` → `maxHealthBonus = 0`
- `attackPowerBonus < 0` → 修正为 0
- `maxHealthBonus < 0` → 修正为 0

注意：

- 当前固定装备属性直接存放在 ItemData 上。
- 这适合第一版固定装备。
- 将来若要支持随机词条，需要升级到 `ItemInstance`。

### `ItemStack.cs`

库存堆叠数据类。

当前内容：

- 保存 `ItemData itemData` 与 `int count`
- 只读属性：
  - `ItemData`
  - `ItemId`
  - `ItemName`
  - `Count`
  - `IsFull`
  - `RemainingCapacity`
- `AddCount(int amount)`：
  - 返回实际增加数量
  - 不超过 `ItemData.MaxStack`
  - `amount <= 0` 时 Warning + return 0
- `RemoveCount(int amount)`：
  - 返回实际减少数量
  - 用于 `PlayerInventory.RemoveItem`
- 构造函数：
  - `count < 1` 修正为 1
  - `count > itemData.MaxStack` 修正为 `MaxStack`

### `PlayerInventory.cs`

玩家运行时背包容器，挂载在 Player 上。

当前内部结构：

- `List<ItemStack> _items`
- `ItemCount`：所有 stack 的 Count 总和
- `StackCount`：`_items.Count`
- `Items`：`IReadOnlyList<ItemStack>`

当前方法：

#### `AddItem(ItemData item)`

规则：

- `item == null` → Warning + false
- `item.ItemType == Equipment`：
  - 永远新增独立 `ItemStack(item, 1)`
  - 不与同 itemId 装备合并
- 非 Equipment：
  - 查找相同 itemId 且未满 stack
  - 找到则 `AddCount(1)`
  - 找不到则新增 `ItemStack(item, 1)`

#### `RemoveItem(ItemData item)`

规则：

- `item == null` → Warning + false
- 查找第一个 itemId 相同的 stack
- 找不到 → Warning + false
- `Count > 1` → `RemoveCount(1)`
- `Count == 1` → 移除整个 stack
- 成功后输出库存统计并 return true

#### `FindFirstEquipmentBySlot(EquipmentSlotType slotType)`

规则：

- 遍历 `_items`
- 找到第一个：
  - `ItemData != null`
  - `Count > 0`
  - `ItemType == Equipment`
  - `EquipmentSlotType == slotType`
- 返回该 `ItemData`
- 不从背包移除
- 找不到返回 null

当前限制：

- 没有背包容量。
- 没有正式背包 UI。
- 没有删除 / 使用 / 排序 / 卖出。
- 停止 Play Mode 后库存消失。
- 装备目前仍以 `ItemData` 表示，不支持同名装备不同词条。

### `PlayerEquipment.cs`

玩家装备容器雏形，当前只支持 Core 槽。

字段 / 属性：

- `[SerializeField] private ItemData equippedCore`
- `EquippedCore`
- `HasCoreEquipped`

事件：

```csharp
public event System.Action OnEquipmentChanged;
```

方法：

#### `EquipCore(ItemData item, out ItemData replacedItem)`

规则：

- `replacedItem = null`
- `item == null` → Warning + false
- `item.ItemType != Equipment` → Warning + false
- `item.EquipmentSlotType != Core` → Warning + false
- 通过检查后：
  - `replacedItem = equippedCore`
  - `equippedCore = item`
  - 触发 `OnEquipmentChanged`
  - return true

保留兼容重载：

```csharp
public bool EquipCore(ItemData item)
{
    return EquipCore(item, out _);
}
```

#### `UnequipCore()`

- 当前无装备 → Warning + null
- 有装备：
  - 保存当前 Core
  - `equippedCore = null`
  - 触发 `OnEquipmentChanged`
  - 返回被卸下的 `ItemData`

#### `ClearEquipment()`

- 若当前有 Core，则清空并触发 `OnEquipmentChanged`
- 若已经为空，不触发事件

### 背包容器 ↔ 装备容器 当前流程

#### 从背包装备第一个 Core

由 `SkeletonDebugUI` 的“装备背包中的第一个 Core”按钮触发：

```text
ResolvePlayerInventory
ResolvePlayerEquipment
FindFirstEquipmentBySlot(Core)
EquipCore(newCore, out replacedCore)
RemoveItem(newCore)
如果 replacedCore != null → AddItem(replacedCore)
OnEquipmentChanged → PlayerCombatStats 自动刷新
```

#### 卸下 Core 到背包

由 `SkeletonDebugUI` 的“卸下 Core 到背包”按钮触发：

```text
ResolvePlayerInventory
ResolvePlayerEquipment
UnequipCore()
如果成功 → PlayerInventory.AddItem(unequippedCore)
OnEquipmentChanged → PlayerCombatStats 自动刷新
```

#### 强制清空 Core（Debug）

保留直接清空装备中的 Core 的 Debug 用途。该按钮只调用 `UnequipCore()`，不把装备放回背包，适合调试清理，不属于正常装备流程。建议按钮显示名使用“强制清空 Core（Debug）”，避免和正常卸下流程混淆。

---

## 3.5 Drop 系统

### `PickupItem.cs`

地面拾取物脚本。

- `itemData`
- `playerTag = "Player"`
- `SetItemData(ItemData data)`：运行时由 EnemyDropper 注入
- Trigger 检测玩家进入 / 离开范围
- 按 E 拾取
- 拾取成功后调用 `PlayerInventory.AddItem(itemData)` 并 Destroy 自身
- 若缺少 `itemData` 或 `PlayerInventory`，只 Warning，不崩溃

### `EnemyDropper.cs`

敌人死亡掉落入口。

当前已从固定 100% 单物品掉落升级为“小型多物品掉落测试版”，并新增 Terrain / 地面贴合生成逻辑。

新增结构：

```csharp
[System.Serializable]
public class DropEntry
{
    public ItemData item;
    [Range(0f, 1f)] public float dropChance = 1f;
    public Vector3 offset;
}
```

字段：

- `dropItem`：旧版单物品 fallback，保留兼容
- `pickupPrefab`
- `dropOffset`
- `List<DropEntry> drops`

Ground Placement 字段：

- `alignDropsToGround = true`：是否启用贴地生成
- `groundRaycastStartHeight = 5f`：从候选点上方多高开始 Raycast
- `groundRaycastDistance = 20f`：向下检测距离
- `groundOffset = 0.1f`：命中地面后上抬，避免嵌入地面
- `groundLayerMask = ~0`：地面检测 LayerMask，当前默认全 Layer
- `maxDropHeightAboveOwnerBounds = 0.2f`：允许命中点略高于敌人 Collider 顶部的容错值

新增 / 修改逻辑：

- `GetGroundedDropPosition(Vector3 candidatePosition)`：
  - 从候选点上方向下 Raycast。
  - 命中地面后返回 `hit.point + Vector3.up * groundOffset`。
  - 未命中时 fallback 到原候选位置，不阻断掉落。
  - 若命中点高度高于敌人自身非 Trigger Collider 顶部 + `maxDropHeightAboveOwnerBounds`，判定为头顶物体 / 生物 / Collider 误命中，忽略并 fallback。
- `GetMaxAllowedDropGroundY()`：
  - 遍历自身及子对象非 Trigger Collider。
  - 取 `bounds.max.y` 最大值作为敌人自身碰撞体顶部高度。
  - 找不到有效 Collider 时 fallback 到 `transform.position.y + maxDropHeightAboveOwnerBounds`。
- `OnValidate()`：保护 Ground Placement 参数不为负或过小。

掉落流程：

```text
HandleDied()
→ pickupPrefab == null 时 Warning + return
→ 如果 drops.Count > 0：
    遍历每个 DropEntry
    item == null → Warning + skip
    Random.value <= dropChance → Instantiate ItemDrop
    candidatePosition = transform.position + dropOffset + entry.offset
    spawnPosition = GetGroundedDropPosition(candidatePosition)
    PickupItem.SetItemData(entry.item)
    return
→ 如果 drops.Count == 0：
    fallback 到旧 dropItem 固定掉落
    candidatePosition = transform.position + dropOffset
    spawnPosition = GetGroundedDropPosition(candidatePosition)
```

注意：

- 掉落概率逻辑未改变。
- `PickupItem.SetItemData()` 调用方式未改变。
- `PickupItem.cs`、`EnemyDeathHandler.cs`、Prefab、Scene、Animator 均未因贴地逻辑修改。
- 当前 `groundLayerMask = ~0` 仍可能检测到不应视为地面的 Layer，后续可改为 Terrain / Ground / Environment 专用 Layer。

### `SkeletonEnemy.prefab` 当前掉落配置

`Assets/Resources/SkeletonEnemy.prefab` 的 `EnemyDropper.drops`：

```text
drops[0]
- Item: Assets/Items/TestItem_Bone.asset
- DropChance: 1.00
- Offset: (0, 0, 0)

drops[1]
- Item: Assets/Items/TestItem_GuardCore.asset
- DropChance: 0.20
- Offset: (0.4, 0, 0.2)
```

含义：

- 骨头 100% 掉落。
- 守护核心 20% 概率额外掉落。
- 可临时将 Core 掉率改为 1.0 来测试拾取 / 背包 / 装备流程，确认后再改回 0.2。

### 测试物品资产

#### `Assets/Items/TestItem_Bone.asset`

- `itemId = bone`
- `itemName = 骨头`
- `ItemType = Material`
- `MaxStack = 99`
- 用途：骷髅基础材料掉落

#### `Assets/Items/TestItem_GuardCore.asset`

- `itemId = test_guard_core`
- `itemName = 守护核心`
- `ItemType = Equipment`
- `EquipmentSlotType = Core`
- `MaxStack = 1`
- `AttackPowerBonus = 20`
- `MaxHealthBonus = 50`
- 用途：Debug 用 Core 装备，骷髅低概率掉落

---

## 3.6 Debug UI / Runtime Debug Menu

### `SkeletonDebugUI.cs`

当前已从单纯 Skeleton 生成器 UI，扩展成运行时 Debug 控制台。

实际挂载对象：

- `SkeletonSpawnerManager`
  - `SkeletonSpawner`
  - `SkeletonDebugUI`
  - `PhysicsLayerSetup`

注意：

- F1 Debug UI 使用 `OnGUI()` / IMGUI 绘制。
- 这些按钮不会出现在 Hierarchy 的 UI 对象列表中。
- 它不是正式游戏 UI，定位是开发 / Debug / Cheat 面板。
- 正式背包 UI、装备 UI、死亡复活 UI 不应使用 OnGUI，应该使用 Canvas + TMP + Button 或 UI Toolkit。

### 当前左侧 Debug 面板功能

- F1 显示 / 隐藏
- Skeleton Spawner：
  - Spawn 1
  - Spawn 5
  - Clear All
- 玩家：
  - 恢复玩家满血
  - 复活玩家测试
  - 复活到最近存档点
- 敌人调试：
  - 显示当前目标
  - 重置当前目标敌人
- 装备调试：
  - 显示当前 Core
  - 装备测试 Core
  - 强制清空 Core（Debug）/ 旧名“卸下测试 Core”
  - 卸下 Core 到背包
  - 装备背包中的第一个 Core
- 战斗属性调试：
  - Base Normal Attack Damage
  - Equipment Attack Bonus
  - Current Normal Attack Damage
  - Equipment Max Health Bonus
  - Base Max Health
  - Current Max Health
  - 应用当前最大生命值

### 当前右侧背包 Debug 窗口

新增独立右侧 OnGUI 区域，不塞进左侧长条。

字段：

- `private Vector2 inventoryScrollPosition`

位置 / 尺寸：

```csharp
float invPanelWidth  = Mathf.Clamp(Screen.width * 0.28f, 320f, 460f);
float invPanelHeight = Mathf.Max(300f, Screen.height - margin * 2f);
float invPanelX      = Screen.width - invPanelWidth - margin;
float invPanelY      = margin;
```

显示内容：

- `--- 背包调试 ---`
- `ItemCount`
- `StackCount`
- 每个 `ItemStack`：
  - 物品名
  - itemId
  - Count
  - ItemType
  - Equipment 时显示 EquipmentSlotType
  - AttackPowerBonus > 0 时显示 ATK Bonus
  - MaxHealthBonus > 0 时显示 Max HP Bonus
- 空背包时显示“背包为空”
- 空 Stack / ItemData 缺失时显示错误信息并继续，不崩溃

用途：

- 验证骨头数量是否合并。
- 验证 Core 是否从背包移除。
- 验证卸下 Core 后是否回到背包。
- 验证替换 Core 时旧 Core 是否回到背包。

### OnGUI 是否可留到正式游戏

- 可以作为隐藏 Debug / Cheat / Developer Menu 保留。
- 不建议作为正式玩家 UI 使用。
- 正式发布前建议使用 `UNITY_EDITOR || DEVELOPMENT_BUILD` 或特殊开关保护，避免普通玩家误触。

---

## 3.7 Respawn / SavePoint 基础系统

### `PlayerRespawnPointTracker.cs`

记录最近复活点位置和朝向。

- `CurrentRespawnPosition`
- `CurrentRespawnRotation`
- `Awake()` 默认使用玩家初始位置和朝向
- `SetRespawnPoint(Vector3, Quaternion)` 更新最近复活点

### `SavePoint.cs`

Trigger 检测玩家进入后更新玩家最近复活点。

当前只记录复活点，不执行真正复活。正式复活 UI 尚未接入。

### Debug 复活流程

```text
玩家进入 SavePoint Trigger
→ SavePoint 更新 PlayerRespawnPointTracker
→ 玩家死亡
→ Debug UI 点击“复活到最近存档点”
→ Player 传送到记录位置 / 朝向
→ RestoreFullHealth()
→ PlayerDeathHandler.ResetForRespawn()
→ LevelObjectiveManager.ClearLevelResultForRespawn()
```

---

## 4. Important Unity Objects

### SampleScene.unity 主要对象

#### Player

当前关键组件：

- Transform
- Animator
- CapsuleCollider
- Rigidbody
- PlayerController
- FactionComponent（faction=Player）
- HealthComponent
- WorldHealthBar
- PlayerTargeting
- PlayerSkillController
- PlayerDeathHandler
- PlayerRespawnPointTracker
- PlayerInventory
- PlayerEquipment
- PlayerCombatStats

Tag：`Player`  
PickupItem 依赖该 Tag 判断玩家。

#### Main Camera

- `RPGCameraController`
- target = Player Transform
- 当前已负责拖拽时隐藏鼠标、松手 / 禁用 / 销毁时恢复鼠标。

#### SkeletonSpawnerManager

- `SkeletonSpawner`
- `SkeletonDebugUI`
- `PhysicsLayerSetup`

F1 Debug UI 来源是这里，不是 Hierarchy 里的 Canvas Button。

#### DebugManager

截图中发现 `DebugManager` 上存在 `Missing (Mono Script)`。

- 这很可能对应 Console 中的：`The referenced script (Unknown) on this Behaviour is missing!`
- 当前 F1 Debug UI 不依赖 DebugManager，而是挂在 SkeletonSpawnerManager 上。
- 后续可单独移除该 Missing Script 组件，或删除无用 DebugManager。

#### NavMeshSurface_World

- SampleScene 中新增的 NavMeshSurface 场景对象。
- 用于当前 Terrain / 地面上的敌人 NavMesh 寻路。
- 当前设置：
  - `agentTypeID = 0`（Humanoid）
  - `collectObjects = All`
  - `useGeometry = PhysicsColliders`
  - `layerMask = ~0`（全 Layer）
- NavMesh 已 Bake。
- NavMeshData 当前嵌入 `SampleScene.unity`，尚未独立保存为 `.asset`。

#### EnemySpawnPoint_Test

- Components: `EnemySpawnPoint`
- enemyPrefab = `Assets/Resources/SkeletonEnemy.prefab`
- respawnDelay = 5 秒
- spawnOnStart = true

#### Terrain / Ground

- 已添加默认 `Terrain`。
- 原 `Ground` 不再作为主要地面使用。
- 需确认旧 Ground 系列对象是否仍有用途。

### Prefabs

#### `Assets/Resources/SkeletonEnemy.prefab`

当前包含：

- EnemyAI
- HealthComponent
- WorldHealthBar
- EnemyDeathHandler
- EnemyDropper
- FactionComponent
- FOVDetector
- NavMeshAgent

当前 NavMeshAgent 主要参数：

- `speed = 2f`（Wander 时与 `wanderMoveSpeed` 一致；Chase / ReturnToSpawn 会由 EnemyAI 使用 `moveSpeed` 覆盖）
- `radius = 0.35f`
- `height = 1.8f`
- `baseOffset = 0f`
- `stoppingDistance = 0.2f`
- `autoBraking = true`
- `updateRotation = true`
- Rigidbody 仍保留，当前用于非 Agent fallback / 既有碰撞结构

当前 `EnemyDropper` 多掉落配置：

- 骨头：100%
- 守护核心：20%

#### `Assets/Resources/ItemDrop.prefab`

- Sphere 临时外观
- SphereCollider，`isTrigger = true`
- 挂载 `PickupItem`
- `itemData` 运行时由 `EnemyDropper` 注入

---

## 5. Input / Control

- 使用：New Input System 1.19.0
- 玩家移动：WASD
- 目标选择：鼠标左键
- 技能释放：键盘 1
- 拾取物品：E
- 摄像机 / 玩家朝向：鼠标右键拖拽
- Debug UI：F1
- 关卡重开：R，仅旧 Victory / Game Over 后生效

Cursor 当前规则：

- 视角拖拽开始：隐藏鼠标
- 视角拖拽结束：显示鼠标
- RPGCameraController 被禁用 / 销毁：强制显示鼠标
- 不使用 CursorLockMode.Locked

---

## 6. Completed Features

### 战斗 / AI

- ✅ 玩家第三人称移动控制
- ✅ 敌人 FSM AI（Idle / Wander / Chase / Attack / ReturnToSpawn）
- ✅ EnemyAI Idle / Wander 混合游荡
- ✅ SampleScene NavMeshSurface_World + NavMesh Bake
- ✅ SkeletonEnemy.prefab 添加 NavMeshAgent
- ✅ EnemyAI Wander 优先使用 NavMeshAgent
- ✅ EnemyAI Chase 优先使用 NavMeshAgent
- ✅ EnemyAI Chase 目标点不可达时保持 Agent Chase，不切 Rigidbody
- ✅ EnemyAI ReturnToSpawn 优先使用 NavMeshAgent
- ✅ EnemyAI Agent 不可用时保留 Rigidbody fallback
- ✅ FOV 视野检测系统
- ✅ 阵营系统
- ✅ 血量系统
- ✅ 世界空间血条显示
- ✅ 敌人攻击动画 + Animation Event 伤害触发
- ✅ 敌人死亡动画与延迟销毁
- ✅ 仇恨系统
- ✅ 敌人脱战回家
- ✅ 玩家死亡处理
- ✅ 玩家普通攻击动画
- ✅ 上半身 / 下半身动画分离
- ✅ 玩家普通攻击伤害读取 PlayerCombatStats

### 复活 / SavePoint

- ✅ 最近复活点记录
- ✅ RestoreFullHealth
- ✅ PlayerDeathHandler.ResetForRespawn
- ✅ Debug 复活到最近 SavePoint
- ✅ 玩家死亡时所有活敌人 ReturnToSpawn

### 掉落 / 拾取 / 背包

- ✅ ItemData ScriptableObject
- ✅ ItemType / MaxStack
- ✅ EquipmentSlotType
- ✅ AttackPowerBonus / MaxHealthBonus
- ✅ ItemStack 堆叠结构
- ✅ ItemStack 支持 AddCount / RemoveCount
- ✅ PlayerInventory AddItem
- ✅ PlayerInventory RemoveItem
- ✅ PlayerInventory FindFirstEquipmentBySlot
- ✅ PickupItem 按 E 拾取
- ✅ ItemDrop.prefab
- ✅ EnemyDropper 多条目概率掉落测试版
- ✅ EnemyDropper 掉落物 Terrain / 地面贴合生成第一版
- ✅ EnemyDropper 贴地 Raycast 高度限制，避免把敌人头顶 Collider 误判为地面
- ✅ SkeletonEnemy.prefab：骨头 100% + 守护核心 20%

### 装备 / 数值

- ✅ PlayerEquipment Core 装备槽
- ✅ EquipCore 支持替换旧 Core
- ✅ UnequipCore 返回卸下装备
- ✅ OnEquipmentChanged 事件
- ✅ PlayerCombatStats 监听装备变化
- ✅ Core 装备影响普通攻击伤害
- ✅ Core 装备影响最大生命值
- ✅ 最大生命值变化自动应用到 HealthComponent
- ✅ 背包 → 装备槽
- ✅ 装备槽 → 背包
- ✅ 替换 Core 时旧 Core 回背包

### Debug / 工具

- ✅ F1 OnGUI Debug 面板
- ✅ 当前目标敌人显示与 ResetToSpawn
- ✅ Core 装备测试按钮
- ✅ 从背包装备第一个 Core
- ✅ 卸下 Core 到背包
- ✅ 战斗属性显示
- ✅ 右侧背包 Debug 窗口
- ✅ 鼠标拖拽后永久消失问题修复

---

## 7. In Progress / Known Issues

### 系统限制

- ⚠️ 当前库存只存在运行时内存中，停止 Play Mode 后会消失。
- ⚠️ 当前装备仍使用 `ItemData` 表示，不支持同名装备不同随机词条。
- ⚠️ 尚未实现 `ItemInstance`。
- ⚠️ 尚未实现正式背包 UI / 正式装备 UI。
- ⚠️ 尚未实现背包容量。
- ⚠️ 尚未实现装备拖拽、使用、删除、卖出。
- ⚠️ 尚未实现正式 DropTable ScriptableObject。
- ⚠️ 当前 EnemyDropper 是 Prefab 上的简单 drops 列表，不是完整掉落表系统。
- ⚠️ 当前 Core 掉落概率配置为 20%，需 Play Mode 多次击杀或临时改 1.0 验证。

### 场景 / UI

- ⚠️ LevelUI TMP 字体仍需确认是否绑定 `SourceHanSansSC-Medium_TMP.asset`。
- ⚠️ DebugManager 上存在 Missing Mono Script，可单独清理。
- ⚠️ F1 Debug UI 是 OnGUI / IMGUI，不是正式 UI。
- ⚠️ 正式发布前应隐藏或限制 Debug 菜单。
- ⚠️ 玩家死亡后 RPGCameraController 被禁用，相机静止在死亡位置，暂无死亡镜头演出。
- ⚠️ 正式死亡 / 复活 UI 尚未接入，当前仍主要依赖 Debug UI。

### 性能 / 架构

- ⚠️ `ScanForTarget()` 每 0.2s 使用 FindObjectsOfType / FindObjectsByType，敌人数量多时有性能隐患。
- ⚠️ `EnemyWorldManager` 与 `EnemySpawnPoint` 当前仍有 Find 系列 API，未来应改为注册缓存。
- ⚠️ `SkeletonDebugUI` 目前职责较多，已接近 Runtime Debug Console，后续可拆分为 InventoryDebugPanel / EquipmentDebugPanel / CombatStatsDebugPanel。
- ⚠️ `EntityStats.cs` 已创建但未集成。
- ⚠️ EnemyAI 目前处于 NavMeshAgent / Rigidbody 过渡架构，`rb.isKinematic` 会在 Agent 状态与 fallback 状态间切换；后续建议整理驱动权，减少物理抖动风险。
- ⚠️ Chase 目标不可达时会保持 Chase 并持续重试，不会因寻路失败主动放弃；如果玩家长期站在敌人永远到不了的位置，敌人可能停在最后可达点附近持续追击，后续可考虑 Evade / Unreachable 规则。

### 地图 / Terrain

- ⚠️ 添加 Terrain 后，需要继续确认：
  - Player / Enemy 落地高度
  - EnemySpawnPoint 位置是否在 NavMesh 上或足够接近 NavMesh
  - SavePoint 位置
  - NavMesh / AI 可行走区域
- ✅ ItemDrop 掉落高度已通过 EnemyDropper 贴地 Raycast 第一版改善。
- ⚠️ 旧 Ground 系列对象需确认是否保留，避免参与 NavMesh Bake 或影响碰撞 / 射线检测。
- ⚠️ 当前 NavMeshSurface 使用 `layerMask = ~0` 与 `collectObjects = All`，后续建议整理 Ground / Terrain / Environment Layer，避免临时对象或旧测试地块影响 NavMesh。
- ⚠️ Terrain 或障碍物变化后需要重新 Bake NavMesh。

---

## 8. Development Rules

### 修改前必读

- ❌ 不要随意重命名 public / SerializedField 字段，Inspector 可能已绑定。
- ❌ 不要重构无关代码。
- ❌ 不要读取完整 Console、完整 Assets、完整 Scene Hierarchy。
- ✅ 修改前先定位相关文件，只读取必要内容。
- ✅ 优先小步修改，每次改动后确认编译通过。
- ✅ 如果功能较大，先拆分任务，再只执行第一步。
- ✅ MCP 提示词应包含当前项目情况、本次目标、限制、读取文件、输出要求和验收标准。

### 当前用户偏好

- 功能拆解不要过细，允许每次任务稍微复杂约 30%。
- 每一步仍应有明确目标。
- 如果需要扩大范围，需要先说明原因。
- 尽量避免 AI 扫描整个 Assets、完整 Console、完整 Scene Hierarchy。
- 不要让 AI 重构无关代码。
- 不要让 AI 修改 Animator / Prefab / Scene，除非本次目标确实需要。

### Animator 修改注意

骷髅：

- `Attack → Idle` 的 `hasExitTime=true, exitTime=0.9` 不可改。
- `IsDead` Trigger 由 EnemyDeathHandler 触发。
- `OnAttackHit()` 方法名不可改。

玩家：

- Base Layer 的 Death 状态无出口过渡，不可添加出口。
- `IsDead` Trigger 由 PlayerDeathHandler 触发。
- `Attack` Trigger 由 PlayerSkillController 触发，仅在 UpperBody Layer 使用。
- UpperBody Layer 的 `Any State → UpperBodyIdle`（IsDead 条件）不可删除。
- `UpperBodyIdle.anim` 不可删除。

### 代码修改原则

- Rigidbody 使用 `rb.linearVelocity`（Unity 6）。
- 输入系统使用 `Mouse.current` / `Keyboard.current`，不得使用旧 `UnityEngine.Input`。
- Debug OnGUI 可以继续用于开发工具，但正式 UI 应使用 Canvas / TMP / Button 或 UI Toolkit。
- EnemyAI 当前优先使用 NavMeshAgent 处理 Wander / Chase / ReturnToSpawn，但必须保留 Agent 不可用时的 Rigidbody fallback。
- Chase 中“目标点暂时不可达”不应被视为 Agent 失效；不要因此切 Rigidbody，应该继续追最后有效 destination 并持续重试。

---

## 9. Files That Should Be Treated Carefully

### 核心脚本

- `Assets/Scripts/Enemy/EnemyAI.cs`
- `Assets/Scripts/Enemy/EnemyWorldManager.cs`
- `Assets/Scripts/Enemy/EnemySpawnPoint.cs`
- `Assets/Scripts/Enemy/EnemyDeathHandler.cs`
- `Assets/Scripts/Enemy/FactionSystem.cs`
- `Assets/Scripts/HealthComponent.cs`
- `Assets/Scripts/RPGCameraController.cs`
- `Assets/Scripts/Player/PlayerTargeting.cs`
- `Assets/Scripts/Player/PlayerSkillController.cs`
- `Assets/Scripts/Player/PlayerDeathHandler.cs`
- `Assets/Scripts/Player/PlayerRespawnPointTracker.cs`
- `Assets/Scripts/Player/PlayerInventory.cs`
- `Assets/Scripts/Player/PlayerEquipment.cs`
- `Assets/Scripts/Player/PlayerCombatStats.cs`
- `Assets/Scripts/Items/ItemData.cs`
- `Assets/Scripts/Items/ItemStack.cs`
- `Assets/Scripts/Items/PickupItem.cs`
- `Assets/Scripts/Items/EnemyDropper.cs`
- `Assets/Scripts/Spawner/SkeletonDebugUI.cs`
- `Assets/Scripts/Level/SavePoint.cs`

### 核心资产

- `Assets/Resources/SkeletonEnemy.prefab`
- `Assets/Resources/ItemDrop.prefab`
- `Assets/Items/TestItem_Bone.asset`
- `Assets/Items/TestItem_GuardCore.asset`
- `Assets/Scripts/SkeletonAnimator.controller`
- `Assets/Scripts/PlayerAnimator.controller`
- `Assets/Scripts/Animation/PlayerUpperBody.mask`
- `Assets/Scripts/Animation/UpperBodyIdle.anim`
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_death.fbx`
- `Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx`
- `Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_R.fbx`
- `Assets/Fonts/09_SourceHanSansSC/TMP/SourceHanSansSC-Medium_TMP.asset`

### 不应修改的文件

- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产本体
- `Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Medium.otf`：源字体，不可改
- `Packages/manifest.json`

---

## 10. Development Direction Notes / 近期开发原则

当前开发应继续优先验证核心闭环，而不是一次性实现完整系统。

### 已完成的短期闭环

```text
刷怪
→ 战斗
→ 掉落骨头 / 概率掉落 Core
→ 拾取
→ 背包显示
→ 从背包装备 Core
→ 攻击力 / 最大生命值变化
→ 卸下 Core 回背包
→ 继续刷怪
```

### 推荐下一阶段方向

#### 优先级 1：整理 EnemyAI / NavMesh 移动基础

1. 实测 Wander / Chase / ReturnToSpawn 的 Agent 行为。
2. 检查 Chase → Attack 是否存在滑动 / 抖动。
3. 检查 Chase → ReturnToSpawn 是否存在 Agent 路径被旧状态清理误停的问题。
4. 整理 EnemyAI 的 NavMeshAgent / Rigidbody 驱动权，减少 `rb.isKinematic` 状态切换。
5. 后续再考虑 Attack 距离 hysteresis、Unreachable / Evade 规则。
6. 整理 NavMeshSurface LayerMask，只让 Terrain / Ground / Environment 参与 Bake。
7. 视需要将嵌入 Scene 的 NavMeshData 另存为独立 `.asset`。

#### 优先级 2：继续完善刷装备闭环

1. 实测并调整 SkeletonEnemy 的 Core 掉率。
2. 增加第二个 Core 测试装备，例如：
   - 攻击核心：AttackPowerBonus 高，MaxHealthBonus 低
   - 守护核心：AttackPowerBonus 中，MaxHealthBonus 高
3. 测试替换 Core：新 Core 从背包进装备槽，旧 Core 回背包。
4. 之后再考虑简单 DropTable ScriptableObject。

#### 优先级 3：装备系统扩展

1. 在继续使用 `ItemData` 的前提下增加 Weapon / Armor / Accessory 槽。
2. `PlayerEquipment` 从单一 Core 字段扩展到多槽位。
3. `PlayerCombatStats` 汇总多个装备槽属性。
4. 暂时不做随机词条。

#### 优先级 4：正式 UI

1. 正式背包 UI。
2. 正式装备 UI。
3. 正式死亡 / 复活 UI。
4. 正式存档点提示。

#### 优先级 5：中长期结构升级

1. 引入 `ItemInstance`，支持同名装备不同词条。
2. 引入 `StatModifier`。
3. 引入存档系统。
4. 引入正式掉落表 / 稀有度抽取。
5. 集成或替换 `EntityStats`。

### 暂不优先实现

- 完整随机词条
- 完整存档系统
- 完整正式背包 UI
- 完整正式装备栏 UI
- 五人真实同屏战斗
- 完整 Hikari AI
- Boss 战最终版
- 复杂仇恨表

---

## 11. Next Suggested Tasks

### ⭐ 最推荐的下一个小任务

**EnemyAI Agent / Rigidbody 驱动权整理第一版。**

目的：

```text
当前 Wander / Chase / ReturnToSpawn 都已经优先使用 NavMeshAgent。
但 EnemyAI 仍处于过渡结构：
Agent 状态下会切 rb.isKinematic = true，
Attack / fallback 时又恢复 rb.isKinematic = false。

下一步应减少 NavMeshAgent 与 Rigidbody 在不同状态之间争夺移动控制权，
降低攻击时滑动、斜坡抖动、状态切换瞬间弹动的风险。
```

建议目标：

```text
只整理 EnemyAI.cs。
不改 Animator / Prefab / Scene。
不改攻击伤害判定。
不改仇恨系统。
确认 Agent 驱动状态与 Rigidbody fallback 状态的进入 / 退出规则。
确保 Attack 状态停止 Agent 且不会继续滑动。
确保 ResetToSpawn / ForceDisengageAndReturnToSpawn 清理 Agent 状态。
```

验收目标：

```text
Wander / Chase / ReturnToSpawn 正常使用 Agent。
Attack 时敌人不滑动。
Agent 不可用时 fallback 仍正常。
Chase 目标点不可达时不切 Rigidbody。
死亡、掉落、刷新流程不受影响。
```

### 备选任务

1. 增加第二个测试 Core 装备，并验证替换装备流程。
2. 整理 NavMeshSurface LayerMask，只让 Terrain / Ground / Environment 参与 Bake。
3. 将 SampleScene 的 NavMeshData 独立保存为 `.asset`。
4. 给 Attack 增加距离 hysteresis，减少 Chase / Attack 边缘抖动。
5. 设计敌人长期不可达时的 Evade / ReturnToSpawn 规则。
6. 清理 `DebugManager` Missing Script。
7. 左侧 F1 Debug 面板加 ScrollView / 动态尺寸。
8. 给 `SkeletonDebugUI` 加 `UNITY_EDITOR || DEVELOPMENT_BUILD` 保护。
9. 将 `SkeletonDebugUI` 拆分为多个 Debug Panel 脚本。

## 12. 本次有效变更摘要（2026-05-11）

1. `ItemData.cs` 新增 `ItemType`、`EquipmentSlotType`、`maxStack`、`attackPowerBonus`、`maxHealthBonus`，并通过 `OnValidate()` 保护装备 / 非装备规则。
2. `ItemStack.cs` 新增 `IsFull`、`RemainingCapacity`，`AddCount()` 改为返回实际增加数量，并新增 `RemoveCount()`。
3. `PlayerInventory.cs` 支持 Equipment 不合并，非装备按 `itemId + maxStack` 合并；新增 `RemoveItem()` 与 `FindFirstEquipmentBySlot()`。
4. 新增 `PlayerEquipment.cs`，实现 Core 装备槽、装备 / 卸下 / 清空、替换旧装备、`OnEquipmentChanged` 事件。
5. 新增 `PlayerCombatStats.cs`，统一计算普通攻击伤害和最大生命值，并监听装备变化自动应用最大生命值。
6. `HealthComponent.cs` 新增 `SetMaxHealth()`，支持动态修改最大生命值。
7. `PlayerSkillController.cs` 改为读取 `PlayerCombatStats.CurrentNormalAttackDamage`，不再写死 Core 伤害加成。
8. `SkeletonDebugUI.cs` 扩展装备 / 背包 / 战斗属性 Debug 功能，支持从背包装备 Core、卸下 Core 到背包、显示右侧背包 Debug 窗口。
9. `RPGCameraController.cs` 增加 Cursor 管理，修复拖拽视角后鼠标永久消失问题。
10. `EnemyDropper.cs` 从固定单物品掉落升级为多条目概率掉落测试版，并保留旧 `dropItem` fallback。
11. `SkeletonEnemy.prefab` 的掉落配置更新为：骨头 100%，守护核心 20%。
12. 使用已有 `Assets/Items/TestItem_GuardCore.asset` 作为 Core 装备掉落测试物。
13. 项目地图从旧 Ground 主地面转向默认 Terrain；旧 Ground 系列对象需后续确认。
14. 已确认 F1 Debug UI 实际挂载在 `SkeletonSpawnerManager`，不是 Hierarchy 中的 Canvas Button。
15. 发现 `DebugManager` 上有 Missing Mono Script，后续可单独清理。

---

## 13. 本次有效变更摘要（2026-05-12）

### EnemyAI / NavMesh

1. `EnemyAI.cs` 新增 `Wander` 状态，实现 Idle / Wander 混合游荡。
2. `EnemyAI.cs` 新增游荡相关参数：`wanderMoveSpeed`、`wanderPointReachDistance`、`minIdleTime`、`maxIdleTime`。
3. `EnemyAI.cs` 新增 `_wanderTarget`、`_idleTimer`，支持随机待机后游荡。
4. `SampleScene.unity` 新增 `NavMeshSurface_World` 并完成 NavMesh Bake。
5. `SkeletonEnemy.prefab` 新增 `NavMeshAgent` 组件。
6. `EnemyAI.cs` 新增 NavMeshAgent 支持字段：`_agent`、`_hasAgent`、`_wanderPath`。
7. Wander 状态改为优先使用 NavMeshAgent：`NavMesh.SamplePosition` + `CalculatePath` + `SetDestination`。
8. Wander 在 Agent 不可用时保留 Rigidbody fallback。
9. ReturnToSpawn 改为优先使用 NavMeshAgent。新增 `returnNavMeshSampleDistance`、`_returnPath`、`_returningWithAgent`。
10. `EnterReturnToSpawn()` 会尝试采样出生点附近 NavMesh，并用 `CalculatePath` 检查回家路径。
11. `HandleReturnToSpawn()` 支持 Agent / Rigidbody 分支，到达后统一 `FinishReturnToSpawn()`，回满血并恢复 Idle。
12. Chase 改为优先使用 NavMeshAgent。新增 `chaseDestinationUpdateInterval`、`chaseNavMeshSampleDistance`、`_chasingWithAgent`、`_chasePath`、`_nextChaseDestinationUpdateTime`。
13. `TryUpdateAgentChaseDestination()` 使用 `NavMesh.SamplePosition` + `CalculatePath` 定期更新玩家附近可达点。
14. `HandleChaseMovement()` 作为 Chase 移动入口，Agent 可用时使用 Agent，Agent 本身不可用时才 fallback 到 Rigidbody。
15. Chase 目标点临时不可达时不再切 Rigidbody，不放弃 Chase。
16. Chase 新增 `_lastValidChaseDestination` 与 `_hasLastValidChaseDestination`，路径更新失败时继续追旧 path / last valid destination，并持续重试。
17. Attack 进入时停止 Agent、ResetPath，并恢复旧攻击逻辑；`OnAttackHit()` 未修改。
18. `ResetToSpawn()`、`EnterReturnToSpawn()`、Chase 退出时会清理 Chase / Return Agent 相关状态。

### EnemyDropper / 掉落物贴地

1. `EnemyDropper.cs` 新增 Ground Placement 参数：`alignDropsToGround`、`groundRaycastStartHeight`、`groundRaycastDistance`、`groundOffset`、`groundLayerMask`。
2. `EnemyDropper.cs` 新增 `GetGroundedDropPosition()`，掉落物生成前从候选位置上方向下 Raycast，将 ItemDrop 放到地面上方。
3. drops 列表路径和旧 `dropItem` fallback 路径都接入贴地位置计算。
4. `EnemyDropper.cs` 新增 `maxDropHeightAboveOwnerBounds` 与 `GetMaxAllowedDropGroundY()`。
5. 贴地 Raycast 命中点若高于敌人自身非 Trigger Collider 顶部 + 容错值，会被视为头顶物体 / 生物 / Collider 误命中并 fallback 到候选位置。
6. `OnValidate()` 增加 Ground Placement 参数保护。

### 当前状态总结

```text
EnemyAI：
Idle / Wander / Chase / Attack / ReturnToSpawn 已成型。
Wander / Chase / ReturnToSpawn 已优先使用 NavMeshAgent。
Attack 保留旧攻击逻辑，进入 Attack 时停止 Agent。
Rigidbody 仍作为 Agent 不可用时的 fallback。

Drop：
EnemyDropper 已支持多条目概率掉落 + Terrain / 地面贴合生成。

地图：
SampleScene 已有 Terrain + NavMeshSurface_World。
NavMeshData 当前嵌入 Scene。
```
