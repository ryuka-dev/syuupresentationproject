# RESTART_PLAN

制定日：2026-09-01
截止目标：2026-11-30（毕业展示）
可用周数：**13 周**

---

## 0. 这份文档的地位

本文档在重启期间**优先级高于 `GAME_DESIGN_NOTES.md` 的开发顺序表**。

`GAME_DESIGN_NOTES.md` 依然是设计方向的唯一来源，但「下一步做什么」以本文档为准，直到 2026-11-30。

### 使用方法（这是重启的核心，不是形式）

> 每次开工的第一件事：打开本文档，做当前 Phase 里最上面那个未打勾的项目。

上一轮开发失败的唯一原因不是能力，是**规划写完之后没有人再打开它**。
5 周里，`GAME_DESIGN_NOTES.md` §21.2 明确列为「暂不优先」的背包 UI / 装备栏，
消耗了整个项目最大的单一开发块（05/25–05/27，约 35 个 commit）；
而 §22.1 推荐顺序的第 1、2、3 项一项都没开始。

---

## 1. 唯一目标

13 周结束时，必须能演示这一句话：

> **一场 90 秒的 Boss 战，玩家会因为没有保护好 Hikari 而失败。**

任何不服务于这句话的工作，一律不做。

### 判定标准（可验收，不是感觉）

演示时旁观者必须能看出至少三种不同的失败原因：

1. 没有及时打断 Boss 的关键读条 → 团灭
2. 没有在正确时机开减伤 → 自己被打死
3. 过度依赖 Hikari 治疗 → 她光负荷过载 → 失去治疗 → 死

第 3 条是这个项目存在的理由。**它现在一次都没有被验证过。**

---

## 2. 冻结清单（明确不再碰）

以下系统**保持现状**。能跑就行。发现 bug 也不修，除非它导致 Boss 战无法进行。

| 系统 | 状态 | 处理 |
|---|---|---|
| 背包 UI / 格子 / 拖拽 / 右键菜单 | 已可用 | 冻结，不再调样式 |
| 装备栏 UI | 已可用 | 冻结 |
| 茶商店 / 金币 / 茶 Buff | 原型可用 | 冻结，T 键测试入口保留 |
| 掉落系统 / DropTable | 简易版可用 | 冻结，不做 ScriptableObject 化 |
| 存档 / SaveData / ItemDatabase | 未实现 | **不做**。展示不需要存档 |
| ItemInstance / 随机词条 | 未实现 | **不做** |
| 复苏之光 Revival Light | 未实现 | **不做**，超出 13 周 |
| 三名支援 NPC | 未实现 | **不做** |
| 多场景 / 关卡切换 | 未实现 | **不做**，单场景演示 |
| 自动前进 / 自由视角 | 已可用 | 冻结 |

### 同样不做的：技术债

以下问题**真实存在且已确认**，但修它们不会让你更接近 11 月 30 日的目标。
记录在此，留给毕业展示之后：

- 45 处 `FindObjectOfType` / `GameObject.Find`，散布在 27 个文件
- `Assets/Resources/` 被当作通用资源目录使用（18 个 prefab），应迁移到 Addressables
- `PlayerSkillEffectType` 枚举已含 10 个值，其中有 `// 旧设计` 与 `// 未来预留、未实现`；每加技能需改 `PlayerSkillManager` 的 switch
- `SampleScene.unity` 单场景 31,437 行、251 个 GameObject（历史上已导致 2 次崩溃）
- 伤害飘字使用 Instantiate / Destroy，无对象池
- `SkeletonDebugUI` 939 行，职责过多

**例外**：Phase 0 的仓库清理必须做。475MB 的 `.git` 会拖慢接下来 13 周的每一次操作。

---

## 3. Phase 0 — 止血（第 1 周）

目标：让仓库恢复到可以快速工作的状态。这是唯一值得花时间的「整理」工作。

- [x] **修 SDF 字体图集的提交问题**（2026-09-01 完成，方案见 `DEV_RULES.md`）
  `SourceHanSansJP-Regular SDF.asset` 已被提交 27 次，`-Bold` 26 次，历史上单版本 33.4MB。
  `.gitattributes` 中 `*.asset` 归为 `unity-yaml`（文本），未走 LFS，因此每次 TMP 烘新字形都存一份完整快照。
  → 约 450MB 的 `.git` 来自这两个文件。
  处理：把 SDF 图集加入 `.gitignore`（它是可再生的烘焙产物），或在 `.gitattributes` 中单独指定走 LFS。
- [x] **清理未使用的第三方美术包 / 精简字体**（2026-09-01 完成）
  依据 `Tools/资源分析/分析未使用的资源` 生成的依赖图报告，删除 87 个未引用资源共 264MB。
  报告保留在 `ProjectDocs/UNUSED_ASSETS_REPORT.md`。
  删除后 Unity 重新导入 0 error。
- [x] **回收 LFS 缓存**（`git lfs prune`）：`.git` 475MB -> 212MB。

- [ ] ~~**重写 git 历史**（`git filter-repo`）~~ **不做。**
  原判断有误：475MB 中 434MB 是 `.git/lfs`（LFS 对象缓存），
  真正的 git 历史 `.git/objects` 只有 41MB。
  SDF 字体确实被提交 27 次（所有版本逻辑体积合计约 1.39GB），
  但 git 的 delta 压缩已将其压到 41MB。
  为省下约 30MB 而不可逆地重写历史，风险收益不成立。
  仓库体积问题已由「删除未使用资源 + lfs prune」解决。
- [x] **删除无引用的旧版 UI 脚本**（2026-09-01 完成）
  实际核查结果与最初判断不同：
  - 技能栏三个脚本**不是重复实现**，是模板（`PlayerSkillCanvasUI`）+
    布局器（`PlayerSkillBarCanvasUI`）+ OnGUI 调试浮层（`PlayerSkillHudUI`），均挂载于当前场景，全部保留。
    其中 `PlayerSkillHudUI` 是调试浮层，Phase 3 需与 F1 Debug UI 一并隐藏。
  - 已删除：`PlayerHealthBar.cs`（0 挂载 0 引用，依赖旧 `EntityStats`）、
    `InventorySlotUI.cs` 与 `InventorySlotPrefab.prefab`（仅互相引用）。
  - `EntityStats` 保留：仍被 `EnemyStateMachine` 引用。
- [x] **打 tag `restart-baseline` 并推送到 origin**（2026-09-01 完成）

**Phase 0 验收（已通过）**：Unity 编译 0 error；`Assets/` = 254MB。

> **Phase 0 完成。下一步进入 Phase 1：Hikari 实体化。**
> 注意 Phase 1 的验收问题不能跳过、不能「差不多算过了」——
> 它是本次重启唯一真正要回答的问题。

### Phase 0 遗留

- 两个 JP SDF 图集在磁盘上会在约 1MB（已清空）与约 34MB（完全烘焙）之间摆动。
  `skip-worktree` 已阻止其进入仓库，但工作区体积仍受影响。
  根治需将图集改为 Static 模式并预烘固定字符集，见 `DEV_RULES.md`。
- `Assets/Resources/` 下 18 个 prefab 仍会无条件进包，未迁移 Addressables（属已记录技术债）。

---

## 4. Phase 1 — Hikari 实体化（第 2–4 周）★ 最重要

### 为什么这是第一优先

当前场景中，Hikari 以三个对象存在：

```
HikariPanel        ← UI 面板
HikariHUDCanvas    ← UI 画布
HikariTest         ← 人形胶囊：Transform / HikariSupportController(731 行)
                      / BoxCollider / MeshRenderer / MeshFilter(内置胶囊网格)
                      位置 (13.41, 2.09, -10.12)　缩放 (0.45, 1.5, 0.45)
```

她**已经是场上一个人形胶囊**，但站着不动、没有任何状态表现、
战斗中从不进入玩家的注意范围。功能上等同于一个杵在角落的静态道具。

这意味着「保护她的紧张感」这个核心玩法假设，在 5 周开发中**从未被验证过一次**。
如果这个假设不成立，越早知道越好——现在知道还有 9 周可以调整方向，
11 月才知道就没救了。

好消息是：既然胶囊、碰撞体、渲染器和全部 Inspector 绑定都已存在，
Phase 1 是在既有对象上做增量，不是从零搭建。

### 硬约束：Hikari 不可被攻击

**第一版 Hikari 不能被敌人选为目标，不能被伤害，没有 HP。**

依据 `GAME_DESIGN_NOTES.md` §6.2：她一旦成为「会被打死的身体」，
就会立刻带出整串护送问题——她要不要躲 AoE、躲错算谁的、
被打死算谁的、寻路卡住怎么办、Boss 战会不会变成护送任务。
这些问题都不是「血条」造成的，是「可被攻击的身体」造成的。

§6.7 定义的循环从来不需要她挨打：

```text
玩家失误 / 过度承伤 → Hikari 治疗 → 光负荷上升
玩家正确开减伤承受关键攻击 → 守护共鸣 → 光负荷下降
```

**光负荷上升的原因是「你挨打了」，不是「她挨打了」。**
保护她的方式是打得好、少需要她，不是站在她前面。

### 光负荷显示在哪

- **不在她头顶放条。** 头顶条会把玩家视线钉在她的位置上，
  制造出「一直看着她」的保姆感——正是 §6.2 要避免的。
- 精确数值留在玩家 HUD（`HikariCombatStatusUI` 已实现：
  状态 / 动作 / 治疗读条 / 光负荷 / 变化提示）。
  光负荷在功能上是**玩家的资源**，输入完全来自玩家表现，放在玩家 HUD 才符合它的本质。
- 她本人只表现**离散阶段**（§6.5），从外观读，不用 UI：

  | 阶段 | 视觉 |
  |---|---|
  | 稳定导光 0–79% | 平稳的柔光 |
  | 光溢出 80–99% | 光变强、开始不稳定地闪 |
  | 导光封锁 100% | 光熄灭 / 变成错误的颜色 |

  光负荷系统被设计成**危险反应炉**（§6.5）。反应炉的状态是从它的样子读出来的，
  不是从仪表盘读的。这样「注意她」发生在余光里，而不是盯条。

### 实现方式：原地扩展 HikariTest，不要新建 GameObject

**不要**新建实体再把 `HikariSupportController` 迁过去。直接在现有的
`HikariTest` 上加组件并改名为 `Hikari`。

理由：GameObject 的 fileID 不变，以下绑定**全部零丢失**：

- `playerHealth` → Player 的 HealthComponent（已在场景中绑定）
- `guardianResonanceSfx` / `guardSuccessSfx` → 两个 AudioClip
- `PlayerGuardCounterController.hikariSupport` → 指向她的 SerializeField

新建对象则这三处全部要重绑，且第三处失败时会静默落到
`FindFirstObjectByType` 兜底，不报错但难排查。

### 最小实现范围（严禁超出）

- [ ] `HikariTest` 改名为 `Hikari`，保留现有胶囊网格与临时材质
      （不做建模、不做动画、不做立绘）
- [ ] 加 `NavMeshAgent` + 跟随玩家的行为
- [ ] **把 BoxCollider 换成 CapsuleCollider，并过滤玩家↔Hikari 碰撞**
      （参照现有 `EnemyPlayerCollisionIgnore` 的做法）。
      当前挂的是 BoxCollider 而网格是胶囊，静止时无所谓，
      一旦跟随，方盒会把玩家推来推去。她不应该在物理上妨碍你。
- [ ] `HikariSupportController` 在 `Start()` 里会自动 `AddComponent<AudioSource>()`。
      **把 `spatialBlend` 设为 0（2D）**，否则守护共鸣与防御成功这两个反馈音
      会随她的距离忽大忽小。
- [ ] 敌人的目标收集**排除** Hikari；不给她加 `HealthComponent`
- [ ] 光负荷三阶段的发光表现（自发光材质改色 / 改强度即可，不做 VFX）
- [ ] 确认既有的守护共鸣降低光负荷的下行路径在场上能被感知

**明确不做：治疗射程。** 当前 `HikariSupportController` 全文没有任何
`Vector3.Distance`，她无视距离治疗玩家。保持现状。
加射程等于要求玩家管理与她的距离，会把位置管理从后门放回来。

跟随距离是**演出问题不是安全问题**——她不可被攻击，跟远跟近都不影响安全。
唯一目的是让她待在玩家的余光里，好让发光变化能被看到。

她为什么仍然需要身体？因为一个 HUD 上的数字没有人可以保护。
§6.1 写明她是「玩家保护欲的来源」。身体是为了情感，不是为了碰撞。

### Phase 1 验收（必须真的坐下来玩一次）

> **测试必须用 `SkeletonBossEnemy_Variant`。**
>
> 守护共鸣要求攻击者身上有 `EnemySkillController` 且用 `CastAttack` 型技能命中：
>
> ```csharp
> var skillCtrl = attacker.GetComponentInParent<EnemySkillController>();
> if (skillCtrl == null) return false;
> return lastSkill.SkillType == EnemySkillType.CastAttack;
> ```
>
> 实际挂了该组件的只有 `EnemyBase.prefab` 与它的变体 `SkeletonBossEnemy_Variant`。
> 独立预制体 `SkeletonBossEnemy` / `SkeletonEnemy` / `Skeleton_110` **都没有**。
> 拿普通骷髅测会永远触发不了守护共鸣，从而得出「功能坏了」的错误结论。

问自己两个问题，诚实回答：

> **1. 我有没有为了不让她治我，而更早、更谨慎地开减伤？**
> **2. 我有没有为了压低她的光负荷，而主动去接一记读条重击（守护共鸣）？**

- **至少一个「有」** → 核心成立。进入 Phase 2。
- **两个都「没有」** → 停下来调整，**不要**进入 Phase 2。可调的方向：
  - 提高每次治疗的光负荷成本
  - 提高光溢出阶段的实际风险与收益幅度
  - 让导光封锁（治疗停止）真的痛
  - 提高守护共鸣的光负荷回收量，让「主动接重击」值得做

  **不要**通过「让敌人打她」来制造压力——那会直接变成护送任务。

这个验收不能跳过，不能「差不多算过了」。这是整个重启的意义所在。

---

## 5. Phase 2 — Boss 战（第 5–8 周）

对应 `GAME_DESIGN_NOTES.md` §22.1 推荐顺序的第 1、2、3 项——上一轮全部跳过的部分。

- [ ] **Boss 固定时间轴**
  当前 `EnemySkillController` 是随机释放，玩家无法学习和预判。
  改为固定循环，例如：`HeavySlash → 8s → CircleAoE → 6s → MoonRing → 10s → 循环`
  可预测 = 玩家能变强 = 玩家觉得自己聪明（§20 核心原则）
- [ ] **打断技能 Interrupt v0.1**
  新建 `Skill_Interrupt.asset`，绑定 Slot6。
  能打断 Boss 的 `CastAttack`，冷却 20 秒左右。
  这是「识别关键读条」这一层玩法的入口。
- [ ] **针对 Hikari 的 Boss 机制**（§22.1 第 3 项）
  → **这个机制是把「保护 Hikari」从设定变成玩法的唯一一步。**

  **前提：不得让 Boss 直接攻击 Hikari。** 见 Phase 1 的硬约束。
  机制的输入必须仍然是「玩家的表现」，不能变成「玩家的站位护送」。
  两个候选方向，二选一实现：

  - **A. 逼她救你**：Boss 打出一记高伤重击或强 DoT，
    玩家不处理就会被打到濒死 → 她被迫拼命治疗 → 光负荷飙升。
    解法是减伤或打断，不是挡在她前面。
  - **B. 污染她的导光**：Boss 给她上一个 debuff，
    持续 20 秒内她的治疗产生双倍光负荷。这段时间玩家必须自己扛。

  两个方案都不需要她有 HP，也不需要玩家护送。
- [ ] Boss 血量 / 伤害按 `BALANCE_BASELINE.md` 的 PDU / PHU / BU 调到 90 秒左右

### Phase 2 验收

连续打 5 次 Boss，记录每次的死因。如果 5 次死因里出现了至少 2 种不同类型，机制成立。
如果 5 次都是同一个原因死，说明只有一个机制在起作用，需要调整。

---

## 6. Phase 3 — 打磨到可展示（第 9–11 周）

到这里核心已经成立，剩下的是让它**看起来不像原型**。

- [ ] 死亡 UI / 胜利 UI（当前完全没有）
- [ ] 命中反馈：hit stop、敌人受击闪光（`PROJECT_STATE.md` §7 已列为推荐下一步）
- [ ] 音效补齐：Boss 读条、打断成功、Hikari 光负荷警告
- [ ] 场景外观：用已有的 SimpleNaturePack 把测试地形做成一个像样的战斗场地
- [ ] 隐藏 F1 Debug UI（`SkeletonDebugUI` 的 OnGUI，展示时必须关掉）
- [ ] Hikari 换掉胶囊体（如果时间允许；不允许就保持胶囊体，**不要为了这个牺牲 Phase 2**）

---

## 7. Phase 4 — 缓冲与排练（第 12–13 周）

- [ ] **出一个正式 Build**（很多项目死在这一步——Editor 里能跑不代表 Build 能跑）
- [ ] 在别人的电脑上跑一次
- [ ] 录一份完整演示视频作为备份（现场演示崩溃时的保险）
- [ ] 准备演示脚本：先展示什么、如何在 3 分钟内让人看懂核心玩法

**这两周不写新功能。** 如果前面的 Phase 超期，从 Phase 3 砍，不要砍 Phase 4。

---

## 8. 「以后再说」区

开发中冒出来的新想法写在这里，不当场做。每个 Phase 结束时统一评估一次。

- （待填写）

---

## 9. 进度记录

| 日期 | Phase | 完成项 | 备注 |
|---|---|---|---|
| 2026-09-01 | — | 制定重启计划 | 上一轮：5 周 223 commits，核心玩法未验证 |
| 2026-09-01 | 0 | 提交遗留的死亡动画修复；字体图集 skip-worktree；添加未使用资源分析器 | 分析器已在 Unity 中验证通过 |
| 2026-09-01 | 0 | 删除 87 个未使用资源（264MB）；`git lfs prune` | `Assets/` 460MB->254MB，`.git` 475MB->212MB，Unity 0 error |
| 2026-09-01 | 0 | 撤销 `git filter-repo` 计划 | 原判断有误，`.git/objects` 仅 41MB，重写历史不划算 |
| 2026-09-01 | 0 | 删除 3 个无引用 UI 文件；重写 CLAUDE.md；打 tag 并推送 | **Phase 0 完成**，技能栏三脚本经核实非重复实现，保留 |
