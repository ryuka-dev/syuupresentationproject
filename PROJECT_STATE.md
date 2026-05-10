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
- 当前开发阶段：早期原型 - 野外战斗 / 刷怪循环基础开发

## 2. Game Concept
- 游戏核心玩法：第三人称动作战斗，玩家对抗 AI 野外敌人。
- 当前主要循环：玩家移动 → 鼠标左键选中敌人 → 按 1 使用普通攻击 → 敌人死亡 → 任务击杀进度增加 → 刷怪点延迟刷新敌人 → 继续战斗。
- 长期方向：暗黑破坏神式刷装备循环 + FF14 式野外怪物分布 / 脱战逻辑 + 第三人称 3D RPG 战斗表现。
- 已确认的设计方向：
  - 基于 FSM 的敌人 AI（Idle/Chase/Attack/ReturnToSpawn）
  - 阵营系统（敌我识别）
  - 仇恨系统（多目标列表 + 仇恨值排序 + 脱战）
  - 野怪出生中心点 / 游荡内圈 / 活动边界外圈
  - 攻击冷却机制
  - 世界空间血条显示
  - 击杀目标逐步从“关卡 Victory”转向“任务目标 / 刷怪循环”
  - 后续将接入掉落物、拾取、背包 / 装备系统
  
### Long-term Core Direction / 最终定位

本项目长期定位为「单人 Tank 保护型动作 RPG」。

玩家扮演一名 Tank 型主角，通过 MMO 式 GCD / oGCD 技能节奏、Boss 时间轴、AoE 机制、减伤技能与装备构筑，在战斗中承受高压攻击，并保护核心治疗角色 Hikari。

游戏不是小队指挥游戏，也不是传统护送 NPC 游戏。玩家主要操作主角本人，Hikari 作为治疗搭档与剧情核心存在，未来计划使用「治疗负担 / 光之负荷」系统替代普通 NPC 血条，避免玩家产生保姆式负担。

长期核心体验：
- 玩家通过正确技能承受原本扛不住的攻击
- 玩家保护 Hikari，使她不因过度治疗而被消耗
- 装备掉落不仅提供数值成长，也改变技能派生、防御方式和支援连携

## 3. Current Architecture

### Enemy AI 系统
- `EnemyAI.cs`：敌人有限状态机（Idle/Chase/Attack/ReturnToSpawn）+ 仇恨系统 + 野怪脱战回家逻辑。
  - 仇恨列表：`Dictionary<Transform, float> hateTable`，key=目标，value=仇恨值
  - `AddHate(Transform, float)`：统一仇恨入口，添加/累加后重新选目标
  - `IsValidTarget(Transform)`：有效性检查（非 null、有 HealthComponent、未死亡、阵营敌对）
  - `RemoveInvalidHateTargets()`：清除死亡/销毁目标
  - `SelectHighestHateTarget()`：选仇恨值最高有效目标为 currentTarget
  - `_spawnPosition / _spawnRotation`：在 Awake() 记录敌人出生中心点与出生朝向
  - `wanderRadius`：预留字段，代表未来 Idle / Wander 状态允许自然游荡的内圈半径；当前暂不参与逻辑
  - `leashRadius`：活动边界外圈；敌人离出生中心点的 XZ 水平距离超过该值时立即进入 ReturnToSpawn
  - `UpdateDisengage()`：目标持续超出 disengageDistance + disengageDelay 后，调用 `EnterReturnToSpawn()` 脱战回家
  - `EnterReturnToSpawn()`：清空仇恨 / currentTarget，停止攻击动画，进入 ReturnToSpawn；不瞬移、不回血
  - `HandleReturnToSpawn()`：使用 XZ 平面方向让敌人自己走回出生中心点；到达后只修正 X/Z、保留当前 Y，清速度、回满血、恢复 Idle
  - `ResetToSpawn()`：Debug / 强制复位接口，直接瞬移回出生点、清仇恨、回满血、Idle；不作为正式脱战行为
  - `ForceDisengageAndReturnToSpawn()`：供外部系统调用；活着且 AI 启用时强制脱战进入 ReturnToSpawn，不调用 ResetToSpawn()
  - `HandleDamaged(float, Transform)`：订阅 `HealthComponent.OnDamaged`，受击时对攻击来源加仇恨；ReturnToSpawn 中受击后不会立刻中断回家，后续是否无视伤害待定
  - `OnAttackHit()`：由 Animation Event 调用，首行有 `!enabled` 保护，**方法名不可改**
  - `OnEnable/OnDisable`：管理 OnDamaged 事件订阅生命周期
- `FOVDetector.cs`：视野检测（FOV），角度 + 距离判断目标可见性。
- `FactionSystem.cs` / `FactionComponent`：阵营枚举（Player/Skeleton/Goblin/Dragon），`ShouldAttack(Faction)` 判断敌对关系。
- `EnemyDeathHandler.cs`：监听 `HealthComponent.OnDied`，禁用 EnemyAI、停止 Rigidbody、禁用 Collider，触发死亡动画，延迟 destroyDelay 秒后 Destroy。
- `EnemyWorldManager.cs`：场景级敌人管理器第一版。
  - 挂载在 SampleScene 的 `EnemyWorldManager` 空 GameObject 上
  - `ForceAllLivingEnemiesReturnToSpawn()`：通过 `FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)` 找到所有 EnemyAI，并逐个调用 `ForceDisengageAndReturnToSpawn()`
  - 当前仅用于玩家死亡时命令所有活着敌人脱战回出生点；暂不做注册缓存、刷新管理、掉落、任务广播
- `EnemySpawnPoint.cs`：第一版正式野外刷怪点。
  - 一个 SpawnPoint 管理一个敌人
  - 字段：`enemyPrefab`、`respawnDelay`、`spawnOnStart`
  - `SpawnEnemy()`：在 SpawnPoint 自身位置 / 朝向 Instantiate 敌人，保证 EnemyAI.Awake() 记录到刷怪点位置；订阅敌人 OnDied；调用 `LevelObjectiveManager.RegisterEnemy(health)`
  - `HandleCurrentEnemyDied()`：敌人死亡时取消订阅、清空当前引用，启动刷新协程
  - `RespawnAfterDelay()`：等待 respawnDelay 秒后重新生成敌人
  - Destroy 仍由 `EnemyDeathHandler` 负责，EnemySpawnPoint 不主动 Destroy 敌人

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
  - 通过 `FindFirstObjectByType<EnemyWorldManager>()` 找到场景敌人管理器，并调用 `ForceAllLivingEnemiesReturnToSpawn()`，让所有活着敌人脱战并自己走回出生点
  - 玩家复活时暂不额外重置敌人；死亡负责敌人脱战，复活只恢复玩家自身
  
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

### Level / Quest Objective 系统
- `LevelObjectiveManager.cs`（`Assets/Scripts/Level/`）：当前已从纯关卡 Victory 控制器转为“临时任务目标管理器 + 旧关卡模式兼容”。
  - `[SerializeField] HealthComponent playerHealth`：引用玩家 HealthComponent
  - `[SerializeField] List<HealthComponent> enemyHealthComponents`：已注册的敌人列表
  - `[SerializeField] int requiredKills = 3`：任务目标击杀数 / 旧关卡胜利所需击杀数
  - `[SerializeField] bool useQuestObjectiveMode = true`：任务目标模式开关
    - true：击杀数达到 requiredKills 后显示“任务完成”，不 Victory、不设置 `_isLevelEnded`、不显示 restartHintText，玩家可继续刷怪
    - false：保持旧关卡模式，击杀数达到 requiredKills 后 Victory，并允许按 R 重载场景
  - `private bool _isQuestCompleted`：任务完成状态；不阻止继续击杀、不阻止 RegisterEnemy、不监听 R 重开
  - `[SerializeField] TextMeshProUGUI progressText / resultText / restartHintText`：UI 文本引用（已在场景中绑定）
  - 玩家死亡时仍走旧 Game Over：`_isLevelEnded = true`、显示 Game Over / “按 R 重新开始”、按 R 重载当前场景；正式死亡 / 复活 UI 尚未接入
  - `RegisterEnemy(HealthComponent)`：公开接口，供 SkeletonSpawner / EnemySpawnPoint 等运行时生成器注册敌人；任务完成后仍允许继续注册，只有 `_isLevelEnded` 为 true 时拒绝注册
  - 内部用 `HashSet<HealthComponent> _countedEnemies` 防重复计数
  - 内部用 `Dictionary<HealthComponent, System.Action> _enemyDiedHandlers` 保存每个敌人 OnDied handler，确保 OnEnable / OnDisable / RegisterEnemy 使用同一委托实例订阅 / 取消订阅
  - `ShowQuestComplete()`：显示“任务完成”，隐藏 restartHintText，不设置 `_isLevelEnded`
  - `ClearLevelResultForRespawn()`：Debug 复活测试用接口。
    - 将 `_isLevelEnded` 重置为 false
    - 隐藏 `resultText`
    - 隐藏 `restartHintText`
    - 不清空 `progressText`
    - 不重置击杀数
    - 不重置 `_isQuestCompleted`
    - 不复活敌人
    - 不重载场景

### Spawner & Debug
- `SkeletonSpawner.cs`（`Assets/Scripts/Spawner/`）：Debug 敌人生成器。
  - `SpawnSkeleton()` 末尾自动调用 `FindFirstObjectByType<LevelObjectiveManager>()?.RegisterEnemy(hc)`，动态生成的敌人自动计入任务击杀进度
  - F1 调试菜单生成的骷髅通过此机制自动注册
- `EnemySpawnPoint.cs`（`Assets/Scripts/Enemy/`）：正式野外刷怪点第一版。
  - `enemyPrefab`：生成用敌人 Prefab
  - `respawnDelay`：死亡后刷新等待时间
  - `spawnOnStart`：Play Mode 开始时是否自动生成
  - 当前 SampleScene 中已有 `EnemySpawnPoint_Test`，绑定 `Assets/Resources/SkeletonEnemy.prefab`，respawnDelay=5s，spawnOnStart=true
- `SkeletonDebugUI.cs`：调试用 UI。
  - 可生成调试用骷髅
  - 提供“恢复玩家满血”按钮：调用 `HealthComponent.RestoreFullHealth()`
  - 提供“原地复活测试”按钮：调用 `RestoreFullHealth()` + `PlayerDeathHandler.ResetForRespawn()`
  - 提供“复活到最近存档点”按钮：
    - 读取 Player 上的 `PlayerRespawnPointTracker`
    - 将 Player 传送到 `CurrentRespawnPosition / CurrentRespawnRotation`
    - 调用 `HealthComponent.RestoreFullHealth()`
    - 调用 `PlayerDeathHandler.ResetForRespawn()`
    - 如已接入，则调用 `LevelObjectiveManager.ClearLevelResultForRespawn()` 清理结算 UI
  - 显示当前玩家锁定目标：
    - 无目标：显示“当前目标：无”
    - 非 EnemyAI：显示“当前目标：{名称}（非可重置敌人）”
    - EnemyAI disabled：显示“当前目标：{名称}（AI已禁用）”
    - HealthComponent.IsDead：显示“当前目标：{名称}（已死亡）”
    - 可重置活敌人：显示“当前目标：{名称}”
  - 提供“重置当前目标敌人”按钮：仅当当前目标存在、挂有 EnemyAI、EnemyAI.enabled=true、HealthComponent 存在且未死亡时，调用 `EnemyAI.ResetToSpawn()`；用于 Debug 强制复位，不代表正式脱战逻辑
- `PhysicsLayerSetup.cs`：物理层设置。

### Debug Respawn 测试流程
- 当前已形成最小 Debug 复活测试闭环：
  1. 玩家进入 SavePoint Trigger
  2. `SavePoint` 调用 `PlayerRespawnPointTracker.SetRespawnPoint()`
  3. 玩家死亡
  4. Debug UI 点击“复活到最近存档点”
  5. Player 传送到最近记录的复活点位置和朝向
  6. `HealthComponent.RestoreFullHealth()` 恢复 HP
  7. `PlayerDeathHandler.ResetForRespawn()` 恢复控制、相机、Rigidbody 和 Animator 状态
  8. 如已接入，`LevelObjectiveManager.ClearLevelResultForRespawn()` 清理结算 UI
- 玩家死亡时，`EnemyWorldManager` 已经命令所有活着敌人进入 ReturnToSpawn；复活到 SavePoint 时不额外重置敌人。
- 注意：该流程仍属于 Debug 测试功能，不是正式游戏 UI / 正式复活系统。


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
- **EnemyWorldManager**
  - Components: EnemyWorldManager
  - 用途：场景级敌人管理入口；玩家死亡时由 PlayerDeathHandler 调用，使所有活着敌人脱战并 ReturnToSpawn
- **EnemySpawnPoint_Test**
  - Components: EnemySpawnPoint
  - 位置：约 `(0, 0, 5)`
  - enemyPrefab = `Assets/Resources/SkeletonEnemy.prefab`
  - respawnDelay = 5 秒
  - spawnOnStart = true
  - 用途：第一版正式刷怪点测试；生成一只骷髅，死亡后延迟刷新
- **SavePoint**
  - Components: Transform, BoxCollider（Is Trigger=true）, SavePoint
  - 用途：玩家进入 Trigger 后更新 `PlayerRespawnPointTracker` 中保存的最近复活点位置和朝向
  - 当前仅记录复活点，不执行真正复活
  - **SavePoint / SavePoint_Test**
  - Components: Transform, BoxCollider（Is Trigger=true）, SavePoint
  - 用途：玩家进入 Trigger 后，将该对象的位置和朝向记录为最近复活点
  - 当前用于 Debug 复活测试，不是最终正式存档点表现

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
- ✅ Debug 复活到最近 SavePoint：Debug UI 新增“复活到最近存档点”按钮，玩家死亡后可传送到 `PlayerRespawnPointTracker.CurrentRespawnPosition / CurrentRespawnRotation`，并执行满血恢复、死亡状态恢复与结算 UI 清理。

- ✅ EnemyAI 新增出生点记录：`_spawnPosition / _spawnRotation`，作为野怪出生中心点与回家朝向。
- ✅ EnemyAI 新增 `ResetToSpawn()`：Debug / 强制复位接口，可瞬间回出生点、清仇恨、回满血、回 Idle。
- ✅ Debug UI 新增当前目标显示与“重置当前目标敌人”按钮，并加入活体检查，避免重置死亡 / AI 已禁用敌人。
- ✅ EnemyAI 新增 ReturnToSpawn 状态：正式脱战时不瞬移，而是自己走回出生点。
- ✅ ReturnToSpawn 使用 XZ 平面距离与方向，避免 Awake 高度 / 落地高度不一致导致无法判定到达。
- ✅ EnemyAI 新增 `wanderRadius` 预留字段与 `leashRadius` 活动边界；超过 leashRadius 会立即脱战 ReturnToSpawn。
- ✅ EnemyAI 新增 `ForceDisengageAndReturnToSpawn()`，供外部系统命令敌人脱战回家。
- ✅ 新增 `EnemyWorldManager`：玩家死亡时统一命令所有活着敌人 ReturnToSpawn；Debug 生成的多个敌人也可被捕捉。
- ✅ LevelObjectiveManager 改为任务目标模式：`useQuestObjectiveMode=true` 时，击杀 requiredKills 后显示“任务完成”，不 Victory，不按 R 重开，后续仍可继续刷怪和计数。
- ✅ LevelObjectiveManager 修复敌人 OnDied 事件订阅：使用 `_enemyDiedHandlers` 字典保存委托实例，避免 OnDisable 取消订阅失败 / 重复计数风险。
- ✅ 新增 `EnemySpawnPoint`：一个刷怪点管理一个敌人，死亡后等待 respawnDelay 自动刷新，并注册到 LevelObjectiveManager。
- ✅ SampleScene 新增 `EnemyWorldManager` 与 `EnemySpawnPoint_Test`，后者绑定 SkeletonEnemy.prefab，respawnDelay=5 秒，测试循环正常。

## 7. In Progress / Known Issues
- ⚠️ **LevelUI TMP 字体未绑定**：旧字体 asset 已删除，新 `SourceHanSansSC-Medium_TMP.asset` 需手动拖拽绑定到 ProgressText / ResultText / RestartHintText 的 Font Asset 字段，否则中文不显示。
- ⚠️ EntityStats.cs 未充分使用
- ⚠️ EnemyDeathHandler 使用固定 destroyDelay 计时，未使用 Animation Event，时机可能受播放速度影响
- ⚠️ `EnemySpawnPoint.OnDisable()` 当前只解除 OnDied 订阅；刷新协程是否需要 StopAllCoroutines() 后续再决定。
- ⚠️ `ScanForTarget()` 每 0.2s 调用 `FindObjectsOfType<FactionComponent>()`，敌人数量多时有性能隐患
- ⚠️ `EnemyWorldManager` 与 `EnemySpawnPoint` 当前使用 Find 系列 API，敌人数量增加后应改为注册缓存 / 场景管理。
- ⚠️ 玩家死亡后 RPGCameraController 被整体禁用，相机静止在死亡位置（无死亡镜头演出）
- ⚠️ 当前复活到 SavePoint 仍属于 Debug UI 测试流程，尚未接入正式死亡 / 复活 UI。
- ⚠️ Victory 已在任务模式下弱化为“任务完成”，但旧 Game Over 仍保留：玩家死亡仍显示 Game Over / 按 R 重新开始；正式死亡 / 复活 UI 尚未接入。
- ⚠️ Debug 清理结算 UI 不等同于最终关卡流程设计；`ClearLevelResultForRespawn()` 目前会隐藏 resultText，即使任务已完成也可能临时隐藏“任务完成”提示。
- ⚠️ 玩家死亡时敌人会 ReturnToSpawn；玩家复活到 SavePoint 时不额外处理敌人。若复活点靠近敌人出生点，敌人回 Idle 后重新发现玩家是允许行为。
- ⚠️ `EnemySpawnPoint` 在玩家死亡 / Game Over 状态下仍可能继续刷新敌人；是否暂停刷新待正式游戏状态管理决定。

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
- `Assets/Scripts/Enemy/EnemyAI.cs`：FSM + 仇恨系统 + ReturnToSpawn / leashRadius 核心，`OnAttackHit()` 有 Animation Event 绑定
- `Assets/Scripts/Enemy/EnemyWorldManager.cs`：场景级敌人全体命令入口，当前用于玩家死亡时全体敌人脱战回家
- `Assets/Scripts/Enemy/EnemySpawnPoint.cs`：正式野外刷怪点第一版，负责生成、监听死亡、延迟刷新、注册到 LevelObjectiveManager
- `Assets/Scripts/Enemy/EnemyDeathHandler.cs`：订阅 `HealthComponent.OnDied`，触发死亡流程
- `Assets/Scripts/Enemy/FactionSystem.cs`：阵营枚举和 `ShouldAttack()` 接口
- `Assets/Scripts/HealthComponent.cs`：`TakeDamage(float/float+Transform)`、`IsDead`、`OnDied`、`OnDamaged` 事件
- `Assets/Scripts/Player/PlayerTargeting.cs`：`CurrentTarget`（public Transform, get-only）
- `Assets/Scripts/Player/PlayerSkillController.cs`：技能判定 + 伤害调用 + `Attack` Trigger 触发
- `Assets/Scripts/Player/PlayerDeathHandler.cs`：玩家死亡处理，`_isDeadHandled` 防重复，禁用5个组件 + Rigidbody isKinematic + IsDead Trigger
- `Assets/Scripts/Player/PlayerRespawnPointTracker.cs`：玩家最近复活点记录组件，保存 `CurrentRespawnPosition` / `CurrentRespawnRotation`，由 SavePoint 更新。
- `Assets/Scripts/Level/SavePoint.cs`：复活点 Trigger，玩家进入后调用 `PlayerRespawnPointTracker.SetRespawnPoint()`。
- `Assets/Scripts/Player/PlayerDeathHandler.cs`：除死亡处理外，包含 `ResetForRespawn()`，用于 Debug 复活时恢复控制、相机、Rigidbody 和 Animator 状态。
- `Assets/Scripts/HealthComponent.cs`：包含 `RestoreFullHealth()`，用于复活测试时恢复满血并刷新血条。
- `Assets/Scripts/Spawner/SkeletonDebugUI.cs`：包含 Debug 复活测试按钮，包括恢复满血、原地复活、复活到最近 SavePoint，以及当前目标敌人 Debug Reset。

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

## Development Direction Notes / 近期开发原则

当前开发应优先验证核心闭环，而不是一次性实现完整系统。

短期优先级：
1. 先完成「刷怪 → 掉落 → 拾取 → 继续刷怪」收益闭环
2. 再实现最小 PlayerInventory
3. 再实现 1 个装备槽，验证装备能改变战斗
4. 再推进 Tank 感：敌人读条重击、玩家减伤技能、成功减伤奖励
5. 最后再接入 Hikari 自动治疗与治疗负担系统

暂不优先实现：
- 完整背包 UI
- 完整装备栏
- 复杂随机词条
- 完整 Hikari AI
- 五人真实同屏战斗
- 复杂仇恨表
- Boss 战最终版

## 10. Next Suggested Tasks

### ⭐ 最推荐的下一个小任务
**掉落系统第一版：地面掉落物 + 拾取日志，不先做完整背包。**
该任务是为了建立暗黑式刷装循环的最小收益闭环，而不是为了立即进入完整背包 / 装备系统。

当前已经具备野外刷怪循环基础：
- EnemySpawnPoint 生成敌人
- 玩家击杀敌人
- LevelObjectiveManager 继续计数且不 Victory
- EnemyDeathHandler 播放死亡动画并 Destroy
- EnemySpawnPoint 延迟刷新敌人

下一步应验证刷装备路线的“收益闭环”：
- 怪物死亡时生成一个 PickupItem
- 玩家靠近 / 按键拾取
- Console 输出“获得：XXX”
- 暂不实现背包 UI、装备栏、随机词条

建议小步拆分：
1. 新增 `ItemData` ScriptableObject：itemName、rarity、description（icon 可后续）
2. 新增 `PickupItem`：地面拾取物，持有 ItemData，玩家靠近后按 E 拾取，Debug.Log 并 Destroy 自己
3. 新增 `EnemyDropper`：挂到敌人 Prefab，监听 HealthComponent.OnDied，死亡时在尸体附近生成 PickupItem
4. 后续再做 `PlayerInventory` 最小版与背包 UI

### 优先级 1：待解决
1. 掉落系统第一版：固定 / 简单随机掉落物 + 玩家拾取日志
2. 正式死亡 / 复活 UI：玩家死亡后显示“复活到最近存档点 / 重新开始”等正式选项，替代 Debug UI 流程
3. Game Over / Respawn / Quest Complete 状态分离：当前玩家死亡仍显示 Game Over / 按 R 重新开始
4. EnemySpawnPoint 与 EnemyWorldManager 的注册缓存：替换 Find 系列 API
5. EnemySpawnPoint 在玩家死亡 / Game Over / 切场景时是否暂停刷新，需要正式 GameState 后决定

### 优先级 2：系统完善
1. Wander / 游荡状态：使用 `wanderRadius` 在出生中心点内圈自然移动
2. 集成 `EntityStats` 系统，支持攻击力 / 最大血量等属性可配置
3. 将 `ScanForTarget()` 的 FindObjectsOfType 替换为注册缓存，减少 EnemyAI 扫描开销
4. 将 Debug 复活流程迁移到正式 Respawn 流程，减少对 Debug UI 的依赖
5. 存档点正式化：加入激活提示、视觉表现、音效 / 特效，以及是否保存到硬盘的规则
6. 背包 / 装备系统：在掉落拾取验证后再实现

---

**最后更新**：2026-05-10
**本次有效变更**：
1. `EnemyAI.cs` 新增出生点记录、Debug 强制 `ResetToSpawn()`、正式 `ReturnToSpawn` 状态、XZ 平面回家判定、`wanderRadius` 预留、`leashRadius` 活动边界与 `ForceDisengageAndReturnToSpawn()` 外部接口。
2. `SkeletonDebugUI.cs` 新增当前目标显示与“重置当前目标敌人”按钮，带 EnemyAI.enabled / HealthComponent.IsDead 活体检查。
3. 新增 `EnemyWorldManager.cs`，并在 SampleScene 中配置 `EnemyWorldManager` GameObject；玩家死亡时所有活着敌人会脱战并自己走回出生点。
4. `PlayerDeathHandler.cs` 在死亡流程中调用 `EnemyWorldManager.ForceAllLivingEnemiesReturnToSpawn()`；复活时不额外处理敌人。
5. `LevelObjectiveManager.cs` 改为任务目标模式：`useQuestObjectiveMode=true` 时，击杀 requiredKills 后显示“任务完成”，不 Victory、不进入 `_isLevelEnded`、不阻止继续刷怪。
6. `LevelObjectiveManager.cs` 修复敌人死亡事件订阅管理，使用 `_enemyDiedHandlers` 字典保存 handler，避免 lambda 取消订阅失败。
7. 新增 `EnemySpawnPoint.cs`，并在 SampleScene 中配置 `EnemySpawnPoint_Test`，绑定 `SkeletonEnemy.prefab`，respawnDelay=5 秒，spawnOnStart=true。
8. 测试确认：Debug 生成的多个敌人会在玩家死亡后被 EnemyWorldManager 捕捉并 ReturnToSpawn；EnemySpawnPoint 生成的敌人死亡后可延迟刷新；任务完成后仍可继续计数与刷怪。
