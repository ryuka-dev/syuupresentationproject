---
description: Unity MCP token saving rules
alwaysApply: true
---

# Unity MCP 使用规则

目标：减少不必要的 MCP 调用和 token 消耗，但不能牺牲修复准确性。

1. 默认不要读取完整 Unity Console、完整 Assets、完整 Scene Hierarchy。
2. 读取 Console 时，默认只读取最近 5 条 error，关闭 stack trace。
3. 如果 5 条 error 不足以判断问题，可以先说明原因，再扩大到最近 10～20 条，或临时开启 stack trace。
4. 不要主动扫描整个 Assets 文件夹。先用文件名、类名、报错信息定位相关脚本。
5. 不要主动读取完整 scene hierarchy。只有在场景、Prefab、UI、组件引用、物理、Animator 等问题需要时，才读取相关 GameObject 或局部层级。
6. 修改代码前，优先用文件搜索定位目标脚本，只读取必要文件。
7. 优先直接编辑相关 C# 文件；Unity MCP 主要用于刷新 Unity、读取少量错误、运行指定测试、检查必要的对象/组件状态。
8. 遇到编译错误时，先读取最新 error，不要读取全部日志。
9. 不要连续反复调用同一个 MCP 工具。如果一次结果不足，先总结已知信息，再决定下一步最小必要调用。
10. 如果某次操作可能返回大量内容，先请求更小范围、过滤条件或摘要。