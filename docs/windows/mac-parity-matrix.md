# Windows 与 macOS 功能对照底稿

核验日期：2026-10-09
macOS 基线：`origin/main`，`ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 来源：当前 PR 分支 `feature/windows-mac-parity`；其历史迁移基线为 `feature/windows-preview` 本地 `d500bbb`（远端为 `64f2b89`）。本分支只迁移 `windows/` 和 `docs/sources/windows-*`，未迁移 Mac 文件。
状态：`完全一致`、`功能存在但行为不同`、`部分实现`、`缺失`、`平台客观限制`、`尚未验证`。

## 数据底稿与来源清单

| 数据 | 来源及范围 | 本次记录 | 限制 |
|---|---|---|---|
| 当前 Mac 行为 | GitHub `origin/main`，以上 SHA；PR #1–#4 均已合并 | 本表逐项记录 UI、配置、字幕和浮层的源码位置 | 只代表该提交，不代表之后尚未拉取的远端更新 |
| Windows 实现 | 当前 PR 分支 `windows/`；历史迁移来源见上文 | 文件清单、现有能力、待对齐行为 | 没有运行 UI 或真实音频硬件 |
| Windows 检查底稿 | `docs/sources/windows-*` | 保留原有构建、核心检查和签名包记录 | 历史测试结论只适用于底稿注明的代码版本 |
| AI 总结呈现 | Mac `EchoMacApp.swift`、`TranscriptViews.swift` | A29 记录 Markdown 分块、默认展开、收起/复制与 Windows 合成验证 | 尚无 GUI 视觉或剪贴板实测 |
| Soniox 多响应处理 | Mac `SpeechViewModel.handleSonioxMessage` 等生产路径 | A30 固定合成序列覆盖 Windows 五个响应后的字幕状态与结束回调 | 期望值按 Mac 源码静态推导；没有在 Mac 运行生产 handler |
| 默认主题与切换顺序 | Mac 默认深色，`AppThemeMode.allCases` 为浅色、深色、系统 | Windows 新配置/缺省字段默认深色，设置项和循环顺序与 Mac 一致；显式保存值保留 | 静态源码及 CoreChecks 通过；系统外观 GUI 尚未实测，见 A31 |

本次核验前已执行 fetch 并以最新 `origin/main` 建立独立分支；没有把旧混合分支合并进来。旧分支曾含 Mac 源码提交，因此不得整体 cherry-pick 或合并。详细测试证据见 [testing.md](testing.md) 与 `docs/sources/windows-*`。

## 逐项对照

| Mac 行为 | Windows 当前实现 | 状态 | 证据与下一步 |
|---|---|---|---|
| 简洁主窗口：品牌、主题、置顶、字幕优先、底部录音与存档工具 | 单页 WinUI，双语列表、主题、置顶、录音、归档、导出等均存在；设置是独立页面 | 功能存在但行为不同 | `macOS/EchoMacApp.swift`；`windows/Echo.Windows/MainPage.xaml`。主题默认与循环次序已修正，见 A31；A33 UIA 已验证主路径控件和启动/关闭；压缩布局与 Mac 视觉仍未对照 |
| 双语字幕列表；支持选择、纠正、说话人、时间和跨天日期分隔 | 双列字幕，带时间、说话人、检测语言、纠正入口；相邻字幕跨本地日历日期时显示日期分隔行 | 功能存在但行为不同 | `macOS/Views/TranscriptViews.swift`；`windows/Echo.Windows/MainPage.xaml`、`MainPageViewModel.cs`、`Core/Transcript.cs`。字体与紧凑行距未 GUI 对照 |
| 字幕编辑：只保存改动字段；录音继续时其余字段继续更新；确认丢弃、载入最新识别稿、撤销和 AI 建议确认 | 编辑器按 baseline 只提交修改字段；识别稿变化时显示提示，可在未编辑时载入最新文本；关闭有未保存修改时要求确认；人工恢复值仍锁定防止迟到识别覆盖 | 功能存在但行为不同 | Mac `macOS/Views/TranscriptViews.swift`、`SpeechViewModel.swift`、`TranscriptModels.swift`；Windows `MainPage.xaml.cs`、`MainPageViewModel.cs`、`Core/Transcript.cs`。字段锁定/原稿/撤销有核心检查；UI 对话框与正在录音时的交互未实测 |
| 用户滚离底部后停止跟随，并显示“有新内容”按钮 | 新内容追加时保留用户历史位置，显示回到底部按钮；加载时滚至最新条目 | 功能存在但行为不同 | Mac `SynchronizedTranscriptView`；Windows `MainPage.xaml`、`MainPage.xaml.cs`。应通过 UI smoke 检查滚动事件与虚拟化列表交互 |
| 主字幕区可选识别语言及翻译目标，录音中提示下次录音生效 | 主界面提供 Mac 同款语言、自动识别、不翻译选项；ComboBox 显示 `Title` 标签而非对象调试文本；更改配置写入本地并提示下次录音生效 | 功能存在但行为不同 | Mac `macOS/Views/TranscriptViews.swift`、`macOS/Models/TranscriptModels.swift`；Windows `MainPage.xaml`、`MainPage.xaml.cs`、`Core/Preferences.cs`。A33 UIA 检查语言选择显示值；Windows 仍保留“翻译”设置开关，菜单项会同步开关状态 |
| 四个设置分类：常规、识别、分段、AI 服务 | WinUI SelectorBar 切换四类设置，参数修改后保存到本地 | 功能存在但行为不同 | Mac `macOS/EchoMacApp.swift`；Windows `windows/Echo.Windows/MainPage.xaml`、`MainPage.xaml.cs`。控件平台原生，实际键盘和 Narrator 行为未验 |
| 音源：电脑音频、麦克风、混合；授权状态和音频电平 | WASAPI loopback、麦克风及本地混音；设备切换、恢复策略和电平已实现 | 部分实现 | `macOS/EchoMacApp.swift`；Windows `Services/AudioCapture.cs`、`SpeechSession.cs`、`MainPageViewModel.cs`。API 权限表达、声卡和睡眠行为尚未端到端对拍 |
| Soniox 临时字幕更新、最终字幕落定、翻译迟到、双语端点、说话人/语言元数据、强语境经济学/微积分词汇纠正及请求上下文 | Token assembler 对原文和译文分游标；每条响应整体替换 provisional 双语快照；Mac 在响应级记录 `<end>`/`<fin>` 并于该响应处理完后最多结束一行；请求包含 Mac 相同的领域/主题/背景文本、39 个术语和 26 组译词，用户词按 Mac 上限追加 | 功能存在但行为不同 | Mac `SpeechViewModel.swift` 的 `handleSonioxMessage`、`ensureCurrentEntry`、`updateCurrentEntry`、`finalizeCurrentEntry`、`correctEconomicTerms`、`correctEconomicTranslation`；Windows `Core/SonioxRequestBuilder.cs`、`Core/Transcript.cs`、`Services/SpeechSession.cs`、`Echo.CoreChecks/Program.cs`。A30 五响应固定序列检查 Windows 的 provisional 替换、跨响应迟到译文、元数据、时间、speaker split 和双标记端点；期望值按 Mac 源码静态推导，生产 handler runtime 对拍、云端表现仍未验证；见 A25/A26/A30 |
| 设置识别模式、优先语言、严格限制、翻译、目标语言、说话人 | 设置页语言选项、自动识别、不翻译、严格语言和说话人开关；开始会话时冻结翻译行为 | 功能存在但行为不同 | Mac `TranscriptModels.swift`、`EchoMacApp.swift`；Windows `Core/Preferences.cs`、`MainPage.xaml(.cs)`、`MainPageViewModel.cs`。UI smoke 和当前会话不变行为仍需验 |
| Soniox 端点最大延迟、灵敏度、延迟级别、本地静音兜底、超长段兜底 | 按 Mac 默认值和范围保存；Soniox 请求发送三项端点参数；500ms 本地策略以相同词数/时长双阈值兜底，翻译开启时等待译文，语义端点优先 | 功能存在但行为不同 | Mac `macOS/Models/TranscriptModels.swift`、`SpeechViewModel.swift`、`SonioxRequestBuilder.swift`；Windows `Core/Preferences.cs`、`TranscriptSegmentationPolicy.cs`、`SonioxRequestBuilder.cs`、`MainPageViewModel.cs`。核心契约自动检查；见 A24 数据底稿，真实云端行为未验 |
| 归档选择、新建、续录、导出、清空、拆分已完成段 | Mac 载入列表按 `updatedAt` 降序；归档中的多个 segment 按各自开始日期保留 | Windows 按 `updatedAt` 降序载入并在保存后移动更新项；A32 Mac→Windows→Mac 生产归档往返已在 Actions 成功 | 部分实现 | Mac `Storage/TranscriptArchiveStore.swift`、`SpeechViewModel.swift`、archive models；Windows `Core/Transcript.cs`、`MainPageViewModel.cs`、`Echo.CoreChecks/Program.cs`；A32 run `37948130637` 通过，保留合成双段底稿见 A32 文档。仍缺用户历史归档广泛互操作验证；见 A17/A27/A32 |
| 停止后生成 SRT；不默认保存原始音频 | Windows 停止后写 SRT，不保存原始音频 | 完全一致 | Windows `MainPageViewModel.cs`、`Core/Transcript.cs`、`Services/AudioCapture.cs`；格式细节见 A17 底稿 |
| 总结新增内容、当前段或完整归档；Markdown 呈现、展开/收起与复制；可选停止后自动总结；AI 校对和重译建议需人工采纳 | Windows 支持三种范围与停止后自动总结开关；总结 Markdown 按 Mac 标题、小标题、项目符号和段落规则展示，默认展开，可收起/展开和复制；校对请求固定 Mac 模型并要求人工确认，手动优先于待自动任务 | 功能存在但行为不同 | Mac `Services/DeepSeekService.swift`、`SpeechViewModel.swift`、`EchoMacApp.swift`、`Views/TranscriptViews.swift`；Windows `Services/SubtitleCorrectionService.cs`、`MainPage.xaml(.cs)`、`MainPageViewModel.cs`、`Core/TranscriptSummaryMarkdown.cs`。Markdown 分块有合成检查；总结状态反馈、面板可见条件、GUI 排版及实际云端结果仍未对齐/验收；见 A28/A29 |
| 全局透明悬浮双语字幕；位置/大小；锁定；点击穿透；多桌面与全屏空间 | 已实现独立 WinUI 窗口、Topmost、layered/DWM 透明窗口样式、无激活/无边框、点击穿透切换、调整时拖动和缩放、规范化位置/大小保存；显示器选择和全屏应用行为尚未运行验收 | 尚未验证 | Mac `macOS/Views/DesktopSubtitleOverlay.swift`、`macOS/Models/DesktopSubtitleOverlayModels.swift`；Windows `windows/Echo.Windows/DesktopSubtitleOverlayWindow.xaml(.cs)`、`MainPage.xaml(.cs)`、`Core/Preferences.cs`。Windows 显示合成、点击穿透和 DPI 需真实 GUI 验证 |
| 浮层显示临时识别；最终文本按保留时长消失；更新或更正重置倒计时 | Windows feed 直接投影当前 `Subtitle`，50ms 节流；final 后按配置隐藏；迟到译文/文字更正重置计时，相同文字不延长；不创建第二条采集或 Soniox 会话 | 部分实现 | Mac `macOS/Services/DesktopSubtitleOverlayFeed.swift`、`macOS/Models/DesktopSubtitleOverlayModels.swift`；Windows `Core/DesktopSubtitleOverlay.cs`、`ViewModels/MainPageViewModel.cs`、`DesktopSubtitleOverlayWindow.xaml.cs`；核心语义有 6 项自动检查，GUI 对拍尚未运行 |
| API Key 本机保护；录音音频不落盘 | Windows 以当前用户 DPAPI 保护密钥；音频缓冲仅用于流式发送 | 部分实现 | Windows `Core/Preferences.cs`、`Services/AudioCapture.cs`；还需对照 Mac Keychain 的错误恢复和设置更新时机 |
| macOS 桌面音频采集受系统屏幕与系统音频权限约束 | Windows 使用 WASAPI loopback 和麦克风权限 | 平台客观限制 | 权限弹窗、设备默认值和系统环回授权由平台决定；需在 Windows 上清楚显示授权和设备状态 |
| GitHub CI 及正式签名安装 | Windows 有 Release 构建、CoreChecks、历史自签名 MSIX 底稿；公众信任链、正式发布流水线未确认 | 尚未验证 | `windows/README.md`、`docs/sources/windows-a18-*`。签名、干净机器安装升级和回滚需要正式发布配置及设备验证 |

## 最高优先级缺口

1. P0：A30 已补充五响应 Soniox 序列和逐响应 Windows 状态回归；仍需在 Mac 合法构建环境运行生产 handler 获取同序列输出，形成真正的两端 runtime differential。A32 双段 Archive 的 Mac→Windows→Mac runtime 检查在 run `37948130637` 通过；真实用户历史 Archive 仍未覆盖。
2. P1：主界面语言菜单、滚动跟随行为、四类设置和分段配置已接入；A33 完成 12 项启动/控件 UIA smoke，但窄窗口、视觉布局、字幕滚动手感、键盘和 Narrator 仍未实测。
3. P1：悬浮字幕逻辑和窗口已实现；优先完成透明合成、点击穿透、DPI、多屏和全屏应用验收。它复用同一识别会话和当前字幕状态。
4. P2：总结面板已补齐 Mac 风格 Markdown 分块、折叠/展开和复制；继续核验自动总结状态反馈、显示条件、主题和窄窗口布局。
5. 发布：签名证书、CI 构建产物留存和干净 Windows 机器安装升级验证。

当前状态结合源码比对、A33 Windows 调试实例 UIA smoke 和 CI；未做 Mac/Windows 截图视觉对照、浮层真实透明/点击穿透、多 DPI、多屏、真实设备采集或正式签名安装验收。
