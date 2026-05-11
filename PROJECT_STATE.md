# PROJECT_STATE

最后更新：2026-05-11  
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

敌人有限状态机（Idle / Chase / Attack / ReturnToSpawn）+ 仇恨系统 + 野怪脱战回家逻辑。

重要点：

- 仇恨列表：`Dictionary<Transform, float> hateTable`
- `AddHate(Transform, float)`：统一仇恨入口
- `IsValidTarget(Transform)`：非 null、有 HealthComponent、未死亡、阵营敌对
- `RemoveInvalidHateTargets()`：清除死亡 / 销毁目标
- `SelectHighestHateTarget()`：选择最高仇恨目标
- `_spawnPosition / _spawnRotation`：Awake 记录出生点与朝向
- `wanderRadius`：未来游荡内圈预留字段
- `leashRadius`：活动边界外圈，超过后 ReturnToSpawn
- `EnterReturnToSpawn()`：清空仇恨、停止攻击动画、进入回家状态
- `HandleReturnToSpawn()`：XZ 平面自己走回出生点，到达后回满血并恢复 Idle
- `ResetToSpawn()`：Debug 强制复位，瞬移回出生点
- `ForceDisengageAndReturnToSpawn()`：外部命令活敌人脱战回家
- `OnAttackHit()`：Animation Event 绑定，方法名不可改

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

当前已从固定 100% 单物品掉落升级为“小型多物品掉落测试版”。

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

掉落流程：

```text
HandleDied()
→ pickupPrefab == null 时 Warning + return
→ 如果 drops.Count > 0：
    遍历每个 DropEntry
    item == null → Warning + skip
    Random.value <= dropChance → Instantiate ItemDrop
    生成位置 = transform.position + dropOffset + entry.offset
    PickupItem.SetItemData(entry.item)
    return
→ 如果 drops.Count == 0：
    fallback 到旧 dropItem 固定掉落
```

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
- ✅ 敌人 FSM AI（Idle / Chase / Attack / ReturnToSpawn）
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

### 地图 / Terrain

- ⚠️ 添加 Terrain 后，需要重新确认：
  - Player / Enemy 落地高度
  - EnemySpawnPoint 位置
  - SavePoint 位置
  - ItemDrop 掉落高度
  - NavMesh / AI 可行走区域
- ⚠️ 旧 Ground 系列对象需确认是否保留。

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

#### 优先级 1：继续完善刷装备闭环

1. 实测并调整 SkeletonEnemy 的 Core 掉率。
2. 增加第二个 Core 测试装备，例如：
   - 攻击核心：AttackPowerBonus 高，MaxHealthBonus 低
   - 守护核心：AttackPowerBonus 中，MaxHealthBonus 高
3. 测试替换 Core：新 Core 从背包进装备槽，旧 Core 回背包。
4. 让掉落物在 Terrain 上更稳定地贴地生成，避免悬空 / 嵌入地面。
5. 之后再考虑简单 DropTable ScriptableObject。

#### 优先级 2：装备系统扩展

1. 在继续使用 `ItemData` 的前提下增加 Weapon / Armor / Accessory 槽。
2. `PlayerEquipment` 从单一 Core 字段扩展到多槽位。
3. `PlayerCombatStats` 汇总多个装备槽属性。
4. 暂时不做随机词条。

#### 优先级 3：正式 UI

1. 正式背包 UI。
2. 正式装备 UI。
3. 正式死亡 / 复活 UI。
4. 正式存档点提示。

#### 优先级 4：中长期结构升级

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

**增加第二个测试 Core 装备，并验证替换装备流程。**

目的：

```text
当前只有 TestItem_GuardCore 一个 Core。
如果要验证“替换装备”是否稳定，需要至少两个不同 Core。
```

建议新增：

```text
Assets/Items/TestItem_AttackCore.asset
- itemId = test_attack_core
- itemName = 攻击核心
- ItemType = Equipment
- EquipmentSlotType = Core
- MaxStack = 1
- AttackPowerBonus = 40
- MaxHealthBonus = 0 或 10
```

然后把它加入 SkeletonEnemy.prefab 的 drops：

```text
TestItem_AttackCore
DropChance = 0.10 或临时 1.0 测试
Offset = (-0.4, 0, 0.2)
```

验收目标：

```text
背包中有守护核心和攻击核心
点击“装备背包中的第一个 Core”装备其中一个
再次点击时替换另一个
旧 Core 回背包
PlayerCombatStats 数值变化正确
右侧背包 Debug 窗口显示正确
```

### 备选任务

1. `EnemyDropper` 掉落物贴合 Terrain。
2. 清理 `DebugManager` Missing Script。
3. 左侧 F1 Debug 面板加 ScrollView / 动态尺寸。
4. 给 `SkeletonDebugUI` 加 `UNITY_EDITOR || DEVELOPMENT_BUILD` 保护。
5. 将 `SkeletonDebugUI` 拆分为多个 Debug Panel 脚本。

---

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
