# ARCHITECTURE_REFERENCE

最后更新：2026-05-25

旧 PROJECT_STATE.md 中的详细系统架构参考。当前实现状态摘要见 PROJECT_STATE.md。

---

## 1. Player 系统

### PlayerController.cs
玩家输入、移动、朝向与动画参数输出。使用 New Input System，applyRootMotion = false。

- FF14 Legacy-like 相机基准移动：WASD 以 cameraTransform.forward / right（去掉 y 轴）为基准
- 有移动输入时 Player 朝实际移动方向平滑转身；无移动输入时不被相机旋转强制转身
- Shift + 任意移动输入 = Sprint，八方向可跑
- 左键 + 右键 = 等价前进输入；左键 + 右键 + Shift = 相机前方跑步
- Rigidbody 移动使用 rb.linearVelocity（Unity 6）
- Animator 参数：Speed = 0/0.5/1.0（Idle/Walk/Run），Horizontal = 0（Legacy-like 始终为 0）
- IsJumping 只在起跳帧短暂为 true，下一帧清除；_jumpConsumed 限制一次离地只消费一次
- fallGravityMultiplier = 2.5，riseGravityMultiplier = 1.0，maxFallSpeed = 25

### PlayerAnimator.controller
路径：Assets/Scripts/PlayerAnimator.controller

- JumpDown / FallingLoop → Idle：条件 Speed < 0.1
- JumpDown / FallingLoop → RunForward：IsGrounded == true && Speed > 0.1 && IsSprinting == false
- JumpDown / FallingLoop → Sprint：IsGrounded == true && Speed > 0.1 && IsSprinting == true
- Base Layer 的 Death 状态无出口过渡，不可添加
- UpperBody Layer 的 Any State → UpperBodyIdle（IsDead 条件）不可删除

### RPGCameraController.cs
- 只负责相机跟随、yaw / pitch、Cursor 显示隐藏；不直接修改 Player rotation
- 只有右键按住时相机才旋转；右键拖拽开始时隐藏 Cursor，松开时显示
- 灵敏度：_yaw += delta.x * rotationSpeed，_pitch -= delta.y * rotationSpeed（不乘 Time.deltaTime）
- rotationSpeed Inspector 可调，用户实测 0.5 合适；实际值以 Main Camera Inspector 为准
- OnDisable() / OnDestroy() 强制恢复 Cursor 显示

### PlayerTargeting.cs
- 鼠标左键 Raycast，通过 HealthComponent + FactionComponent + ShouldAttack() 验证目标
- CurrentTarget 统一为 faction.transform
- Tab 选择：收集屏幕内、摄像机前方、未死亡、敌对、距离 <= tabTargetMaxDistance 的目标，按 viewport.x 从左到右排序
- 当前使用 FindObjectsByType，敌人数量多时应改为注册缓存
- ClearTarget()：玩家死亡时调用

### TargetSelectionIndicator.cs
路径：Assets/Scripts/UI/TargetSelectionIndicator.cs
- 读取 PlayerTargeting.CurrentTarget，在目标头顶显示 indicatorPrefab
- 目标切换时计算一次头顶高度偏移，运行中不再每帧扫描 Collider（修正频闪）
- 指示器 transform.forward = camera.transform.forward（始终面向相机）

### PlayerDeathHandler.cs
- 清空目标 → 禁用 PlayerController / PlayerSkillController / PlayerTargeting / RPGCameraController
- 清零 Rigidbody 并设置 isKinematic = true → 播放死亡动画
- 调用 EnemyWorldManager.ForceAllLivingEnemiesReturnToSpawn()
- 复活时 ResetForRespawn() 只恢复玩家自身，不额外重置敌人

### PlayerSkillController.cs
- 当前仍保留在 Player 上，主要用于兼容 PlayerDeathHandler 的禁用流程。
- 1 / 4 输入处理已移除，不再分发 Basic Attack / Area Attack。
- 玩家技能输入统一由 PlayerSkillManager 读取 PlayerSkillData.inputSlot。
- 不要把普通攻击、AOE 搜索、冷却、伤害结算重新塞回 PlayerSkillController。

---

## 2. Player Skill 系统（v0.2）

### 职责分层

```
PlayerSkillData              → 技能静态数据（按键槽、类型、距离、倍率、冷却等）
PlayerSkillManager           → 读取 skills、生成 RuntimeStates、分发输入到执行器
PlayerBasicAttackController  → 执行 Slot1 BasicMeleeAttack / Slot4 BasicAreaAttack，管理共享冷却
PlayerGuardCounterController → 执行 Slot5 Radiant Riposte，管理 10 秒反击窗口
PlayerStatusEffectController → 统一修正减伤 / 攻击倍率 / 治疗倍率
```

### PlayerSkillData.cs
路径：Assets/Scripts/Player/Skills/PlayerSkillData.cs

关键枚举：
- PlayerSkillInputSlot：Slot1 ... Slot9
- PlayerSkillEffectType：None / DamageReduction / AttackPowerMultiplier / AreaDamage / GuardCounter / BasicMeleeAttack / BasicAreaAttack
- PlayerSkillRangeType：Self(0m) / Melee(3m) / Area(5m) / Ranged(20m) / Custom
- PlayerSkillVisualType：None / DefenseRing

关键字段：skillId / skillName / localizationKey / inputSlot / keyLabel / cooldown / duration / effectType / rangeType / damageTakenMultiplier / attackPowerMultiplier / healingReceivedMultiplier / areaDamageMultiplier / grantsGuardCounter

当前有效技能资产（路径：Assets/Skills/Player/）：
```
Skill_BasicAttack.asset      Slot1  BasicMeleeAttack  Melee 3m
Skill_IronBulwark.asset      Slot2  DamageReduction   Self  grantsGuardCounter=true
Skill_StoneGuard.asset       Slot3  DamageReduction   Self  healingReceivedMultiplier=1.5
Skill_BasicAreaAttack.asset  Slot4  BasicAreaAttack   Area 5m  areaDamageMultiplier=0.4
Skill_RadiantRiposte.asset   Slot5  GuardCounter      Ranged 20m
```

### PlayerSkillManager.cs
路径：Assets/Scripts/Player/Skills/PlayerSkillManager.cs
- skills 数组顺序 = 技能栏显示顺序
- 分发规则：BasicMeleeAttack/BasicAreaAttack → PlayerBasicAttackController；GuardCounter → PlayerGuardCounterController；其他 → TryActivateSkill(state)
- 玩家死亡时不处理输入
- OnSkillActivated 事件供未来执行器订阅

### PlayerBasicAttackController.cs
路径：Assets/Scripts/Player/PlayerBasicAttackController.cs
- 执行 Slot1（BasicMeleeAttack，目标距离 3m）与 Slot4（BasicAreaAttack，OverlapSphere 5m）
- 管理共享基础攻击冷却 basicAttackRecast = 1.0f
- AOE 伤害 = 普通攻击最终伤害 × areaBasicAttackDamageMultiplier（0.4）
- AOE 使用 HashSet 避免多 Collider 重复命中
- 伤害通过 PlayerStatusEffectController.ModifyOutgoingNormalAttackDamage() 修正

### PlayerGuardCounterController.cs
路径：Assets/Scripts/Player/PlayerGuardCounterController.cs
- 监听 HikariSupportController.OnGuardResonanceTriggered(attacker, grantsGuardCounter)
- 只有 grantsGuardCounter == true 时进入 Radiant Riposte Ready（10 秒）
- 保存 attacker；再次收到授权的 Guard Resonance 时刷新目标与时间
- TryUseCounter(skillData)：attacker 死亡 / 距离 > 20m 时不消耗 Ready
- 命中造成 3 PDU（Tier 1 = 60 damage）
- 玩家死亡时清除 Ready

### PlayerStatusEffectController.cs
路径：Assets/Scripts/Player/Skills/PlayerStatusEffectController.cs
- 读取 PlayerSkillManager.RuntimeStates，对 Active 技能应用：
  - DamageReduction → ModifyIncomingDamage(damage)
  - AttackPowerMultiplier → ModifyOutgoingNormalAttackDamage(baseDamage)
  - HealingReceivedMultiplier → ModifyIncomingHealing(amount)
- 多个倍率均使用乘算叠加

### PlayerSkillCanvasUI.cs / PlayerSkillBarCanvasUI.cs
- PlayerSkillBarCanvasUI（挂在 SkillBar）：Play Mode 根据 RuntimeStates 动态 Instantiate 技能格
- PlayerSkillCanvasUI：通用技能格，显示图标 / 按键 / 技能名 / Active 时间 / Cooldown 遮罩
  - BasicMeleeAttack / BasicAreaAttack：读取 PlayerBasicAttackController 共享冷却
  - GuardCounter：条件未满时灰色遮罩，Ready 时发光并显示 10 秒倒计时
- SkillSlotTemplate 是隐藏模板，不是具体技能格
- 技能栏锚点右下，pivot = (1, 0)，新技能显示在最右侧

### PlayerMitigationVisualFeedback.cs
- Iron Bulwark Active 时显示脚下蓝色 LineRenderer 防御光环
- 读取 PlayerSkillManager.GetStateBySkillId(skillId: iron_bulwark).IsActive

---

## 3. Enemy 系统

### EnemyAI.cs
FSM（Idle / Wander / Chase / Attack / ReturnToSpawn）+ 仇恨系统 + NavMeshAgent 优先移动

重要字段：
- hateTable：Dictionary<Transform, float> 仇恨列表
- _spawnPosition / _spawnRotation：Awake 记录出生点
- wanderRadius / leashRadius：Wander 内圈 / 活动外圈半径
- _spawnAreaCenter / _hasSpawnAreaContext：EnemySpawnArea 注入的区域中心
- WanderCenter / LeashCenter：属性，SpawnArea 注入后以区域中心为基准
- chaseDestinationUpdateInterval = 0.2s：Chase 中更新 NavMesh destination 的间隔
- _lastValidChaseDestination / _hasLastValidChaseDestination：路径更新失败时继续追旧 path

FSM 状态摘要：
```
Idle          → 随机待机后进入 Wander
Wander        → NavMeshAgent 优先游荡；Agent 不可用时 Rigidbody fallback
Chase         → NavMeshAgent 优先追击；目标点暂时不可达时不切 Rigidbody，保持 Agent Chase
              → 只有 Agent 本身不可用时才 fallback 到 Rigidbody
Attack        → 停止 Agent，保留旧普通攻击逻辑；EnemySkillController.IsCasting 时保持 Attack 状态
ReturnToSpawn → NavMeshAgent 优先回家；到达后回满血，进入 Idle
```

重要约定：
- OnAttackHit()：Animation Event 入口，方法名不可改
- Chase 中「目标点暂时不可达 ≠ Agent 失效」；不要因路径更新失败切 Rigidbody
- SetSpawnAreaContext(areaCenter, wanderR, leashR, spawnPos, spawnRot)：由 EnemySpawnArea 调用，不调用时兼容旧 EnemySpawnPoint

### EnemyWorldManager.cs
- 玩家死亡时调用 ForceAllLivingEnemiesReturnToSpawn()，查找所有 EnemyAI
- 敌人增多后应改为注册缓存，不依赖 Find 系列 API

### EnemySpawnPoint.cs
- 单个 SpawnPoint 管理一个敌人，死亡后等待 respawnDelay 刷新
- 注册生成敌人到 LevelObjectiveManager

### EnemySpawnArea.cs
- 区域刷怪：SpawnEntry（prefab / weight / maxAlive）加权随机选择
- maxAliveCount：区域内总存活上限；spawnRadius（内圈 Wander 范围）/ leashRadius（外圈脱战范围）
- 死亡后延迟 respawnInterval 补怪
- Gizmo：绿色内圈 / 橙色外圈
- 暂未接入 LevelObjectiveManager

### EnemyDropper.cs
- List<DropEntry> drops：多条目概率掉落（item / dropChance / offset）
- alignDropsToGround = true：掉落物通过 Raycast 向下贴地生成
- maxDropHeightAboveOwnerBounds：避免将敌人头顶 Collider 误判为地面
- drops 为空时 fallback 到旧单物品 dropItem

---

## 4. Health / Combat / Feedback

### HealthComponent.cs
- TakeDamage(float) / TakeDamage(float, Transform) / TakeDamage(float, Transform, CombatTextSourceLabel)
- Heal(float amount, Transform healer)：应用 ModifyIncomingHealing() 后触发 OnHealed；死亡状态不绕过复活流程
- RestoreFullHealth()：回满血并触发刷新
- SetMaxHealth(float, bool keepCurrentRatio = false)：动态修改最大生命值
- 事件：OnHealthChanged(float, float) / OnDied / OnDamaged(float, Transform) / OnHealed(float, Transform)
- LastDamageSourceLabel / LastDamageHasSourceLabel：供 DamageNumberSpawner 显示技能名副文本
- 伤害修正：通过同对象的 PlayerStatusEffectController.ModifyIncomingDamage() 应用减伤

### PlayerCombatStats.cs
- EquipmentAttackPowerBonus = Core + Armor + Accessory AttackPowerBonus
- CurrentNormalAttackDamage = BaseNormalAttackDamage + EquipmentAttackPowerBonus
- EquipmentMaxHealthBonus = Core + Armor + Accessory MaxHealthBonus
- CurrentMaxHealth = Max(1, BaseMaxHealth + EquipmentMaxHealthBonus)
- 监听 PlayerEquipment.OnEquipmentChanged，自动调用 SetMaxHealth(CurrentMaxHealth, false)

### DamageNumberSpawner.cs / DamageNumberPopup.cs
路径：Assets/Scripts/UI/
- Spawner 订阅 HealthComponent.OnDamaged / OnHealed，在头顶生成飘字
- 不计算伤害，不修改血量
- healingPopupPrefab 为空时 fallback 到 popupPrefab
- 当前直接 Instantiate / Destroy，未实现对象池

---

## 5. Hikari Support 系统

### HikariSupportController.cs
路径：Assets/Scripts/Hikari/HikariSupportController.cs

治疗行为：
```
Light Mend（微光治愈）：    HP < 80% → 治疗 15 / 冷却 5s / Burden +5
Emergency Prayer（紧急祈愿）：HP < 35% 优先 → 治疗 45 / 冷却 25s / Burden +25
```

光负荷规则（以 GLOSSARY.md 术语为准）：
```
稳定导光（0%~79%）  → 正常治疗
光溢出（80%~99%）   → 治疗量 × overburdenHealingMultiplier（默认 0.5）
导光封锁（100%）    → 治疗停摆
导光恢复（<= 60%）  → 解除导光封锁，恢复治疗
```

光负荷自然下降：burdenRecoveryPerSecond = 1（可通过 F1 Debug 开关）

Guard Resonance（守护共鸣）触发条件：
1. guardResonanceEnabled == true
2. 玩家未死亡
3. Guard Resonance 不在 3 秒内置冷却中
4. 玩家至少有一个 DamageReduction 技能处于 Active
5. 本次伤害来自 EnemySkillType.CastAttack（通过 EnemySkillController.LastDamageSkillData 判断，窗口 0.25s）

Guard Resonance 效果：Burden -10，不治疗，不生成飘字，触发 OnGuardResonanceTriggered(attacker, grantsGuardCounter)

溢光反震（Overflow Counter）触发条件：
1. Guard Resonance 成功触发
2. Guard Resonance 触发前 BurdenRatio 位于 80%~99%
3. attacker 有有效 HealthComponent 且未死亡

效果：对 attacker 造成 30 伤害，不影响光负荷

代码层保留旧变量名（暂不重命名，避免 Inspector 序列化丢失）：
currentBurden / maxBurden / isOverloaded / overburdenHealingMultiplier / lightCounterEnabled / lightCounterDamage

---

## 6. Item / Inventory / Equipment

### ItemData.cs
ScriptableObject，OnValidate() 保护规则：
- Equipment → maxStack = 1，非 Equipment → equipmentSlotType = None，attackPowerBonus = 0，maxHealthBonus = 0
- 枚举 ItemType: Material / Equipment / Consumable / Currency / Quest / Cosmetic
- 枚举 EquipmentSlotType: None / Core / Armor / Accessory

### PlayerInventory.cs
- Equipment 永远新增独立 ItemStack（不合并）
- 非 Equipment 优先合并到相同 itemId 且未满的 stack
- FindFirstEquipmentBySlot(EquipmentSlotType) → 返回第一个匹配的 ItemData，不移除

### PlayerEquipment.cs
装备槽：Core / Armor / Accessory（主角武器固定不入装备系统）
- EquipCore/Armor/Accessory(item, out replacedItem) → 替换时 replacedItem 由调用方放回背包
- UnequipCore/Armor/Accessory() → 返回被卸下的 ItemData
- ClearEquipment() → 一次性清空三槽，只触发一次 OnEquipmentChanged
- OnEquipmentChanged 事件驱动 PlayerCombatStats 刷新

当前测试装备资产：
```
TestItem_Bone.asset       Material / MaxStack=99
TestItem_GuardCore.asset  Equipment / Core / ATK+20 / MaxHP+50
```

---

## 7. Respawn / SavePoint

### PlayerRespawnPointTracker.cs
- Awake 默认以玩家初始位置为复活点
- SetRespawnPoint(Vector3, Quaternion) 更新最近复活点

### SavePoint.cs
- Trigger 检测玩家进入后更新 PlayerRespawnPointTracker
- 只记录复活点，正式复活 UI 未接入

---

## 8. Level / Objective 系统

### LevelObjectiveManager.cs
- EnemySpawnPoint 生成敌人时会注册到 LevelObjectiveManager。
- 敌人死亡会增加任务击杀进度。
- 玩家复活到最近存档点时，Debug 流程会调用 LevelObjectiveManager.ClearLevelResultForRespawn() 清理关卡结果显示。
- EnemySpawnArea 暂未接入 LevelObjectiveManager。

---


## 9. Scene / NavMesh / Terrain

### SampleScene 主要对象

Player 挂载组件（关键）：
PlayerController / FactionComponent(Player) / HealthComponent / PlayerTargeting / PlayerSkillController / PlayerBasicAttackController / PlayerDeathHandler / PlayerRespawnPointTracker / PlayerInventory / PlayerEquipment / PlayerCombatStats / PlayerSkillManager / PlayerStatusEffectController / PlayerMitigationVisualFeedback / DamageNumberSpawner / TargetSelectionIndicator / PlayerSkillHudUI(OnGUI Debug)

SkillCanvas 结构：
```
SkillCanvas
└─ SkillBar (PlayerSkillBarCanvasUI)
   └─ SkillSlotTemplate (隐藏模板)
```

- SkeletonSpawnerManager：F1 Debug UI 来源（SkeletonDebugUI 挂在此处）
- NavMeshSurface_World：agentTypeID=0（Humanoid），layerMask=~0，NavMesh 已 Bake（嵌入 SampleScene.unity）
- Hikari 测试对象：临时 Cube，挂载 HikariSupportController，由 FindFirstObjectByType 查找

### Prefabs

| 路径 | 用途 |
|---|---|
| Resources/EnemyBase.prefab | 敌人通用基础模板，不直接生成 |
| Resources/SkeletonEnemy_Variant.prefab | EnemyBase Variant，无技能普通小怪 |
| Resources/SkeletonBossEnemy_Variant.prefab | EnemyBase Variant，VisualRoot 1.5×，有技能 Boss |
| Resources/SkeletonEnemy.prefab | 旧独立 Prefab，保留未删 |
| Resources/ItemDrop.prefab | SphereCollider Trigger，挂 PickupItem |
| Resources/UI/DamageNumberPopup*.prefab | 伤害 / 治疗飘字 4 个版本 |
| Resources/VFX/EnemyAoE/CircleAoETelegraph.prefab | CircleAoE 地面圆形提示 |
| Resources/VFX/EnemyAoE/DonutAoETelegraph.prefab | DonutAoE 程序化真环形 Mesh 提示 |

EnemyBase Variant 工作流：
```
EnemyBase.prefab
├─ SkeletonEnemy_Variant.prefab  (无技能，骨头100%+守护核心20%)
└─ SkeletonBossEnemy_Variant.prefab  (有技能：HeavySlash/BossShockwave/MoonRing，守护核心100%)
```

VisualRoot 缩放规则：根对象 scale = 1，VisualRoot.scale 用于视觉体型（Boss 1.5），Collider / NavMeshAgent 单独调整。

---

## 10. Enemy Skill 系统

### EnemySkillData.cs
ScriptableObject，skillType：None / CastAttack / CircleAoE / DonutAoE

关键字段：skillId / displayName / skillType / damage / castTime / cooldown / range / aoeRadius / aoeInnerRadius / aoeOuterRadius / aoeTelegraphPrefab

当前敌人技能资产（路径：Assets/Skills/Enemy/）：
```
SK_CastAttack_HeavySlash.asset    CastAttack  damage=50  cast=2.0  cd=10.0  range=2.5
SK_CircleAoE_BossShockwave.asset  CircleAoE   damage=30  cast=2.5  cd=12    range=10  r=5
SK_DonutAoE_MoonRing.asset        DonutAoE    damage=35  cast=3.0  cd=14    range=10  inner=2.8  outer=7.0
```

### EnemySkillController.cs
- IsCasting / CurrentSkill / CurrentCastProgress / CurrentCastElapsed / CurrentCastDuration / CurrentCastRemaining
- LastDamageSkillData / LastDamageSkillTime：CastAttack 命中时记录（供 Hikari Guard Resonance 判断）
- CircleAoE / DonutAoE 不写入 LastDamageSkillData，不触发 Guard Resonance
- InterruptCurrentCast()：未来打断技能的最小入口，当前未接入玩家技能

技能行为：
```
CastAttack → 读条完成后对目标一次伤害；读条开始后玩家拉开距离不取消
CircleAoE  → 读条期间圆形提示跟随 Boss；完成后以 Boss 当前坐标为中心 XZ 平面距离判定
DonutAoE   → 月环提示（DonutAoETelegraphController 程序化 Mesh）；inner 以内安全，inner~outer 受伤
```

安全规则：
- skills == null / Count == 0 不报错，继续普通攻击
- skillType == None 不执行
- CastAttack range 只用于读条开始；读条中不因距离取消

### EnemyCastBarUI.cs
- OnGUI + Camera.WorldToScreenPoint 绘制
- GUIStyle 在 OnGUI() 内懒初始化（不在 Awake / Start 中访问 GUI.skin）
- IsCasting == false 时不显示

---

## 11. Balance Baseline（参考）

> 详细数值定义与换算表见 BALANCE_BASELINE.md。

当前 Tier 1 定义：
```
1 PDU = 20 enemy damage（敌人HP / 玩家对敌输出）
1 PHU = 10 player damage（敌人对玩家伤害 / Hikari 治疗）
1 BU  = 5 Burden（Hikari 光负荷）
标准玩家 HP = 100 = 10 PHU
标准玩家普通攻击 = 20 = 1 PDU
```

当前已落地数值（Prefab / Asset 为准）：
```
SkeletonEnemy / Variant:     HP=100  attack=10  CD=2.0  DPS=0.5 PHU/s  无技能
SkeletonBossEnemy_Variant:   HP=400  attack=15  CD=2.0  DPS=0.75 PHU/s  有3种技能
```
