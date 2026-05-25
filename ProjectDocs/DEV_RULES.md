# DEV_RULES

最后更新：2026-05-25

修改任何代码前必须阅读本文件。以下规则不可违反。

---

## 1. Unity / Input / 基础规则

- 当前项目使用 Unity 6（6000.4.3f1）。
- Rigidbody 速度使用 rb.linearVelocity，不使用 rb.velocity（Unity 6 已弃用）。
- 使用 New Input System（Mouse.current / Keyboard.current），不要新增旧 UnityEngine.Input 逻辑。
- 不要随意重命名 public / SerializeField 字段，Inspector 可能已绑定序列化数据。
- 不要重构与本次任务无关的代码。
- 不要扫描完整 Assets 文件夹、完整 Console 或完整 Scene Hierarchy。
- 修改前先定位相关文件，只读取必要内容，优先小步修改，每次改动后确认编译通过。

---

## 2. Player 移动 / 相机

- Player 移动由 PlayerController 负责，使用相机水平 forward / right 为基准。
- RPGCameraController 只负责相机，不直接旋转 Player。
- 玩家朝向由 PlayerController 在有移动输入时负责；无输入时 Player 不因相机旋转转身。
- 鼠标视角旋转使用 Mouse.delta * rotationSpeed，不要再乘 Time.deltaTime。
- Tab 目标选择属于 PlayerTargeting；不要另建第二套目标系统与 CurrentTarget 抢控制权。
- 玩家死亡时需要清空目标（PlayerTargeting.ClearTarget()）并禁用相关控制组件。

---

## 3. Player Skill System

- 玩家技能输入统一走 PlayerSkillManager，不要在其他脚本单独读键盘分发技能。
- PlayerSkillManager.skills 的 Inspector 顺序决定 Canvas 技能栏显示顺序。
- Slot1 Basic Attack 与 Slot4 Area Attack 由 PlayerBasicAttackController 执行，共享基础攻击冷却。
- Slot5 Radiant Riposte 由 PlayerGuardCounterController 执行。
- 不要把攻击伤害、AOE 搜索、反击执行逻辑塞回 PlayerSkillManager。
- 不要恢复已删除的 PlayerMitigationController。
- DamageReduction / AttackPowerMultiplier / HealingReceivedMultiplier 由 PlayerStatusEffectController 统一处理。
- SkillSlotTemplate 是隐藏模板，不是具体技能格；不要把它当具体技能格修改。
- 当前 Skill_WhirlwindSlash.asset 保留但未注册到 PlayerSkillManager.skills，不要误加回去。

---

## 4. Enemy

- EnemyAI 正常移动优先由 NavMeshAgent 主导；Rigidbody 主要用于碰撞 / 物理辅助 / fallback 兼容。
- Chase 中「目标点暂时不可达 ≠ Agent 失效」：不要因路径更新失败切 Rigidbody，应继续追旧 path / last valid destination 并持续重试。
- 只有 Agent 本身不可用时才 fallback 到 Rigidbody Chase。
- OnAttackHit() 是 Animation Event 入口，方法名不可随意修改。
- 敌人技能通过 EnemySkillController / EnemySkillData 配置，不要写死进 EnemyAI。
- EnemySkillController.skills 为空 → 无技能 → 不影响普通攻击，必须保持该行为。
- EnemyCastBarUI GUIStyle 必须在 OnGUI() 内懒初始化，不要在 Awake() / Start() 中访问 GUI.skin。
- CastAttack 的 range 只用于开始读条；读条中玩家拉开距离不取消，完成时不因距离失败。
- CircleAoE / DonutAoE 不写入 LastDamageSkillData，不触发 Guard Resonance / Radiant Riposte。
- 创建新敌人：使用 EnemyBase.prefab Prefab Variant，在 VisualRoot 下放模型，不要直接复制 SkeletonEnemy.prefab。

---

## 5. Hikari

- 正式术语以 GLOSSARY.md 为准：光负荷 / Burden，稳定导光 / Stable Channeling，光溢出 / Light Overflow，导光封锁 / Channel Lockdown，导光恢复 / Channel Recovery，守护共鸣 / Guard Resonance，溢光反震 / Overflow Counter。
- 不要把 Hikari 写成普通 MP、体力或奶妈血条。
- Guard Resonance 不治疗玩家，只降低 Burden。
- Overflow Counter 是光溢出区间的危险收益，不是普通反伤。
- Guard Resonance 只识别 EnemySkillType.CastAttack；普通攻击、CircleAoE、DonutAoE 不触发。
- Guard Resonance 依赖 PlayerSkillManager.RuntimeStates 与 PlayerSkillEffectType.DamageReduction；不要通过 skillId 硬判断 Iron Bulwark / Stone Guard。
- HikariSupportController 代码层旧变量名（currentBurden 等）暂不重命名，避免 Inspector 序列化丢失。

---

## 6. UI / Debug

- OnGUI 只作为 Debug / Developer Menu（F1 Debug UI）。
- 正式 UI 使用 Canvas + TMP + Button 或 UI Toolkit。
- 正式发布前应通过 UNITY_EDITOR || DEVELOPMENT_BUILD 或特殊开关隐藏 Debug 菜单。

---

## 7. Health / HealthComponent 事件约定

- HealthComponent.OnDamaged(float, Transform) 是伤害飘字与 Hikari 受伤响应的基础事件，不要随意改签名。
- 带来源名的伤害通过 TakeDamage(float, Transform, CombatTextSourceLabel) 与 LastDamageSourceLabel 传递，不改 OnDamaged 签名。
- HealthComponent.OnHealed(float, Transform) 负责治疗飘字；治疗逻辑应通过 Heal() 触发，不要由 UI 或 Spawner 直接改血。
- EnemySkillController.LastDamageSkillData / LastDamageSkillTime 当前用于 Guard Resonance 判断 CastAttack。
  普通攻击、CircleAoE、DonutAoE 不应写入该记录，避免误触发 Radiant Riposte。

---

## 8. Animator 修改注意

### 骷髅 Animator
- Attack → Idle 的 hasExitTime=true, exitTime=0.9 不可改。
- IsDead Trigger 由 EnemyDeathHandler 触发。
- OnAttackHit() 方法名不可改（Animation Event 入口）。

### 玩家 Animator
- Base Layer 的 Death 状态无出口过渡，不可添加出口。
- IsDead Trigger 由 PlayerDeathHandler 触发。
- Attack Trigger 只应由当前实际攻击执行路径触发；Slot1 / Slot4 当前由 `PlayerBasicAttackController` 触发。不要把 1 / 4 输入或攻击动画逻辑塞回 `PlayerSkillController`。
- 非锁定 Legacy-like 移动下，移动动画参数保持 Horizontal = 0，通过 Speed = 0/0.5/1.0 表示 Idle/Walk/Run。
- UpperBody Layer 的 Any State → UpperBodyIdle（IsDead 条件）不可删除。
- UpperBodyIdle.anim 不可删除。
- JumpDown / FallingLoop 到 RunForward / Sprint 的直接 Transition 不可删除。
- IsJumping 是起跳帧信号，不是整个空中状态；不要让它在空中持续 true。

---

## 9. 核心脚本清单（修改前必须确认）

以下脚本改动风险高，修改前请先确认架构关系（见 ARCHITECTURE_REFERENCE.md）：

Enemy：EnemyAI / EnemyWorldManager / EnemySpawnPoint / EnemySpawnArea / EnemyDeathHandler / EnemyPlayerCollisionIgnore / EnemySkillData / EnemySkillController / EnemyCastBarUI / DonutAoETelegraphController
Player：PlayerController / RPGCameraController / PlayerTargeting / TargetSelectionIndicator / PlayerSkillController / PlayerBasicAttackController / PlayerGuardCounterController / PlayerDeathHandler / PlayerRespawnPointTracker / PlayerInventory / PlayerEquipment / PlayerCombatStats / PlayerSkillManager / PlayerStatusEffectController / PlayerSkillCanvasUI / PlayerSkillBarCanvasUI / PlayerMitigationVisualFeedback
Hikari：HikariSupportController
Combat：HealthComponent / DamageNumberPopup / DamageNumberSpawner / CombatTextSourceLabel
Items：ItemData / ItemStack / PickupItem / EnemyDropper
Level：SavePoint
Debug：SkeletonDebugUI / SkeletonSpawner

### 不应修改的文件
- Assets/Blink/（第三方角色资产）
- Assets/SazenGames/（第三方骷髅资产本体）
- Assets/Fonts/09_SourceHanSansSC/OTF/（源字体，不可改）
- Packages/manifest.json

### TMP Dynamic Font Asset Git 问题
运行中出现新字符会导致 SourceHanSansSC-Medium_TMP.asset 被重新写入（字符图集更新）。
提交前可选择性地 git checkout 字体资产，或使用 .gitignore 排除。
