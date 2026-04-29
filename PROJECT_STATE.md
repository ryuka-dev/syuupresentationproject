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
- 玩家目标：未确认（当前聚焦于战斗系统原型）
- 主要循环：玩家移动 → 敌人检测并追击 → 近战攻击 → 血量消耗
- 已确认的设计方向：
  - 基于 FSM 的敌人 AI（Idle/Chase/Attack）
  - 阵营系统（敌我识别）
  - 攻击冷却机制
  - 世界空间血条显示
- 主要循环：玩家移动 → 鼠标左键选中敌人 → 按 1 使用普通攻击 → 敌人血量归零 → 死亡动画 → 实体销毁

## 3. Current Architecture

### Enemy AI 系统
- `EnemyAI.cs`：敌人有限状态机（Idle/Chase/Attack）。攻击触发：冷却结束时 SetBool("IsAttacking", true)，动画进入 Attack 后立即清除。`OnAttackHit()` 由 Animation Event 调用，首行有 `!enabled` 保护。
- `FOVDetector.cs`：视野检测（FOV），角度 + 距离判断目标可见性。
- `FactionSystem.cs` / `FactionComponent`：阵营枚举（Player/Skeleton/Goblin/Dragon），`ShouldAttack(Faction)` 判断敌对关系。
- `EnemyDeathHandler.cs`：监听 `HealthComponent.OnDied`，禁用 EnemyAI、停止 Rigidbody、禁用 Collider，触发死亡动画，延迟 destroyDelay 秒后 Destroy。

### Player 系统
- `PlayerController.cs`：玩家输入处理、移动控制（New Input System）。
- `RPGCameraController.cs`：第三人称相机跟随。
- `PlayerTargeting.cs`：鼠标左键点击，用 Physics.Raycast 检测目标，验证 HealthComponent + FactionComponent + 敌对关系后设为 `CurrentTarget`（public Transform，只读）。
- `PlayerSkillController.cs`：按数字键 1 读取 `PlayerTargeting.CurrentTarget`，验证目标有效性（非空、有 HealthComponent、是敌对、在距离内、冷却完成）后调用 `HealthComponent.TakeDamage()`。

### Health & Combat
- `HealthComponent.cs`：通用血量组件，`TakeDamage(float)`，`Heal(float)`，`IsDead`（只读属性），C# 事件 `OnHealthChanged(float, float)` 和 `OnDied`。
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
- `Assets/Resources/SkeletonEnemy.prefab`：含 EnemyDeathHandler（已更新）
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
- `Skeleton_slash01.fbx` 第 20 帧：调用 `OnAttackHit()`（方法名不可改）
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
- ✅ 血量系统（HealthComponent，含 IsDead + OnDied 事件）
- ✅ 世界空间血条显示（头顶）
- ✅ 敌人攻击动画 + Animation Event 伤害触发（OnAttackHit）
- ✅ 攻击冷却机制（attackCooldown 默认 2 秒）
- ✅ 骷髅敌人生成器
- ✅ 追击时 stoppingDistance 停止移动
- ✅ 玩家鼠标左键选中敌对目标（PlayerTargeting）
- ✅ 玩家按 1 使用普通攻击（PlayerSkillController，默认 20 伤害，2m 范围，1s 冷却）
- ✅ 骷髅死亡动画播放（root|death，1.4s）
- ✅ 死亡后 AI/物理/Collider 禁用，延迟销毁实体（EnemyDeathHandler）

## 7. In Progress / Known Issues
- ⚠️ Player 组件重复：HealthComponent 和 WorldHealthBar 可能各有两个实例（未确认）
- ⚠️ EntityStats.cs 未充分使用
- ⚠️ 死亡后 PlayerTargeting.CurrentTarget 仍可能持有已销毁对象的引用（对象销毁后 Transform 变 null，PlayerSkillController 有 null 检查保护，但未主动清除引用）
- ⚠️ EnemyDeathHandler 使用固定 destroyDelay 计时，未使用 Animation Event，时机可能受播放速度影响

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
- `Assets/Scripts/Enemy/EnemyAI.cs`：FSM 核心，`OnAttackHit()` 有 Animation Event 绑定
- `Assets/Scripts/Enemy/EnemyDeathHandler.cs`：订阅 `HealthComponent.OnDied`，触发死亡流程
- `Assets/Scripts/Enemy/FactionSystem.cs`：阵营枚举和 `ShouldAttack()` 接口
- `Assets/Scripts/HealthComponent.cs`：`TakeDamage(float)`、`IsDead`、`OnDied` 事件
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
2. 死亡后主动清除 PlayerTargeting.CurrentTarget（在 EnemyDeathHandler 中广播事件，或 PlayerTargeting 在 Update 检查 CurrentTarget == null）

### 优先级 2：战斗体验
3. 玩家普通攻击动画（按 1 时播放玩家攻击动作）
4. 集成 EntityStats 系统，支持攻击力/血量等属性可配置

### 优先级 3：扩展
5. 多敌人场景压力测试：评估 EnemyAI 的 `FindObjectsOfType` 扫描在敌人数量增加时的性能

---

**最后更新**：2026-04-29
**本次有效变更**：
1. 新增玩家目标选择（PlayerTargeting.cs）+ 技能系统（PlayerSkillController.cs），玩家可左键选中骷髅并按 1 造成伤害。
2. 新增骷髅死亡流程（EnemyDeathHandler.cs）：血量归零 → 禁用 AI → 播放 root|death 动画 → 延迟销毁。
3. SkeletonAnimator.controller 新增 IsDead Trigger 和 Death 状态（Any State → Death 过渡）。
