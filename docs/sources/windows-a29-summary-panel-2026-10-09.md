# A29 AI 总结面板呈现对齐底稿

核验日期：2026-10-09  
Mac 源码基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`  
范围：总结文本的 Markdown 分块、展开/收起和复制交互；不包含 DeepSeek 服务质量。未启动 Echo、未发起云请求、未使用用户文字稿，也未修改 Mac 源码。

## 来源清单

| 数据项 | 来源 | 记录的行为 | 限制 |
|---|---|---|---|
| 面板可见条件及交互 | `macOS/EchoMacApp.swift`，`summaryPanel`、`isSummaryExpanded`、`copySummaryToPasteboard` | 总结面板在自动总结开启或有总结/状态时显示；有总结时默认展开，提供收起/展开全部和复制原始总结文本 | 源码核对；未运行 Mac 界面 |
| Markdown 分块 | `macOS/Views/TranscriptViews.swift`，`MarkdownSummaryView.parseBlocks` | 支持 `##` 标题、`###` 小标题、`-`/`*` 项目符号、空行分段，其余相邻行合并为段落 | 不处理粗体、链接等完整 CommonMark 语法 |
| Windows 实现 | `windows/Echo.Windows/Core/TranscriptSummaryMarkdown.cs`、`MainPage.xaml(.cs)`、`ViewModels/MainPageViewModel.cs` | 使用同一分块规则；标题/小标题加粗、段落与项目符号可选取；总结默认展开，限制显示高度为 220 DIP，可收起并复制原文 | 自动构建通过；未做 GUI 字体/布局对照 |
| 合成检查 | `windows/Echo.CoreChecks/Program.cs` | 固定 CRLF 输入验证标题、段落合并、小标题、两种项目符号和顺序 | 只检查解析结果，不验证实际渲染像素或系统剪贴板 |

## 验证记录

- Windows CoreChecks：84 项通过；其中一项检查 Markdown 分块结果。
- Windows Release x64 build：0 警告、0 错误。
- 没有启动 Echo、访问 DeepSeek 或复制真实用户内容。
- 后续 GUI 验收仍需检查深浅色、窄窗口、文本选择、键盘和 Narrator。
