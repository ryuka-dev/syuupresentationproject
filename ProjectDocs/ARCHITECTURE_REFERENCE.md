# ARCHITECTURE_REFERENCE

最后更新：2026-05-28

旧 PROJECT_STATE.md 中的详细系统架构参考。当前实现状态摘要见 PROJECT_STATE.md。

---

## 1. Player 系统

### PlayerController.cs
玩家输入、移动、朝向、自动前进与动画参数输出。使用 New Input System，applyRootMotion = false。

- FF14 Legacy-like 相机基准移动：WASD 以 cameraTransform.forward / right（去掉 y 轴）为基准
- 有移动输入时 Player 朝实际移动方向平滑转身；无输入时不被相机旋转强制转身
- 若 `PlayerCombatFacingController.IsFacingLocked` 为 true，则 FixedUpdate 强制保持 `LockedFacingRotation`，避免攻击瞬间面向目标后被移动朝向覆盖
- Shift + 任意移动输入 = Sprint，八方向可跑
- 场景左键 + 右键 = 等价前进输入；UI 起始鼠标输入不参与双键前进
- R 键自动前进 v1：等效持续 W；右键调整前进方向；左键进入自由视角并锁定进入时前进方向；左键松开后相机 yaw 回正
- 自动前进中，W/A/S/D 任意方向输入或重新形成的场景双键会打断；持续方向输入或双键状态下按 R 可由自动前进接管，直到对应输入松开后恢复打断规则
- 自动前进自由视角 / 回正期间使用 locked forward，避免松开左键时角色转向自由视角方向；回正期间右键可接管当前相机方向
- Rigidbody 移动使用 rb.linearVelocity（Unity 6）
- Animator 参数：Speed = 0/0.5/1.0（Idle/Walk/Run），Horizontal = 0（Legacy-like 始终为 0）
- IsJumping 只在起跳帧短暂为 true，下一帧清除；_jumpConsumed 限制一次离地只消费一次
- fallGravityMultiplier = 2.5，riseGravityMultiplier = 1.0，maxFallSpeed = 25

### MouseInputGate.cs
路径：Assets/Scripts/Player/MouseInputGate.cs

- 统一记录鼠标左键 / 右键“按下瞬间”是否从 UI 开始，而不是每帧按当前位置判断
- 对外提供 LeftWorldHeld / RightWorldHeld / BothWorldButtonsHeld / LeftWorldPressedThisFrame 等状态
- `DefaultExecutionOrder(-100)`，保证 PlayerController / PlayerTargeting / RPGCameraController 读取时状态已更新
- UI 检测使用 EventSystem RaycastAll，不使用旧 Input 或单纯 IsPointerOverGameObject
- PlayerController、RPGCameraController、PlayerTargeting 都应依赖 MouseInputGate，不要各自维护独立 UI 命中判断

### PlayerAnimator.controller
路径：Assets/Scripts/PlayerAnimator.controller

- JumpDown / FallingLoop → Idle：条件 Speed < 0.1
- JumpDown / FallingLoop → RunForward：IsGrounded == true && Speed > 0.1 && IsSprinting == false
- JumpDown / FallingLoop → Sprint：IsGrounded == true && Speed > 0.1 && IsSprinting == true
- Base Layer 的 Death 状态无出口过渡，不可添加
- UpperBody Layer 的 Any State → UpperBodyIdle（IsDead 条件）不可删除
- UpperBody Layer 当前包含 `Action_RadiantRiposte` 状态，由 `RadiantRiposte` Trigger 进入，Motion 使用 `HumanM@AttackShield01`
- Radiant Riposte 动作目前不使用 Root Motion，也不使用 Animation Event；伤害仍在技能逻辑中即时结算

### RPGCameraController.cs
- 只负责相机跟随、yaw / pitch、Cursor 显示隐藏；不直接修改 Player rotation
- `shakeOffset` 是战斗反馈用相机偏移，由 `SimpleScreenFeedback` 写入，LateUpdate 最终位置计算时叠加并自然衰减
- 场景起始左键或右键按住时均可旋转相机；鼠标拖拽开始时隐藏 Cursor，松开时显示
- 鼠标输入归属统一通过 MouseInputGate 判断；UI 起始鼠标输入不进入相机拖拽
- 自动前进 + 左键自由视角松开后，HandleCameraReturn() 只慢速回正 yaw，不强制回正 pitch
- 自动前进回正目标来自 PlayerController.AutoForwardLockedForward；回正完成后调用 PlayerController.NotifyCameraReturnComplete()
- 灵敏度：_yaw += delta.x * rotationSpeed，_pitch -= delta.y * rotationSpeed（不乘 Time.deltaTime）
- rotationSpeed Inspector 可调，用户实测 0.5 合适；实际值以 Main Camera Inspector 为准
- OnDisable() / OnDestroy() 强制恢复 Cursor 显示

### PlayerTargeting.cs
- 鼠标左键 Raycast，通过 HealthComponent + FactionComponent + ShouldAttack() 验证目标
- 鼠标左键选目标只在 MouseInputGate.LeftWorldPressedThisFrame 时处理；点击 UI 不选中背后敌人
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
PlayerSkillData                → 技能静态数据（按键槽、类型、距离、倍率、冷却等）
PlayerSkillManager             → 读取 skills、生成 RuntimeStates、分发输入到执行器
PlayerCombatFacingController   → 技能执行时统一面向目标，并提供短暂朝向锁定
PlayerCombatAnimationController→ 玩家战斗动作播放入口
PlayerBasicAttackController    → 执行 Slot1 BasicMeleeAttack / Slot4 BasicAreaAttack，管理共享冷却
PlayerGuardCounterController   → 执行 Slot5 Radiant Riposte，管理 10 秒反击窗口
PlayerStatusEffectController   → 统一修正减伤 / 攻击倍率 / 治疗倍率
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

### PlayerCombatFacingController.cs
路径：Assets/Scripts/Player/Combat/PlayerCombatFacingController.cs
- 技能执行瞬间自动面向目标的统一入口，不负责选目标、伤害或技能条件
- `FaceTarget(Transform target)` 只计算水平面 yaw，并设置 `LockedFacingRotation`
- 成功面向后进入短暂 `faceLockDuration`（当前 0.30s），供 `PlayerController.FixedUpdate()` 保持 combat facing，避免移动朝向下一帧覆盖
- Slot1 BasicMeleeAttack 与 Slot5 Radiant Riposte 已接入；Slot4 BasicAreaAttack / AreaDamage 因无特定目标暂未接入

### PlayerCombatAnimationController.cs
路径：Assets/Scripts/Player/Animation/PlayerCombatAnimationController.cs
- 玩家战斗动作播放入口；技能脚本不直接硬写 Animator 细节
- 当前公开 `PlayRadiantRiposte()`，内部触发 Animator Trigger `RadiantRiposte`
- Animator 缺失时只 warning，不应阻断伤害、音效、光效、震动或 Hikari 逻辑
- 未来新增战斗动作应优先扩展此控制器或其后续数据化入口，而不是在各技能脚本散写 Animator Trigger

### PlayerBasicAttackController.cs
路径：Assets/Scripts/Player/PlayerBasicAttackController.cs
- 执行 Slot1（BasicMeleeAttack，目标距离 3m）与 Slot4（BasicAreaAttack，OverlapSphere 5m）
- Slot1 实际入口为 `TryExecuteBasicMeleeAttack()`；攻击结算前调用 `PlayerCombatFacingController.FaceTarget(CurrentTarget)`
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
- 成功释放时先面向 attacker，再通过 `PlayerCombatAnimationController.PlayRadiantRiposte()` 播放盾击动作
- 命中造成 3 PDU（Tier 1 = 60 damage），随后触发 `SimpleScreenFeedback` 左手弱光效 / 轻微屏幕震动
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
- List<DropEntry> drops：多条目概率 ItemData 掉落（item / dropChance / offset）
- Tea Buff 接入：非 100% ItemData 掉落概率可受 PlayerTeaBuffController 的掉率茶倍率影响；Material 成功掉落后可按素材茶概率额外生成 1 个
- Gold Drop 独立于 ItemData：dropGold / goldDropChance / goldMin / goldMax / goldPickupPrefab / goldDropOffset 生成 GoldPickup，不受茶 Buff 影响
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


### SimpleScreenFeedback.cs
路径：Assets/Scripts/Effects/SimpleScreenFeedback.cs
- 当前用于 Radiant Riposte 命中反馈
- 通过 `RPGCameraController.shakeOffset` 添加短暂轻微相机震动，不直接改相机最终位置
- 在 `leftHandVfxAnchor`（当前自动找到 `Wrist_L`）附近生成短暂弱 Point Light；不再使用强全屏闪光
- 只做表现反馈，不改变伤害或技能逻辑

---

## 5. Hikari Support 系统

### HikariSupportController.cs
路径：Assets/Scripts/Hikari/HikariSupportController.cs

治疗行为：
```
Light Mend（微光治愈）：    HP < 80% → 读条后治疗 15 / 冷却 5s / Burden +5
Emergency Prayer（紧急祈愿）：HP < 35% 优先 → 读条后治疗 45 / 冷却 25s / Burden +25
```
- 两个治疗共用 `healCastDuration`（当前 1.5s）；读条中通过 `CurrentActionLabel` 暴露给正式 UI
- 读条完成后才结算治疗、光负荷与冷却；导光封锁、玩家死亡或目标无效时取消读条，不结算治疗

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

Guard Resonance 效果：Burden -10，不治疗，不生成飘字，记录最近一次光负荷变化原因，播放临时防御成功 / 守护共鸣 SFX，触发 OnGuardResonanceTriggered(attacker, grantsGuardCounter)

溢光反震（Overflow Counter）触发条件：
1. Guard Resonance 成功触发
2. Guard Resonance 触发前 BurdenRatio 位于 80%~99%
3. attacker 有有效 HealthComponent 且未死亡

效果：对 attacker 造成 30 伤害，不影响光负荷

`LastBurdenDelta` / `LastBurdenReason` / `LastBurdenChangeTime` 记录最近一次明确事件变化，供 `HikariCombatStatusUI` 显示；自然恢复不写入提示，避免刷屏。

代码层保留旧变量名（暂不重命名，避免 Inspector 序列化丢失）：
currentBurden / maxBurden / isOverloaded / overburdenHealingMultiplier / lightCounterEnabled / lightCounterDamage

### HikariCombatStatusUI.cs
路径：Assets/Scripts/UI/HikariCombatStatusUI.cs
- 正式 Canvas UI v0.1，不是 F1 Debug UI
- 当前场景对象：`UI/HikariHUDCanvas/HikariPanel`
- 使用 SerializedField 绑定 TMP_Text / Image；不要恢复旧的全代码 Runtime 自动生成 UI
- 显示 Hikari 标题、当前状态、当前动作、治疗读条、光负荷条、光负荷数值、变化提示
- 变化提示读取 `HikariSupportController.LastBurden*`，显示约 3 秒后回到「变化提示：--」

---

## 6. Item / Inventory / Equipment

### ItemData.cs
ScriptableObject，OnValidate() 保护规则：
- Equipment → maxStack = 1，非 Equipment → equipmentSlotType = None，attackPowerBonus = 0，maxHealthBonus = 0
- 枚举 ItemType 当前包含 Material / Equipment / Consumable / Currency / Quest / Cosmetic / Tea
- Tea 类型可引用 TeaBuffData；Tea 不是装备，不提供攻击力 / 最大生命值
- 枚举 EquipmentSlotType: None / Core / Armor / Accessory
- 当前用于正式背包 UI 的图标字段：`Sprite icon` / `Icon` 只读属性

### TeaBuffData.cs
路径：Assets/Scripts/Items/TeaBuffData.cs

- ScriptableObject，描述茶 Buff 静态数据：buffId / displayName / effectType / value / durationSeconds
- 当前效果类型：NonGuaranteedDropChanceMultiplier、MaterialExtraQuantityChance
- 一个 Tea ItemData 通过 TeaBuffData 绑定一种茶效果；第一版不做多效果数组

### PlayerTeaBuffController.cs
路径：Assets/Scripts/Player/PlayerTeaBuffController.cs

- 挂在 Player，管理当前 TeaBuffData、剩余时间与覆盖规则
- TryUseTea(ItemData)：只接受 ItemType.Tea 且有 TeaBuffData 的物品；成功后覆盖当前茶 Buff
- 为 EnemyDropper 提供 GetNonGuaranteedDropChanceMultiplier() / GetMaterialExtraQuantityChance()
- 背包 Use 会消耗 1 个茶道具；试饮会直接应用茶 Buff，不加入背包也不扣金币

### PlayerWallet.cs
路径：Assets/Scripts/Player/PlayerWallet.cs

- 管理 Gold，提供 Gold / OnGoldChanged / AddGold(amount) / CanSpendGold(amount) / TrySpendGold(amount)
- Gold 不是 ItemData，不进入 PlayerInventory，不占背包格子
- 当前无存档、无上限、无多货币

### GoldPickup.cs
路径：Assets/Scripts/Items/GoldPickup.cs

- 地面金币掉落物，amount 可由 EnemyDropper.SetAmount() 设置
- 玩家进入触发范围后按 E，调用 PlayerWallet.AddGold(amount)，成功后销毁自身
- 找不到 PlayerWallet 时不销毁并输出 Warning

### PlayerInventory.cs
- 固定 slot 背包模型：`_items` 保持固定长度，`null` 表示空格；当前 `DefaultMinimumSlots = 54`，`Awake()` 会把旧 Inspector maxSlots=30 等值提升到最低容量
- Equipment 永远新增独立 ItemStack（不合并）；非 Equipment 优先合并到相同 itemId 且未满的 stack，不能合并时放入第一个 null 空格
- AddItem 失败时返回 false；`PickupItem` 必须检查返回值，失败时地上物品保留
- OnInventoryChanged：Add / Remove / Move / Swap 等库存变化后触发，用于正式 InventoryCanvas 刷新
- 按 slot API：GetStackAt / HasStackAt / MoveStack / SwapStacks / RemoveOneAt / RemoveStackAt；所有 slot 删除必须设为 null，不压缩 List，不使用 RemoveAt
- 右键菜单 Equip / Use Tea 与丢弃确认必须按 slotIndex 处理，避免多个同名物品时误操作第一个 itemId 匹配项
- FindFirstEquipmentBySlot(EquipmentSlotType) → 返回第一个匹配的 ItemData，不移除（Debug / 兼容用途）

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

### Formal Inventory / Equipment UI (v1)

当前正式背包 UI 位于 `SampleScene` 的 `UI/InventoryCanvas`。F1 OnGUI 背包窗口仍是 Debug，不是正式 UI。

关键脚本：
- `InventoryInputController.cs`：B 打开 / 关闭背包，Esc 关闭。使用 New Input System，不使用旧 `UnityEngine.Input`。
- `InventoryCanvasUI.cs`：正式背包 UI 总控；读取 `PlayerInventory` / `PlayerEquipment` / `PlayerCombatStats`，刷新 InventoryWindow / EquipmentWindow / StatSummary，处理 slot 移动 / 交换 / 丢弃确认，并调用 Equip / Unequip。
- `InventoryGridSlotUI.cs`：背包格子，由 `InventoryCanvasUI` 根据 `visibleSlotCount` 程序生成；显示 Icon / Count / SelectedFrame，支持 Hover Tooltip、左键短按常驻抓取、左键长按临时抓取、右键菜单。
- `EquipmentSlotUI.cs`：Core / Armor / Accessory 装备槽显示；不依赖 SlotLabel / EquippedItem 文本子物体，空槽只显示空槽背景，有装备时显示 Icon；支持 Hover Tooltip 与右键 Unequip 菜单。
- `ItemDetailPanelUI.cs`：纯 Hover Tooltip，只显示信息；无 TitleBar、无 DraggableUIWindow、无操作按钮；高度根据内容自动调整，`CanvasGroup.blocksRaycasts=false`，不阻挡底层格子 Hover。
- `InventoryContextMenuUI.cs`：右键操作菜单；背包 Equipment 显示 Equip，背包 Tea 显示 Use，已装备槽显示 Unequip；右键打开走 PointerDown；点击任意菜单项后关闭，点击菜单外部 / 拖动窗口 / 关闭背包时关闭。该对象可能初始 inactive，`Awake()` 只做初始化，不能调用 Hide() / SetActive(false)，否则第一次 Show 会被自己关闭。
- `DraggableUIWindow.cs`：InventoryWindow / EquipmentWindow 的窗口拖动；开始拖动时会隐藏右键菜单。
- `UIWindowBringToFront.cs`：窗口点击置顶；InventoryCanvas 内部窗口用 `SetAsLastSibling()` 控制前后顺序。

显示与交互规则：
- `InventoryCanvas` Canvas sortingOrder = 1000，用于压住 `SkillCanvas` / `LevelUI`。
- 背包格子总数由 `InventoryCanvasUI.visibleSlotCount` 控制；当前测试为 54 格。列数由 `GridRoot` 的 `GridLayoutGroup.Constraint Count` 控制；PlayerInventory 会在运行时保证容量不低于 UI 格子数。
- 背包抓取是 Pending Move / 表现层状态：按下或选中来源格时 PlayerInventory 数据不变，只有点击 / 释放到有效目标格时才调用 Swap / Move。右键取消、点击无效区域、关闭背包或刷新导致 source 无效时只清状态和视觉，不改数据。
- 左键短按进入常驻抓取，显示鼠标跟随 icon；左键长按（当前约 0.10s）进入临时抓取，释放在格子上移动 / 交换，释放在非格子区域转为常驻抓取。
- 常驻抓取点击背包内部非格子区域取消；点击背包外 / 非 UI 区域打开丢弃二次确认。确认后按 source slot 调用 RemoveStackAt，取消则不删物品。
- 丢弃确认窗口可通过 `InventoryCanvasUI` Inspector 字段绑定正式 UI：discardConfirmPanel / discardConfirmMessageText / discardConfirmButton / discardCancelButton；未绑定完整时使用 runtime fallback。确认窗口打开时锁定背包其他操作。
- 右键菜单按被点击 slotIndex 执行 Equip / Use Tea；装备槽 Unequip 前必须检查背包空位，AddItem 失败时回滚，防止装备消失。
- Tooltip 定位基于 `Root` RectTransform 坐标系：目标在屏幕左半边时显示在右侧，右半边时显示在左侧，并 Clamp 到屏幕内。
- ItemDetailWindow 是纯信息层，不能接收 Raycast；InventoryContextMenu 是可交互菜单，必须接收 Raycast。
- 当前没有背包保存、ItemDatabase、ItemInstance、随机词条或格子位置持久化。


### TeaShop UI (v1)

当前正式茶商店 UI 位于 `SampleScene` 的 `TeaShopCanvas`（Canvas + TMP + Button + Image），不是 F1 Debug，也暂未接 NPC。

关键脚本：
- `TeaShopItemData.cs`：单个商店商品配置（category / teaItem / price / unlocked / sortOrder / description override / giftCost / giftCooldownSeconds）
- `TeaShopCatalogData.cs`：商店商品列表，UI 按分类筛选 unlocked 商品
- `TeaShopCanvasUI.cs`：茶商店总控，负责分类、分页、商品详情、数量、购买、试饮、赠送、金币显示
- `TeaShopItemSlotUI.cs`：商品格显示与点击回调
- `TeaShopCategoryTabUI.cs` 未创建；分类按钮逻辑当前内置在 TeaShopCanvasUI 中

当前 UI 层级摘要：
```
TeaShopCanvas
└─ RootPanel (初始隐藏)
   ├─ TitleText
   ├─ LeftPanel
   │  ├─ CategoryTabs (绿茶 / 红茶 / 花茶 / 特饮)
   │  ├─ PaginationRow (Prev / PageInfo / Next)
   │  └─ ItemListRoot (动态商品格)
   ├─ RightPanel
   │  ├─ EmptyDetailText
   │  └─ DetailPanel (Icon / Name / Description / Price / Owned / Quantity / Buy / Sample / Gift)
   └─ BottomBar (GoldText / CloseButton)
```

行为：
- 每页最多 6 个商品格；商品格按当前分类与 unlocked 动态生成
- 购买：PlayerWallet.TrySpendGold(price × quantity) → 成功后 PlayerInventory.AddItem(teaItem, quantity)
- 试饮：每小时 1 次，直接 PlayerTeaBuffController.TryUseTea(teaItem)，不扣金币，不加入背包
- 赠送：扣 giftCost，增加 TeaShopCanvasUI 内部运行时 affinity，进入冷却；当前不接正式 NPC 好感系统
- 玩家持有数来自 PlayerInventory；金币显示来自 PlayerWallet.OnGoldChanged；持有数显示监听 PlayerInventory.OnInventoryChanged


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
PlayerController / MouseInputGate / FactionComponent(Player) / HealthComponent / PlayerTargeting / PlayerSkillController / PlayerBasicAttackController / PlayerDeathHandler / PlayerRespawnPointTracker / PlayerInventory / PlayerEquipment / PlayerCombatStats / PlayerWallet / PlayerTeaBuffController / PlayerSkillManager / PlayerStatusEffectController / PlayerMitigationVisualFeedback / DamageNumberSpawner / TargetSelectionIndicator / PlayerSkillHudUI(OnGUI Debug)

UI 根结构：
```
UI
├─ SkillCanvas
├─ LevelUI
├─ InventoryCanvas (sortingOrder=1000)
└─ TeaShopCanvas (RootPanel 初始隐藏)
```

SkillCanvas 结构：
```
SkillCanvas
└─ SkillBar (PlayerSkillBarCanvasUI)
   └─ SkillSlotTemplate (隐藏模板)
```

InventoryCanvas 结构摘要：
```
InventoryCanvas
└─ Root
   ├─ InventoryWindow
   │  └─ GridRoot (InventoryGridSlotUI × visibleSlotCount)
   ├─ EquipmentWindow
   │  ├─ CoreSlot / ArmorSlot / AccessorySlot (EquipmentSlotUI)
   │  └─ StatSummary
   ├─ ItemDetailWindow (pure Tooltip, blocksRaycasts=false)
   ├─ InventoryContextMenu (right-click actions)
   └─ DiscardConfirmPanel（可选正式绑定；未绑定时运行时生成 fallback）
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
| Resources/GoldPickup.prefab | 金币拾取物，挂 GoldPickup |
| Assets/Prefabs/UI/TeaShopItemSlot.prefab | 茶商店商品格模板，挂 TeaShopItemSlotUI |
| Resources/UI/DamageNumberPopup*.prefab | 伤害 / 治疗飘字 4 个版本 |
| Resources/VFX/EnemyAoE/CircleAoETelegraph.prefab | CircleAoE 地面圆形提示 |
| Resources/VFX/EnemyAoE/DonutAoETelegraph.prefab | DonutAoE 程序化真环形 Mesh 提示 |

EnemyBase Variant 工作流：
```
EnemyBase.prefab
├─ SkeletonEnemy_Variant.prefab  (无技能，骨头100%+守护核心20%，金币掉落以 Prefab 当前 Inspector 为准)
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
- `CastAttackRoutine()` 使用 try/finally 确保 `StartCooldown()` / `CleanupCast()` 执行；伤害、SFX 或 Guard Resonance 结算异常不应导致读条永久卡在 100%

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
