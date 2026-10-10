# Windows UI 对齐方案

目标：保留 Windows 原生 WinUI 控件和可访问性，同时把 Mac 的信息层级和交互节奏对齐。Mac 视觉参考固定为 `origin/main` 的 `ae0359dc90da0ccb5e526a275da1747954a49a4f`，不追求逐像素复制 SwiftUI。

## 主窗口

### A80 主界面 Mac 信息层级收敛（2026-10-10）

基于 PR #5 最新头 `b52f68efafd7fd1b0d1671ded4fc3f37c9a701e7`，参考 Mac `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` 的 SwiftUI 实现。下表记录参数来源、改前差异与当前改动；Mac 实际绘制效果仍以用户视觉验收为准。

| 区域 | Mac 源码参数/表现 | Windows PR #5 改前 | 当前 Windows 改动/仍有的差异 |
|---|---|---|---|
| 顶部品牌与工具栏 | 根视图 `.padding(20)`、纵向间隔 18；ECHO 为 caption bold/Mint，右侧浮层/设置/主题/置顶为 borderless 图标按钮 | 主内容边距 `28,12,28,24`、行距 16；ECHO 12 DIP；工具按钮固定 36×36 并带 QuietButton 内边距 | 根边距调整为 20、主行距 18；保留 Windows 标题栏与 36×36 工具按钮及全部入口，保持现有主题 Mint。WinUI 标题栏/图标视觉继续采用平台样式 |
| 语言选择 | 两个等宽菜单，列距 14；subheadline semibold/secondary，小箭头 9 pt/tertiary；录音提示 caption2/tertiary，语言下方 Divider opacity .55 | 两个带 Header 的 ComboBox，默认背景、边框、内边距和最小高度 | Button + MenuFlyout；按钮透明、padding 0×3、min-height 30，文字 14 DIP semibold/secondary、箭头 9/tertiary；录音提示改用 tertiary brush；选中项有勾；“不翻译”始终在目标列。保留设置保存、动态名称与 AutomationId |
| 字幕列表容器 | ScrollView + `LazyVStack(spacing: 0)`；日期头上/下留白 12/4，单条内容上下留白 7，元信息至正文间距 5；仅 Divider opacity .55 | ListView 外 padding `0,16,0,24`；未覆盖 ListViewItem 默认 padding/min-height；数据行 Grid padding `0,8`、行间距 5；区域容器 opacity .55 会连字幕一起变淡 | ListView padding `0,8,0,12`，列表项 Padding/Margin 清零、MinHeight=0、背景透明；每行上下 6 DIP、行间距 4。日期头补 12/4 DIP，保留动态高度和虚拟化；顶部 1 DIP 分隔线单独设 opacity .55，不降低整块字幕对比度；无逐字幕背景卡片 |
| 双栏正文 | HStack 顶部对齐、间距 14；Mac `.font(.body)`、lineSpacing 3；空原文显示省略号，译文留空位 | 两列等宽，间距 14；系统默认正文样式和容器留白 | 两列仍等宽并顶部对齐，间距 14；正文显式 15 DIP，自动换行，空原文显示“…”、空译文保留一行；行间距沿用字体默认；逐字幕底线高 1 DIP、低对比度。正文基线字号和光栅效果待截图核验 |
| Speaker/时间/语言 | caption.monospacedDigit + secondary；时间格式 HH:mm:ss；语言和纠正状态只在需要时显示 | CaptionTextBlockStyle、secondary；元信息间距 8 | 字号 12 DIP、secondary；时间启用 Tabular numeral；保留语言与“已纠正”条件。元信息横向间距收至 7 DIP |
| 纠正操作 | Pencil +“纠正/查看校对”，borderless，caption 色，与元信息同行右对齐 | 每行普通 Button，padding `8,2`、min-height 28，WinUI 默认按钮状态可见 | 透明背景、padding `4,2`、min-width 0、min-height 30；12 DIP 铅笔与文字，右对齐；继承 DefaultButtonStyle 以保留 hover/focus 键盘反馈与逐字幕 AutomationId |
| 录音控制 | HStack 间距 10；音源菜单最小宽 148；同一切换按钮最小宽 126，borderedProminent，录音时红色、空闲 Mint；状态圆点 8、文字间距 7 | 三态 ComboBox 和开始/停止按钮列；WinUI 默认按钮高度/填充，状态最大宽 360 | 轻量三态菜单复用原 Mode 事件，min-width 148；开始/停止统一 min-width 126、min-height 34；开始沿用 Echo Mint，停止用系统 Critical brush；状态仍为 8 DIP 圆点并限制 360 DIP 文本 |
| 存档工具栏 | HStack 间距 10；存档菜单最大宽 260，Spacer 将导出和更多推至右侧 | 四列 Auto/Auto/Auto/*，存档宽限 240；导出、更多实际靠左排列 | 改为 */Auto/Auto；存档按钮横向 Stretch、内容左对齐，导出/更多靠右；三个操作原 AutomationId 和业务处理保留 |
| 波形 | 48 个样本；条宽/间距 3，圆角 2，活动高 `max(2,min(46,3+sample×44))`；容器 52、水平 padding 12、quaternary opacity .35/corner 10 | 48 个样本和 52 高已对齐，但容器用 CardBackgroundFillColorDefaultBrush | 保留 48 点、条宽/间距/高度映射；改为主题化弱表面：深色白色 6% 不透明、浅色黑色 4%、高对比度系统 Window 色；真实合成效果待 GUI 验收 |
| AI 总结 | 轻量标题与右侧总结菜单；内部状态/内容直接可见，正文 ScrollView 最大高 220；面板 padding 12、quaternary opacity .35、圆角 10 | 默认折叠 Expander；scope ComboBox 和“生成总结”仅在展开后可见；主体卡片填充默认卡片色 | 改为始终可见的轻表面和标题右侧 MenuFlyout（三种总结范围）；保留录音提示、状态、最大高 220 的内容滚动、展开/复制与原 ViewModel 调用；范围选择继续由原索引状态接线 |
| 颜色、窗口与主题 | 系统深/浅色语义，Mint；窗口 min 680×520、ideal 820×650，主视图 padding 20 | Windows 已有 Echo 深/浅/高对比主题、Accent 资源与 A48 窗口尺寸 | 沿用现有 Echo 和系统语义资源，不改 macOS；补充低强调表面主题资源并为高对比度映射到系统 Window brush。未通过 GUI 核实各状态下对比度 |

### 全局视觉规范与控件状态

这些颜色以 SwiftUI 语义色为准，而不是固定 RGB。Mac 源码没有给主窗口背景写死十六进制值；字体也使用系统语义样式，因此 Windows 数值是可审阅的对应参数，是否在实际像素上接近仍需同主题、同尺寸截图确认。

| 视觉项 | Mac 源码基准 | Windows 当前实现 | 验收边界 |
|---|---|---|---|
| 主背景 | `WindowGroup` 的系统窗口表面；主题为 dark/light/system，没有自定义窗口底色 | 深色 `#202222`、浅色 `#FAFBF9`；High Contrast 映射系统 Window brush | Mac 与 Windows 的窗口材质/颜色不能从源码数值直接一一换算，需截图对照 |
| 次级表面 | 波形和总结使用 `.quaternary.opacity(0.35)`，圆角 10 | `EchoSubtleSurfaceBrush`：深色白色 6%、浅色黑色 4%；High Contrast 使用系统 Window brush | 语义对齐，实际混色与对比度待深浅主题截图验收 |
| 主文字 | SwiftUI 默认 primary 文本色 | WinUI 系统 primary text brush | 随系统主题；未对比实际显示器像素 |
| 次级/三级文字 | `.secondary` 用于语言标题、元信息和状态；`.tertiary` 用于箭头与录音中提示 | `TextFillColorSecondaryBrush` / `TextFillColorTertiaryBrush`；A81 将语言箭头和提示改为 tertiary | 语义层级明确；高对比度/焦点场景仍待 GUI 检查 |
| 品牌强调色 | SwiftUI `.mint`；录音状态、ECHO 标记和活动波形按语义使用 | 深色 `#94D9BD`、浅色 `#27664F`，High Contrast 使用系统 Highlight brush | Mac Mint 为系统语义色；两端 RGB 不保证相同 |
| 分隔线 | SwiftUI `Divider().opacity(0.55)` | `DividerStrokeColorDefaultBrush`，透明度 0.55 | 需在真实浅/深主题里确认线条强弱 |
| 字体与字号 | 系统字体；标题/菜单使用 `.headline`、`.subheadline`，正文 `.body` + 行距 3，元信息 `.caption.monospacedDigit()`，录音提示 `.caption2` | 系统 WinUI 字体；语言 14 DIP，正文 15 DIP，元信息/状态 12 DIP，数字 Tabular；正文没有自定义 3 DIP 行距 | SwiftUI 点值与 Windows DIP/字体栅格不能仅靠数值等同，正文行高是待截图验证的差异 |
| 圆角与间距 | 主视图 padding 20、行距 18；语言列距 14；波形/总结圆角 10；波形高度 52 | 主视图 margin 20、行距 18；语言列距 14；波形/总结圆角 10、高度 52 | 源码参数已对齐；不同平台控件模板占用空间仍需看截图 |
| Hover / Pressed / Disabled / Focused | SwiftUI borderless、bordered 与 borderedProminent 样式使用 macOS 原生状态反馈 | 语言和纠正按钮基于 `DefaultButtonStyle`，默认背景透明、无边框，保留 WinUI hover/pressed/disabled/focus 视觉状态与键盘焦点 | 源码保留平台状态样式；实际焦点环、悬停反馈和触控目标尚未 GUI 验收 |

`TextFillColorTertiaryBrush` 是 WinUI 官方主题资源，用于比 secondary 更弱的次级文字层级：[Microsoft theming guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming)。

本轮执行 CoreChecks、Release x64 构建及 GitHub PR CI，没有启动窗口。目标附件目录中没有截图文件；先前随消息交付的两张图实际显示邮件撰写/回复界面，不是 Echo，未作为设计参考或复制进仓库。当前没有可用的 Mac/Windows Echo 截图，因此**尚未完成截图视觉对照**，不宣称像素级一致。用户负责视觉评估；等用户检查后再按反馈调整。窗口缩放、键盘/Narrator 和主题对比度等运行态验收也未由本轮 GUI 验证。变更文件哈希、完整验证边界和日志见 [A80 来源底稿](../sources/windows-a80-main-ui-parity-2026-10-10/README.md)。

- 顶栏保留 ECHO、悬浮字幕菜单、设置、主题和置顶，动作数量和位置接近 Mac。A48 将初始窗口从 920×720 DIP 调整为 Mac 理想值 820×650 DIP，最小窗口约束对齐 Mac 的 680×520 DIP，并移除空字幕时常驻的“回到最新”动作；隔离 GUI 在 144 DPI 实测通过，证据见 `docs/sources/windows-a48-main-window-size-2026-10-10.md`。Mac/Windows 截图并排对照仍待完成。
- 主题默认深色；设置项及主界面循环顺序遵循 Mac 的浅色、深色、跟随系统，选择后立即保存并应用。已显式保存的旧设置继续保留。代码与自动检查见 `docs/sources/windows-a31-theme-parity-2026-10-09.md`；实际系统主题 GUI 对照仍待验。
- 中央区域优先给字幕；双列原文/译文，保留说话人、时间、纠正入口和日期分隔。
- 已实现：用户向上查看历史时冻结自动滚动，新字幕到达时出现“有新内容”入口；点击后回到最新字幕。A56 按 Mac 区分用户操作与内容增长，接入 WinUI Direct Manipulation、滚轮和键盘滚动输入；134 项 CoreChecks 与 Release x64 构建通过。触控板惯性、滚轮及 ListView 虚拟化仍待隔离 UI smoke 验收，见 [A56](../sources/windows-a56-transcript-follow-2026-10-10.md)。
- 底部保留音源选择、开始/停止、连接/音频状态、存档选择、新建/导出、更多操作和波形；窄窗口可折叠次要动作。
- A73 为“开始录音”“停止录音”、本地存档列表和实时录音状态名称绑定当前状态文本（“录音状态：…”），并保留 Polite live region。160 项 CoreChecks 检查 XAML 契约，Release x64 构建通过。实际键盘遍历、Narrator/NVDA 播报和 Accessibility Insights 检查仍待隔离 GUI 验收。
- 新建 Archive 默认名遵循 Mac 的“课程 MM-dd HH:mm”本地时间格式；A45 以固定时间通过自动检查。存档选择器、命名和实际界面排版仍需 GUI 对照。
- 设置面板按 Mac 的 520×560 DIP 作为常规目标尺寸；Windows 面板将其作为最大尺寸，窄窗口或短工作区下随窗口收缩，表单留在内部滚动区。A47 修复滚动器在纵向 StackPanel 中无法取得有限高度的问题；隔离 UIA 在 144 DPI 下实测 430×360 DIP 窗口，确认滚动到底时底部控件与“完成”均可见。125%/200% DPI、键盘和 Narrator 仍待 GUI 验收，证据见 `docs/sources/windows-a47-responsive-settings-scroll-2026-10-10.md`。
- 字幕纠正编辑器使用 480 DIP 最大高度的纵向滚动视口，避免长文本和历史版本挤出保存/关闭操作；“识别稿与修改前版本”折叠面板展示原始识别和带本地时间的逐次修订，UI Automation ID 为 `CorrectionHistory`。源码契约与构建通过；动态展开、窄窗口滚动和键盘导航仍待隔离 GUI 验收，见 `docs/sources/windows-a53-correction-editor-scroll-2026-10-10.md`。
- 编辑器可直接添加校对术语，最长 80 个 Unicode 文本元素、100 项上限且大小写不敏感去重；成功后即时持久化并同步设置页列表。控件 AutomationId 为 `CorrectionTerm`、`AddCorrectionTerm` 和 `CorrectionTermNotice`。逻辑及接线源码契约通过，GUI 输入/错误反馈待隔离验收，见 `docs/sources/windows-a55-inline-correction-term-2026-10-10.md`。
- 波形与 Mac 一样显示最近 48 个 RMS 电平样本，20Hz 采样并快速响应声音、缓慢回落；每点高度和静止/活动透明度按 Mac 映射。A40 自动检查通过，实际声卡响应与 GUI 视觉仍待验。
- 总结面板提供新增内容、当前段、整个存档三种范围；按 Mac 条件在自动总结开启、已有总结或存在状态文字时显示。录音中且没有总结时显示提示，状态文字显示在面板内；总结按 Mac 规则呈现标题、小标题、项目符号和段落，并可收起/展开或复制原文。WinUI 展示高度上限为 220 DIP。A41 逻辑检查和 Release 构建通过，窄窗口/深浅主题及真实 GUI 排版仍待验；证据见 `docs/sources/windows-a29-summary-panel-2026-10-09.md` 和 `docs/sources/windows-a41-summary-panel-parity-2026-10-10.md`。
- 默认窗体尺寸与布局根据 Mac 680×520 最小、820×650 理想尺寸做相近的可读性验证，不照搬不可适用的像素尺寸。
- 音源菜单与 Mac 使用相同的三种模式，默认话筒并持久化选择。录音期间菜单仍可用；切换模式复用当前识别会话，“切换设备”仍用于选择当前模式下的具体设备。A51 有设置、路由和 XAML 契约检查；权限弹窗、设备故障回滚和音频连续性仍待实机验收。

## 设置

已按 Mac 的四个可切换分类实现：

1. **常规**：主题、音频设备选择、文字稿及音频保存说明。
2. **识别**：用双项 SelectorBar 选择“自动识别 / 优先语言”；只有优先模式显示语言选择和“仅识别此语言”，严格关闭时显示识别提示说明。切回自动时保留上次优先语言。A71 对齐 Mac 的条件显示和模式语义；键盘焦点、Narrator 和视觉实测仍待验收。
3. **分段**：端点最大延迟、灵敏度、延迟等级、本地静音和超长段兜底；控件范围与 Mac 一致。
4. **AI 服务**：Soniox/DeepSeek Key、校对开关与术语表，并明确哪些文字会发送；实时与 AI 模型固定遵循 Mac 当前配置。

源语言和目标语言已改用选项菜单，不要求用户记忆语言代码。分段参数按 Mac 默认值/范围校验并立即存本机，每次建连冻结一份配置，当前会话不会被设置改动影响；API Key 仍经 Windows DPAPI 加密。Windows 主界面在录音时提示语言修改下次生效。各设置项视觉和辅助技术仍需 GUI 验收。

A33 UIA smoke 确认两种语言菜单显示当前选择，并覆盖设置分类与密钥输入控件。它发现并修正了语言 ComboBox 把 C# 对象调试文本显示为选项标题的问题。完整截图没有保留：应用从 MSIX 包隔离目录载入了既有字幕内容，为避免把本机文字稿写入仓库，只保留不含文字稿的检查结果。Mac 截图视觉对照仍未完成。

## 透明悬浮字幕

Windows 当前分支已实现独立顶层窗口，显示原文与译文两行，可分别显示/隐藏。Mac 默认外观及关键交互：透明背景、白字和阴影、底部居中、宽度占可用屏幕约 75%、非调整状态点击穿透、可拖动调整并记忆位置、可锁定。Windows 设置默认值为 5 秒保留、原文 26 pt、译文 24 pt，字号/透明度/宽度/阴影范围与 Mac 一致；位置采用当前显示区域内的规范化坐标。

原文与译文各自最多显示两行，超出部分在尾部省略。翻译关闭时始终显示原文，即使保存的“显示原文”开关为关闭；这样不会让用户在无译文可显示时得到空白浮层。A76 清除了从活动路径退役的 XAML 浮层实现，改由当前 Win2D/DirectWrite 原生窗口创建两行高的 `CanvasTextLayout`，并以字符粒度在末尾绘制省略号；CoreChecks 现在检查这条实际渲染路径。自动检查不证明真实字体排版、逐像素截断效果或桌面透明合成，仍需隔离 GUI 视觉验收。

字幕内容现在由独立原生 Win32 layered HWND 承载；主界面仍为 WinUI 3。窗口用 Win2D（Direct2D/DirectWrite）渲染预乘 BGRA 像素，再交给 `UpdateLayeredWindow(ULW_ALPHA)` 合成，因此字幕外区域可以保持透明。主菜单可开关浮层、进入/退出调整并独立锁定/解锁位置；外观设置保存保留当前调整状态，隐藏浮层退出调整。设置面板可改两种文字显示、字号、透明度、宽度、保留时间、阴影、点击穿透和位置锁定，也可恢复默认或重置位置。位置记忆显示器设备名与规范化坐标，旧 `DisplayId` 配置继续兼容。

窗口只订阅当前活动字幕 entry：临时识别持续更新；最终文本开始倒计时；有意义的迟到翻译或更正重置倒计时；相同文本更新不重置。禁止为浮层另建录音、音频采集器或 Soniox 连接。

A49 首次在独立包和临时数据目录启动浮层，实际扩展样式缺少 `WS_EX_LAYERED`，黑底遮挡后方内容。A50 已替换为上述原生 layered HWND。隔离 GUI 共 10 项通过：确认 layered/tool/no-activate 样式、主窗口上方、可见、点击穿透、合成双语文字、透明/可见像素、预乘 Alpha，以及调整模式取消点击穿透并在退出后恢复。测试缓冲区为 1920×225，透明像素 418,425、可见像素 13,575，未发现非预乘像素。A58 增加 `SPI_SETWORKAREA` 处理，使任务栏位置或可用工作区变化时重新计算浮层位置；消息分支通过 CoreChecks 源码契约验证，真实任务栏变化仍待 GUI 验收。测试未保存桌面截图；缓冲区统计不等同于 Mac 并排视觉验收。多显示器/DPI 切换、全屏和虚拟桌面仍待验证。详细结果和 SHA-256 见 [A50 底稿](../sources/windows-a50-native-overlay-2026-10-10.md) 与 [A58 底稿](../sources/windows-a58-overlay-work-area-2026-10-10.md)。

麦克风权限：A61 对 `UnauthorizedAccessException`、Win32 错误 5、COM `E_ACCESSDENIED` 及包装异常统一给出中文 Windows 麦克风隐私设置路径；设备启动、会话故障和切换路径复用该指引。合成异常检查通过，真实系统隐私开关拒绝/恢复与 UI 展示仍待实机验收。见 [A61 底稿](../sources/windows-a61-microphone-permission-2026-10-10.md)。

实现参考：[UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)、[Win2D alpha modes](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/premultiplied-alpha)、[Win2D](https://microsoft.github.io/Win2D/)。历史 WinUI windowing 与 DPI 参考见前版记录。

## 视觉与无障碍验收

- 深浅色与系统主题；对比度、字体缩放和高对比度主题可读。
- Tab 顺序覆盖所有控件，Narrator 能读出控件用途和字幕语言。
- 不依赖颜色单独表达录音、连接或错误状态。
- 主窗口缩放、窄宽布局、100/150/200% DPI、多个显示器分别验收。
- UI 截图对照必须注明 Windows 版本、分辨率、DPI、主题和窗口尺寸；未运行应用的静态检查不能标作视觉验收通过。

## A83 主字幕对比度回归（2026-10-10）

静态对照 Mac `macOS/Views/TranscriptViews.swift` 的 Divider `.opacity(0.55)` 与 Windows `MainPage.xaml` 后发现，Windows 曾将透明度设在容纳 ListView、空态与新内容提示的父 Grid 上，整块内容会一起变淡。现改为仅让独立的 1 DIP 顶部分隔线使用 0.55 透明度；CoreChecks 检查父容器无 Opacity、透明度仅作用于细线。该检查是静态 XAML 契约，不替代深浅主题及显示器上的视觉验收。未访问用户字幕、录音、API Key 或截图；未启动 Echo；`macOS/` 未改。
