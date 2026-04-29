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
  - 攻击触发逻辑：`attackCooldownTimer <= 0` 时设 `IsAttacking=true` 一帧，检测到动画已进入 Attack 状态后立刻清除 bool
  - 目标扫描：排除自身，调用 `FactionComponent.ShouldAttack()` 筛选敌对目标
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
  - ⚠️ HealthComponent と WorldHealthBar が各2インスタンス存在する可能性あり（未確認）
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
  - ⚠️ Idle → Attack 的 Transition Duration / Has Exit Time 设置未完整确认

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
- ✅ 攻击冷却机制（attackCooldown，Inspector 可调）
- ✅ 骷髅敌人生成器
- ✅ 追击时 stoppingDistance 停止移动

## 7. In Progress / Known Issues

### 未解决问题（优先级高）
- 🐛 **骷髅攻击仍会连续触发两次**：每次攻击（包括第一次）会额外触发一次动画和伤害判定
  - 脚本侧已多次尝试修复（wasAnimPlaying flag、attackCooldownTimer=999f、inAttackAnim 清除 bool 等），均未彻底解决
  - 当前 EnemyAI.cs 逻辑：timer 归零 → SetBool(IsAttacking, true)；检测到动画播放中 → SetBool(IsAttacking, false)
  - **怀疑根本原因在 Animator Controller 侧**：Idle → Attack 过渡的 Has Exit Time / Transition Duration 配置可能导致过渡被触发两次，或 Attack → Idle 过渡后因某种原因再次满足条件
  - 建议下次优先检查 Animator Controller 的过渡条件，而不是继续修改脚本
  - 相关文件：`EnemyAI.cs`、`Assets/Scripts/SkeletonAnimator.controller`

### 其他已知问题
- ⚠️ Player 组件重复：HealthComponent 和 WorldHealthBar 可能各有两个实例
- ⚠️ EntityStats.cs 未充分使用

## 8. Development Rules

### 修改前必读
- ❌ **不要随意重命名 public / SerializedField 字段**（Inspector 可能已绑定）
- ❌ **不要重构无关代码**
- ❌ **不要读取完整 Console、完整 Assets、完整 Scene Hierarchy**
- ✅ 修改前先定位相关文件，只读取必要内容
- ✅ 优先小步修改，每次改动后确认编译通过

### Animator 修改注意
- Attack → Idle 的 `hasExitTime=true, exitTime=0.9` 是核心配置
- `IsAttacking` bool 参数：EnemyAI 使用一帧脉冲方式触发（set true → 检测到动画播放后 set false）
- Idle → Attack 过渡的完整参数尚未确认，修改前需在 Inspector 中核查

### 代码修改原则
- Rigidbody 使用 `rb.linearVelocity`（Unity 6）
- `OnAttackHit()` 方法名不可改（Animation Event 绑定）
- `FaceTarget()` 仅在 FixedUpdate 中调用，使用 `Time.fixedDeltaTime`

## 9. Files That Should Be Treated Carefully

### 核心脚本
- `Assets/Scripts/Enemy/EnemyAI.cs`：攻击 bug 尚未完全解决，逻辑已多次改动
- `Assets/Scripts/Enemy/FOVDetector.cs`：视野检测逻辑
- `Assets/Scripts/HealthComponent.cs`：血量系统基础
- `Assets/Scripts/PlayerController.cs`：玩家控制

### 核心资产
- `Assets/Scripts/SkeletonAnimator.controller`：**攻击双触发的潜在根源，下次应优先检查**
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`：攻击动画（第 20 帧有 Animation Event）
- `Assets/Resources/SkeletonEnemy.prefab`：骷髅敌人 Prefab
- `Assets/Scenes/SampleScene.unity`：主场景

### 不应修改的文件
- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产（除非需要修改动画）
- `Packages/manifest.json`：包配置

## 10. Next Suggested Tasks

### 优先级 1：修复攻击双触发（从 Animator Controller 入手）
1. 在 Unity Editor 中打开 `SkeletonAnimator.controller`，检查 Idle → Attack 过渡的以下设置：
   - Has Exit Time：应为 false（否则 Idle 动画会先播完再切换）
   - Transition Duration：应为 0（瞬时切换）
   - Conditions：确认只有 `IsAttacking = true` 一个条件
2. 检查是否有多个从 Idle 或 Walk 到 Attack 的过渡（重复过渡会导致双触发）
3. 确认 Attack → Idle 的过渡没有额外 Condition（只靠 Exit Time 返回）

### 优先级 2：清理
4. 修复 Player 组件重复（移除多余的 HealthComponent / WorldHealthBar 实例）
5. 攻击 bug 解决后移除 EnemyAI.cs 中残留的无用注释

### 优先级 3：功能扩展
5. 添加玩家攻击能力（当前玩家只能被攻击）
6. 集成 EntityStats 系统，支持属性配置

---

**最后更新**：2025-04-29
**当前状态**：攻击双触发 bug 未解决，脚本侧多次修改无效，怀疑根本原因在 Animator Controller，已暂时搁置。EnemyAI.cs 其他逻辑（目标筛选、冷却、停止距离）已整理完毕。
