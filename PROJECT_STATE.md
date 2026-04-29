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
- 目前已确认的设计方向：
  - 基于 FSM 的敌人 AI（Idle/Chase/Attack）
  - 阵营系统（敌我识别）
  - 攻击冷却机制
  - 世界空间血条显示

## 3. Current Architecture

### Enemy AI 系统
- `EnemyAI.cs`：敌人有限状态机（FSM），控制 Idle/Chase/Attack 状态切换、移动、攻击触发
- `FOVDetector.cs`：视野检测（FOV），基于角度和距离判断目标可见性
- `FactionSystem.cs` / `FactionComponent.cs`：阵营系统，定义敌对关系

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

### Stats (未充分使用)
- `EntityStats.cs`：实体属性系统（已创建但可能未集成）

## 4. Important Unity Objects

### Scene Objects (SampleScene.unity)
- **Player**
  - Components: Transform, Animator, CapsuleCollider, Rigidbody, PlayerController, FactionComponent, HealthComponent, WorldHealthBar
  - 说明：玩家角色，使用 New Input System，有血量和阵营
  
- **Skeleton_Enemy**
  - Components: Transform, Animator, Rigidbody, CapsuleCollider, FactionComponent, FOVDetector, EnemyAI, HealthComponent, WorldHealthBar
  - 说明：骷髅敌人，FSM AI + FOV 检测 + 攻击系统
  
- **Ground**：地面
- **Main Camera**：主相机（可能绑定 RPGCameraController）
- **Directional Light**：主光源
- **Global Volume**：URP 后处理体积
- **DebugManager**：调试管理器
- **SkeletonSpawnerManager**：骷髅生成管理器

### Prefabs
- `Assets/Resources/SkeletonEnemy.prefab`：骷髅敌人 Prefab
- `Assets/Resources/Skeleton_110.prefab`：骷髅模型资产

### Animator Controllers
- `Assets/Scripts/SkeletonAnimator.controller`：骷髅动画控制器
  - 状态：Idle, Walk, Attack
  - 过渡：Idle ↔ Walk (Speed 参数)，Idle → Attack (IsAttacking 参数)
  - Attack → Idle：hasExitTime=true, exitTime=0.9（动画播放到 90% 自动返回）

### Animation Events
- `Skeleton_slash01.fbx` 攻击动画：第 20 帧触发 `OnAttackHit()` 方法（造成伤害）

## 5. Input / Control
- 使用：**New Input System** (com.unity.inputsystem 1.19.0)
- 已确认的操作：
  - 玩家移动：WASD / 左摇杆
  - 相机控制：未确认
- 相关脚本：`PlayerController.cs`

## 6. Completed Features
- ✅ 玩家第三人称移动控制
- ✅ 敌人 FSM AI（Idle、Chase、Attack 状态）
- ✅ FOV 视野检测系统
- ✅ 阵营系统（FactionComponent，敌我识别）
- ✅ 血量系统（HealthComponent）
- ✅ 世界空间血条显示（头顶）
- ✅ 敌人攻击动画 + Animation Event 伤害触发
- ✅ 攻击冷却机制（attackCooldown 可在 Inspector 调整）
- ✅ 骷髅敌人生成器

## 7. In Progress / Known Issues

### 当前问题（优先级高）
- 🐛 **骷髅攻击会连续触发两次**：每次攻击（包括第一次）都会额外触发一次攻击动画和判定，导致无冷却的连续两次攻击
  - 原因：Animator 过渡配置问题（Idle → Attack 的 hasExitTime=false，IsAttacking=true 时立刻过渡；Attack → Idle 的 exitTime=0.9 自动返回，此时如果 IsAttacking 仍为 true 会立刻再次进入 Attack）
  - 临时方案：已添加 `wasAnimPlaying` flag 追踪动画状态，但问题尚未完全解决
  - 相关脚本：`EnemyAI.cs` FixedUpdate 的 Attack case

### 其他已知问题
- ⚠️ Player 组件重复：HealthComponent 和 WorldHealthBar 各有两个实例（可能是调试时重复添加）
- ⚠️ 攻击冷却第一次进入 Attack 状态时的行为需要验证
- ⚠️ EntityStats.cs 未充分使用

## 8. Development Rules

### 修改前必读
- ❌ **不要随意重命名 public / SerializedField 字段**，因为可能已在 Inspector 中绑定数据
- ❌ **不要重构无关代码**，当前聚焦于修复攻击系统 bug
- ✅ 修改前先定位相关文件（EnemyAI.cs、Animator Controller）
- ✅ 优先小步修改，每次改动后测试
- ✅ 修改后检查 Unity Console 最近的 error 和 warning
- ❌ **不要读取完整 Console、完整 Assets、完整 Scene Hierarchy**，除非有明确必要

### Animator 修改注意
- Attack → Idle 过渡的 `hasExitTime=true, exitTime=0.9` 是核心配置，不要随意改动
- IsAttacking 参数的设置时机很关键，需要配合 FixedUpdate 中的 `wasAnimPlaying` flag

### 代码修改原则
- EnemyAI.cs 的 FixedUpdate 中已有详细 Debug.Log，修改前先在 Play Mode 观察 Console 输出
- OnAttackHit() 方法由 Animation Event 调用，修改时注意第 20 帧的时机
- TransitionTo(Attack) 设置 `attackCooldownTimer = 0` 确保第一次立即攻击

## 9. Files That Should Be Treated Carefully

### 核心脚本（修改需谨慎）
- `Assets/Scripts/Enemy/EnemyAI.cs`：敌人 AI 核心，当前正在调试攻击系统
- `Assets/Scripts/Enemy/FOVDetector.cs`：视野检测逻辑
- `Assets/Scripts/HealthComponent.cs`：血量系统基础
- `Assets/Scripts/PlayerController.cs`：玩家控制

### 核心资产
- `Assets/Scripts/SkeletonAnimator.controller`：骷髅动画状态机
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`：攻击动画（第 20 帧有 Animation Event）
- `Assets/Resources/SkeletonEnemy.prefab`：骷髅敌人 Prefab
- `Assets/Scenes/SampleScene.unity`：主场景

### 不应修改的文件
- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产（除非需要修改动画）
- Packages/manifest.json：包配置（除非需要添加新包）

## 10. Next Suggested Tasks

### 优先级 1：修复攻击系统 bug
1. **修复骷髅连续两次攻击的问题**
   - 方法 A：检查 Console 输出的 Debug.Log，定位 IsAttacking 何时被重复触发
   - 方法 B：在 FixedUpdate 中添加更严格的状态检查，确保 IsAttacking 在动画结束时立刻设为 false
   - 方法 C：考虑在 Animator Controller 中修改 Idle → Attack 的过渡条件

### 优先级 2：清理和优化
2. **清理 Player 组件重复**：移除重复的 HealthComponent 和 WorldHealthBar 实例
3. **移除 Debug.Log**：修复 bug 后移除 EnemyAI.cs、HealthComponent.cs 中的调试日志
4. **验证攻击冷却**：在不同 attackCooldown 值（0.3s、1.5s、3.0s）下测试攻击节奏

### 优先级 3：功能扩展
5. **完善玩家战斗**：添加玩家攻击能力（当前只能被攻击）
6. **集成 EntityStats 系统**：将 HealthComponent 与 EntityStats 关联，支持属性配置

---

**最后更新**：2025-04-29  
**当前状态**：正在修复骷髅攻击连续触发两次的 bug，已添加 Debug.Log 追踪
