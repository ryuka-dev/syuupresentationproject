# PROJECT_STATE

## 1. Project Overview
- 项目类型：3D RPG 动作游戏原型
- Unity 版本：6000.4.3f1 (Unity 6)
- 使用的主要包：
  - URP (Universal Render Pipeline) 17.4.0
  - New Input System 1.19.0
  - AI Navigation 2.0.12
  - Unity MCP (GitHub)
- 当前主要场景：`Assets/Scenes/SampleScene.unity`
- 当前开发阶段：早期原型 - 核心战斗系统开发

## 2. Game Concept
- 游戏核心玩法：第三人称动作战斗，玩家对抗 AI 敌人
- 主要循环：玩家移动 → 鼠标左键选中敌人 → 按 1 使用普通攻击 → 敌人血量归零 → 死亡动画 → 实体销毁
- 已确认的设计方向：
  - 基于 FSM 的敌人 AI（Idle/Chase/Attack）
  - 阵营系统（敌我识别）
  - 仇恨系统（多目标列表 + 仇恨值排序 + 脱战）
  - 攻击冷却机制
  - 世界空间血条显示

## 3. Current Architecture

### Enemy AI 系统
- `EnemyAI.cs`：敌人有限状态机（Idle/Chase/Attack）+ 仇恨系统。
  - 仇恨列表：`Dictionary<Transform, float> hateTable`，key=目标，value=仇恨值
  - `AddHate(Transform, float)`：统一仇恨入口，添加/累加后重新选目标
  - `IsValidTarget(Transform)`：有效性检查（非 null、有 HealthComponent、未死亡、阵营敌对）
  - `RemoveInvalidHateTargets()`：清除死亡/销毁目标
  - `SelectHighestHateTarget()`：选仇恨值最高有效目标为 currentTarget
  - `UpdateDisengage()`：距离脱战计时，目标持续超出 disengageDistance 后脱战
  - `HandleDamaged(float, Transform)`：订阅 `HealthComponent.OnDamaged`，受击时对攻击来源加仇恨
  - `OnAttackHit()`：由 Animation Event 调用，首行有 `!enabled` 保护，**方法名不可改**
  - `OnEnable/OnDisable`：管理 OnDamaged 事件订阅生命周期
- `FOVDetector.cs`：视野检测（FOV），角度 + 距离判断目标可见性。
- `FactionSystem.cs` / `FactionComponent`：阵营枚举（Player/Skeleton/Goblin/Dragon），`ShouldAttack(Faction)` 判断敌对关系。
- `EnemyDeathHandler.cs`：监听 `HealthComponent.OnDied`，禁用 EnemyAI、停止 Rigidbody、禁用 Collider，触发死亡动画，延迟 destroyDelay 秒后 Destroy。

### Player 系统
- `PlayerController.cs`：玩家输入处理、移动控制（New Input System）。
- `RPGCameraController.cs`：第三人称相机跟随。
- `PlayerTargeting.cs`：鼠标左键点击，Physics.Raycast 检测目标，验证 HealthComponent + FactionComponent + 敌对关系后设为 `CurrentTarget`（public Transform，只读）。
- `PlayerSkillController.cs`：按数字键 1 读取 `PlayerTargeting.CurrentTarget`，验证目标有效性后调用 `HealthComponent.TakeDamage(damage, transform)`（传入攻击来源）。

### Health & Combat
- `HealthComponent.cs`：通用血量组件。
  - `TakeDamage(float)`：向后兼容接口，内部调用带来源版本
  - `TakeDamage(float, Transform attacker)`：带攻击来源接口，attacker 可为 null
  - `IsDead`（只读属性）
  - 事件：`OnHealthChanged(float, float)`、`OnDied`、`OnDamaged(float, Transform)`
- `WorldHealthBar.cs`：世界空间血条 UI（头顶）。
- `PlayerHealthBar.cs`：玩家血条 UI。

### Spawner & Debug
- `SkeletonSpawner.cs`：敌人生成器。
- `SkeletonDebugUI.cs`：调试用 UI。
- `PhysicsLayerSetup.cs`：物理层设置。

### Stats（未充分使用）
- `EntityStats.cs`：已创建但未集成。

## 4. Important Unity Objects

### Scene Objects (SampleScene.unity)
- **Player**
  - Components: Transform, Animator, CapsuleCollider, Rigidbody, PlayerController, FactionComponent（faction=Player）, HealthComponent, WorldHealthBar, PlayerTargeting, PlayerSkillController
  - ⚠️ HealthComponent 和 WorldHealthBar 可能各有两个实例（未确认）
- **Skeleton_Enemy**
  - Components: Transform, Animator, Rigidbody, CapsuleCollider, FactionComponent（faction=Skeleton）, FOVDetector, EnemyAI, HealthComponent, WorldHealthBar, EnemyDeathHandler
- **Ground**、**Main Camera**、**Directional Light**、**Global Volume**、**DebugManager**、**SkeletonSpawnerManager**

### Prefabs
- `Assets/Resources/SkeletonEnemy.prefab`：含 EnemyDeathHandler
- `Assets/Resources/Skeleton_110.prefab`：骷髅模型资产

### Animator Controllers
- `Assets/Scripts/SkeletonAnimator.controller`：
  - 参数：`Speed`（Float）、`IsAttacking`（Bool）、`IsDead`（Trigger）
  - 状态：Idle、Walk、Attack、Death
  - Idle → Attack：`IsAttacking` == true
  - Attack → Idle：`hasExitTime=true, exitTime=0.9`（不可改，核心配置）
  - Any State → Death：`IsDead` Trigger（canTransitionToSelf=false）
  - Death 状态：clip=`root|death`（1.4s），无出口过渡

### Animation Events
- `Skeleton_slash01.fbx` 第 20 帧：调用 `OnAttackHit()`（**方法名不可改**）
- Death 动画：当前无 Animation Event，使用计时器销毁

## 5. Input / Control
- 使用：New Input System 1.19.0
- 玩家移动：WASD（PlayerController）
- 目标选择：鼠标左键（Mouse.current.leftButton.wasPressedThisFrame）
- 技能释放：键盘 1（Keyboard.current.digit1Key.wasPressedThisFrame）

## 6. Completed Features
- ✅ 玩家第三人称移动控制
- ✅ 敌人 FSM AI（Idle、Chase、Attack 状态）
- ✅ FOV 视野检测系统
- ✅ 阵营系统（FactionComponent，ShouldAttack()）
- ✅ 血量系统（HealthComponent，含 IsDead + OnDied + OnDamaged 事件）
- ✅ 带攻击来源的伤害接口（TakeDamage(float, Transform)）
- ✅ 世界空间血条显示（头顶）
- ✅ 敌人攻击动画 + Animation Event 伤害触发（OnAttackHit）
- ✅ 攻击冷却机制（attackCooldown 默认 2 秒）
- ✅ 骷髅敌人生成器
- ✅ 追击时 stoppingDistance 停止移动
- ✅ 玩家鼠标左键选中敌对目标（PlayerTargeting）
- ✅ 玩家按 1 使用普通攻击（PlayerSkillController，默认 20 伤害，2m 范围，1s 冷却）
- ✅ 骷髅死亡动画播放（root|death，1.4s）
- ✅ 死亡后 AI/物理/Collider 禁用，延迟销毁实体（EnemyDeathHandler）
- ✅ 敌人仇恨系统：多目标列表 + 仇恨值排序 + 视野/受击两路加仇恨 + 距离脱战

## 7. In Progress / Known Issues
- ⚠️ Player 组件重复：HealthComponent 和 WorldHealthBar 可能各有两个实例（未确认）
- ⚠️ EntityStats.cs 未充分使用
- ⚠️ 死亡后 PlayerTargeting.CurrentTarget 仍可能持有已销毁对象的引用（PlayerSkillController 有 null 检查保护，但未主动清除引用）
- ⚠️ EnemyDeathHandler 使用固定 destroyDelay 计时，未使用 Animation Event，时机可能受播放速度影响
- ⚠️ `ScanForTarget()` 每 0.2s 调用 `FindObjectsOfType<FactionComponent>()`，敌人数量多时有性能隐患

## 8. Development Rules

### 修改前必读
- ❌ 不要随意重命名 public / SerializedField 字段（Inspector 可能已绑定）
- ❌ 不要重构无关代码
- ❌ 不要读取完整 Console、完整 Assets、完整 Scene Hierarchy
- ✅ 修改前先定位相关文件，只读取必要内容
- ✅ 优先小步修改，每次改动后确认编译通过

### Animator 修改注意
- `Attack → Idle` 的 `hasExitTime=true, exitTime=0.9` 是核心配置，不可改
- `IsAttacking` bool：EnemyAI 脉冲式触发（set true → 检测到 Attack 动画播放后 set false）
- `IsDead` Trigger：EnemyDeathHandler 触发，从 Any State 过渡到 Death，不可回到其他状态
- `OnAttackHit()` 方法名不可改（Animation Event 绑定）

### 代码修改原则
- Rigidbody 使用 `rb.linearVelocity`（Unity 6）
- 输入系统使用 `Mouse.current` / `Keyboard.current`（New Input System），不得使用 `UnityEngine.Input`
- `FaceTarget()` 仅在 FixedUpdate 中调用，使用 `Time.fixedDeltaTime`

## 9. Files That Should Be Treated Carefully

### 核心脚本
- `Assets/Scripts/Enemy/EnemyAI.cs`：FSM + 仇恨系统核心，`OnAttackHit()` 有 Animation Event 绑定
- `Assets/Scripts/Enemy/EnemyDeathHandler.cs`：订阅 `HealthComponent.OnDied`，触发死亡流程
- `Assets/Scripts/Enemy/FactionSystem.cs`：阵营枚举和 `ShouldAttack()` 接口
- `Assets/Scripts/HealthComponent.cs`：`TakeDamage(float/float+Transform)`、`IsDead`、`OnDied`、`OnDamaged` 事件
- `Assets/Scripts/Player/PlayerTargeting.cs`：`CurrentTarget`（public Transform, get-only）
- `Assets/Scripts/Player/PlayerSkillController.cs`：技能判定 + 伤害调用

### 核心资产
- `Assets/Scripts/SkeletonAnimator.controller`：参数 Speed/IsAttacking/IsDead，状态 Idle/Walk/Attack/Death
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`：第 20 帧 Animation Event
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_death.fbx`：clip `root|death`，1.4s
- `Assets/Resources/SkeletonEnemy.prefab`：含 EnemyDeathHandler

### 不应修改的文件
- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产（模型/贴图本体）
- `Packages/manifest.json`：包配置

## 10. Next Suggested Tasks

### ⭐ 最推荐的下一个小任务
**修复 Player 组件重复问题**：确认并移除 Player 上多余的 HealthComponent / WorldHealthBar 实例，避免血量计算异常。

### 优先级 1：稳定性
1. 修复 Player 组件重复（HealthComponent / WorldHealthBar 各可能有两个实例）
2. 死亡后主动清除 PlayerTargeting.CurrentTarget（在 EnemyDeathHandler 中或 PlayerTargeting 每帧检查 null）
3. 将 `FindObjectsOfType<FactionComponent>()` 替换为缓存列表（如场景 Manager 注册），减少 EnemyAI 扫描开销

### 优先级 2：战斗体验
4. 玩家普通攻击动画（按 1 时播放玩家攻击动作）
5. 集成 EntityStats 系统，支持攻击力/血量等属性可配置

---

**最后更新**：2026-04-29
**本次有效变更**：
1. `HealthComponent` 新增 `TakeDamage(float, Transform)` 重载和 `OnDamaged(float, Transform)` 事件，旧接口保持兼容。
2. `EnemyAI` 仇恨系统升级为多目标列表（hateTable）：视野发现、受击均通过 `AddHate()` 统一加仇恨，始终追击仇恨值最高的有效目标。
3. `EnemyAI` 新增距离脱战机制（disengageDistance=15m，disengageDelay=3s），目标超距持续超时后清除仇恨并回 Idle。
