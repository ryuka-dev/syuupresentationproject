# PROJECT_STATE

最后更新：2026-05-18  
当前主要场景：`Assets/Scenes/SampleScene.unity`  
Unity 版本：6000.4.3f1 (Unity 6)

---

## 1. Project Overview

- 项目类型：3D RPG 动作游戏原型
- 当前开发阶段：早期原型 - 野外战斗 / 刷怪 / 掉落 / 背包 / 装备数值 / 玩家技能系统闭环验证
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
→ 鼠标左键点击或 Tab 从屏幕左到右循环选中敌人
→ 当前目标头顶显示倒三角指示器
→ 按 1 使用普通攻击
→ 按 PlayerSkillManager 注册的技能键使用玩家技能（当前至少 Slot2 / Slot3，另有攻击强化测试技能，具体资产路径未确认）
→ Canvas 技能栏显示技能图标、持续时间与冷却
→ PlayerStatusEffectController 根据 Active 技能修正玩家受到的伤害 / 普通攻击输出伤害
→ 伤害飘字显示玩家打出的实际伤害与受到的实际伤害
→ 敌人普通攻击 / 指定敌人释放读条技能
→ 敌人死亡
→ 任务击杀进度增加
→ 敌人生成多个 ItemDrop
→ 玩家靠近按 E 拾取
→ PlayerInventory 按物品规则加入库存
→ F1 右侧背包 Debug 面板显示当前库存
→ 从背包中装备 Core / Armor / Accessory
→ PlayerEquipment 更新对应装备槽
→ PlayerCombatStats 汇总多装备槽攻击力 / 最大生命值
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

当前 EnemyAI 已从纯 Rigidbody 直线移动，推进到“NavMeshAgent 主导移动，Rigidbody 主要用于碰撞 / 物理辅助，并保留必要旧 fallback 兼容”的过渡架构。

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
- `EnemySkillController`：可选敌人技能控制器；skills 为空时不影响普通攻击

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
→ 进入攻击距离后停止 Agent，保留旧普通攻击逻辑
→ 若挂载 EnemySkillController 且存在可用技能，会优先尝试释放敌人技能
→ 无技能 / 技能不可用时回落到普通攻击
→ 普通攻击伤害仍通过 OnAttackHit() Animation Event 结算，方法名不可改

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

玩家输入、移动、移动时朝向与移动动画参数输出。使用 New Input System。`applyRootMotion = false`。

当前操作逻辑为 FF14 Legacy-like / 现代第三人称 RPG 风格：

- WASD 以相机水平面方向为基准移动。
- `cameraTransform.forward / right` 会用 `Vector3.ProjectOnPlane(..., Vector3.up)` 去掉 y 轴影响。
- 有移动输入时，Player 朝实际移动方向平滑转身。
- 无移动输入时，Player 不会因为相机旋转而原地转身。
- Shift + 任意移动输入 = Sprint，八方向都可跑步。
- 鼠标左键 + 右键同时按住时，等价于前进输入；若同时按 Shift，则向相机前方跑步。
- 左键 + 右键 + A / D 允许斜向移动。
- 左键 + 右键 + S 时，双键前进优先，不被 S 抵消。
- 移动仍使用 Rigidbody `rb.linearVelocity`。

Animator 移动参数当前适配 Legacy-like 操作：

```text
无输入：Speed = 0, Horizontal = 0
任意方向走路：Speed = 0.5, Horizontal = 0
任意方向跑步：Speed = 1.0, Horizontal = 0, IsSprinting = true
```

说明：

- A / D / S 不再输出左走、右走、后退动画参数。
- 角色会先转向实际移动方向，因此任意方向移动都播放 Forward Walk / Forward Run。
- `IsJumping`、`IsGrounded`、`VerticalVelocity` 仍按原逻辑处理。

### `RPGCameraController.cs`

第三人称相机跟随与右键视角旋转控制。

当前职责：

- 只负责相机跟随、相机 yaw / pitch、Cursor 显示隐藏。
- 不再直接修改 Player rotation。
- Player 移动时朝向由 `PlayerController` 负责。

当前鼠标规则：

- 只有鼠标右键按住时，相机才会旋转。
- 左键单独点击 / 拖动不旋转相机，不隐藏 Cursor。
- 右键拖拽开始时隐藏 Cursor。
- 右键松开时显示 Cursor。
- `OnDisable()` / `OnDestroy()` 强制恢复 Cursor 显示。
- 不使用 `CursorLockMode.Locked`，避免光标跳到屏幕中心。

当前灵敏度规则：

```csharp
_yaw   += delta.x * rotationSpeed;
_pitch -= delta.y * rotationSpeed;
```

- `Mouse.current.delta.ReadValue()` 已经是本帧鼠标移动量，因此不再乘 `Time.deltaTime`。
- `rotationSpeed` 为 Inspector 可调灵敏度，Range 当前为 `0.01f ~ 1.0f`。
- 用户实测 `rotationSpeed = 0.5` 体感合适；当前场景实际值以 Main Camera Inspector 保存值为准。
- `minPitch / maxPitch` 仍由 Inspector 控制。

注意：

- 如果脚本默认值与 Scene 中 Main Camera 组件序列化值不同，Unity 会以 Inspector 中保存的组件值为准。
- Unity 原生 API 不擅长精确恢复 OS 鼠标坐标；如果将来需要完全回到按下前的屏幕坐标，需单独设计。

### `PlayerTargeting.cs`

玩家当前目标选择系统。

当前职责：

- 鼠标左键点击时，使用 Physics.Raycast 检测目标。
- 验证目标是否有 `HealthComponent`、`FactionComponent`，并通过 `_selfFaction.ShouldAttack(faction.faction)` 判断敌对关系。
- `CurrentTarget` 当前统一设置为 `faction.transform`，鼠标点击与 Tab 选择保持一致。
- 提供 `ClearTarget()`，玩家死亡时会主动清空目标。
- 使用 New Input System 读取 `Keyboard.current.tabKey.wasPressedThisFrame`。
- Tab 目标选择第一版：收集屏幕内、摄像机前方、未死亡、敌对、距离玩家不超过 `tabTargetMaxDistance` 的敌人，按 `Camera.WorldToViewportPoint(...).x` 从左到右排序。
- 按 Tab 时：无目标选最左；有目标选右侧下一个；到最右后循环回最左；当前目标死亡 / 画面外 / 不在候选列表时重新从最左开始；无候选时 `ClearTarget()`。
- 当前候选收集使用 `FindObjectsByType<HealthComponent>(FindObjectsSortMode.None)`，敌人数量增多后应改为注册缓存。

关键字段：

- `allowTabTargeting`
- `tabTargetMaxDistance`
- `tabTargetViewportPadding`

### `TargetSelectionIndicator.cs`

路径：`Assets/Scripts/UI/TargetSelectionIndicator.cs`

当前目标视觉指示器，预期挂载在 Player 上。

职责：

- 读取同一 Player 上的 `PlayerTargeting.CurrentTarget`。
- 当前目标存在且未死亡时，控制 `indicatorPrefab` 显示在目标头顶。
- 当前目标为空 / 死亡 / 丢失时隐藏。
- 目标切换时重新计算一次头顶高度偏移。
- 每帧只使用 `target.position + _cachedWorldOffsetFromTarget` 跟随，不再每帧扫描 Collider，避免静止目标因动画 / bounds 变化导致倒三角上下频闪。
- 指示器面向 Main Camera，当前使用 `transform.forward = camera.transform.forward`。
- 不负责 Raycast、敌我判断、目标选择或攻击逻辑。

重要字段：

- `playerTargeting`
- `indicatorPrefab`
- `targetOffset`
- `fallbackHeight`
- `hideWhenTargetDead`

### `PlayerSkillController.cs`

按数字键 1 使用普通攻击。

当前职责：

- 读取 `PlayerTargeting.CurrentTarget`
- 验证目标有效性
- 读取 `PlayerCombatStats.CurrentNormalAttackDamage` 作为普通攻击基础伤害
- 通过同一 Player 上的 `PlayerStatusEffectController.ModifyOutgoingNormalAttackDamage(...)` 应用 Active 技能的普通攻击输出倍率
- 调用 `HealthComponent.TakeDamage(finalDamage, transform)`
- 触发 `Attack` Trigger 播放攻击动画

当前逻辑变化：

- 不再直接读取 `PlayerEquipment.EquippedCore.AttackPowerBonus`。
- 不再写死 Core 装备 +10 伤害。
- 若 Player 上没有 `PlayerCombatStats`，回退使用原本 `normalAttackDamage`。
- 若 Player 上没有 `PlayerStatusEffectController`，普通攻击伤害不做技能输出倍率修正。

### `PlayerDeathHandler.cs`

玩家死亡处理：

- 清除当前锁定目标
- 禁用 PlayerController / PlayerSkillController / PlayerTargeting / RPGCameraController
- 清零 Rigidbody 并设置 `isKinematic = true`
- 播放死亡动画
- 调用 `EnemyWorldManager.ForceAllLivingEnemiesReturnToSpawn()`
- 复活时只恢复玩家自身，不额外重置敌人


### Player Skill System v0.1

当前玩家技能已经从单个减伤原型，整理为最小统一技能系统。

#### `PlayerSkillData.cs`

路径：`Assets/Scripts/Player/Skills/PlayerSkillData.cs`

玩家技能静态数据 ScriptableObject。

当前包含：

- `PlayerSkillInputSlot`：`None / Slot1 ... Slot9`
- `PlayerSkillEffectType`：当前至少 `None / DamageReduction / AttackPowerMultiplier`
- `PlayerSkillVisualType`：当前至少 `None / DefenseRing`
- 字段：`skillId`、`skillName`、`description`、`icon`、`inputSlot`、`keyLabel`、`cooldown`、`duration`、`effectType`、`damageTakenMultiplier`、`attackPowerMultiplier`、`visualType`

当前已确认资产：

- `Assets/Skills/Player/Skill_IronBulwark.asset`
  - `skillId = iron_bulwark`
  - `inputSlot = Slot2`
  - `keyLabel = 2`
  - `cooldown = 12`
  - `duration = 4`
  - `effectType = DamageReduction`
  - `damageTakenMultiplier = 0.5`
  - `visualType = DefenseRing`
- `Assets/Skills/Player/Skill_StoneGuard.asset`
  - 当前作为第二个 DamageReduction 测试技能使用；详细参数以 Inspector 为准。
- 攻击强化测试技能资产
  - 当前已创建并注册到 Player 的 `PlayerSkillManager.skills`，Play Mode 测试正常。
  - `effectType = AttackPowerMultiplier`
  - 会在 Active 期间提高普通攻击最终伤害。
  - 具体资产路径 / 参数未确认，以下次读取项目文件或 Inspector 为准。

#### `PlayerSkillManager.cs`

路径：`Assets/Scripts/Player/Skills/PlayerSkillManager.cs`

职责：

- 持有 `PlayerSkillData[] skills`。
- Play Mode 中根据 `skills` 生成 `RuntimeStates`。
- 使用 New Input System 将 `PlayerSkillInputSlot` 映射到 `Keyboard.current.digit1Key ... digit9Key`。
- 管理每个技能的 Active / Cooldown / Ready 状态。
- 记录 `LastPressedSkillState`，即最后一次按下的技能；冷却中按下也会更新。
- 只管理输入、持续时间、冷却与运行时状态，不直接执行伤害或视觉效果。

重要规则：

- `PlayerSkillManager.skills` 的顺序是正式技能栏显示顺序。
- 新增技能应优先创建 `PlayerSkillData` 资产，再加入 Player 上 `PlayerSkillManager.skills` 数组。
- 当前普通攻击 `1` 仍由 `PlayerSkillController` 管理；技能系统主要管理 Slot2 之后的技能。

#### `PlayerStatusEffectController.cs`

路径：`Assets/Scripts/Player/Skills/PlayerStatusEffectController.cs`

职责：

- 读取 `PlayerSkillManager.RuntimeStates`。
- 对 Active 且 `EffectType == DamageReduction` 的技能应用受到伤害倍率。
- 对 Active 且 `EffectType == AttackPowerMultiplier` 的技能应用普通攻击输出倍率。
- 当前多个 DamageReduction 技能同时 Active 时使用乘算叠加：
  - 例如 `0.5 * 0.8 = 0.4`
- 当前多个 AttackPowerMultiplier 技能同时 Active 时也使用乘算叠加：
  - 例如 `1.5 * 1.2 = 1.8`
- `HealthComponent` 通过它统一修正玩家受到的伤害。
- `PlayerSkillController` 通过 `ModifyOutgoingNormalAttackDamage(float baseDamage)` 修正玩家普通攻击最终伤害。

注意：

- 当前没有完整 Buff 优先级、覆盖规则、持续状态列表或 `StatModifier`。
- 目前支持的玩家技能效果仍是最小原型：`DamageReduction` 与 `AttackPowerMultiplier`。

#### `PlayerSkillCanvasUI.cs`

路径：`Assets/Scripts/Player/PlayerSkillCanvasUI.cs`

通用单个 Canvas 技能格 UI。

职责：

- 通过 `Initialize(PlayerSkillManager manager, PlayerSkillRuntimeState state)` 绑定具体技能。
- 从 `PlayerSkillData` 自动设置技能名、按键文本、图标。
- 根据 `PlayerSkillRuntimeState` 显示 READY / ACTIVE / COOLDOWN。
- 冷却时显示遮罩与倒计时。

#### `PlayerSkillBarCanvasUI.cs`

路径：`Assets/Scripts/Player/PlayerSkillBarCanvasUI.cs`

Canvas 技能栏控制器，挂载在 `SkillBar` 上。

当前使用 B 方案：

```text
SkillSlotTemplate 只作为隐藏模板
→ Play Mode 根据 PlayerSkillManager.RuntimeStates 动态 Instantiate 全部技能格
→ 技能格数量 = RuntimeStates 数量
→ 顺序 = PlayerSkillManager.skills 顺序
```

布局规则：

- `SkillBar` 锚点固定右下角。
- `SkillBar` 的 `pivot = (1, 0)`。
- `SkillBar` 距屏幕右下约 `(-40, 40)`。
- 技能格在 `SkillBar` 内从左到右排列。
- 新增技能显示在最右侧；技能数量增加时，整个技能栏向左扩展，右边缘保持固定。
- 当前技能格尺寸约 `96 x 112`，图标区域约 `96 x 96`。

#### `PlayerSkillHudUI.cs`

OnGUI 技能调试 HUD。

当前显示 `PlayerSkillManager.LastPressedSkillState`，用于调试最后按过的技能：

- 未按过技能：显示 `No skill pressed yet`
- 按下 Slot2 / Slot3 / Slot4 等：显示对应技能名、Key、SkillId、Status、Active Remaining、Cooldown Remaining、EffectType 与 Damage Taken Multiplier

该脚本仍是 OnGUI 调试用途，不是正式 UI。

#### `PlayerMitigationVisualFeedback.cs`

Iron Bulwark 视觉反馈原型。

当前不再读取旧 `PlayerMitigationController`，而是读取 `PlayerSkillManager.GetStateBySkillId("iron_bulwark").IsActive`。

行为：

- `iron_bulwark` Active 时，在玩家脚下显示蓝色 LineRenderer 防御光环。
- Active 结束或冷却中时隐藏。
- 当前只针对 `iron_bulwark`，尚未抽象为通用技能视觉系统。

#### 已移除旧原型

`Assets/Scripts/Player/PlayerMitigationController.cs` 已删除。

当前不再保留旧减伤控制器：

- `HealthComponent` 不再 fallback 到 `PlayerMitigationController`。
- `SkeletonDebugUI` 不再 fallback 到 `PlayerMitigationController`。
- `PlayerSkillCanvasUI` / `PlayerMitigationVisualFeedback` 不再引用 `PlayerMitigationController`。
- SampleScene 的 Player 上已移除旧 Missing Script。


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


当前伤害修正规则：

```text
TakeDamage(...)
→ ApplyIncomingDamageModifiers(damage)
→ 如果同一 GameObject 上有 PlayerStatusEffectController：
     使用 PlayerStatusEffectController.ModifyIncomingDamage(damage)
→ 否则伤害不变
```

说明：

- 玩家受到伤害时，DamageReduction 技能效果由 `PlayerStatusEffectController` 统一处理。
- 敌人通常没有 `PlayerStatusEffectController`，因此敌人受伤不受玩家技能系统影响。
- 旧 `PlayerMitigationController` fallback 已移除。
- `OnDamaged` 传入最终伤害值，当前伤害飘字系统通过该事件显示玩家打出的实际伤害与受到的实际伤害。

`SetMaxHealth` 规则：

- `newMaxHealth < 1f` 时修正为 1。
- `keepCurrentRatio == true`：按旧生命比例换算当前生命值。
- `keepCurrentRatio == false`：当前生命值保持原值，但超过新上限时裁剪。
- 最后触发 `OnHealthChanged(currentHealth, maxHealth)`。
- 不处理死亡 / 复活状态。
- 不调用 `RestoreFullHealth()`。

### Damage Number / Combat Feedback

#### `DamageNumberPopup.cs`

路径：`Assets/Scripts/UI/DamageNumberPopup.cs`

单个世界空间伤害飘字。

职责：

- 使用 `TextMeshPro` 显示伤害数字。
- `Initialize(float damage)` / `Initialize(float damage, Camera targetCamera)` 初始化显示值。
- 伤害值四舍五入为整数，最小显示 1。
- 生命周期内向上移动、逐渐淡出、面向摄像机，结束后 Destroy 自身。
- 当前第一版直接 Instantiate / Destroy，尚未使用对象池。

#### `DamageNumberSpawner.cs`

路径：`Assets/Scripts/UI/DamageNumberSpawner.cs`

伤害飘字生成器，挂在带 `HealthComponent` 的对象上。

职责：

- 在 `OnEnable()` 订阅同对象 `HealthComponent.OnDamaged`。
- 在 `OnDisable()` 取消订阅。
- 受到最终伤害时，在对象头顶附近生成 `DamageNumberPopup`。
- 不计算伤害、不修改血量、不区分攻击来源。
- 依赖 Inspector 的 `popupPrefab` 绑定，缺失时只 Warning，不崩溃。

当前绑定状态：

- Player 已绑定 `DamageNumberSpawner`，使用玩家受伤用 Popup Prefab。
- `Assets/Resources/EnemyBase.prefab` 已绑定 `DamageNumberSpawner`，使用玩家打出伤害用 Popup Prefab。
- 当前已创建并使用：
  - `Assets/Resources/UI/DamageNumberPopup.prefab`
  - `Assets/Resources/UI/DamageNumberPopup_PlayerDamage.prefab`
  - `Assets/Resources/UI/DamageNumberPopup_PlayerTaken.prefab`

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
EquipmentAttackPowerBonus
= EquippedCore.AttackPowerBonus
+ EquippedArmor.AttackPowerBonus
+ EquippedAccessory.AttackPowerBonus

CurrentNormalAttackDamage
= BaseNormalAttackDamage + EquipmentAttackPowerBonus

EquipmentMaxHealthBonus
= EquippedCore.MaxHealthBonus
+ EquippedArmor.MaxHealthBonus
+ EquippedAccessory.MaxHealthBonus

CurrentMaxHealth
= Max(1, BaseMaxHealth + EquipmentMaxHealthBonus)
```

安全规则：

- `PlayerEquipment == null` 时装备加成按 0 处理，不崩溃。
- 任意空装备槽按 0 处理。
- 当前不汇总 Weapon，因为主角武器固定，不进入装备系统。

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

玩家装备容器，当前支持三个装备槽：

```text
Core
Armor
Accessory
```

设计说明：

- 主角武器固定，不进入装备系统。
- 当前没有 Weapon 装备槽。
- 当前装备仍以 `ItemData` 表示，不支持同名装备不同词条。
- `OnEquipmentChanged` 会在任意装备槽变化时触发。

字段 / 属性：

- `[SerializeField] private ItemData equippedCore`
- `[SerializeField] private ItemData equippedArmor`
- `[SerializeField] private ItemData equippedAccessory`
- `EquippedCore`
- `EquippedArmor`
- `EquippedAccessory`
- `HasCoreEquipped`
- `HasArmorEquipped`
- `HasAccessoryEquipped`

事件：

```csharp
public event System.Action OnEquipmentChanged;
```

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

#### `EquipArmor(ItemData item, out ItemData replacedItem)`

规则与 Core 相同，但要求：

```text
item.EquipmentSlotType == Armor
```

保留重载：

```csharp
public bool EquipArmor(ItemData item)
{
    return EquipArmor(item, out _);
}
```

#### `EquipAccessory(ItemData item, out ItemData replacedItem)`

规则与 Core 相同，但要求：

```text
item.EquipmentSlotType == Accessory
```

保留重载：

```csharp
public bool EquipAccessory(ItemData item)
{
    return EquipAccessory(item, out _);
}
```

#### `UnequipCore()` / `UnequipArmor()` / `UnequipAccessory()`

规则：

- 当前槽位无装备 → Warning + null
- 有装备：
  - 保存当前装备
  - 对应槽位置 null
  - 触发 `OnEquipmentChanged`
  - 返回被卸下的 `ItemData`

#### `ClearEquipment()`

- 当前任意槽位有装备时：
  - 一次性清空 `equippedCore / equippedArmor / equippedAccessory`
  - 只触发一次 `OnEquipmentChanged`
- 若三个槽位本来都为空，不触发事件。

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
  - 装备背包中的第一个 Armor
  - 卸下 Armor 到背包
  - 装备背包中的第一个 Accessory
  - 卸下 Accessory 到背包
- 战斗属性调试：
  - Base Normal Attack Damage
  - Equipment Attack Bonus
  - Current Normal Attack Damage
  - Equipment Max Health Bonus
  - Base Max Health
  - Current Max Health
  - 应用当前最大生命值
  - 玩家减伤状态（读取 PlayerSkillManager 的 iron_bulwark state）


### 玩家技能 / 减伤 Debug 显示

`SkeletonDebugUI.cs` 的“玩家减伤状态”现在读取 `PlayerSkillManager.GetStateBySkillId("iron_bulwark")`。

显示内容：

- `Skill Source: PlayerSkillManager`
- `Skill Id: iron_bulwark`
- `Mitigation Active`
- `Active Remaining`
- `Cooldown Remaining`
- `Damage Taken Multiplier`

旧 `PlayerMitigationController` fallback 已移除；找不到 `PlayerSkillManager` 或对应 state 时显示 `PlayerSkillManager state not found`。


### 当前装备状态 Debug 窗口

新增独立装备状态窗口，不塞进左侧按钮长条，也不放进右侧背包窗口。

绘制方法：

- `DrawEquipmentStatusWindow(float margin, float leftPanelWidth)`
- `DrawEquipmentSlotLine(string slotName, ItemData item)`

位置 / 尺寸：

```csharp
float equipX = margin + leftPanelWidth + 12f;
float equipY = margin;
float width  = 310f;
float height = CalculateEquipmentStatusWindowHeight(width);
```

说明：

- `margin` 当前为 `20f`。
- 左侧主面板宽度为 `Mathf.Clamp(Screen.width * 0.32f, 320f, 420f)`。
- 装备窗口跟随左侧主面板宽度变化，显示在主 Debug 按钮面板右侧。
- 窗口位于屏幕左上区域。
- 窗口高度会根据实际显示行数动态计算，不低于最小高度 240f。
- 装备槽显示行由 `BuildEquipmentSlotLines()` 生成，高度计算与实际绘制共用同一行数据。
- 第一版只显示信息，不提供装备 / 卸下按钮。

显示内容：

```text
--- 装备状态 ---
Core: 当前装备 / 未装备
Armor: 当前装备 / 未装备
Accessory: 当前装备 / 未装备

--- 战斗属性汇总 ---
Equipment ATK Bonus
Equipment Max HP Bonus
Current Normal Attack
Current Max Health
```

安全规则：

- 缺少 `PlayerEquipment` 时显示 `PlayerEquipment not found`，不崩溃。
- 缺少 `PlayerCombatStats` 时显示 `PlayerCombatStats not found`，不崩溃。
- 空槽位显示“未装备”。

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
- PlayerSkillManager
- PlayerStatusEffectController
- PlayerMitigationVisualFeedback
- PlayerSkillHudUI（OnGUI 调试 HUD，显示最后按过的技能）
- DamageNumberSpawner（玩家受伤飘字）
- TargetSelectionIndicator（当前目标头顶倒三角指示器）

Tag：`Player`  
PickupItem 依赖该 Tag 判断玩家。

#### Main Camera

- `RPGCameraController`
- target = Player Transform
- 当前负责相机跟随、右键旋转、Cursor 显示 / 隐藏。
- 当前不再直接旋转 Player。
- `rotationSpeed` 为 Inspector 可调鼠标灵敏度；用户实测值为 0.5，实际保存值以 Inspector 为准。


#### SkillCanvas

正式 Canvas 技能栏第一版。

当前结构：

```text
SkillCanvas
└─ SkillBar
   └─ SkillSlotTemplate
      ├─ Icon
      ├─ CooldownOverlay
      ├─ CooldownText
      ├─ KeyText
      └─ SkillNameText
```

说明：

- `SkillBar` 挂载 `PlayerSkillBarCanvasUI`。
- `SkillSlotTemplate` 是隐藏模板，不作为实际技能格显示。
- Play Mode 中根据 `PlayerSkillManager.RuntimeStates` 动态生成 `SkillSlot_<skillId>`。
- 技能显示顺序与 Player 的 `PlayerSkillManager.skills` 数组一致。
- 技能栏锚点在右下，`pivot = (1, 0)`，新增技能显示在最右侧，技能数量增加时整体向左扩展。
- 不要把 `SkillSlotTemplate` 当成具体 Iron Bulwark 技能格修改；它是通用模板。

#### SkeletonSpawnerManager

- `SkeletonSpawner`
- `SkeletonDebugUI`
- `PhysicsLayerSetup`

F1 Debug UI 来源是这里，不是 Hierarchy 里的 Canvas Button。

#### DebugManager

- 先前发现的 `Missing (Mono Script)` 已清理。
- 当前 F1 Debug UI 不依赖 DebugManager，而是挂在 SkeletonSpawnerManager 上。

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

#### `Assets/Resources/EnemyBase.prefab`

敌人通用基础模板，不直接用于生成。当前已统一挂载 `EnemySkillController`、`EnemyCastBarUI` 与 `DamageNumberSpawner`。

- `EnemySkillController.skills` 默认为空，表示普通敌人默认无技能。
- `EnemyCastBarUI` 默认可保留；没有读条时不显示。
- `DamageNumberSpawner` 用于敌人受伤时显示玩家打出的实际伤害数字。
- 具体敌人通过 Prefab Variant 覆写模型、掉落、视野绑定、体型、技能等配置。

#### `Assets/Resources/SkeletonEnemy_Variant.prefab`

- Source：`EnemyBase.prefab`。
- 当前不覆写技能列表，继承空 skills，作为无技能普通小怪。
- 掉落配置：骨头100% + 守护核心20%。

#### `Assets/Resources/SkeletonBossEnemy_Variant.prefab`

- Source：`EnemyBase.prefab`。
- VisualRoot 1.5 倍视觉缩放，Collider / NavMeshAgent 单独调整。
- EnemySkillController.skills 覆写配置读条重击（CastAttack）。
- 掉落配置：守护核心100%（Boss 测试用）。

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

#### `Assets/Resources/UI/DamageNumberPopup.prefab`

- 伤害飘字基础 Prefab。
- 当前已复制出玩家打出伤害 / 玩家受到伤害两个显示版本。

#### `Assets/Resources/UI/DamageNumberPopup_PlayerDamage.prefab`

- 敌人受到伤害时使用。
- 用于显示玩家打出的实际伤害。

#### `Assets/Resources/UI/DamageNumberPopup_PlayerTaken.prefab`

- Player 受到伤害时使用。
- 当前为红色加粗、字号 5 的测试表现。

#### Target Indicator Prefab

- 当前目标倒三角指示器 Prefab 已创建并绑定到 Player 的 `TargetSelectionIndicator.indicatorPrefab`。
- 具体资产路径未确认；下一次修改前如需调整，请先读取或在 Inspector 确认。

---

## 5. Input / Control

- 使用：New Input System 1.19.0
- 当前操作风格：FF14 Legacy-like / 现代第三人称 RPG 风格
- 玩家移动：WASD，相机水平面基准移动
  - W：朝相机前方移动
  - S：朝相机后方移动，不是慢速倒退
  - A / D：朝相机左 / 右方向移动，角色会转向实际移动方向
- 跑步：Shift + 任意移动输入，八方向都可跑步
- 鼠标左键：点击目标选择，不旋转相机，不隐藏 Cursor
- Tab：从屏幕左侧到右侧循环选中屏幕内敌对目标；到最右后回到最左
- 鼠标右键：按住并移动鼠标时旋转相机
- 鼠标左键 + 右键：向当前相机前方移动，等价于前进输入
- 鼠标左键 + 右键 + Shift：向当前相机前方跑步
- 普通攻击：键盘 1
- 玩家技能槽：由 `PlayerSkillManager` 读取 `PlayerSkillData.inputSlot`，映射到键盘数字键 1～9；当前已用于 Slot2 / Slot3 等测试技能
- 拾取物品：E
- Debug UI：F1
- 关卡重开：R，仅旧 Victory / Game Over 后生效

移动 / 动画规则：

- Player 有移动输入时朝实际移动方向平滑转身。
- Player 无移动输入时不会被相机旋转强制转身。
- 任意方向普通移动播放 Forward Walk。
- 任意方向 + Shift 播放 Forward Run。
- 左走 / 右走 / 后退动画当前不用于非锁定 Legacy-like 移动。

Cursor 当前规则：

- 右键视角拖拽开始：隐藏鼠标
- 右键视角拖拽结束：显示鼠标
- 左键单独点击 / 拖动：不隐藏鼠标
- RPGCameraController 被禁用 / 销毁：强制显示鼠标
- 不使用 CursorLockMode.Locked


目标选择规则：

- 鼠标左键使用 Raycast 选择敌对目标。
- Tab 使用 New Input System 的 `Keyboard.current.tabKey.wasPressedThisFrame`。
- Tab 候选目标按摄像机 viewport x 从小到大排序，即屏幕左侧到右侧。
- Tab 候选目标必须在屏幕内、摄像机前方、未死亡、敌对且在 `tabTargetMaxDistance` 内。
- 当前目标由 `PlayerTargeting.CurrentTarget` 统一提供；`TargetSelectionIndicator` 只读取该值显示倒三角。

玩家技能输入规则：

- `PlayerSkillManager` 使用 New Input System 的 `Keyboard.current.digit1Key ... digit9Key`。
- `PlayerSkillData.inputSlot` 决定技能按键槽。
- `keyLabel` 只用于 UI 显示。
- 当前普通攻击仍由 `PlayerSkillController` 的键盘 1 管理；不要在未统一前让 Skill Slot1 与普通攻击冲突。

相机灵敏度：

- `RPGCameraController.rotationSpeed` 控制鼠标视角灵敏度。
- 鼠标 delta 不乘 `Time.deltaTime`。
- 当前用户实测 `rotationSpeed = 0.5` 体感合适；实际值以 Main Camera Inspector 为准。


---

## 6. Completed Features

### 战斗 / AI

- ✅ 玩家第三人称移动控制
- ✅ FF14 Legacy-like 相机基准移动
- ✅ WASD 八方向移动时角色朝实际移动方向转身
- ✅ Shift + 任意移动输入支持八方向跑步
- ✅ 鼠标左键 + 右键双键前进
- ✅ 非锁定移动动画统一使用 Forward Walk / Forward Run
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
- ✅ 玩家普通攻击可通过 `AttackPowerMultiplier` 技能效果临时提高最终伤害
- ✅ Tab 键从屏幕左侧到右侧循环选中屏幕内敌对目标
- ✅ 当前目标头顶倒三角指示器第一版
- ✅ 敌人技能系统第一版（EnemySkillData / EnemySkillController）
- ✅ CastAttack / 读条重击第一版
- ✅ EnemyAI Attack 状态接入技能调用：有技能时尝试释放，无技能时普通攻击
- ✅ EnemyCastBarUI 读条提示第一版（OnGUI 显示技能名 / 进度 / 时间）
- ✅ EnemyBase 可统一挂载 EnemySkillController / EnemyCastBarUI，skills 为空时无影响
- ✅ SkeletonBossEnemy_Variant 可通过 skills 覆写配置读条重击


### 玩家技能 / 状态效果 / 技能 UI

- ✅ `PlayerSkillData` 玩家技能数据资产第一版
- ✅ `PlayerSkillManager` 统一管理注册技能的输入、Active、Cooldown、Ready 状态
- ✅ `PlayerSkillManager.LastPressedSkillState` 记录最后按过的技能，冷却中按下也会更新
- ✅ `PlayerStatusEffectController` 根据 Active 技能统一修正玩家受到的伤害
- ✅ `PlayerStatusEffectController` 可根据 Active 技能修正玩家普通攻击输出伤害
- ✅ DamageReduction 技能效果第一版
- ✅ AttackPowerMultiplier 技能效果第一版
- ✅ 多个 DamageReduction 同时 Active 时使用乘算叠加
- ✅ 多个 AttackPowerMultiplier 同时 Active 时使用乘算叠加
- ✅ `HealthComponent` 通过 `PlayerStatusEffectController` 应用玩家技能减伤
- ✅ Iron Bulwark 减伤技能已迁移到 `PlayerSkillData / PlayerSkillManager / PlayerStatusEffectController`
- ✅ Stone Guard 作为第二个 DamageReduction 测试技能已可按键激活并正常减伤
- ✅ `PlayerMitigationController.cs` 旧原型脚本已删除
- ✅ 正式 Canvas 技能栏第一版：`SkillCanvas / SkillBar / SkillSlotTemplate`
- ✅ `PlayerSkillBarCanvasUI` 根据注册技能数量动态生成技能格
- ✅ 技能格顺序与 `PlayerSkillManager.skills` 顺序一致
- ✅ 新增技能显示在最右侧，技能栏锚定右下并向左扩展
- ✅ `PlayerSkillCanvasUI` 通用技能格：显示图标、按键、技能名、Active 时间、Cooldown 遮罩与倒计时
- ✅ `PlayerSkillHudUI` OnGUI 调试 HUD 显示最后按过的技能
- ✅ `PlayerMitigationVisualFeedback` 读取 `PlayerSkillManager`，在 Iron Bulwark Active 时显示脚下防御光环
- ✅ F1 Debug UI 的玩家减伤状态显示读取 `PlayerSkillManager`

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
- ✅ PlayerEquipment Armor 装备槽
- ✅ PlayerEquipment Accessory 装备槽
- ✅ 主角武器固定，不进入装备系统
- ✅ EquipCore 支持替换旧 Core
- ✅ EquipArmor 支持替换旧 Armor
- ✅ EquipAccessory 支持替换旧 Accessory
- ✅ UnequipCore / UnequipArmor / UnequipAccessory 返回卸下装备
- ✅ OnEquipmentChanged 事件
- ✅ PlayerCombatStats 监听装备变化
- ✅ PlayerCombatStats 汇总 Core / Armor / Accessory 的攻击力加成
- ✅ PlayerCombatStats 汇总 Core / Armor / Accessory 的最大生命值加成
- ✅ 最大生命值变化自动应用到 HealthComponent
- ✅ 背包 → 装备槽
- ✅ 装备槽 → 背包
- ✅ 替换装备时旧装备回背包

### 战斗反馈 / 目标显示

- ✅ `DamageNumberPopup` / `DamageNumberSpawner` 伤害飘字第一版
- ✅ `HealthComponent.OnDamaged` 驱动最终伤害数字显示
- ✅ Player 受到伤害与敌人受到伤害可使用不同 Popup Prefab
- ✅ `TargetSelectionIndicator` 读取 `PlayerTargeting.CurrentTarget` 显示目标头顶倒三角
- ✅ TargetSelectionIndicator 已修正静止目标上下频闪问题：目标切换时计算一次高度偏移，运行中不再每帧扫描 Collider

### Debug / 工具

- ✅ F1 OnGUI Debug 面板
- ✅ 当前目标敌人显示与 ResetToSpawn
- ✅ Core 装备测试按钮
- ✅ 从背包装备第一个 Core
- ✅ 卸下 Core 到背包
- ✅ 从背包装备第一个 Armor
- ✅ 卸下 Armor 到背包
- ✅ 从背包装备第一个 Accessory
- ✅ 卸下 Accessory 到背包
- ✅ 战斗属性显示
- ✅ 独立装备状态 Debug 窗口
- ✅ 右侧背包 Debug 窗口
- ✅ 鼠标拖拽后永久消失问题修复
- ✅ 左键不再触发相机旋转或 Cursor 隐藏
- ✅ RPGCameraController 鼠标灵敏度改为不乘 Time.deltaTime，并可在 Inspector 调整
- ✅ 装备状态 Debug 窗口高度按实际显示行数动态计算

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
- ⚠️ 当前部分测试装备资产 / 掉落配置仍是调试用途，正式掉落表尚未实现。


### 玩家技能系统限制

- ⚠️ 当前玩家技能系统 v0.1 只支持最小 DamageReduction / AttackPowerMultiplier 流程。
- ⚠️ 尚未实现正式 Buff / StatusEffect 数据结构、优先级、覆盖规则、图标状态列表或效果取消事件。
- ⚠️ 多个 DamageReduction / AttackPowerMultiplier 当前按乘算叠加，尚未设计同类覆盖、上限或职业平衡规则。
- ⚠️ `PlayerMitigationVisualFeedback` 当前仍是 Iron Bulwark 专用视觉反馈，尚未抽象为通用 Skill Visual 系统。
- ⚠️ `PlayerSkillHudUI` 是 OnGUI 调试 HUD，不是正式玩家 UI。
- ⚠️ `PlayerSkillManager.skills` 当前通过 Inspector 注册，尚未实现技能学习、解锁、保存、拖拽或热键自定义。
- ⚠️ 普通攻击仍由 `PlayerSkillController` 独立处理，尚未统一进 `PlayerSkillManager`；不要让 Slot1 与普通攻击冲突。

### 场景 / UI

- ⚠️ LevelUI TMP 字体仍需确认是否绑定 `SourceHanSansSC-Medium_TMP.asset`。
- ✅ DebugManager 上的 Missing Mono Script 已清理。
- ⚠️ F1 Debug UI 是 OnGUI / IMGUI，不是正式 UI。
- ⚠️ 正式发布前应隐藏或限制 Debug 菜单。
- ⚠️ 玩家死亡后 RPGCameraController 被禁用，相机静止在死亡位置，暂无死亡镜头演出。
- ⚠️ 正式死亡 / 复活 UI 尚未接入，当前仍主要依赖 Debug UI。
- ⚠️ 当前操作逻辑是非锁定 Legacy-like 移动；尚未实现锁定目标时的 strafe / backstep 战斗移动模式。
- ⚠️ 鼠标灵敏度当前只支持 Inspector 调整，尚未实现正式设置菜单或保存设置。
- ⚠️ 当前目标倒三角是第一版世界空间指示器，尚未实现目标信息 UI、目标血条高亮、描边或锁定目标战斗移动模式。
- ⚠️ 伤害飘字当前使用 Instantiate / Destroy，尚未实现对象池；伤害数字很多时可能需要优化。

### 性能 / 架构

- ⚠️ `ScanForTarget()` 每 0.2s 使用 FindObjectsOfType / FindObjectsByType，敌人数量多时有性能隐患。
- ⚠️ `EnemyWorldManager` 与 `EnemySpawnPoint` 当前仍有 Find 系列 API，未来应改为注册缓存。
- ⚠️ `PlayerTargeting` 的 Tab 候选收集当前使用 `FindObjectsByType<HealthComponent>`，敌人数量变多后应改为敌人注册缓存。
- ⚠️ `SkeletonDebugUI` 目前职责较多，已接近 Runtime Debug Console，后续可拆分为 InventoryDebugPanel / EquipmentDebugPanel / CombatStatsDebugPanel。
- ⚠️ `EntityStats.cs` 已创建但未集成。
- ⚠️ EnemyAI 已整理移动控制权第一版，正常移动由 NavMeshAgent 主导；Rigidbody fallback 仍保留必要兼容，后续可继续收敛到“Rigidbody 只做碰撞”。
- ⚠️ Chase 目标不可达时会保持 Chase 并持续重试，不会因寻路失败主动放弃；如果玩家长期站在敌人永远到不了的位置，敌人可能停在最后可达点附近持续追击，后续可考虑 Evade / Unreachable 规则。

### 地图 / Terrain

- ⚠️ 添加 Terrain 后，需要继续确认：
  - Player / Enemy 落地高度
  - EnemySpawnPoint 位置是否在 NavMesh 上或足够接近 NavMesh
  - SavePoint 位置
  - NavMesh / AI 可行走区域
- ✅ ItemDrop 掉落高度已通过 EnemyDropper 贴地 Raycast 第一版改善。
- ✅ 旧 Ground 系列对象已删除，当前主地面以 Terrain 为准。
- ⚠️ 当前 NavMeshSurface 使用 `layerMask = ~0` 与 `collectObjects = All`，后续建议整理 Ground / Terrain / Environment Layer，避免临时对象或旧测试地块影响 NavMesh。
- ⚠️ Terrain 或障碍物变化后需要重新 Bake NavMesh。
- ⚠️ 当前主要测试地形仍接近纯平面，尚未在复杂地形上充分验证 NavMeshAgent 寻路、Wander / Chase / ReturnToSpawn 稳定性、Attack / 读条技能高低差判定、移动动画坡面表现、ItemDrop 贴地 Raycast 在复杂地形上的可靠性；当前暂不处理，后续基础战斗稳定后再验证。

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
- 非锁定 Legacy-like 移动下，移动动画参数应保持 `Horizontal = 0`，通过 `Speed = 0 / 0.5 / 1.0` 表示 Idle / Walk / Run。
- UpperBody Layer 的 `Any State → UpperBodyIdle`（IsDead 条件）不可删除。
- `UpperBodyIdle.anim` 不可删除。

### 代码修改原则

- Rigidbody 使用 `rb.linearVelocity`（Unity 6）。
- 输入系统使用 `Mouse.current` / `Keyboard.current`，不得使用旧 `UnityEngine.Input`。
- Tab 目标选择逻辑属于 `PlayerTargeting.cs`；不要另建第二套目标系统与 `CurrentTarget` 抢控制权。
- Player 当前采用 Legacy-like 相机基准移动：移动方向由 `PlayerController` 根据相机水平 forward / right 计算。
- `RPGCameraController` 不应直接旋转 Player；Player 移动时朝向由 `PlayerController` 负责。
- 鼠标视角旋转使用 `Mouse.delta * rotationSpeed`，不要再乘 `Time.deltaTime`。
- Debug OnGUI 可以继续用于开发工具，但正式 UI 应使用 Canvas / TMP / Button 或 UI Toolkit。
- EnemyAI 当前正常移动由 NavMeshAgent 主导；Rigidbody 主要用于碰撞 / 物理辅助，并保留必要旧 fallback 兼容。
- Chase 中“目标点暂时不可达”不应被视为 Agent 失效；不要因此切 Rigidbody，应该继续追最后有效 destination 并持续重试。
- 主角武器固定，不进入装备系统；当前装备槽为 Core / Armor / Accessory。
- 敌人技能不要直接写死进 `EnemyAI.cs`；应通过 `EnemySkillData` + `EnemySkillController` 配置。
- `EnemySkillController.skills` 为空代表无技能，必须保持不影响普通攻击。
- `EnemyCastBarUI` 依赖 OnGUI，GUIStyle 必须在 `OnGUI()` 内懒初始化，不要在 `Awake()` / `Start()` 中访问 `GUI.skin`。
- 玩家技能系统 v0.1 使用 `PlayerSkillData` + `PlayerSkillManager` + `PlayerStatusEffectController`，不要再恢复已删除的 `PlayerMitigationController`。
- `PlayerStatusEffectController` 当前同时负责 DamageReduction 与 AttackPowerMultiplier 的最小状态效果修正；新增效果类型前应先确认是否继续放在该脚本内。
- `PlayerSkillManager.skills` 的 Inspector 顺序决定 Canvas 技能栏显示顺序。
- `SkillCanvas/SkillBar/SkillSlotTemplate` 是正式技能栏第一版的场景绑定；`SkillSlotTemplate` 是隐藏模板，不是具体技能格。
- `PlayerSkillBarCanvasUI` 使用运行时 Instantiate 生成全部技能格，不要回退到直接复用模板本体作为第一个技能格。
- 技能栏锚点在右下，`pivot = (1, 0)`；新增技能显示在最右侧，整体向左扩展。


---

## 9. Files That Should Be Treated Carefully

### 核心脚本

- `Assets/Scripts/Enemy/EnemyAI.cs`
- `Assets/Scripts/Enemy/EnemyWorldManager.cs`
- `Assets/Scripts/Enemy/EnemySpawnPoint.cs`
- `Assets/Scripts/Enemy/EnemyDeathHandler.cs`
- `Assets/Scripts/Enemy/FactionSystem.cs`
- `Assets/Scripts/Enemy/Skills/EnemySkillData.cs`
- `Assets/Scripts/Enemy/Skills/EnemySkillController.cs`
- `Assets/Scripts/Enemy/Skills/EnemyCastBarUI.cs`
- `Assets/Scripts/HealthComponent.cs`
- `Assets/Scripts/PlayerController.cs`
- `Assets/Scripts/RPGCameraController.cs`
- `Assets/Scripts/Player/PlayerTargeting.cs`
- `Assets/Scripts/UI/TargetSelectionIndicator.cs`
- `Assets/Scripts/UI/DamageNumberPopup.cs`
- `Assets/Scripts/UI/DamageNumberSpawner.cs`
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
- `Assets/Scripts/Player/Skills/PlayerSkillData.cs`
- `Assets/Scripts/Player/Skills/PlayerSkillManager.cs`
- `Assets/Scripts/Player/Skills/PlayerStatusEffectController.cs`
- `Assets/Scripts/Player/PlayerSkillCanvasUI.cs`
- `Assets/Scripts/Player/PlayerSkillBarCanvasUI.cs`
- `Assets/Scripts/Player/PlayerSkillHudUI.cs`
- `Assets/Scripts/Player/PlayerMitigationVisualFeedback.cs`
- `Assets/Scripts/Spawner/SkeletonDebugUI.cs`
- `Assets/Scripts/Level/SavePoint.cs`

### 核心资产

- `Assets/Resources/EnemyBase.prefab`
- `Assets/Resources/SkeletonEnemy_Variant.prefab`
- `Assets/Resources/SkeletonBossEnemy_Variant.prefab`
- `Assets/Resources/SkeletonEnemy.prefab`
- `Assets/Resources/ItemDrop.prefab`
- `Assets/Resources/UI/DamageNumberPopup.prefab`
- `Assets/Resources/UI/DamageNumberPopup_PlayerDamage.prefab`
- `Assets/Resources/UI/DamageNumberPopup_PlayerTaken.prefab`
- 目标倒三角指示器 Prefab（路径未确认，已绑定到 Player 的 `TargetSelectionIndicator.indicatorPrefab`）
- 读条重击 `EnemySkillData` 资产（当前路径未确认，已配置到 `SkeletonBossEnemy_Variant.prefab`）
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
- `Assets/Skills/Player/Skill_IronBulwark.asset`
- `Assets/Skills/Player/Skill_StoneGuard.asset`
- `Assets/Art/UI/SkillIcons/Skill_IronBulwark.png`
- `Assets/Art/UI/SkillIcons/`：玩家技能图标目录


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
→ 玩家使用注册技能（如 Iron Bulwark / Stone Guard）
→ Canvas 技能栏显示 Active / Cooldown
→ PlayerStatusEffectController 修正玩家受到的伤害
→ 掉落骨头 / 装备
→ 拾取
→ 背包显示
→ 从背包装备 Core / Armor / Accessory
→ 攻击力 / 最大生命值汇总变化
→ 卸下装备回背包
→ 替换装备时旧装备回背包
→ 指定敌人释放读条技能并显示机制预告
→ 继续刷怪
```

### 推荐下一阶段方向

#### 优先级 1：玩家技能系统 v0.1 稳定化与小范围扩展

1. 当前 Iron Bulwark 与 Stone Guard 已验证 DamageReduction 技能流程。
2. 下一步优先在现有 v0.1 框架上增加一个不同类型的小技能或整理通用视觉反馈。
3. 暂时不要直接做完整技能树、技能学习、拖拽热键栏或复杂 Buff 系统。
4. 若新增效果类型，应从 `PlayerSkillData.PlayerSkillEffectType` 与 `PlayerStatusEffectController` 小步扩展。

#### 优先级 2：整理 EnemyAI / NavMesh 移动基础

1. 实测 Wander / Chase / ReturnToSpawn 的 Agent 行为。
2. 检查 Chase → Attack 是否存在滑动 / 抖动。
3. 检查 Chase → ReturnToSpawn 是否存在 Agent 路径被旧状态清理误停的问题。
4. 整理 EnemyAI 的 NavMeshAgent / Rigidbody 驱动权，减少 `rb.isKinematic` 状态切换。
5. 后续再考虑 Attack 距离 hysteresis、Unreachable / Evade 规则。
6. 整理 NavMeshSurface LayerMask，只让 Terrain / Ground / Environment 参与 Bake。
7. 视需要将嵌入 Scene 的 NavMeshData 另存为独立 `.asset`。

#### 优先级 3：继续完善刷装备闭环

1. 实测并调整 SkeletonEnemy 的 Core 掉率。
2. 增加第二个 Core 测试装备，例如：
   - 攻击核心：AttackPowerBonus 高，MaxHealthBonus 低
   - 守护核心：AttackPowerBonus 中，MaxHealthBonus 高
3. 测试替换 Core：新 Core 从背包进装备槽，旧 Core 回背包。
4. 之后再考虑简单 DropTable ScriptableObject。

#### 优先级 4：装备系统扩展

1. 当前已完成 Core / Armor / Accessory 三槽结构。
2. 当前已完成 PlayerCombatStats 多槽属性汇总。
3. 后续可增加正式装备 UI、装备说明显示、装备来源整理。
4. 主角武器固定，不进入装备系统。
5. 暂时不做随机词条。

#### 优先级 5：正式 UI

1. Canvas 技能栏第一版已完成。
2. 正式背包 UI。
3. 正式装备 UI。
4. 正式死亡 / 复活 UI。
5. 正式存档点提示。

#### 优先级 6：中长期结构升级

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
- 完整技能树 / 技能学习 / 拖拽技能栏
- 五人真实同屏战斗
- 完整 Hikari AI
- Boss 战最终版
- 复杂仇恨表

---

## 11. Next Suggested Tasks

### ⭐ 最推荐的下一个小任务

**完善 `PlayerSkillHudUI` / Debug 显示，让不同玩家技能效果类型显示对应参数。**

目的：

```text
当前玩家技能已支持：
- DamageReduction
- AttackPowerMultiplier
```

建议目标：

```text
当 LastPressedSkillState 的 EffectType 是 DamageReduction 时显示 Damage Taken Multiplier。
当 EffectType 是 AttackPowerMultiplier 时显示 Attack Power Multiplier。
保持 OnGUI 调试用途，不做正式 UI。
不修改技能执行逻辑，不修改 Animator / Prefab / Scene。
```

验收目标：

```text
按 Iron Bulwark / Stone Guard 时能看到减伤倍率。
按攻击强化测试技能时能看到攻击倍率。
冷却中按键也能更新 LastPressedSkillState 的显示。
```

### 备选任务

1. 给 `PlayerTargeting` 增加 Shift+Tab 反向选敌，仍复用同一候选列表与敌对判定。
2. 给伤害飘字增加对象池 `DamageNumberPool`，减少频繁 Instantiate / Destroy。
3. 抽象 `PlayerMitigationVisualFeedback` 为通用 `PlayerSkillVisualController`，根据 `PlayerSkillVisualType` 显示不同视觉反馈。
4. 给目标显示追加目标血条高亮或简单目标信息 UI，但不要改目标选择逻辑。
5. 给 `SkeletonDebugUI` 加 `UNITY_EDITOR || DEVELOPMENT_BUILD` 保护。
6. 整理 NavMeshSurface LayerMask，只让 Terrain / Ground / Environment 参与 Bake。


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


## 14. 本次有效变更摘要（2026-05-12 第二次）

### EnemyAI SpawnArea コンテキスト注入インターフェース

1. `EnemyAI.cs` 新増フィールド：`_spawnAreaCenter`（Vector3）、`_hasSpawnAreaContext`（bool）。
2. `EnemyAI.cs` 新増プロパティ：`WanderCenter`（Wander 範囲基準中心）、`LeashCenter`（Leash 範囲基準中心）。
   - `_hasSpawnAreaContext` が false の場合（旧 EnemySpawnPoint）：`_spawnPosition` を返す。
   - `_hasSpawnAreaContext` が true の場合（EnemySpawnArea）：`_spawnAreaCenter` を返す。
3. `EnemyAI.Awake()` で `_spawnAreaCenter = _spawnPosition`、`_hasSpawnAreaContext = false` を初期化。
4. `GetHorizontalDistanceFromSpawn()` の基準点を `_spawnPosition` から `LeashCenter` に変更（Leash 判定をエリア中心基準に）。
5. `TryPickWanderPoint()` の Wander 中心を `_spawnPosition` から `WanderCenter` に変更（Wander ランダム点選択をエリア中心基準に）。
6. `EnemyAI.cs` 新增公开方法 `SetSpawnAreaContext(areaCenter, areaWanderRadius, areaLeashRadius, spawnPosition, spawnRotation)`：
   - EnemySpawnArea が Instantiate 直後に呼び出す想定。
   - `_hasSpawnAreaContext = true` に設定し、エリア中心と半径を上書き。
   - `areaLeashRadius < areaWanderRadius` の場合は自動修正（`Mathf.Max(wanderRadius, areaLeashRadius)`）。
   - `_spawnPosition / _spawnRotation` をこの敵の実際の出生点/朝向に更新。
7. ReturnToSpawn 帰還先・FinishReturnToSpawn・ResetToSpawn はすべて `_spawnPosition`（実際の出生点）を継続使用。
8. 旧 EnemySpawnPoint は `SetSpawnAreaContext` を呼び出さないため完全互換。
9. Prefab / Scene / Animator 未修改。

---

## 15. EnemySpawnArea 区域刷怪系统

### `EnemySpawnArea.cs`

挂载在场景 GameObject 上的区域刷怪组件。

**主要字段：**

| 字段 | 含义 |
|---|---|
| `spawnEntries` | SpawnEntry 列表（支持权重 / 单种上限） |
| `maxAliveCount` | 区域内总存活上限 |
| `spawnRadius` | 内圈：生成范围 = Wander 游荡范围 |
| `leashRadius` | 外圈：追逐 / 脱战范围 |
| `respawnInterval` | 死亡后补怪延迟（秒） |
| `spawnOnStart` | Play Mode 开始时是否自动刷满 |
| `navMeshSampleDistance` | NavMesh.SamplePosition 最大采样距离 |
| `maxSpawnPositionAttempts` | 随机生成点最大尝试次数 |

**SpawnEntry 结构：**

```csharp
[System.Serializable]
private class SpawnEntry
{
    public GameObject enemyPrefab;
    [Min(0)] public int weight   = 1;   // 0 = 不参与
    [Min(0)] public int maxAlive = 999; // 0 = 禁用
}
```

**生成流程：**

```text
Start() → FillToMaxAlive()
→ CleanupAliveList()
→ while alive < maxAliveCount：
    TrySpawnOneEnemy()
    → TryPickSpawnEntry()（加权随机，跳过达到 maxAlive 的条目）
    → TryGetRandomSpawnPosition()（NavMesh.SamplePosition）
    → Instantiate
    → EnemyAI.SetSpawnAreaContext()
    → 订阅 HealthComponent.OnDied
    → 加入 _aliveEnemies 和 _spawnedPrefabByEnemy

死亡回调 → HandleEnemyDied()
    → 从 _aliveEnemies 和 _spawnedPrefabByEnemy 移除
    → StartCoroutine(RespawnAfterDelay(respawnInterval))
    → FillToMaxAlive()
```

**运行时数据：**
- `_aliveEnemies`：活着的敌人 GameObject 列表
- `_spawnedPrefabByEnemy`：记录每个敌人使用的原始 Prefab（用于 CountAliveForPrefab）

**注意：**
- EnemySpawnArea 第一版暂未接入 LevelObjectiveManager。
- Gizmos：OnDrawGizmosSelected 画绿色内圈（spawnRadius）和橙色外圈（leashRadius）。

---

## 3.1 Enemy AI 系统 补充

### EnemyAI SpawnArea 支持

EnemyAI 已增加以下字段支持区域刷怪：

| 字段 / 属性 | 含义 |
|---|---|
| `_spawnAreaCenter` | SpawnArea 注入的区域中心 |
| `_hasSpawnAreaContext` | 是否已调用 SetSpawnAreaContext |
| `WanderCenter`（属性） | Wander 范围基准中心 |
| `LeashCenter`（属性） | Leash / 脱战范围基准中心 |

**逻辑：**
- 未调用 SetSpawnAreaContext：`WanderCenter = _spawnPosition`（旧逻辑，EnemySpawnPoint 兼容）
- 已调用：`WanderCenter = _spawnAreaCenter`（区域中心）

**ReturnToSpawn 不受影响：**
- 仍回到各怪物自己的 `_spawnPosition`（不是区域中心）

**`SetSpawnAreaContext()` 接口：**

```csharp
public void SetSpawnAreaContext(
    Vector3    areaCenter,
    float      areaWanderRadius,
    float      areaLeashRadius,
    Vector3    spawnPosition,
    Quaternion spawnRotation)
```

---

## 3.8 EnemyBase Prefab 与 Variant 工作流

### EnemyBase.prefab

路径：`Assets/Resources/EnemyBase.prefab`

EnemyBase 是所有敌人 Prefab Variant 的基础模板，不直接用于生成。

**根对象组件：**
Transform、Animator、Rigidbody、CapsuleCollider、NavMeshAgent、FactionComponent、FOVDetector、EnemyAI、HealthComponent、WorldHealthBar、EnemyDeathHandler、EnemyDropper、EnemySkillController、EnemyCastBarUI

**子对象：**
- `VisualRoot`（空 GameObject，localPosition=0，scale=1）
  - 具体敌人模型 / 骨骼放在这里

**设计说明：**
- EnemyDropper.drops 保持为空（Variant 中覆写）
- FOVDetector.eyePosition 保持为 null（Variant 中绑定自己骨骼）
- Animator Controller 当前使用 SkeletonAnimator.controller（Variant 可覆写）
- 后续新增所有敌人通用脚本时，只加到 EnemyBase.prefab
- EnemySkillController.skills 在 EnemyBase 中保持为空，表示默认无技能
- EnemyCastBarUI 可保留在 EnemyBase 上；没有读条时不会显示

### Variant 工作流

当前已验证的层级：

```text
EnemyBase.prefab
├─ SkeletonEnemy_Variant.prefab  (Prefab Variant)
└─ SkeletonBossEnemy_Variant.prefab  (Prefab Variant)
```

**创建 Variant 的规范流程：**
1. 右键 EnemyBase.prefab → Create → Prefab Variant，命名为 `<EnemyName>_Variant.prefab`
2. 在 VisualRoot 下放入具体模型和骨骼层级
3. 覆写 Animator.Controller 为该敌人专用
4. 将 FOVDetector.eyePosition 绑定到 VisualRoot 内的 head 骨骼
5. 在 EnemyDropper.drops 中配置掉落
6. 按需覆写 CapsuleCollider、NavMeshAgent、EnemyAI、HealthComponent 参数

**VisualRoot 缩放规则（重要）：**

```text
推荐：根对象 scale = 1
      VisualRoot.scale = 用于控制视觉体型（如 Boss 用 1.5）
      Collider / NavMeshAgent 单独在 Variant 中调整

原因：根对象缩放会同时影响 Collider / NavMeshAgent / Rigidbody
      VisualRoot 缩放只影响视觉模型，更安全
```

### SkeletonEnemy_Variant.prefab

路径：`Assets/Resources/SkeletonEnemy_Variant.prefab`

- Source：EnemyBase.prefab（true Prefab Variant）
- VisualRoot 下：M_Skeleton.Body / Jaw / Skull / root 骨骼层级
- FOVDetector.eyePosition = VisualRoot/.../head.x
- EnemyDropper.drops = 骨头100% + 守护核心20%（同旧 SkeletonEnemy）
- EnemySkillController.skills 继承 EnemyBase 空列表，当前无技能
- 其余参数继承 EnemyBase（与旧 SkeletonEnemy 一致）

### SkeletonBossEnemy_Variant.prefab

路径：`Assets/Resources/SkeletonBossEnemy_Variant.prefab`

- Source：EnemyBase.prefab（true Prefab Variant）
- VisualRoot.localScale = (1.5, 1.5, 1.5)
- 根对象 scale = (1, 1, 1)
- Collider / NavMeshAgent 单独调整（匹配 1.5× 体型）
- EnemyDropper.drops = 守护核心100%（Boss 测试用）
- EnemySkillController.skills 覆写配置读条重击（CastAttack）
- 与旧 SkeletonBossEnemy 行为基本一致，但当前具备读条重击测试技能

**注意：**
旧 `SkeletonEnemy.prefab` 和 `SkeletonBossEnemy.prefab` 仍然存在并可用，未被删除。
新 Variant 与旧独立 Prefab 并行存在，可分别挂载到 EnemySpawnArea 使用。

### 推荐长期结构

```text
当前（敌人种类少）：
EnemyBase → Variant（直接派生）

未来（同族敌人增多时）：
EnemyBase
└─ SkeletonBase（可选中间层）
   ├─ SkeletonEnemy_Variant
   ├─ SkeletonBossEnemy_Variant
   └─ SkeletonArcherEnemy_Variant

当前阶段不建议过早加 SkeletonBase 中间层。
```


## 3.9 Enemy Skill / 敌人技能系统

### `EnemySkillData.cs`

敌人技能数据 ScriptableObject。

当前用途：

- 定义敌人可配置技能，不把技能写死进 `EnemyAI.cs`。
- 支持通过 Prefab / Variant 的 Inspector 配置不同敌人会哪些技能。
- 第一版已用于 `CastAttack`（读条重击）。

当前关键字段 / 属性：

- `skillId`：技能内部 ID
- `displayName`：显示名，用于读条 UI
- `skillType`：当前至少包含 `None` / `CastAttack`
- `damage`：技能伤害
- `castTime`：读条时间
- `cooldown`：冷却时间
- `range`：释放 / 命中范围

注意：

- `EnemySkillData` 只保存数据，不负责执行技能。
- 后续新增敌人技能类型时，应优先扩展该数据结构和 `EnemySkillController`，不要直接把技能逻辑写死进 `EnemyAI.cs`。

### `EnemySkillController.cs`

敌人技能控制器，挂载在敌人 Prefab 根对象上。

职责：

- 持有 `List<EnemySkillData> skills`。
- 判断技能是否可用、距离是否满足、冷却是否结束。
- 执行第一版 `CastAttack` 读条技能。
- 暴露读条状态供 UI 读取。
- 在 Attack 状态离开、ReturnToSpawn、ResetToSpawn、ForceDisengage、OnDisable 等情况下清理读条。

当前关键状态 / 属性：

- `IsCasting`：是否正在读条。
- `CurrentSkill`：当前读条技能。
- `CurrentCastElapsed`：当前读条经过时间。
- `CurrentCastDuration`：当前读条总时间。
- `CurrentCastRemaining`：剩余读条时间。
- `CurrentCastProgress`：0～1 的读条进度。

当前关键方法：

- `TryGetReadySkillInRange(Transform target, out EnemySkillData skill)`
- `TryStartSkill(EnemySkillData skill, Transform target)`
- `CancelCasting(string reason)`

安全规则：

- `skills == null` 或 `skills.Count == 0` 时不报错，返回无可用技能。
- `skills` 中有 null 元素时跳过。
- `skillType == None` 时不会执行。
- 目标 null、缺少 `HealthComponent`、目标死亡、超出范围时不会造成伤害。
- 无技能 / 技能不可用时，`EnemyAI` 会继续普通攻击。
- `OnDisable()` 会清理读条状态，避免对象被禁用时残留 cast 状态。

### `EnemyCastBarUI.cs`

敌人读条 UI 第一版，当前使用 `OnGUI + Camera.WorldToScreenPoint` 绘制。

职责：

- 读取同一敌人身上的 `EnemySkillController`。
- `IsCasting == true` 时显示技能名、进度条、读条时间。
- `IsCasting == false` 时不显示。

实现注意：

- GUIStyle 通过 `EnsureStyles()` 在 `OnGUI()` 内懒初始化。
- 不要在 `Awake()` / `Start()` 中访问 `GUI.skin`。
- 使用 `Texture2D.whiteTexture + GUI.color` 绘制进度条，不每帧创建贴图。
- `Camera.main == null`、`CurrentSkill == null`、`CurrentCastDuration <= 0` 时安全 return 或 fallback。

### 当前 Prefab 使用方式

当前设计目标是：敌人基础模板统一挂技能相关组件，具体敌人通过 Variant 决定是否有技能。

```text
EnemyBase.prefab
- EnemySkillController（skills 为空）
- EnemyCastBarUI
→ 默认无技能、无读条显示、无影响

SkeletonEnemy_Variant.prefab
→ 继承 EnemyBase，保持 skills 为空
→ 普通小怪无技能，只普通攻击

SkeletonBossEnemy_Variant.prefab
→ 覆写 EnemySkillController.skills，配置读条重击
→ 可释放 CastAttack / 读条重击
```

已确认：不设置技能的小怪不会触发技能；配置读条重击的敌人可以正常读条、显示 CastBar，并在命中时造成伤害。

---

## 16. TMP Dynamic Font Asset Git 修改问题

使用 `TMP Dynamic Font Asset`（如 `SourceHanSansSC-Medium_TMP.asset`）时，
运行中出现新字符会导致字体资产被重新写入（字符图集更新），Git 会检测到 `.asset` 文件被修改。
当前阶段暂不处理，提交前可选择性地 `git checkout` 字体资产，或使用 `.gitignore` 排除。

---

## 17. 本次有效变更摘要（2026-05-12 第三次）

### EnemySpawnArea / SpawnEntry

1. `EnemySpawnArea.cs` 创建（第一版）：区域刷怪，NavMesh 随机点，死亡后补怪。
2. `EnemySpawnArea.cs` 升级（第二版）：`enemyPrefabs` → `SpawnEntry` 列表，支持 weight / maxAlive 加权随机与单种存活上限。
3. 新增 `_spawnedPrefabByEnemy` dictionary，用于统计单种存活数量。
4. `TryPickSpawnEntry()` 实现加权随机，跳过 weight≤0 / maxAlive≤0 / 已达上限的条目。

### EnemyAI SpawnArea 接口

5. `EnemyAI.cs` 新增 `_spawnAreaCenter`、`_hasSpawnAreaContext` 字段。
6. 新增 `WanderCenter`、`LeashCenter` 计算属性。
7. 新增 `SetSpawnAreaContext()` 公开方法。
8. `GetHorizontalDistanceFromSpawn()` 改为使用 `LeashCenter`。
9. `TryPickWanderPoint()` 改为使用 `WanderCenter`。

### EnemyBase Prefab

10. `Assets/Resources/EnemyBase.prefab` 创建（从 SkeletonEnemy 复制）。
11. EnemyBase 清理：移除具体骷髅模型 / 骨骼，新增 VisualRoot 空子对象。
12. FOVDetector.eyePosition 置 null，EnemyDropper.drops 清空。

### Prefab Variant

13. `Assets/Resources/SkeletonEnemy_Variant.prefab`：EnemyBase 的真正 Prefab Variant，模型 / 骨骼位于 VisualRoot 下，已恢复 eyePosition、drops、完整参数。
14. `Assets/Resources/SkeletonBossEnemy_Variant.prefab`：EnemyBase 的真正 Prefab Variant，VisualRoot.scale=1.5，根对象 scale=1，已单独调整 Collider / NavMeshAgent。
15. 旧 `SkeletonEnemy.prefab` / `SkeletonBossEnemy.prefab` 保留，未删除。

---

## 18. 本次有效变更摘要（2026-05-13）

### EnemyAI 移动控制权整理第一版

1. `EnemyAI.cs` 只做移动控制权整理，未修改 Animator / Prefab / Scene。
2. 新增 / 整理 `ClearRigidbodyVelocity()`，统一清理 `rb.linearVelocity` 与 `rb.angularVelocity`。
3. 新增 / 整理 `StopAgentMovement(bool resetPath)`，安全停止 NavMeshAgent，并按需 ResetPath。
4. 新增 / 整理 `PrepareAgentDrivenMovement()`，Wander / Chase / ReturnToSpawn 进入 Agent 驱动前清理 Rigidbody 残留速度。
5. 新增 / 整理 `StopMovementForAttack()`，Attack 进入时停止 Agent 并清理 Rigidbody 残留速度，避免攻击时滑动。
6. `StopAgentAndRestoreRigidbody()` 整理为停止 Agent、恢复 Rigidbody 并清理残留速度。
7. 正常移动路径继续由 NavMeshAgent 主导。
8. Rigidbody 不再作为常规移动目标扩展方向，只保留碰撞 / 物理辅助 / 旧 fallback 兼容。
9. Chase 目标点暂时不可达时仍不切 Rigidbody，继续追旧 path / last valid destination。
10. `OnAttackHit()`、攻击伤害、仇恨系统、`SetSpawnAreaContext()` 均未修改。
11. 用户实测未发现明显 Chase / Attack 抖动，因此暂不做 Attack 距离 hysteresis。

### 清理项状态确认

1. `DebugManager` 上的 Missing Mono Script 已清理。
2. 旧 `Ground / Ground(1) ...` 系列对象已删除。
3. 当前主地面以 Terrain 为准。
4. EnemySpawnArea 区域刷怪系统经用户确认目前没有明显问题。
5. Core 替换闭环已验证：背包多个 Core、装备新 Core、旧 Core 回背包、数值变化、卸下回背包均正常。

### 装备系统扩展：Core / Armor / Accessory

1. 项目设计确认：主角武器固定，不进入装备系统。
2. 当前装备槽为 `Core / Armor / Accessory`，不包含 Weapon。
3. `PlayerEquipment.cs` 从单 Core 槽扩展为三槽：`equippedCore / equippedArmor / equippedAccessory`。
4. 新增属性：`EquippedArmor`、`EquippedAccessory`、`HasArmorEquipped`、`HasAccessoryEquipped`。
5. 新增方法：`EquipArmor()`、`EquipArmor(item, out replacedItem)`、`UnequipArmor()`。
6. 新增方法：`EquipAccessory()`、`EquipAccessory(item, out replacedItem)`、`UnequipAccessory()`。
7. 保留所有既有 Core API：`EquippedCore`、`HasCoreEquipped`、`EquipCore()`、`UnequipCore()`、`ClearEquipment()`、`OnEquipmentChanged`。
8. `ClearEquipment()` 现在一次性清空 Core / Armor / Accessory；任意槽位有装备时只触发一次 `OnEquipmentChanged`。

### PlayerCombatStats 多装备槽属性汇总

1. `PlayerCombatStats.cs` 只修改属性汇总逻辑。
2. 新增私有 helper：`GetAttackPowerBonus(ItemData item)` 与 `GetMaxHealthBonus(ItemData item)`。
3. `EquipmentAttackPowerBonus` 现在汇总 Core + Armor + Accessory。
4. `EquipmentMaxHealthBonus` 现在汇总 Core + Armor + Accessory。
5. `CurrentNormalAttackDamage = BaseNormalAttackDamage + EquipmentAttackPowerBonus` 保持不变。
6. `CurrentMaxHealth = Mathf.Max(1f, BaseMaxHealth + EquipmentMaxHealthBonus)` 保持不变。
7. `ApplyCurrentMaxHealth(false)` 行为保持不变：装备加最大生命值不会自动补满血，卸下导致上限降低时由 `HealthComponent.SetMaxHealth()` 裁剪当前 HP。
8. `PlayerEquipment == null` 或空槽位时按 0 加成处理，不崩溃。
9. 未新增 Weapon 汇总逻辑。

### SkeletonDebugUI 装备状态窗口

1. `SkeletonDebugUI.cs` 新增独立装备状态 Debug 窗口。
2. 新窗口显示在左侧 Debug 按钮面板右侧，整体仍在屏幕左上区域。
3. 新窗口高度固定较小，不延伸到屏幕下方。
4. 新增方法：`DrawEquipmentStatusWindow(float margin, float leftPanelWidth)`。
5. 新增方法：`DrawEquipmentSlotLine(string slotName, ItemData item)`。
6. 窗口显示 Core / Armor / Accessory 当前装备状态。
7. 窗口显示 `EquipmentAttackPowerBonus`、`EquipmentMaxHealthBonus`、`CurrentNormalAttackDamage`、`CurrentMaxHealth`。
8. 缺少 `PlayerEquipment` 或 `PlayerCombatStats` 时只显示提示，不崩溃。
9. 未影响右侧背包 Debug 窗口。
10. 未新增 Weapon 显示。

### SkeletonDebugUI 多装备槽操作按钮

1. `SkeletonDebugUI.cs` 新增 Armor / Accessory Debug 操作按钮。
2. 新增按钮：装备背包中的第一个 Armor。
3. 新增按钮：卸下 Armor 到背包。
4. 新增按钮：装备背包中的第一个 Accessory。
5. 新增按钮：卸下 Accessory 到背包。
6. 新增方法：`EquipFirstArmorFromInventory()`。
7. 新增方法：`UnequipArmorToInventory()`。
8. 新增方法：`EquipFirstAccessoryFromInventory()`。
9. 新增方法：`UnequipAccessoryToInventory()`。
10. 装备流程与 Core 保持一致：`FindFirstEquipmentBySlot()` → `EquipX(out replacedItem)` → `RemoveItem(newItem)` → 旧装备回背包。
11. 卸下流程与 Core 保持一致：`UnequipX()` → `AddItem(unequippedItem)`。
12. 未修改 PlayerEquipment / PlayerInventory / PlayerCombatStats / ItemData。
13. 未修改 Animator / Prefab / Scene。
14. 未新增 Weapon 按钮或 Weapon 逻辑。

### 当前有效状态总结

```text
EnemyAI：
正常移动由 NavMeshAgent 主导。
Rigidbody 主要承担碰撞 / 物理辅助，并保留必要旧 fallback 兼容。
Attack 进入时会停止 Agent 并清理 Rigidbody 残留速度。

装备：
当前装备槽为 Core / Armor / Accessory。
主角武器固定，不进入装备系统。
PlayerEquipment 已支持三槽装备 / 卸下 / 替换。
PlayerCombatStats 已汇总三槽攻击力与最大生命值。

Debug：
F1 Debug UI 左侧按钮区支持 Core / Armor / Accessory 装备操作。
左上区域有独立装备状态窗口。
右侧仍为背包 Debug 窗口。

场景清理：
DebugManager Missing Script 已清理。
旧 Ground 系列对象已删除。
EnemySpawnArea 与装备替换闭环已由用户确认没有明显问题。
```

---

## 19. 本次有效变更摘要（2026-05-14）

1. `PlayerController.cs` 与 `RPGCameraController.cs` 的玩家操作已整理为 FF14 Legacy-like：WASD 相机基准移动、移动时角色朝实际方向转身、右键只控制相机、左键只用于目标选择、左键 + 右键支持双键前进，Shift 支持八方向跑步。
2. 玩家移动动画输出已适配 Legacy-like：非锁定移动下任意方向只使用 Forward Walk / Forward Run，不再使用左走 / 右走 / 后退动画；`Horizontal` 保持 0，`Speed` 表示 Idle / Walk / Run。
3. `RPGCameraController.cs` 鼠标视角旋转改为 `Mouse.delta * rotationSpeed`，不再乘 `Time.deltaTime`；`rotationSpeed` 可在 Inspector 中调整。`SkeletonDebugUI.cs` 装备状态窗口高度已改为按实际显示行数动态计算。


---

## 20. 本次有效变更摘要（2026-05-14 第二次）

1. 敌人技能系统第一版已落地：新增 `EnemySkillData.cs`、`EnemySkillController.cs`、`EnemyCastBarUI.cs`；`EnemyAI.cs` 的 Attack 状态已接入技能调用，有技能时尝试释放，无技能时继续普通攻击。
2. `CastAttack` / 读条重击第一版已可用：指定敌人可读条、显示技能名 / 进度 / 时间，读条结束后按距离和目标状态结算伤害；Miss / Cancel / ReturnToSpawn / ResetToSpawn / ForceDisengage / OnDisable 会清理读条。
3. 敌人 Prefab 工作流更新：`EnemyBase.prefab` 统一挂载 `EnemySkillController` 与 `EnemyCastBarUI` 且 skills 为空；`SkeletonEnemy_Variant.prefab` 保持无技能；`SkeletonBossEnemy_Variant.prefab` 覆写配置读条重击。当前复杂地形下的寻路、动作、动画、读条范围和 ItemDrop 贴地表现尚未验证，暂不处理。

---

## 21. 本次有效变更摘要（2026-05-18）

1. 玩家技能系统 v0.1 已落地：新增并接入 `PlayerSkillData`、`PlayerSkillManager`、`PlayerStatusEffectController`、`PlayerSkillCanvasUI`、`PlayerSkillBarCanvasUI`、`PlayerSkillHudUI`；`PlayerMitigationController.cs` 已删除，`HealthComponent` 现在只通过 `PlayerStatusEffectController` 修正玩家受到的伤害。
2. Iron Bulwark / Stone Guard 等 DamageReduction 技能可通过 `PlayerSkillManager.skills` 注册，按对应数字键激活；Canvas 技能栏会按注册顺序自动生成技能格，Active / Cooldown / Ready 显示正常，多个减伤按乘算叠加。
3. `SkillCanvas / SkillBar / SkillSlotTemplate` 已成为正式技能栏第一版：模板隐藏，运行时生成所有技能格；技能栏锚定右下，新技能显示在最右侧，技能数量增加时整体向左扩展。


## 22. 本次有效变更摘要（2026-05-18 第二次）

1. 玩家技能系统新增 `AttackPowerMultiplier` 效果类型；`PlayerStatusEffectController` 可修正普通攻击输出伤害，`PlayerSkillController` 普通攻击结算已接入该修正。攻击强化测试技能已创建并注册，Play Mode 测试正常，具体资产路径未确认。
2. 战斗反馈新增伤害飘字系统：`DamageNumberPopup` / `DamageNumberSpawner` 通过 `HealthComponent.OnDamaged` 显示最终实际伤害；Player 与 `EnemyBase.prefab` 已绑定对应 Spawner，并区分玩家打出伤害 / 玩家受到伤害的 Popup Prefab。
3. 目标选择与显示已强化：`PlayerTargeting` 支持 Tab 从屏幕左侧到右侧循环选择屏幕内敌人；`TargetSelectionIndicator` 读取 `CurrentTarget` 在目标头顶显示倒三角，并已修正静止目标指示器上下频闪问题。

