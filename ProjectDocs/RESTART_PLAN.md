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
- [ ] **清理未使用的第三方美术包**
  `Assets/ThirdParty/` 共 217MB：Blink 98MB / SazenGames 64MB / Kevin Iglesias 53MB / SimpleNaturePack 3MB。
  工具已就绪：Unity 菜单 `Tools/资源分析/分析未使用的资源`，
  输出 `ProjectDocs/UNUSED_ASSETS_REPORT.md`。**先看报告再删。**
  注意报告无法检测 `Resources.Load` 等动态加载，删除前再确认一次。
- [ ] **精简字体**
  `Assets/Fonts/` 224MB，思源黑体 JP 导入了 7 个字重，实际只用 Regular + Bold。删除其余 5 个。
- [ ] **重写 git 历史**（`git filter-repo`）
  先完整备份整个项目文件夹再执行。目标：475MB → 50MB 以内。
- [ ] **删除三代重叠的 UI 脚本**
  技能栏：`PlayerSkillCanvasUI` / `PlayerSkillBarCanvasUI` / `PlayerSkillHudUI` — 保留场景实际使用的那个
  血条：`PlayerHealthBar` / `PlayerHealthShieldBarUI` — 保留后者
  格子：`InventorySlotUI` / `InventoryGridSlotUI` — 保留后者
- [ ] **提交一次干净的基线**，打 tag `restart-baseline`

**Phase 0 验收**：`git clone` 一次在一分钟内完成；Unity 打开无 Missing Script 警告。

---

## 4. Phase 1 — Hikari 实体化（第 2–4 周）★ 最重要

### 为什么这是第一优先

当前场景中，Hikari 只以三个对象存在：

```
HikariPanel        ← UI 面板
HikariHUDCanvas    ← UI 画布
HikariTest         ← 空 GameObject，挂着 731 行的 HikariSupportController
```

**这个游戏的核心角色不在游戏世界里。** 她是一个数字和一块 UI。

这意味着「保护她的紧张感」这个核心玩法假设，在 5 周开发中**从未被验证过一次**。
如果这个假设不成立，越早知道越好——现在知道还有 9 周可以调整方向，
11 月才知道就没救了。

### 最小实现范围（严禁超出）

- [ ] Hikari 成为场上实体：**胶囊体 + 临时材质即可**，不做建模、不做动画、不做立绘
- [ ] `NavMeshAgent` 跟随玩家，保持 3～5m 距离
- [ ] 头顶世界空间光负荷条（复用现有 `WorldHealthBar` 的做法）
- [ ] `HikariSupportController` 从 `HikariTest` 迁移到这个实体上
- [ ] 敌人可以仇恨并攻击 Hikari（扩展现有 `EnemyAI` 仇恨系统，不重写）
- [ ] Hikari 被攻击时光负荷加速上升
- [ ] 玩家可以用身体挡在 Hikari 与敌人之间并起到实际作用

### Phase 1 验收（必须真的坐下来玩一次）

问自己一个问题，诚实回答：

> **我有没有因为担心 Hikari 而改变过自己的走位？**

- **有** → 核心成立。进入 Phase 2。
- **没有** → 停下来，先调整机制（提高她被攻击的频率 / 提高光负荷压力 / 让她死亡真的有代价），**不要**进入 Phase 2。

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
  Boss 周期性对 Hikari 释放一个高伤害技能。
  玩家必须打断它，或者挡在中间，或者接受她光负荷飙升。
  → **这个机制是把「保护 Hikari」从设定变成玩法的唯一一步。**
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
| 2026-09-01 | 0 | 提交遗留的死亡动画修复；字体图集 skip-worktree；添加未使用资源分析器 | 分析器尚未在 Unity 中编译验证 |
