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
- `PlayerController.cs`：玩家输入处理、移动控制（New Input System）。`applyRootMotion = false`。
- `RPGCameraController.cs`：第三人称相机跟随。右键拖拽时同步玩家朝向（`target.rotation`）。
- `PlayerTargeting.cs`：鼠标左键点击，Physics.Raycast 检测目标，验证 HealthComponent + FactionComponent + 敌对关系后设为 `CurrentTarget`（public Transform，只读）。
- `PlayerSkillController.cs`：按数字键 1 读取 `PlayerTargeting.CurrentTarget`，验证目标有效性后调用 `HealthComponent.TakeDamage(damage, transform)`（传入攻击来源）。
- `PlayerDeathHandler.cs`：挂载在 Player 上，监听 `HealthComponent.OnDied`，死亡时执行以下操作（通过 `_isDeadHandled` 防止重复）：
  - 禁用 `PlayerController`、`PlayerSkillController`、`PlayerTargeting`
  - 禁用 `RPGCameraController`（阻止右键改变玩家朝向）
  - 清零 Rigidbody 速度并设置 `isKinematic = true`（防止斜面滑动）
  - 调用 `animator.SetTrigger("IsDead")` 播放死亡动画
  - 输出 `[PlayerDeathHandler] Player died. Controls disabled.`

### Health & Combat
- `HealthComponent.cs`：通用血量组件。
  - `TakeDamage(float)`：向后兼容接口，内部调用带来源版本
  - `TakeDamage(float, Transform attacker)`：带攻击来源接口，attacker 可为 null
  - `IsDead`（只读属性）
  - 事件：`OnHealthChanged(float, float)`、`OnDied`（Action，无参数）、`OnDamaged(float, Transform)`
- `WorldHealthBar.cs`：世界空间血条 UI（头顶）。
- `PlayerHealthBar.cs`：玩家血条 UI。

### Level 系统
- `LevelObjectiveManager.cs`（`Assets/Scripts/Level/`）：最小关卡流程控制器。
  - `[SerializeField] HealthComponent playerHealth`：引用玩家 HealthComponent
  - `[SerializeField] List<HealthComponent> enemyHealthComponents`：已注册的敌人列表
  - `[SerializeField] int requiredKills = 3`：胜利所需击杀数
  - `[SerializeField] TextMeshProUGUI progressText / resultText / restartHintText`：UI 文本引用（已在场景中绑定）
  - 击杀数达到 requiredKills 时 Victory；玩家死亡时 Game Over
  - 胜利/失败后按 R（`Keyboard.current.rKey.wasPressedThisFrame`）重载当前场景
  - `RegisterEnemy(HealthComponent)`：公开接口，供动态生成的敌人注册，防重复注册
  - 内部用 `HashSet<HealthComponent> _countedEnemies` 防重复计数，`bool _isLevelEnded` 防重复结算
  - 使用 `MakeEnemyDiedHandler(enemy)` 闭包捕获，保证事件取消订阅正确匹配

### Spawner & Debug
- `SkeletonSpawner.cs`（`Assets/Scripts/Spawner/`）：敌人生成器。
  - `SpawnSkeleton()` 末尾自动调用 `FindFirstObjectByType<LevelObjectiveManager>()?.RegisterEnemy(hc)`，动态生成的敌人自动计入关卡目标
  - F1 调试菜单生成的骷髅通过此机制自动注册
- `SkeletonDebugUI.cs`：调试用 UI。
- `PhysicsLayerSetup.cs`：物理层设置。

### Stats（未充分使用）
- `EntityStats.cs`：已创建但未集成。

## 4. Important Unity Objects

### Scene Objects (SampleScene.unity)
- **Player**
  - Components: Transform, Animator, CapsuleCollider, Rigidbody, PlayerController, FactionComponent（faction=Player）, HealthComponent, WorldHealthBar, PlayerTargeting, PlayerSkillController, PlayerDeathHandler
  - HealthComponent と WorldHealthBar は各1インスタンスに整理済み
- **Skeleton_Enemy**
  - Components: Transform, Animator, Rigidbody, CapsuleCollider, FactionComponent（faction=Skeleton）, FOVDetector, EnemyAI, HealthComponent, WorldHealthBar, EnemyDeathHandler
- **Main Camera**
  - Components: RPGCameraController（target = Player Transform）
- **LevelObjectiveManager**（空 GameObject）
  - Components: LevelObjectiveManager
  - Inspector 绑定：`playerHealth` = Player.HealthComponent、`enemyHealthComponents` = 场景内预置敌人列表、`progressText` / `resultText` / `restartHintText` = LevelUI 下各 TMP 文本
- **LevelUI**（Canvas, Screen Space Overlay, sortingOrder=10）
  - CanvasScaler、GraphicRaycaster
  - 子对象：
    - `ProgressText`（TextMeshProUGUI）：左上，字号 36，常时显示击杀进度
    - `ResultText`（TextMeshProUGUI）：画面中央，黄色，字号 72，初始隐藏，Victory/Game Over 时显示
    - `RestartHintText`（TextMeshProUGUI）：中央偏下，字号 32，初始隐藏，结算后显示"按 R 重新开始"
  - 所有 TMP 文本字体：`SourceHanSansSC-Medium SDF`（Dynamic，支持中日英）
- **Ground**、**Directional Light**、**Global Volume**、**DebugManager**、**SkeletonSpawnerManager**

### Prefabs
- `Assets/Resources/SkeletonEnemy.prefab`：含 EnemyDeathHandler
- `Assets/Resources/Skeleton_110.prefab`：骷髅模型资产

### Animator Controllers
- `Assets/Scripts/SkeletonAnimator.controller`（敌人用）：
  - 参数：`Speed`（Float）、`IsAttacking`（Bool）、`IsDead`（Trigger）
  - 状态：Idle、Walk、Attack、Death
  - Attack → Idle：`hasExitTime=true, exitTime=0.9`（**不可改**）
  - Any State → Death：`IsDead` Trigger（canTransitionToSelf=false）
  - Death 状态：clip=`root|death`（1.4s），无出口过渡
- `Assets/Scripts/PlayerAnimator.controller`（玩家用）：
  - 参数：`Speed`（Float）、`Horizontal`（Float）、`IsGrounded`（Bool）、`IsSprinting`（Bool）、`VerticalVelocity`（Float）、`IsJumping`（Bool）、`IsDead`（Trigger）
  - 状态：Idle、RunForward、RunBackward、StrafeLeft、StrafeRight、Jump、JumpDown、FallingLoop、Sprint、Death
  - Any State → Death：`IsDead` Trigger（canTransitionToSelf=false、hasExitTime=false）
  - Death 状态：clip=`HumanM@CombatDamage01`（1.0s、非ループ）、**无出口过渡**

### Animation Events
- `Skeleton_slash01.fbx` 第 20 帧：调用 `OnAttackHit()`（**方法名不可改**）
- 玩家死亡动画：`Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx`

### Font Assets
- `Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Medium.otf`：源字体文件
- `Assets/Fonts/09_SourceHanSansSC/TMP/SourceHanSansSC-Medium SDF.asset`：Dynamic TMP Font Asset（中日英，samplingPointSize=90，atlas=1024x1024，SDFAA）

## 5. Input / Control
- 使用：New Input System 1.19.0
- 玩家移动：WASD（PlayerController）
- 目标选择：鼠标左键（Mouse.current.leftButton.wasPressedThisFrame）
- 技能释放：键盘 1（Keyboard.current.digit1Key.wasPressedThisFrame）
- 摄像机/玩家朝向：鼠标右键拖拽（RPGCameraController.LateUpdate）。死亡後は RPGCameraController ごと無効化。
- 关卡重开：R 键（`Keyboard.current.rKey.wasPressedThisFrame`），仅在 Victory/Game Over 后生效

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
- ✅ 骷髅敌人生成器（SkeletonSpawner）
- ✅ 追击时 stoppingDistance 停止移动
- ✅ 玩家鼠标左键选中敌对目标（PlayerTargeting）
- ✅ 玩家按 1 使用普通攻击（PlayerSkillController，默认 20 伤害，2m 范围，1s 冷却）
- ✅ 骷髅死亡动画播放（root|death，1.4s）
- ✅ 死亡后 AI/物理/Collider 禁用，延迟销毁实体（EnemyDeathHandler）
- ✅ 敌人仇恨系统：多目标列表 + 仇恨值排序 + 视野/受击两路加仇恨 + 距离脱战
- ✅ 玩家死亡处理：禁用移动/攻击/目标选择/摄像机控制，物理静止，播放死亡动画（PlayerDeathHandler）

## 7. In Progress / Known Issues
- ⚠️ EntityStats.cs 未充分使用
- ⚠️ 死亡后 PlayerTargeting.CurrentTarget 未主动清除（PlayerSkillController 有 null 检查保护，但引用仍存在）
- ⚠️ EnemyDeathHandler 使用固定 destroyDelay 计时，未使用 Animation Event，时机可能受播放速度影响
- ⚠️ `ScanForTarget()` 每 0.2s 调用 `FindObjectsOfType<FactionComponent>()`，敌人数量多时有性能隐患
- ⚠️ 玩家死亡后 RPGCameraController 被整体禁用，相机静止在死亡位置（无死亡镜头演出）

## 8. Development Rules

### 修改前必读
- ❌ 不要随意重命名 public / SerializedField 字段（Inspector 可能已绑定）
- ❌ 不要重构无关代码
- ❌ 不要读取完整 Console、完整 Assets、完整 Scene Hierarchy
- ✅ 修改前先定位相关文件，只读取必要内容
- ✅ 优先小步修改，每次改动后确认编译通过

### Animator 修改注意
- **骷髅（SkeletonAnimator.controller）**：
  - `Attack → Idle` 的 `hasExitTime=true, exitTime=0.9` 不可改
  - `IsDead` Trigger 由 EnemyDeathHandler 触发，不可回到其他状态
  - `OnAttackHit()` 方法名不可改（Animation Event 绑定）
- **玩家（PlayerAnimator.controller）**：
  - Death 状态无出口过渡，死亡后不会自动回到其他状态，不可添加出口
  - `IsDead` Trigger 由 PlayerDeathHandler 触发

### 代码修改原则
- Rigidbody 使用 `rb.linearVelocity`（Unity 6）
- 输入系统使用 `Mouse.current` / `Keyboard.current`（New Input System），不得使用 `UnityEngine.Input`

## 9. Files That Should Be Treated Carefully

### 核心脚本
- `Assets/Scripts/Enemy/EnemyAI.cs`：FSM + 仇恨系统核心，`OnAttackHit()` 有 Animation Event 绑定
- `Assets/Scripts/Enemy/EnemyDeathHandler.cs`：订阅 `HealthComponent.OnDied`，触发死亡流程
- `Assets/Scripts/Enemy/FactionSystem.cs`：阵营枚举和 `ShouldAttack()` 接口
- `Assets/Scripts/HealthComponent.cs`：`TakeDamage(float/float+Transform)`、`IsDead`、`OnDied`、`OnDamaged` 事件
- `Assets/Scripts/Player/PlayerTargeting.cs`：`CurrentTarget`（public Transform, get-only）
- `Assets/Scripts/Player/PlayerSkillController.cs`：技能判定 + 伤害调用
- `Assets/Scripts/Player/PlayerDeathHandler.cs`：玩家死亡处理，`_isDeadHandled` 防重复，禁用5个组件 + Rigidbody isKinematic + IsDead Trigger

### 核心资产
- `Assets/Scripts/SkeletonAnimator.controller`：参数 Speed/IsAttacking/IsDead，状态 Idle/Walk/Attack/Death
- `Assets/Scripts/PlayerAnimator.controller`：参数含 IsDead Trigger，Death 状态无出口过渡
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`：第 20 帧 Animation Event
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_death.fbx`：clip `root|death`，1.4s
- `Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx`：玩家死亡动画，clip `HumanM@CombatDamage01`（1.0s）
- `Assets/Resources/SkeletonEnemy.prefab`：含 EnemyDeathHandler

### 不应修改的文件
- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产（模型/贴图本体）
- `Packages/manifest.json`：包配置

## 10. Next Suggested Tasks

### ⭐ 最推荐的下一个小任务
**玩家普通攻击动画**：按 1 攻击时播放玩家挥击动作，需要在 PlayerAnimator.controller 中添加 Attack 状态和过渡，在 PlayerSkillController 中调用 SetTrigger。

### 优先级 1：战斗体验
1. ⭐ 玩家普通攻击动画（PlayerAnimator.controller + PlayerSkillController）
2. Game Over UI：玩家死亡后显示简单的"Game Over"提示界面
3. 玩家死亡后相机演出（死亡时 RPGCameraController 被禁用，可改为死亡镜头而非静止）

### 优先级 2：系统完善
4. 死亡后主动清除 `PlayerTargeting.CurrentTarget`（PlayerDeathHandler 中 `_targeting.CurrentTarget` 无法直接清除，需为 PlayerTargeting 添加 `ClearTarget()` 方法）
5. 集成 EntityStats 系统，支持攻击力/血量等属性可配置
6. 将 `FindObjectsOfType<FactionComponent>()` 替换为注册缓存，减少 EnemyAI 扫描开销

---

**最后更新**：2026-05-01
**本次有效变更**：
1. 新增 `PlayerDeathHandler.cs`（`Assets/Scripts/Player/`）：玩家死亡时禁用 PlayerController / PlayerSkillController / PlayerTargeting / RPGCameraController，Rigidbody 速度清零并设 isKinematic，触发死亡动画 IsDead Trigger。
2. `PlayerAnimator.controller` 新增 `IsDead` Trigger 参数、`Death` 状态（clip: `HumanM@CombatDamage01`，1.0s）、Any State → Death 无出口过渡。
3. Player 上多余的 HealthComponent / WorldHealthBar 实例已手动删除，当前各保留1个。
