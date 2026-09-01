# CLAUDE.md

## 1. 开工第一件事

打开 `ProjectDocs/RESTART_PLAN.md`，做当前 Phase 里**最上面那个未打勾的项目**。

重启期（至 2026-11-30 毕业展示）内，本计划的优先级高于
`GAME_DESIGN_NOTES.md` 的开发顺序表。

开发中冒出来的新想法写进 `RESTART_PLAN.md` §8「以后再说」区，**不当场做**。
每个 Phase 结束时统一评估一次。

> 上一轮开发（2026-04-27～06-01，5 周 223 commits）失败的原因不是能力不足，
> 而是规划写完之后没有人再打开它。`GAME_DESIGN_NOTES.md` §21.2 明确列为
> 「暂不优先」的背包 UI 与装备栏，消耗了整个项目最大的单一开发块；
> §22.1 推荐顺序的第 1、2、3 项一项都没开始。
>
> 这条规则是为了防止同一件事再发生一次。

### 唯一目标

> 一场 90 秒的 Boss 战，玩家会因为没有保护好 Hikari 而失败。

不服务于这句话的工作，一律不做。冻结清单见 `RESTART_PLAN.md` §2。

---

## 2. 文档

| 文档 | 用途 |
|---|---|
| `ProjectDocs/RESTART_PLAN.md` | **先读这个。** 重启期计划与冻结清单 |
| `ProjectDocs/PROJECT_STATE.md` | 当前实现状态摘要 |
| `ProjectDocs/DEV_RULES.md` | 改代码前必读的硬规则 |
| `ProjectDocs/GAME_DESIGN_NOTES.md` | 长期设计方向（不等于当前实现） |
| `ProjectDocs/GLOSSARY.md` | 术语统一，Hikari / 技能 / 数值命名以此为准 |
| `ProjectDocs/BALANCE_BASELINE.md` | Tier 1 数值基准，PDU / PHU / BU |
| `ProjectDocs/ARCHITECTURE_REFERENCE.md` | 详细脚本架构与调用关系 |
| `ProjectDocs/DEBUG_GUIDE.md` | F1 Debug UI 与测试流程 |

---

## 3. 改代码前

1. 先用文件名 / 类名 / 报错信息定位目标脚本，只读必要文件。
2. 不要主动扫描整个 `Assets/`，不要主动读完整 scene hierarchy。
3. 改动前确认该状态的**唯一所有者**是谁（见 `ARCHITECTURE_REFERENCE.md`）。
4. 优先直接编辑 C# 文件；Unity MCP 用于编译验证、读少量错误、检查必要的对象状态。
5. 一次提交只做一件事。不要把清理、重命名、功能、重构混在一起。

---

## 4. Unity MCP 使用规则

当前使用 Unity 官方 MCP（默认安全设置）。

### 读日志

- 默认只读**最近 5 条 error**，`includeStackTrace: false`。
- 5 条不足以判断时，先说明原因，再扩大到 10～20 条或临时开 stack trace。
- 空 console 不等于编译通过，可能只是被清空过。要确认编译，用 `Unity_RunCommand`
  引用目标类型——能编译通过本身就是证明。

### Unity_RunCommand

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand      // 类名必须是 CommandScript
{                                                // 必须是 internal，不能 public
    public void Execute(ExecutionResult result)
    {
        result.Log("...");
    }
}
```

默认设置下**已确认被禁止**的操作（不要再试）：

| 操作 | 结果 |
|---|---|
| `System.Reflection.*` | 拒绝：unauthorized namespaces |
| `AssetDatabase.DeleteAsset` | 拒绝：User interactions are not supported |

需要删除资源时，改用 `git rm` 连同 `.meta` 一起删，然后
`AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` 让 Unity 同步，再读 console 确认无 error。

### 通用

- 不要连续反复调用同一个工具。一次结果不足时，先总结已知信息，再决定下一步最小必要调用。
- 可能返回大量内容的操作，先请求更小范围或摘要。

---

## 5. 本仓库的特殊约定

### TMP 字体图集（重要）

`SourceHanSansJP-Regular SDF.asset` 与 `-Bold SDF.asset` 已标记 `skip-worktree`。
它们是 Dynamic 图集，磁盘体积会在约 1MB（已清空）与约 34MB（完全烘焙）之间摆动，
历史上曾被提交 27 次。**git 看不到对它们的改动是故意的，不是 bug。**

确需提交字体配置变更时的完整步骤见 `ProjectDocs/DEV_RULES.md`。

### 资源删除

删除前先跑 `Tools/资源分析/分析未使用的资源`（`Assets/Editor/UnusedAssetAnalyzer.cs`），
生成 `ProjectDocs/UNUSED_ASSETS_REPORT.md`。

报告的已知盲区，**不能按报告盲删**：

- `Resources.Load` / 字符串路径动态加载的资源
- Editor 工具（靠菜单与快捷键工作，依赖图看不到，如 `Assets/Rowlan/Fullscreen`）
- 第三方包内的 `.cs`（脚本不以资源形式被引用，删除有编译连带风险）
- LICENSE 文件（思源黑体为 SIL OFL，许可证必须随字体保留）

### 其他

- `Assets/Resources/` 下的内容**无条件进包**，不参与依赖裁剪。做资源分析时必须视为根节点。
- 大量美术资源走 Git LFS，见 `.gitattributes`。
- 第三方资源路径含空格（如 `Assets/ThirdParty/Kevin Iglesias/`），
  shell 循环需用 `find -print0` / `while IFS= read -r -d ''`，否则会被拆断。
- 提交信息用中文，与现有历史保持一致。
