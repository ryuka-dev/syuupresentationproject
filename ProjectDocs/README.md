# ProjectDocs

このフォルダはプロジェクト開発ドキュメントの置き場です。
未来の AI が開発を引き継ぐ際は、以下の順序で読むことを推奨します。

## 推荐阅读顺序

1. **PROJECT_STATE.md**
   - 当前项目状态摘要。未来 AI 首先阅读。

2. **DEV_RULES.md**
   - 修改代码前必须阅读。记录不能违反的开发规则与重要绑定约定。

3. **GAME_DESIGN_NOTES.md**
   - 长期游戏设计方向。不是当前实现状态。

4. **GLOSSARY.md**
   - 术语统一。Hikari / 技能 / 数值单位命名以此为准。

5. **BALANCE_BASELINE.md**
   - 当前 Tier 1 数值基准。PDU / PHU / BU 换算表与遭遇预算。

6. **ARCHITECTURE_REFERENCE.md**
   - 需要理解详细脚本架构、调用关系、特殊规则时阅读。

7. **DEBUG_GUIDE.md**
   - 需要用 F1 Debug UI 或进行测试流程时阅读。

8. **CHANGELOG_ARCHIVE.md**
   - 需要查看历史变更记录时阅读。

---

## 注意事项

- `GAME_DESIGN_NOTES.md` / `GLOSSARY.md` / `BALANCE_BASELINE.md` 为独立文档，不要将其内容合并进 `PROJECT_STATE.md`。
- `PROJECT_STATE.md` 只记录当前实现状态摘要，不是聊天总结，也不是完整架构文档。
- `BalanceTables/` CSV 文件位于项目根目录，与 `ProjectDocs/` 并列。
