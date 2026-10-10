# Windows 与 macOS 功能对照底稿

## A80 主界面信息层级更新（2026-10-10）

| 区域 | Mac 基准 | PR #5 改前 | A80 Windows 当前 | 验证状态 |
|---|---|---|---|---|
| 顶部品牌/工具栏 | 内容 padding 20、纵向 spacing 18；caption bold Mint 品牌；borderless 图标按钮 | 主面板 margin `28,12,28,24`、row spacing 16；36×36 QuietButton | margin 20、row spacing 18；保留 36×36 Windows 图标入口 | 源码参数和 CoreChecks 通过；未做画面对比 |
| 语言选择 | 两列间隔 14；subheadline semibold/secondary，小箭头 9 pt tertiary；录音提示下方显示；divider opacity .55 | 两个有 Header 的默认 ComboBox | 透明 Button + MenuFlyout；min-height 30、padding 0×3；当前选择、勾选、“不翻译”常驻和下次录音提示；保留设置保存与 AutomationId | 静态接线通过；菜单展开和键盘/Narrator 未实测 |
| 日期/字幕列表 | LazyVStack 间距 0；日期标签 top/bottom 12/4；仅 Divider opacity .55 | ListView padding `0,16,0,24`，默认 ListViewItem padding/min-height 与行模板叠加；曾将 opacity 设在整块容器 | ListView padding `0,8,0,12`；容器 padding/margin 0、min-height 0、透明；日期 margin `0,12,0,4`；A83 将 .55 仅施于 1 DIP 顶部分隔线 | CoreChecks/构建通过；真实像素、长列表抖动/滚动仍未 GUI 验证 |
| Speaker/时间戳 | 仅在当前显示配置启用 speaker 且行内标签非空时显示 Speaker；时间始终显示，次级 caption/等宽数字 | 12 DIP secondary，Tabular numeral；A86 令 Speaker 与分隔点跟随当前活动/保存配置及非空字段 | CoreChecks 与 Release x64 编译通过；字体和实际间距仍待截图验收 |
| 双栏正文/分隔线 | 翻译开启时双栏顶部对齐、间距14；关闭时原文占整行；body、lineSpacing3、padding7；低对比度 Divider | 双栏固定平分，未翻译时右侧空白 | 开翻译等宽双栏；关翻译折叠译文、原文 Grid.ColumnSpan=2；间距14、正文15 DIP、自动换行；行上下6；1 DIP 分隔线 .55 | CoreChecks/构建通过；当前会话/下次会话切换行为由静态契约验证，GUI 像素仍待看 |
| 纠正操作 | 元信息行右侧 Pencil + 文字，borderless/secondary | 普通按钮 padding `8,2`、min-height 28 | 透明按钮 padding `4,2`、min-height30；12 DIP 铅笔/文字，保留逐字幕 AutomationId 和焦点状态 | CoreChecks/构建通过；焦点可见态未 GUI 检查 |
| 录音控制 | gap10；音源宽148；同一切换按钮宽126；空闲 Mint、录音 red；8点状态圆点；ViewThatFits 空间不足时换行 | 三态 ComboBox、默认 WinUI 高度/边框、状态固定横排 | 轻量三态菜单保留实时路由；A82 开始/停止统一高34、宽126；A88 按实际内容宽度700 DIP切换状态同行/换行，宽屏边距20、窄屏16 | A88 在144 DPI实测820/680 DIP并以UIA确认；其他DPI及键盘焦点待验 |
| 存档工具栏 | gap10；存档最大260，Spacer 将导出/更多推右 | Auto/Auto/Auto/*，操作靠左 | */Auto/Auto，存档伸展且内容左对齐，导出/更多右靠 | XAML 布局契约通过；窄窗口实际换行未测 |
| 波形 | 48 samples，宽/间距3、圆角2，高52、pad12、quaternary opacity .35 | 48点与52高匹配，默认 CardBackgroundFill | 保留真实48点映射，低强调主题表面深色6%/浅色4%/HC系统色 | CoreChecks/构建通过；真实声卡和合成对照未测 |
| AI 总结 | 轻量面板，pad12、圆角10、quaternary opacity .35；标题右侧总结菜单；正文最大高220 | 默认折叠 Expander，scope ComboBox 与生成按钮隐藏在内容区；卡片默认填充 | 主题低强调表面，标题右侧菜单直接提供三种总结范围；状态、结果滚动、展开/复制保留；A88 按剩余空间约束正文并保留字幕最小高度 | CoreChecks、构建与合成摘要截图通过；其他主题和较长文本滚动待验 |
| 截图对照 | 需要同数据、同窗口/DPI/主题看实际呈现 | 当前用户附件无法用作 Echo 参考 | A88 主窗口深色截图覆盖820×650与680×520 DIP、144 DPI；A90 增加设置页、校对弹窗和空存档截图 | Windows 截图已留底；尚无 Mac 并排图，也未声称像素一致。125%/200% DPI、录音中和长列表滚动截图未完成 |

核验日期：2026-10-10
macOS 基线：`origin/main`，`ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 来源：当前 PR 分支 `feature/windows-mac-parity`；其历史迁移基线 `feature/windows-preview` 当前远端为 `d500bbb21bc9bfa4d811c614576f38c0476efc50`。该旧分支含 Mac 文件变更，本分支只迁移经检查的 Windows 内容与 Windows 专属证据，未迁移 Mac 文件。
状态：`完全一致`、`功能存在但行为不同`、`部分实现`、`缺失`、`平台客观限制`、`尚未验证`。

## 数据底稿与来源清单

| 数据 | 来源及范围 | 本次记录 | 限制 |
|---|---|---|---|
| 当前 Mac 行为 | GitHub `origin/main`，以上 SHA；PR #1–#4 均已合并 | 本表逐项记录 UI、配置、字幕和浮层的源码位置 | 只代表该提交，不代表之后尚未拉取的远端更新 |
| Windows 实现 | 当前 PR 分支 `windows/`；历史迁移来源见上文 | 文件清单、现有能力、待对齐行为 | A33/A47/A48/A49/A50 已做选定窗口与控件 UIA/Win32 实测；并非完整 GUI 对照，真实音频硬件未测 |
| Windows 检查底稿 | `docs/sources/windows-*` | 保留原有构建、核心检查和签名包记录 | 历史测试结论只适用于底稿注明的代码版本 |
| AI 总结呈现 | Mac `EchoMacApp.swift`、`TranscriptViews.swift` | A29 记录 Markdown 分块、默认展开、收起/复制与 Windows 合成验证 | 尚无 GUI 视觉或剪贴板实测 |
| Soniox 多响应与控制帧 | Mac 对字符串 `error_message` 立即报错并返回；仅布尔 `finished: true` 才结束；转写字段错误类型安全忽略 | A35 七响应、A42 十三响应、A43 十八响应 production differential；A44 补无 error_code 错误和错误类型 finished 的 loopback 测试 | A35/A42/A43 production differential 和 A44 的 Mac/Windows/Archive CI 全通过；真实云响应仍未覆盖 |
| Soniox 本地静默后备计时 | Mac 在错误/finished 早退后，对每条有效转写响应更新时间，包括空 token 响应和仅端点标记响应 | Windows 现按每条非错误、非 finished 的有效响应更新时间；不要求响应含普通语音 token | 之前仅普通文本 token 会刷新 Windows 计时，空响应期间可能比 Mac 提前本地切句；A39 已修正并以四类固定 JSON 检查 | 纯逻辑与 Release 构建已验证；真实服务空响应节奏和长时间静默仍未验 |
| 录音电平与波形 | Mac 对转换样本算 RMS 并乘 7.5，20Hz 快攻慢放平滑，保留 48 个样本并逐点绘制 | Windows 按每路有效重采样样本算 RMS，采用相同放大、20Hz 平滑系数和 48 点波形；已移除与真实声音无关的正弦条形动画 | A40 以合成样本检查 RMS、攻击/回落、历史容量和重置 | 纯逻辑与 Release 构建通过；真实设备电平响应和视觉波形仍待 GUI/设备验收 |
| 双路 PCM 混音欠载行为 | Mac `PCM16TimelineMixer` 与 Windows `AudioCapture.ReadFrame` / `AudioFrameMixer` | A36 对照发现并修正 Windows 欠载时固定双路除数导致的音量衰减；97 项 CoreChecks 与 Windows Release 构建通过 | 确定性样本检查不替代真实 WASAPI 设备、时钟漂移及长时间采集验收；Mac 源码基线和 Windows 代码版本见 A36 |
| 浮层语言可见性与长文本 | Mac 翻译关闭时即使“显示原文”开关关闭也显示原文；每路字幕最多两行并尾部截断 | Windows 按 Mac 规则计算可见行；活动的 DirectWrite 渲染路径用两行高度的 `CanvasTextLayout`、字符级裁切和省略号绘制原文/译文/阴影 | A38 对齐可见行；A76 发现旧检查只覆盖已弃用 XAML，已移除旧浮层并改为检查原生 HWND 当前绘制路径。166 项 CoreChecks、Release x64 构建通过 | 静态源码与构建已验；真实字体布局、逐像素尾部省略及透明合成仍未在当前渲染器上 GUI 验收；见 A76 |
| Soniox WebSocket 鉴权 | Mac `SpeechViewModel.openSonioxSocket` 仍把 `api_key` 放在起始配置 JSON；Windows 在握手发送 Bearer header，并从配置 JSON 排除密钥 | Windows 按 Soniox 当前推荐方式实现；保留 Mac 当前行为，不反向降级 Windows | 功能存在但行为不同 | 本地模拟 WebSocket 检查验证 Bearer header、配置不含密钥、握手后 401/402/403/429/503/413 错误分类；官方旧方式迁移时间及源码证据见 [A37](../sources/windows-a37-soniox-auth-protocol-2026-10-10.md) |
| 默认主题与切换顺序 | Mac 默认深色，`AppThemeMode.allCases` 为浅色、深色、系统 | Windows 新配置/缺省字段默认深色，设置项和循环顺序与 Mac 一致；显式保存值保留 | 静态源码及 CoreChecks 通过；系统外观 GUI 尚未实测，见 A31 |
| 跨显示器悬浮字幕布局与 DPI 变化 | Mac 监听显示器配置变化，按目标屏幕重新计算透明字幕窗位置和宽度，并恢复已保存的屏幕位置 | Windows 按设备名记住上次屏幕；处理 `WM_DISPLAYCHANGE`、`WM_DPICHANGED` 与 `SPI_SETWORKAREA`，重新计算物理像素矩形；拖动/缩放使用屏幕坐标 | A34 的 100/150/200% 合成布局与 A58 消息接线检查通过；真实异 DPI 显示器、拔插、任务栏/工作区变化仍待 GUI 验收 |
| 音源模式：电脑音频、话筒、混合；录音中热切换与默认设备变化 | Mac 默认话筒并保存模式；录音中按需启动新来源，成功后再移除不需要的来源；授权/启动失败时保留旧模式和会话 | Windows 默认并持久化话筒；录音中先预启动目标 WASAPI 来源，成功后暂停发送、排空旧采集尾部、清除重叠预热样本并提交新来源，不重建 Soniox WebSocket 或当前字幕段；预启动失败时旧源持续采集；提交异常会尝试恢复旧源；跟随系统默认端点时响应设备变化；拒绝访问时显示隐私指引 | A70 合成采集器验证新源预启动失败时旧源和单一 WebSocket 保持运行，另检查预热缓冲清空；A59/A60 验证切换和默认话筒事件；A61 验证拒绝异常的中文权限指引；156 项 CoreChecks、Release x64 通过。真实 WASAPI 拔插、隐私开关和声学连续性仍待验收，见 A51/A59/A60/A61/A70 |
| 麦克风首帧启动与转换健康 | Mac 麦克风安装后 1.2 秒无原始回调时最多重试 2 次，间隔 450ms；原始回调正常但转换 PCM 未到时另有 2.5 秒健康检查，失败则停止采集并报告错误 | Windows 话筒/混合模式等待首个非空 WASAPI 回调，1.2 秒超时后最多重启 2 次、间隔 450ms；随后 2.5 秒内必须产生 16 kHz 重采样输出，否则明确报错。默认话筒热切换先验证候选原始帧和转换输出；未通过时保留旧源。WASAPI 静音标记视为有效的静音输出 | A74/A75 合成采集器和本地 WebSocket 覆盖无首帧重试、无转换输出错误、候选首帧重试、旧源继续发 PCM 和单一连接；165 项 CoreChecks、Release x64 通过。真实 WASAPI、权限恢复和硬件转换故障仍待验；见 [A74](../sources/windows-a74-microphone-first-frame-retry-2026-10-10/README.md) 与 [A75](../sources/windows-a75-microphone-conversion-health-2026-10-10/README.md) |

本次核验前已执行 fetch 并以最新 `origin/main` 建立独立分支；没有把旧混合分支合并进来。旧分支曾含 Mac 源码提交，因此不得整体 cherry-pick 或合并。详细测试证据见 [testing.md](testing.md) 与 `docs/sources/windows-*`。

## 逐项对照

| Mac 行为 | Windows 当前实现 | 状态 | 证据与下一步 |
|---|---|---|---|
| 简洁主窗口：品牌、主题、置顶、字幕优先、底部录音与存档工具 | 单页 WinUI，双语列表、主题、置顶、录音、归档、导出等均存在；设置是独立页面 | 功能存在但行为不同 | `macOS/EchoMacApp.swift`；`windows/Echo.Windows/MainPage.xaml`。A48 对齐 820×650 DIP 理想、680×520 DIP 最小尺寸；去掉空字幕时常驻的回底按钮。隔离 GUI 已验初始/最小尺寸且截图检查未见裁切；Mac/Windows 并排视觉对照、多屏工作区仍待验 |
| 双语字幕列表；支持选择、纠正、说话人、时间和跨天日期分隔 | 双列字幕，带时间、说话人、检测语言、纠正入口；相邻字幕跨本地日历日期时显示日期分隔行 | 功能存在但行为不同 | `macOS/Views/TranscriptViews.swift`；`windows/Echo.Windows/MainPage.xaml`、`MainPageViewModel.cs`、`Core/Transcript.cs`。字体与紧凑行距未 GUI 对照 |
| 字幕编辑：只保存改动字段；录音继续时其余字段继续更新；确认丢弃、载入最新识别稿、撤销、AI 建议确认、查看修订历史及添加术语 | 编辑器按 baseline 只提交改动字段；识别稿变化时显示提示，可在未编辑时载入最新文本；关闭有未保存修改时要求确认；人工恢复值仍锁定防止迟到识别覆盖。原始识别稿及每条带本地时间的修订可在折叠面板查看；编辑内容置于 480 DIP 上限的滚动视口；可就地添加并保存校对术语 | 功能存在但行为不同 | Mac `macOS/Views/TranscriptViews.swift`、`SpeechViewModel.swift`、`TranscriptModels.swift`；Windows `MainPage.xaml.cs`、`MainPageViewModel.cs`、`Core/Transcript.cs`、`Core/CorrectionTermList.cs`、`Core/CorrectionRecognitionSnapshot.cs`。CoreChecks 覆盖历史/滚动源码契约、术语约束与 A62 的识别设置过期丢弃逻辑。动态编辑、展开、添加及录音中交互仍未 GUI 实测，见 A53/A55/A62 底稿 |
| 用户滚离底部后停止跟随，并显示“有新内容”按钮 | `SynchronizedTranscriptView` 以滚动阶段区分用户操作和内容增长；离开底部后保留历史位置，新内容出现回到底部入口，回到底部后恢复跟随 | Windows 通过 Direct Manipulation、滚轮和键盘输入识别用户滚动；被动内容布局变化不改变跟随状态；末尾距离阈值为 32 DIP | 状态模型和事件接线已自动检查；实际触控板惯性、滚轮与虚拟化列表仍待隔离 GUI 验收 | Mac `macOS/Views/TranscriptViews.swift`；Windows `MainPage.xaml`、`MainPage.xaml.cs`、`Core/TranscriptFollowState.cs`。134 项 CoreChecks 覆盖状态迁移和输入事件接线，见 A56 |
| 主字幕区可选识别语言及翻译目标，录音中提示下次录音生效 | 主界面提供 Mac 同款语言、自动识别、不翻译选项；ComboBox 显示 `Title` 标签而非对象调试文本；更改配置写入本地并提示下次录音生效 | 功能存在但行为不同 | Mac `macOS/Views/TranscriptViews.swift`、`macOS/Models/TranscriptModels.swift`；Windows `MainPage.xaml`、`MainPage.xaml.cs`、`Core/Preferences.cs`。A33 UIA 检查语言选择显示值；Windows 仍保留“翻译”设置开关，菜单项会同步开关状态 |
| 四个设置分类：常规、识别、分段、AI 服务 | Mac 设置 sheet 常规尺寸 520×560 DIP，四类设置在滚动内容区内 | Windows 用 WinUI SelectorBar 切换四类设置；面板保留 520×560 DIP 最大尺寸，受限工作区下收缩；有限星号行承载 ScrollViewer | 功能存在但行为不同 | Mac `macOS/EchoMacApp.swift`；Windows `windows/Echo.Windows/MainPage.xaml`、`MainWindow.xaml.cs`。A47 在 144 DPI、430×360 DIP 隔离窗口 UIA 实测滚动和按钮可见；125%/200% DPI、键盘和 Narrator 未验，见 A47 底稿 |
| 音源：电脑音频、话筒、混合；授权状态和音频电平 | WASAPI loopback、话筒及本地混音；只平均当前帧实际可用来源，缺帧补静音但不压低另一路；电平按来源 RMS 计算并平滑。采样率转换遇多声道时保留首声道，对齐 Mac converter | 固定输入的生产转换对拍通过；真实硬件验收待完成 | Mac `Audio/AudioCapture.swift`、`SpeechViewModel.swift`、`PCM16AudioPipeline.swift`；Windows `Services/AudioCapture.cs`、`SpeechSession.cs`、`Core/AudioLevelHistory.cs`。A36 对齐欠载混音，A40 对齐 meter/波形；A79 的 Mac 生产工件对拍由 Actions run `38012345830` 验证通过，发现并修正 Windows 原平均声道差异。录音中模式切换见 A51；A61 对合成拒绝异常给出 Windows 麦克风隐私指引。真实声卡、隐私开关拒绝/恢复、睡眠和长时间双路验收仍待完成 |
| Soniox 临时字幕更新、最终字幕落定、翻译迟到、双语端点、说话人/语言元数据、强语境经济学/微积分词汇纠正及请求上下文 | Mac 每条响应整体替换 provisional 双语快照、累加 final；endpoint 在整条响应完成后最多定稿一次。同响应新建 row 时多个 speaker 合并并采用末尾元数据；已有 row 在下一响应遇到 final speaker 变化时分行。JSON 字段类型不符时以 Swift 可选转换回退/忽略 | A35 七条、A42 十三条和 A43 十八条生产对拍通过；A67 以生产 `TokenAssembler` 处理两小时合成字幕时间轴；A68 配置 Mac 生产 handler 对应的大序列逐行对拍；云端真实负载待验 | A43 Actions run `37966271007` 的 Mac handler、Windows 对比和 Archive 回读均通过。A42 对齐畸形字段安全回退；A43 覆盖经济学上下文纠正、微积分词汇与中文联动、保守负例和 han/hand 上下文修正。A67 有 14,400 条 synthetic response / 7,200 条双语 final row；A68 run 37998251868 的 Mac production handler、Windows 7,200 行逐字段比较、Release x64 与 Mac Archive/SRT 回读全部通过；同一代码在当前 PR head 的 run 37998929503 再次全绿。不是实际两小时墙钟运行或云端验收，见 A42/A43/A67/A68 |
| 设置识别模式、优先语言、严格限制、翻译、目标语言、说话人 | 自动/优先语言为独立模式；只有优先模式显示源语言和严格限制；自动模式不丢弃上次优先语言；设置完成后保存 | Windows 识别设置用两项 SelectorBar 呈现自动识别/优先语言；自动模式隐藏源语言和严格限制；独立保存 `PreferredSourceLanguage`，旧设置从现有源语言迁移；请求仍在指定模式发送一个 hint，strict 仅随指定模式发送 | 功能存在但行为不同 | Mac `EchoMacApp.swift`、`TranscriptModels.swift`、`SonioxRequestBuilder.swift`、`SpeechViewModel.swift`；Windows `Core/Preferences.cs`、`Core/SonioxRequestBuilder.cs`、`MainPage.xaml(.cs)`。A71 的设置结构/事件/旧配置迁移检查通过，159 项 CoreChecks 与 Release x64 构建通过；真实设置页视觉、键盘和 Narrator 尚未验收，见 A54/A71 |
| 录音中切换电脑音频/话筒/混合模式并保持当前识别连接 | Mac 在当前录音会话内启动新增采集源；成功后再停止旧源；必要授权或启动失败时保留原模式与会话 | Windows 模式菜单仍可操作；暂停音频发送、排空采集尾部、重启 WASAPI 源，继续用同一 Soniox WebSocket 和当前字幕段；设备失败先回滚原模式，无法恢复时保存并停止；拒绝访问时提示 Windows 隐私设置 | 功能存在但行为不同 | Mac `macOS/Models/TranscriptModels.swift`、`ViewModels/SpeechViewModel.swift`；Windows `Core/AudioInputModeSelection.cs`、`Services/SpeechSession.cs`、`Services/AudioCapture.cs`、`ViewModels/MainPageViewModel.cs`。A59/A60 用合成采集器和 loopback 验证失败回滚、默认话筒变化、PCM 连续发送和单连接；A61 用合成拒绝异常验证中文权限指引；142 项 CoreChecks、Release x64 通过。真实设备声学间隙、隐私开关和拔插仍待验收，见 A51/A59/A60/A61 |
| Soniox 端点参数与本地静音/长段分段策略 | Mac 按默认值和范围保存；语义端点优先；本地策略每 500ms 检查词数和静音时长，翻译开启时等待译文；长段按词数与当前字幕时长双阈值兜底 | Windows 保存并冻结会话配置，使用相同阈值、优先级与译文等待规则；Soniox 请求发送三项 endpoint 参数 | A77 以 13 组 Mac 生产策略边界验证触发器；A78 将 Mac 生产 token handler 与 `autoFinalizeIfNeeded`、Windows 生产 `TokenAssembler` 和 ViewModel runtime helper 放进同一 9 事件固定会话，比较最终行、文本、起止/现实时间和元数据。Mac artifact 已保存，本机 177 项 CoreChecks 中 9 个事件逐项匹配；提交 `0bb6bd0` 的 Actions run `38010496387` 中 Mac 策略/会话生成、Windows CoreChecks、Release x64 和 Mac Archive 往返全部通过。固定时钟仍不能证明真实 timer 调度和云端 endpoint 节奏；见 A24/A77/A78 |
| 归档选择、新建、续录、导出、清空、拆分已完成段 | Mac 载入列表按 `updatedAt` 降序；新存档名为“课程 MM-dd HH:mm”；多段 archive 按段开始日期导出 | Windows 按 `updatedAt` 降序载入并在保存后移动更新项；A45 将默认新存档名对齐为“课程 MM-dd HH:mm”；A32 Mac→Windows→Mac 生产归档往返已成功 | 部分实现 | Mac `Storage/TranscriptArchiveStore.swift`、`SpeechViewModel.swift`、archive models；Windows `Core/Transcript.cs`、`MainPageViewModel.cs`、`Echo.CoreChecks/Program.cs`；当前 PR head `ce4bac7` 的 Windows CI run `37998929503` 中 Mac production decoder/SRT 回读通过。仍缺用户历史归档广泛互操作验证；见 A17/A27/A32/A45 |
| 停止后生成 SRT；不默认保存原始音频 | Windows 停止后写 SRT，不保存原始音频 | 完全一致 | Windows `MainPageViewModel.cs`、`Core/Transcript.cs`、`Services/AudioCapture.cs`；格式细节见 A17 底稿 |
| 总结新增内容、当前段或完整归档；Markdown 呈现、展开/收起与复制；可选停止后自动总结；AI 校对和重译建议需人工采纳 | Mac 支持三种总结范围；新内容候选从本次录音开始索引之后选择，当前段与全存档分别使用当前段/全部 Segment；AI 输入为编号、真实本地时间及中英双语字幕，并按范围选择提示词 | Windows 支持三种范围与自动总结；A63 记录本次录音起始 Segment，新内容只扫本次会话内 Segment，再按已总结签名去重；A64 对齐双语文字稿、编号/本地时间格式、三类范围提示词和 system prompt。Markdown 默认展开，可收起/复制，面板可见条件与校对建议确认均已接入 | 主要逻辑已对齐，呈现待验 | Mac `Services/DeepSeekService.swift`、`SpeechViewModel.swift`、`EchoMacApp.swift`、`Views/TranscriptViews.swift`；Windows `Services/SubtitleCorrectionService.cs`、`MainPage.xaml(.cs)`、`MainPageViewModel.cs`、`Core/TranscriptSummarySelection`、`TranscriptTextChunks`。A64 跨午夜验证双语输入时间与三类提示词，150 项 CoreChecks、Release x64 构建通过。真实 GUI、自动总结生命周期及云端结果仍待验收；见 A28/A29/A41/A63/A64 |
| 全局透明悬浮双语字幕；位置/大小；锁定；点击穿透；多桌面与全屏空间 | A49 修复 WinRT 显示器枚举崩溃。A50 将活动浮层迁移为原生顶层 HWND；Win2D/Direct2D 绘制预乘 BGRA 并用 `UpdateLayeredWindow(ULW_ALPHA)` 显示，保留无激活、置顶、调整、点击穿透及同一字幕 feed。A65 补主菜单独立锁定/解锁并让设置保存保留调整状态。A66 为无 owner 窗口和关闭清理增加回归检查。隔离 GUI 验证 10 项通过，直接检查 1920×225 渲染缓冲区：418,425 个完全透明像素、13,575 个绘制像素，预乘 Alpha 不变量全部成立 | 部分实现 | Mac `macOS/Views/DesktopSubtitleOverlay.swift` 与 `DesktopSubtitleOverlayModels.swift`；Windows `NativeDesktopSubtitleOverlayWindow.cs`、`Core/DesktopSubtitleOverlayPlacement.cs`、`Core/DesktopSubtitleOverlay.cs`、`MainPage.xaml.cs`、`MainWindow.xaml.cs`、`Core/Preferences.cs`。A65/A66 源码契约检查通过；没有真实 GUI 重测。异 DPI 多显示器切换、拔插、主窗最小化后的桌面合成、虚拟桌面、全屏应用兼容、字幕视觉并排对照仍未验收；见 [A50 实测底稿](../sources/windows-a50-native-overlay-2026-10-10.md)、[A65](../sources/windows-a65-overlay-lock-menu-2026-10-10/README.md) 和 [A66](../sources/windows-a66-overlay-lifecycle-2026-10-10/README.md) |
| 浮层显示临时识别；最终文本按保留时长消失；更新或更正重置倒计时 | Windows feed 直接投影当前 `Subtitle`，50ms 节流；final 后按配置隐藏；迟到译文/文字更正重置计时，相同文字不延长；不创建第二条采集或 Soniox 会话 | 部分实现 | Mac `macOS/Services/DesktopSubtitleOverlayFeed.swift`、`macOS/Models/DesktopSubtitleOverlayModels.swift`；Windows `Core/DesktopSubtitleOverlay.cs`、`ViewModels/MainPageViewModel.cs`、`DesktopSubtitleOverlayWindow.xaml.cs`；核心语义有 6 项自动检查，GUI 对拍尚未运行 |
| API Key 本机保存；录音音频不落盘 | Mac 在“完成”时把空白 Key 删除，否则 trim 后写入 `UserDefaults`；音频不落盘 | Windows 用当前用户 DPAPI 加密 Key，缓冲仅用于流式发送；若密文无法解密，单独提示并在空输入时保留，输入替换 Key 后重新加密；可解密 Key 清空仍会删除 | 功能存在但行为不同 | Mac `SpeechViewModel.saveAPIKey/saveSummarySettings`、`EchoMacApp.swift`；Windows `Core/Preferences.cs`、`MainPage.xaml.cs`、`Services/AudioCapture.cs`。A57 的 3 项 DPAPI 替换/保留/清空检查和设置事件接线检查通过；真实用户配置 GUI 尚未实测，见 A57 |
| macOS 桌面音频采集受系统屏幕与系统音频权限约束 | Windows 使用 WASAPI loopback 和麦克风权限 | 平台客观限制 | 授权弹窗、设备默认值和环回能力由平台决定；A61 对合成访问拒绝显示“设置 → 隐私和安全性 → 麦克风”指引。真实隐私开关拒绝、恢复后重试仍待 Windows 实机验收 |
| GitHub CI 及正式签名安装 | Windows 有 Release 构建、CoreChecks、历史自签名 MSIX 底稿；公众信任链、正式发布流水线未确认 | 尚未验证 | `windows/README.md`、`windows/package-release-candidate.ps1`、`.github/workflows/windows-release-candidate.yml`。已具备手动签名候选包流程：校验证书 Subject/Publisher、SHA-256、RFC 3161 时间戳、runner 信任链和包内二进制；实际可信签名与干净机器安装/升级仍未验，当前身份是占位 `CN=AppPublisher` |

## 最高优先级缺口

1. P0：A35 七条、A42 十三条、A43 十八条 Mac production handler 对拍均通过。A39 对齐空响应静默计时，A42 对齐畸形字段安全回退，A43 对齐纠正规则，A44 对齐 error/finished 控制帧；A51/A59/A60/A70 覆盖会话内模式切换、预启动失败保留旧源、失败恢复和合成默认话筒端点变化，A61 覆盖拒绝异常的权限提示。下一步核验真实音频设备热切换、设备拔插恢复和 Windows 隐私开关拒绝/恢复；真实云响应仍未覆盖。
2. P1：主界面语言菜单、滚动跟随行为、四类设置和分段配置已接入；A33 完成 12 项启动/控件 UIA smoke，但窄窗口、视觉布局、字幕滚动手感、键盘和 Narrator 仍未实测。
3. P1：原生 Win32/Direct2D 浮层已替换活动 XAML 浮层，主界面继续使用 WinUI；透明像素、置顶、点击穿透与调整模式已隔离实测。下一步验收真实异 DPI 多屏、拔插、全屏应用，并做 Mac/Windows 视觉对照。浮层继续复用同一识别会话和当前字幕状态。
4. P2：总结面板已补齐 Mac 风格 Markdown 分块、折叠/展开、复制、状态反馈和显示条件；继续核验主题与窄窗口布局。
5. 发布：签名证书、CI 构建产物留存和干净 Windows 机器安装升级验证。

当前状态结合 `origin/main` 源码比对、A33/A49/A50 隔离 GUI 和 CI；透明缓冲区及点击穿透已实测，但未做 Mac/Windows 截图视觉对照、真实异 DPI 多屏、全屏应用、真实设备采集或正式签名安装验收。

## A83 主字幕对比度回归（2026-10-10）

静态对照 Mac `macOS/Views/TranscriptViews.swift` 的 Divider `.opacity(0.55)` 与 Windows `MainPage.xaml` 后发现，Windows 曾将透明度设在容纳 ListView、空态与新内容提示的父 Grid 上，整块内容会一起变淡。现改为仅让独立的 1 DIP 顶部分隔线使用 0.55 透明度；CoreChecks 检查父容器无 Opacity、透明度仅作用于细线。该检查是静态 XAML 契约，不替代深浅主题及显示器上的视觉验收。未访问用户字幕、录音、API Key 或截图；未启动 Echo；`macOS/` 未改。

## A84 录音工具栏窄窗布局（2026-10-10）

Mac `EchoMacApp.swift` 用 `ViewThatFits` 在横向空间不足时把连接状态放到录音按钮下方；Windows 原来一直固定在同一行。现增加 760 DIP `AdaptiveTrigger`：窄窗状态置于第二行，宽窗恢复右侧排列，状态内容与原 360 DIP 省略及 UI Automation 名称保持不变。CoreChecks 验证默认窄窗位置和宽窗触发/Setter。源码契约与构建不是运行态缩放验收；未启动 Echo，真实布局仍交由用户查看。

## A85 翻译关闭时的字幕宽度

Mac SwiftUI 仅在启用翻译时构建译文列。Windows 现在在翻译关闭时将译文 TextBlock 收起并使原文跨双列；活动录音仍使用会话开始时的翻译状态，避免将下次会话设置套到当前结果。

## A86 Speaker 元信息条件显示（2026-10-10）

Windows 字幕元信息现在与 Mac `metadata(for:)` 一致：录音时遵循本次会话启动时冻结的 speaker 设置；闲置时遵循已保存设置。Speaker 名称和分隔点仅在该行有非空标签时显示。虚拟化行的 DataContextChanged 和 Speaker 字段变化会更新状态。最终源码 188 项 CoreChecks 与隔离 Release x64（0 警告、0 错误）通过。没有启动 GUI；真实 UI 排版、键盘/Narrator 和主题/DPI 对照仍待用户视觉验收。

## A87 新内容提示与字幕纠正入口避让（2026-10-10）

| 区域 | Mac 行为 | Windows 调整 | 验证与边界 |
|---|---|---|---|
| 新内容提示 | 用户离开最新位置后才显示，并提供回到最新入口 | 提示放在 ListView 下方独立 Auto 行，列表可滚动区域不再被按钮覆盖；可见性继续由 transcript follow state 驱动 | CoreChecks 静态断言 XAML 行位置、折叠默认值、AutomationId 和可见性接线；Release x64 构建通过。未启动 GUI，按钮出现时的实际高度和视觉间距待用户评估 |

## A92 Windows 浮层交互优先实现（2026-10-10）

| Mac/目标行为 | Windows 当前实现 | 状态 | 证据与边界 |
|---|---|---|---|
| 悬停显示拖动、锁定、穿透、设置、关闭工具条；浮层内直接设置字幕 | 字幕由透明分层 HWND 绘制；独立 WinUI 控件 HWND 通过光标轮询显示工具栏和设置窗。主菜单仅保留开关和紧急恢复。设置包含字号、透明度、阴影、显示语言、最大宽度、保留时间、锁定/穿透、默认值和位置重置 | 部分实现 | A92 仅有一次未留原始 JSON 的控制台全流程通过记录；后续复跑失败，UI Automation 稳定性未证实。单屏及异 DPI场景未验，详见 [A92 交互说明](overlay-interaction.md) 和 [A92 测试记录](testing.md) |
| 锁定与点击穿透互不影响；穿透时可恢复；拖动位置持久化 | `PositionLocked` 控制拖动，`ClickThrough` 独立决定字幕 HWND 的 `WS_EX_TRANSPARENT`；工具/设置 HWND 保持可操作；主窗口可强制关闭穿透并解锁。拖动与键盘方向键更新现有显示器归一化位置 | 部分实现 | A92 原始诊断 JSON 记录 UIA/拖动时序失败；单次旧 GUI 操作不能代表稳定通过。真实多屏/DPI仍待测，详见 A92 交互说明 |

## A93 Windows 悬浮设置面板紧凑化（2026-10-10）

| 目标行为 | Windows 当前实现 | 状态 | 验证与边界 |
|---|---|---|---|
| 小型统一图标工具栏；设置在浮层旁打开，常用控件不裁切，其他选项可展开 | 184×40 DIP 工具栏、30×30 DIP 图标；304×416 DIP 设置窗，标题/底栏固定、中央滚动；常用项默认展示，保留时间/阴影/锁定/穿透/位置重置/恢复默认折叠在“更多设置” | 已实现，GUI 待验 | CoreChecks 几何覆盖边缘、短工作区和 100/125/150/200% DPI；真实独立 GUI 桌面不可用/未确认，本机没有进行任何鼠标或 GUI 操作，故实际控件边界、视觉和焦点未验 |
| 设置面板靠近工具栏且不漂移或越出屏幕 | 以工具栏 HWND 为锚点，选取上下空间较大的一侧，在当前显示器工作区内夹取；缓存锚点、工作区和 DPI，轮询输入不变时跳过 resize/position | 已实现，GUI 待验 | `OverlayPopoverLayout.Place` 纯函数由 CoreChecks 覆盖；没有实测显示器热切换、任务栏变化或每显示器 DPI 更新 |

历史 GUI 证据说明：A92 曾有一次未保存原始 JSON 的控制台通过记录，后续自动化诊断 JSON 有 6 项失败；不能将其表述为可重复全绿验收。详见 [A92/A93 测试记录](testing.md) 和 [A93 来源底稿](../sources/windows-pr7-overlay-popover-2026-10-10/README.md)。
