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

## 3. Current Architecture

### Enemy AI 系统
- `EnemyAI.cs`：敌人有限状态机（FSM），控制 Idle/Chase/Attack 状态切换、移动、攻击触发
  - 攻击触发逻辑：`attackCooldownTimer <= 0` 时设 `IsAttacking=true`，立即重置冷却；检测到动画已进入 Attack 状态后立刻清除 `IsAttacking`
  - 攻击双触发问题：脚本侧已通过攻击后立即重置冷却、进入 Attack 动画后清除 `IsAttacking` 解决；当前 `attackCooldown` 默认值为 `2f`
  - 攻击冷却计时：仅在 `attackCooldownTimer > 0` 时递减
  - 目标扫描：固定间隔执行，避免每帧全场搜索；扫描时排除自身，并调用 `FactionComponent.ShouldAttack()` 筛选敌对目标
  - `stoppingDistance` 已启用：追击时距离小于该值则原地停止并朝向目标
  - `FaceTarget()` 使用 `Time.fixedDeltaTime`（仅在 FixedUpdate 中调用）
  - `OnAttackHit()` 命中时实时 GetComponent 获取 HealthComponent，不依赖缓存
- `FOVDetector.cs`：视野检测（FOV），基于角度和距离判断目标可见性
- `FactionSystem.cs` / `FactionComponent`：阵营系统，`ShouldAttack(Faction)` 判断敌对关系

### Player 系统
- `PlayerController.cs`：玩家输入处理、移动控制
- `RPGCameraController.cs`：第三人称相机跟随

### Health & Combat
- `HealthComponent.cs`：通用血量组件，处理伤害、死亡事件
- `WorldHealthBar.cs`：世界空间血条 UI（头顶显示）
- `PlayerHealthBar.cs`：玩家血条 UI

### Spawner & Debug
- `SkeletonSpawner.cs`：敌人生成器
- `SkeletonDebugUI.cs`：调试用 UI
- `PhysicsLayerSetup.cs`：物理层设置

### Stats（未充分使用）
- `EntityStats.cs`：实体属性系统（已创建但未集成）

## 4. Important Unity Objects

### Scene Objects (SampleScene.unity)
- **Player**
  - Components: Transform, Animator, CapsuleCollider, Rigidbody, PlayerController, FactionComponent, HealthComponent, WorldHealthBar
  - 说明：玩家角色，使用 New Input System，有血量和阵营
  - ⚠️ HealthComponent 和 WorldHealthBar 可能各有两个实例（未确认）
- **Skeleton_Enemy**
  - Components: Transform, Animator, Rigidbody, CapsuleCollider, FactionComponent, FOVDetector, EnemyAI, HealthComponent, WorldHealthBar
  - 说明：骷髅敌人，FSM AI + FOV 检测 + 攻击系统
- **Ground**、**Main Camera**、**Directional Light**、**Global Volume**、**DebugManager**、**SkeletonSpawnerManager**

### Prefabs
- `Assets/Resources/SkeletonEnemy.prefab`：骷髅敌人 Prefab
- `Assets/Resources/Skeleton_110.prefab`：骷髅模型资产

### Animator Controllers
- `Assets/Scripts/SkeletonAnimator.controller`：骷髅动画控制器
  - 状态：Idle, Walk, Attack
  - Idle → Attack：`IsAttacking`（bool）为 true 时触发
  - Attack → Idle：`hasExitTime=true, exitTime=0.9`（动画播放到 90% 自动返回）
  - Idle → Attack 的 Transition Duration / Has Exit Time 设置未完整确认

### Animation Events
- `Skeleton_slash01.fbx` 攻击动画：第 20 帧触发 `OnAttackHit()` 方法

## 5. Input / Control
- 相关脚本：`PlayerController.cs`
- 使用：New Input System (1.19.0)
- 玩家移动：WASD / 左摇杆

## 6. Completed Features
- ✅ 玩家第三人称移动控制
- ✅ 敌人 FSM AI（Idle、Chase、Attack 状态）
- ✅ FOV 视野检测系统
- ✅ 阵营系统（FactionComponent，敌我识别，ShouldAttack() 已在 EnemyAI 中调用）
- ✅ 血量系统（HealthComponent）
- ✅ 世界空间血条显示（头顶）
- ✅ 敌人攻击动画 + Animation Event 伤害触发（OnAttackHit）
- ✅ 攻击冷却机制（attackCooldown，Inspector 可调，当前默认 2 秒）
- ✅ EnemyAI 攻击双触发问题已修复：攻击触发后立即重置冷却，进入 Attack 动画后清除 IsAttacking
- ✅ EnemyAI 目标扫描已优化：改为固定间隔扫描，减少不必要的每帧处理
- ✅ 骷髅敌人生成器
- ✅ 追击时 stoppingDistance 停止移动

## 7. In Progress / Known Issues

### 已知问题
- ⚠️ Player 组件重复：HealthComponent 和 WorldHealthBar 可能各有两个实例（未确认）
- ⚠️ EntityStats.cs 未充分使用
- ⚠️ `SkeletonAnimator.controller` 的 Idle → Attack 过渡细节未完整确认；当前脚本逻辑已可用，暂不需要优先修改 Animator

## 8. Development Rules

### 修改前必读
- ❌ **不要随意重命名 public / SerializedField 字段**（Inspector 可能已绑定）
- ❌ **不要重构无关代码**
- ❌ **不要读取完整 Console、完整 Assets、完整 Scene Hierarchy**
- ✅ 修改前先定位相关文件，只读取必要内容
- ✅ 优先小步修改，每次改动后确认编译通过

### Animator 修改注意
- Attack → Idle 的 `hasExitTime=true, exitTime=0.9` 是核心配置
- `IsAttacking` bool 参数：EnemyAI 使用脉冲式触发（set true → 检测到 Attack 动画播放后 set false）
- `OnAttackHit()` 方法名不可改（Animation Event 绑定）

### 代码修改原则
- Rigidbody 使用 `rb.linearVelocity`（Unity 6）
- `OnAttackHit()` 方法名不可改（Animation Event 绑定）
- `FaceTarget()` 仅在 FixedUpdate 中调用，使用 `Time.fixedDeltaTime`
- EnemyAI 的目标扫描已改为固定间隔执行，不要轻易改回每帧 `FindObjectsOfType`

## 9. Files That Should Be Treated Carefully

### 核心脚本
- `Assets/Scripts/Enemy/EnemyAI.cs`：敌人 AI 核心，包含目标扫描、FSM、攻击冷却、攻击动画触发
- `Assets/Scripts/Enemy/FOVDetector.cs`：视野检测逻辑
- `Assets/Scripts/HealthComponent.cs`：血量系统基础
- `Assets/Scripts/PlayerController.cs`：玩家控制

### 核心资产
- `Assets/Scripts/SkeletonAnimator.controller`：攻击动画状态机，依赖 `IsAttacking` 参数和 Attack 状态名
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`：攻击动画（第 20 帧有 Animation Event）
- `Assets/Resources/SkeletonEnemy.prefab`：骷髅敌人 Prefab
- `Assets/Scenes/SampleScene.unity`：主场景

### 不应修改的文件
- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产（除非需要修改动画）
- `Packages/manifest.json`：包配置

## 10. Next Suggested Tasks

### 优先级 1：验证和清理
1. 在 Play Mode 中确认 `attackCooldown=2` 时骷髅攻击节奏稳定，无连续双触发
2. 修复 Player 组件重复问题（确认并移除多余的 HealthComponent / WorldHealthBar 实例）
3. 清理 EnemyAI.cs 和相关脚本中的过期调试输出或误导性注释

### 优先级 2：战斗功能扩展
4. 添加玩家攻击能力（当前玩家只能被攻击）
5. 集成 EntityStats 系统，支持属性配置和可调数值

### 优先级 3：AI 扩展
6. 后续敌人数量增加时，评估是否需要进一步优化目标管理，避免大量敌人同时进行全局搜索

---

**最后更新**：2026-04-29  
**当前状态**：EnemyAI 攻击双触发已修复；目标扫描已改为固定间隔执行，攻击冷却只在大于 0 时递减。当前重点可转向 Play Mode 验证、组件清理和玩家攻击功能。
