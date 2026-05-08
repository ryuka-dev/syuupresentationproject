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
- `PlayerTargeting.cs`：鼠标左键点击，Physics.Raycast 检测目标，验证 HealthComponent + FactionComponent + 敌对关系后设为 `CurrentTarget`（public Transform，只读）。提供 `ClearTarget()` 方法，用于在玩家死亡等场景主动清空当前目标。
- `PlayerSkillController.cs`：按数字键 1 读取 `PlayerTargeting.CurrentTarget`，验证目标有效性后调用 `HealthComponent.TakeDamage(damage, transform)`。攻击判定成功后调用 `animator.SetTrigger("Attack")` 触发攻击动画。持有 `_animator`、`_selfHealth` 引用（Awake 中 GetComponent 获取）。
- `PlayerDeathHandler.cs`：挂载在 Player 上，监听 `HealthComponent.OnDied`，死亡时执行以下操作（通过 `_isDeadHandled` 防止重复）：
  - 调用 `PlayerTargeting.ClearTarget()` 清除当前锁定目标
  - 禁用 `PlayerController`、`PlayerSkillController`、`PlayerTargeting`
  - 禁用 `RPGCameraController`（阻止右键改变玩家朝向）
  - 清零 Rigidbody 速度并设置 `isKinematic = true`（防止斜面滑动）
  - 调用 `animator.SetTrigger("IsDead")` 播放死亡动画
  - 输出 `[PlayerDeathHandler] Player died. Controls disabled.`
  
### Respawn / SavePoint 基础系统
- `PlayerRespawnPointTracker.cs`：挂载在 Player 上，记录当前最近复活点的位置和朝向。
  - `CurrentRespawnPosition`：当前最近复活点位置，只读属性
  - `CurrentRespawnRotation`：当前最近复活点朝向，只读属性
  - `Awake()`：默认将玩家初始位置和朝向作为初始复活点
  - `SetRespawnPoint(Vector3, Quaternion)`：更新最近复活点位置和朝向，并输出 Debug.Log
- `SavePoint.cs`：挂载在复活点对象上，通过 Trigger 检测玩家进入。
  - `OnTriggerEnter(Collider other)`：从进入对象上查找 `PlayerRespawnPointTracker`
  - 找到后调用 `SetRespawnPoint(transform.position, transform.rotation)`
  - 使用 `_hasActivated` 防止同一个 SavePoint 反复触发日志

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
- `SkeletonDebugUI.cs`：调试用 UI（F1 开关）。
  - "恢复玩家满血" 按钮：查找 FactionComponent(Player) 并调用 HealthComponent.RestoreFullHealth()。
  - "复活玩家测试" 按钮：依次调用 RestoreFullHealth() → ResetForRespawn() → ClearLevelResultForRespawn()，实现 Debug 级别完整复活（不传送到复活点）。
- `PhysicsLayerSetup.cs`：物理层设置。

### Stats（未充分使用）
- `EntityStats.cs`：已创建但未集成。

## 4. Important Unity Objects

### Scene Objects (SampleScene.unity)
- **Player**
  - Components: Transform, Animator, CapsuleCollider, Rigidbody, PlayerController, FactionComponent（faction=Player）, HealthComponent, WorldHealthBar, PlayerTargeting, PlayerSkillController, PlayerDeathHandler, PlayerRespawnPointTracker
  - 骨骼层级：`Armature/Root_M/.../Wrist_R/WeaponHolder_R/TempStaff`
    - `WeaponHolder_R`：空 GameObject，挂在 Wrist_R（右手骨骼）下，作为武器挂点
    - `TempStaff`：Cylinder（无 Collider、无 Rigidbody），已调整到右手握持位置，跟随 Wrist_R / WeaponHolder_R 运动。
- **Skeleton_Enemy**
  - Components: Transform, Animator, Rigidbody, CapsuleCollider, FactionComponent（faction=Skeleton）, FOVDetector, EnemyAI, HealthComponent, WorldHealthBar, EnemyDeathHandler
- **Main Camera**
  - Components: RPGCameraController（target = Player Transform）
- **LevelObjectiveManager**（空 GameObject）
  - Components: LevelObjectiveManager
  - Inspector 绑定：`playerHealth` = Player.HealthComponent、`enemyHealthComponents` = 场景内预置敌人列表、`progressText` / `resultText` / `restartHintText` = LevelUI 下各 TMP 文本
  - ⚠️ LevelUI 的 TMP 文本字体当前需要手动重新绑定为 `SourceHanSansSC-Medium_TMP`（见 Font Assets）
- **LevelUI**（Canvas, Screen Space Overlay, sortingOrder=10, ScaleWithScreenSize 1920x1080）
  - CanvasScaler（matchWidthOrHeight=0.5）、GraphicRaycaster
  - 子对象：
    - `ProgressText`（TextMeshProUGUI）：左上，字号 36，常时显示击杀进度
    - `ResultText`（TextMeshProUGUI）：画面中央，初始隐藏，Victory/Game Over 时显示
    - `RestartHintText`（TextMeshProUGUI）：中央偏下，初始隐藏，结算后显示「按 R 重新开始」
- **Ground**、**Directional Light**、**Global Volume**、**DebugManager**、**SkeletonSpawnerManager**
- **SavePoint**
  - Components: Transform, BoxCollider（Is Trigger=true）, SavePoint
  - 用途：玩家进入 Trigger 后更新 `PlayerRespawnPointTracker` 中保存的最近复活点位置和朝向
  - 当前仅记录复活点，不执行真正复活

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
  - **参数：** `Speed`（Float）、`Horizontal`（Float）、`IsGrounded`（Bool）、`IsSprinting`（Bool）、`VerticalVelocity`（Float）、`IsJumping`（Bool）、`IsDead`（Trigger）、`Attack`（Trigger）
  - **Base Layer（移动/全身动画）：**
    - 状态：Idle、RunForward、RunBackward、StrafeLeft、StrafeRight、Jump、JumpDown、FallingLoop、Sprint、Death
    - Any State → Death：`IsDead` Trigger（hasExitTime=false）、Death 状态无出口过渡
  - **UpperBody Layer（上半身攻击覆盖）：**
    - Blending: Override、Weight: 1、Avatar Mask: `PlayerUpperBody`
    - 状态：`UpperBodyIdle`（default、motion=`UpperBodyIdle.anim`空clip）、`Attack1H`（motion=`HumanM@Attack1H01_R`）
    - Any State → Attack1H：`Attack` Trigger、hasExitTime=false、duration=0.05
    - Attack1H → UpperBodyIdle：hasExitTime=true、exitTime=0.9、duration=0.1
    - Any State → UpperBodyIdle：`IsDead` Trigger（死亡时清除攻击覆盖，确保全身死亡动画正常）

### Animation Events
- `Skeleton_slash01.fbx` 第 20 帧：调用 `OnAttackHit()`（**方法名不可改**）
- 玩家死亡动画：`Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx`
- 玩家攻击动画：`Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_R.fbx`（无 Animation Event，伤害由 PlayerSkillController 立即判定）

### Font Assets
- `Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Medium.otf`：源字体文件（不可修改）
- `Assets/Fonts/09_SourceHanSansSC/TMP/SourceHanSansSC-Medium_TMP.asset`：Dynamic TMP Font Asset（samplingPointSize=90、atlas=2048x2048、SDFAA、padding=9、MultiAtlas=true）
  - ⚠️ 旧的 `SourceHanSansSC-Medium SDF.asset` 已删除。新 asset 需手动绑定到场景中 3 个 TMP 文本组件。

### Animation Assets（新增）
- `Assets/Scripts/Animation/PlayerUpperBody.mask`：上半身 Avatar Mask（Head/Body/Arms/Fingers 有效，Legs/Root 无效）
- `Assets/Scripts/Animation/UpperBodyIdle.anim`：空动画 clip，用于 UpperBody Layer 默认状态

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
- ✅ 玩家普通攻击动画：按 1 攻击成功时触发 `Attack` Trigger，UpperBody Layer 播放 `HumanM@Attack1H01_R`，结束后自动回到 UpperBodyIdle
- ✅ 上半身/下半身动画分离：移动时攻击，下半身继续跑步，上半身播放攻击动画（PlayerUpperBody Avatar Mask）
- ✅ 玩家右手临时长棍视觉模型（TempStaff，无 Collider/Rigidbody，跟随 Wrist_R 骨骼）
- ✅ 玩家死亡时主动清除当前锁定目标：`PlayerTargeting.ClearTarget()` 会将 `CurrentTarget` 设为 null，`PlayerDeathHandler` 在死亡流程中先清除目标再禁用 `PlayerTargeting`。
- ✅ 复活点记录基础功能：玩家进入 SavePoint Trigger 后，会记录最近复活点的位置和朝向。
- ✅ 复活用恢复满血接口：HealthComponent.RestoreFullHealth() 可将 currentHealth 恢复为 maxHealth 并触发 OnHealthChanged。
- ✅ 玩家死亡状态恢复接口：PlayerDeathHandler.ResetForRespawn() 可恢复控制/物理/Animator 状态，使玩家重新可操作。
- ✅ Debug 复活后结算 UI 清理：LevelObjectiveManager.ClearLevelResultForRespawn() 可隐藏 Game Over / Victory UI 并重置 _isLevelEnded。
- ✅ Debug UI 完整复活测试："复活玩家测试" 按钮依次调用上述三个接口，可在不重载场景的情况下完成 Debug 级别复活。

## 7. In Progress / Known Issues
- ⚠️ **LevelUI TMP 字体未绑定**：旧字体 asset 已删除，新 `SourceHanSansSC-Medium_TMP.asset` 需手动拖拽绑定到 ProgressText / ResultText / RestartHintText 的 Font Asset 字段，否则中文不显示。
- ⚠️ EntityStats.cs 未充分使用
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
  - Base Layer 的 Death 状态无出口过渡，不可添加出口
  - `IsDead` Trigger 由 PlayerDeathHandler 触发
  - `Attack` Trigger 由 PlayerSkillController 触发，仅在 UpperBody Layer 使用
  - UpperBody Layer 的 `AnyState → UpperBodyIdle`（IsDead 条件）确保死亡时全身死亡动画不被上半身层覆盖，不可删除
  - `UpperBodyIdle` 状态必须有 motion（当前为 `UpperBodyIdle.anim` 空 clip），设为 null 会导致 Inspector DoPopup NullReferenceException 报错刷屏

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
- `Assets/Scripts/Player/PlayerSkillController.cs`：技能判定 + 伤害调用 + `Attack` Trigger 触发
- `Assets/Scripts/Player/PlayerDeathHandler.cs`：玩家死亡处理，`_isDeadHandled` 防重复，禁用5个组件 + Rigidbody isKinematic + IsDead Trigger
- `Assets/Scripts/Player/PlayerRespawnPointTracker.cs`：玩家最近复活点记录组件，当前只负责保存位置和朝向，不负责真正复活。
- `Assets/Scripts/Level/SavePoint.cs`：复活点触发器，进入 Trigger 后调用 `PlayerRespawnPointTracker.SetRespawnPoint()`。

### 核心资产
- `Assets/Scripts/SkeletonAnimator.controller`：参数 Speed/IsAttacking/IsDead，状态 Idle/Walk/Attack/Death
- `Assets/Scripts/PlayerAnimator.controller`：2层结构（Base Layer + UpperBody Layer），参数含 IsDead/Attack Trigger，详见第 4 节
- `Assets/Scripts/Animation/PlayerUpperBody.mask`：上半身 Avatar Mask，UpperBody Layer 依赖此文件
- `Assets/Scripts/Animation/UpperBodyIdle.anim`：UpperBody Layer 默认状态 clip，不可删除
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_slash01.fbx`：第 20 帧 Animation Event
- `Assets/SazenGames/Skeleton/Art/Animations/Skeleton_death.fbx`：clip `root|death`，1.4s
- `Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx`：玩家死亡动画，clip `HumanM@CombatDamage01`（1.0s）
- `Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_R.fbx`：玩家普通攻击动画
- `Assets/Fonts/09_SourceHanSansSC/TMP/SourceHanSansSC-Medium_TMP.asset`：当前唯一中文 TMP Font Asset，需手动绑定到 LevelUI 的 3 个 TMP 文本
- `Assets/Resources/SkeletonEnemy.prefab`：含 EnemyDeathHandler

### 不应修改的文件
- `Assets/Blink/`：第三方角色资产
- `Assets/SazenGames/`：第三方骷髅资产（模型/贴图本体）
- `Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Medium.otf`：源字体，不可改
- `Packages/manifest.json`：包配置

## 10. Next Suggested Tasks

### ⭐ 最推荐的下一个小任务
**绑定新 TMP Font Asset**：将 `SourceHanSansSC-Medium_TMP.asset` 手动拖拽到 LevelUI 下 ProgressText / ResultText / RestartHintText 的 Font Asset 字段，并确认 Play Mode 中中文正常显示。（已绑定）

### 优先级 1：待解决
2. 玩家死亡后相机演出（死亡时 RPGCameraController 被禁用，可改为死亡镜头而非静止）

### 优先级 2：系统完善
4. 死亡后主动清除 `PlayerTargeting.CurrentTarget`（需为 PlayerTargeting 添加 `ClearTarget()` 方法）
5. 集成 EntityStats 系统，支持攻击力/血量等属性可配置
6. 将 `FindObjectsOfType<FactionComponent>()` 替换为注册缓存，减少 EnemyAI 扫描开销

---

**最后更新**：2026-05-08
**本次有效变更**：
1. `HealthComponent.cs` 新增 `RestoreFullHealth()`：将 currentHealth 恢复为 maxHealth，触发 OnHealthChanged，不触发 OnDied / OnDamaged。
2. `PlayerDeathHandler.cs` 新增 `ResetForRespawn()`：恢复死亡时禁用的 PlayerController / PlayerSkillController / PlayerTargeting / RPGCameraController、Rigidbody 物理状态、Animator（Rebind+Update）。
3. `LevelObjectiveManager.cs` 新增 `ClearLevelResultForRespawn()`：隐藏 Game Over / Victory 结算 UI，重置 _isLevelEnded，不改变击杀数。
4. `SkeletonDebugUI.cs` 新增 "恢复玩家满血" 和 "复活玩家测试" 两个 Debug 按钮；后者完整串联上述三个接口，可在 Play Mode 中 Debug 复活玩家（不传送到复活点）。
5. 测试确认：玩家死亡后点击 Debug 复活按钮，可正常恢复移动、攻击、摄像机控制，敌人仇恨系统恢复正常，Game Over UI 消失，_isLevelEnded 重置后可再次触发死亡结算。
6. 本次未修改 Animator Controller、Prefab、Scene，未实现传送到 SavePoint。