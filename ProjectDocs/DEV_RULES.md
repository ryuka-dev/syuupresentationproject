# DEV_RULES

最后更新：2026-05-28

修改任何代码前必须阅读本文件。以下规则不可违反。

---

## 1. Unity / Input / 基础规则

- 当前项目使用 Unity 6（6000.4.3f1）。
- Rigidbody 速度使用 rb.linearVelocity，不使用 rb.velocity（Unity 6 已弃用）。
- 使用 New Input System（Mouse.current / Keyboard.current），不要新增旧 UnityEngine.Input / KeyCode 逻辑。正式或临时测试输入也必须走 New Input System。
- 不要随意重命名 public / SerializeField 字段，Inspector 可能已绑定序列化数据。
- 不要重构与本次任务无关的代码。
- 不要扫描完整 Assets 文件夹、完整 Console 或完整 Scene Hierarchy。
- 修改前先定位相关文件，只读取必要内容，优先小步修改，每次改动后确认编译通过。

---

## 2. Player 移动 / 相机

- Player 移动由 PlayerController 负责，使用相机水平 forward / right 为基准。
- RPGCameraController 只负责相机，不直接旋转 Player。
- 鼠标输入属于 UI 还是场景的判断统一走 `MouseInputGate`，不要在 PlayerController / RPGCameraController / PlayerTargeting 中各自新增 UI 命中判断。
- 左键 / 右键视角拖动、双键前进、左键选目标都必须基于 MouseInputGate 的 World 状态；UI 起始鼠标输入不能触发场景操作。
- 自动前进（R）由 PlayerController 管理：W/A/S/D 任意方向输入和重新形成的场景双键会打断；持续方向输入或双键状态下按 R 时，应由自动前进接管当前输入直到该输入松开。
- 鼠标位于 UI 上时，右键不应触发相机旋转；当前使用 EventSystem RaycastAll 做 UI 命中检测，不要改回单纯 `IsPointerOverGameObject()` 或旧 Input。
- 玩家朝向由 PlayerController 在有移动输入时负责；无输入时 Player 不因相机旋转转身。
- 技能执行瞬间面向目标必须走 `PlayerCombatFacingController`；不要在技能脚本中散写 `transform.LookAt()` / `transform.rotation`。
- `PlayerController` 必须尊重 `PlayerCombatFacingController.IsFacingLocked` 与 `LockedFacingRotation`；不要新增绕过该锁定的 Player rotation 覆盖。
- 鼠标视角旋转使用 Mouse.delta * rotationSpeed，不要再乘 Time.deltaTime。
- Tab 目标选择属于 PlayerTargeting；不要另建第二套目标系统与 CurrentTarget 抢控制权。
- 玩家死亡时需要清空目标（PlayerTargeting.ClearTarget()）并禁用相关控制组件。

---

## 3. Player Skill System

- 玩家技能输入统一走 PlayerSkillManager，不要在其他脚本单独读键盘分发技能。
- PlayerSkillManager.skills 的 Inspector 顺序决定 Canvas 技能栏显示顺序。
- Slot1 Basic Attack 与 Slot4 Area Attack 由 PlayerBasicAttackController 执行，共享基础攻击冷却。
- Slot5 Radiant Riposte 由 PlayerGuardCounterController 执行。
- 玩家战斗动画统一经 `PlayerCombatAnimationController` 请求；不要在技能脚本里直接硬编码 Animator Trigger / State。
- 当前 Radiant Riposte 动画只做表现，不使用 Root Motion / Animation Event，不要把伤害结算移入动画事件，除非任务明确要求。
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
- `EnemySkillController.CastAttackRoutine()` 必须在异常时也清理读条状态；不要移除其 try/finally，避免 Boss 读条卡在 100%。
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
- Guard Resonance / 防御成功 SFX 失败不得中断战斗结算；音效播放必须保持 try/catch 或等价安全保护。
- HikariSupportController 的 `LastBurdenDelta` / `LastBurdenReason` / `LastBurdenChangeTime` 只记录明确事件变化；自然恢复不要刷 UI 提示。
- HikariSupportController 代码层旧变量名（currentBurden 等）暂不重命名，避免 Inspector 序列化丢失。

---

## 6. UI / Debug

- OnGUI 只作为 Debug / Developer Menu（F1 Debug UI）。
- 正式 UI 使用 Canvas + TMP + Button 或 UI Toolkit。
- `HikariCombatStatusUI` 是正式 Hikari 战斗 UI，不是 Debug UI；不要恢复旧的 runtime 全代码生成 UI，也不要把它放进 F1 OnGUI。
- 正式背包 UI 位于 `UI/InventoryCanvas`，不要把 F1 Debug 背包当正式 UI 修改。
- `TeaShopCanvas` 是正式茶商店 UI，不能用 F1 Debug 或 OnGUI 实现；当前 T 键仅为临时测试入口，必须使用 New Input System，NPC 接入后再移除或隐藏。
- 茶道具属于 ItemData.Tea，可进入 PlayerInventory；金币属于 PlayerWallet，不是 ItemData，不进入背包。
- 茶 Buff 不应影响 Hikari 光负荷、玩家攻击力、减伤、HP 或技能系统；金币掉落不受茶 Buff 影响。
- `InventoryCanvas` 需要压住 HUD：Canvas sortingOrder 当前为 1000；内部窗口置顶用 `SetAsLastSibling()`。
- `ItemDetailWindow` 是纯 Hover Tooltip：不要加回 TitleBar / DraggableUIWindow / 操作按钮；必须保持不挡鼠标 Raycast。
- `InventoryContextMenu` 是右键操作菜单：必须接收 Raycast；菜单项执行、点击外部、拖动窗口、关闭背包时应关闭。
- `EquipmentSlotUI` 当前不依赖 SlotLabel / EquippedItem 文本子物体；不要恢复对这些被删除对象的硬依赖。
- `PlayerInventory` 是固定 slot 背包，`null` 表示空格；不要用 `_items.RemoveAt()` / 压缩 List。删除格子内容应设为 null，并通过 OnInventoryChanged 刷新 UI。
- 背包 UI 的移动 / 交换 / 丢弃必须按 slotIndex 操作；不要用 itemId 删除右键点击或丢弃的物品，否则多个同名物品会误删第一个。
- 背包抓取是 Pending Move / 表现层状态：开始抓取时不得修改 PlayerInventory；只有确认放到有效格子或确认丢弃时才改数据。取消、右键、无效区域、关闭窗口只清理状态和视觉。
- `InventoryContextMenuUI.Awake()` 只允许做引用缓存和字段初始化；不要在 Awake 中调用 Hide() 或 SetActive(false)，否则初次 Show 会被 Awake 反向关闭。
- `InventoryCanvasUI` 的 Discard Confirm 可绑定正式 UI，也保留 runtime fallback；按钮 OnClick 由脚本 AddListener，不要在 Inspector 重复绑定确认 / 取消事件。
- 临时诊断日志（如 `DebugRightClickTrace` / `[InventoryRightClickTrace]`）默认应保持 false，除非正在定位具体 bug。
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
- UpperBody Layer 的 `RadiantRiposte` Trigger / `Action_RadiantRiposte` 状态用于守护反击动作；不要误删或改成 Root Motion / Animation Event 驱动。
- UpperBodyIdle.anim 不可删除。
- JumpDown / FallingLoop 到 RunForward / Sprint 的直接 Transition 不可删除。
- IsJumping 是起跳帧信号，不是整个空中状态；不要让它在空中持续 true。

---

## 9. 核心脚本清单（修改前必须确认）

以下脚本改动风险高，修改前请先确认架构关系（见 ARCHITECTURE_REFERENCE.md）：

Enemy：EnemyAI / EnemyWorldManager / EnemySpawnPoint / EnemySpawnArea / EnemyDeathHandler / EnemyPlayerCollisionIgnore / EnemySkillData / EnemySkillController / EnemyCastBarUI / DonutAoETelegraphController
Player：PlayerController / MouseInputGate / RPGCameraController / PlayerTargeting / TargetSelectionIndicator / PlayerSkillController / PlayerBasicAttackController / PlayerGuardCounterController / PlayerCombatFacingController / PlayerCombatAnimationController / PlayerDeathHandler / PlayerRespawnPointTracker / PlayerInventory / PlayerEquipment / PlayerCombatStats / PlayerWallet / PlayerTeaBuffController / PlayerSkillManager / PlayerStatusEffectController / PlayerSkillCanvasUI / PlayerSkillBarCanvasUI / PlayerMitigationVisualFeedback
Inventory UI：InventoryInputController / InventoryCanvasUI / InventoryGridSlotUI / EquipmentSlotUI / ItemDetailPanelUI / InventoryContextMenuUI / DraggableUIWindow / UIWindowBringToFront / TeaShopCanvasUI / TeaShopItemSlotUI
Hikari：HikariSupportController / HikariCombatStatusUI
Combat：HealthComponent / DamageNumberPopup / DamageNumberSpawner / CombatTextSourceLabel / SimpleScreenFeedback
Items：ItemData / ItemStack / PickupItem / GoldPickup / TeaBuffData / TeaShopItemData / TeaShopCatalogData / EnemyDropper
Level：SavePoint
Debug：SkeletonDebugUI / SkeletonSpawner

### 不应修改的文件
- Assets/Blink/（第三方角色资产）
- Assets/SazenGames/（第三方骷髅资产本体）
- Assets/Fonts/09_SourceHanSansSC/OTF/（源字体，不可改）
- Packages/manifest.json

### TMP Dynamic Font Asset Git 问题（已处理，勿回退）

**症状**：Editor 中渲染到尚未烘焙的日文 / 中文字符时，TMP 会把新 glyph 烤进图集并将资产标脏。
保存时整张 glyph 表重写，git 每次存一份完整快照。

**造成的实际后果**：
`SourceHanSansJP-Regular SDF.asset` 被提交 27 次、`-Bold` 26 次，历史上单版本达 33.4MB，
`.git` 因此膨胀到 475MB。

**为什么不能用 .gitignore 排除**：
该 `.asset` 除 glyph 缓存外还包含 face info、材质引用、fallback 链等真实配置，
且被全部 UI 通过 GUID 引用。排除后从干净仓库 clone 会导致全项目字体丢失。

**当前处理方式**：两个 JP SDF 资产已标记 `skip-worktree`。
文件仍在版本控制中（clone 正常），但 git 不再跟踪本地重烘焙产生的改动。

```bash
# 查看当前被 skip 的文件（前缀 S）
git ls-files -v | grep '^S'

# 确实需要提交字体配置变更时（改了 fallback / 材质 / sampling size）：
git update-index --no-skip-worktree "<path>"
git add "<path>" && git commit
git update-index --skip-worktree "<path>"
```

注意：`skip-worktree` 是本地设置，不随仓库分发。换机器或重新 clone 后需要重新执行。

**根治方案（时间允许时）**：把图集模式从 Dynamic 改为 Static 并预烘固定字符集。
当前 `m_AtlasPopulationMode: 1`（Dynamic）、`m_ClearDynamicDataOnBuild: 1`
—— 即 Unity 打包时本就会丢弃这些数据，它们没有提交价值。
