# Windows 与 macOS 功能对照底稿

核验日期：2026-10-10
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
| Soniox 多响应与控制帧 | Mac 对字符串 `error_message` 立即报错并返回；仅布尔 `finished: true` 才结束；转写字段错误类型安全忽略 | A35 七响应、A42 十三响应、A43 十八响应 production differential；A44 补无 error_code 错误和错误类型 finished 的 loopback 测试 | A35/A42/A43 production differential 和 A44 的 Mac/Windows/Archive CI 全通过；真实云响应仍未覆盖 |
| Soniox 本地静默后备计时 | Mac 在错误/finished 早退后，对每条有效转写响应更新时间，包括空 token 响应和仅端点标记响应 | Windows 现按每条非错误、非 finished 的有效响应更新时间；不要求响应含普通语音 token | 之前仅普通文本 token 会刷新 Windows 计时，空响应期间可能比 Mac 提前本地切句；A39 已修正并以四类固定 JSON 检查 | 纯逻辑与 Release 构建已验证；真实服务空响应节奏和长时间静默仍未验 |
| 录音电平与波形 | Mac 对转换样本算 RMS 并乘 7.5，20Hz 快攻慢放平滑，保留 48 个样本并逐点绘制 | Windows 按每路有效重采样样本算 RMS，采用相同放大、20Hz 平滑系数和 48 点波形；已移除与真实声音无关的正弦条形动画 | A40 以合成样本检查 RMS、攻击/回落、历史容量和重置 | 纯逻辑与 Release 构建通过；真实设备电平响应和视觉波形仍待 GUI/设备验收 |
| 双路 PCM 混音欠载行为 | Mac `PCM16TimelineMixer` 与 Windows `AudioCapture.ReadFrame` / `AudioFrameMixer` | A36 对照发现并修正 Windows 欠载时固定双路除数导致的音量衰减；97 项 CoreChecks 与 Windows Release 构建通过 | 确定性样本检查不替代真实 WASAPI 设备、时钟漂移及长时间采集验收；Mac 源码基线和 Windows 代码版本见 A36 |
| 浮层语言可见性与长文本 | Mac 翻译关闭时即使“显示原文”开关关闭也显示原文；每路字幕最多两行并尾部截断 | Windows 现按 Mac 规则计算可见行，原文/译文/阴影均限制两行并省略尾部 | A38 修正源码差异，并以规则和 XAML 合成检查覆盖 | 浮层实际渲染、字体裁切和透明合成仍未 GUI 验收，见 A38 |
| Soniox WebSocket 鉴权 | Mac `SpeechViewModel.openSonioxSocket` 仍把 `api_key` 放在起始配置 JSON；Windows 在握手发送 Bearer header，并从配置 JSON 排除密钥 | Windows 按 Soniox 当前推荐方式实现；保留 Mac 当前行为，不反向降级 Windows | 功能存在但行为不同 | 本地模拟 WebSocket 检查验证 Bearer header、配置不含密钥、握手后 401/402/403/429/503/413 错误分类；官方旧方式迁移时间及源码证据见 [A37](../sources/windows-a37-soniox-auth-protocol-2026-10-10.md) |
| 默认主题与切换顺序 | Mac 默认深色，`AppThemeMode.allCases` 为浅色、深色、系统 | Windows 新配置/缺省字段默认深色，设置项和循环顺序与 Mac 一致；显式保存值保留 | 静态源码及 CoreChecks 通过；系统外观 GUI 尚未实测，见 A31 |
| 跨显示器悬浮字幕布局与 DPI 变化 | Mac 监听显示器配置变化，按目标屏幕重新计算透明字幕窗位置和宽度，并恢复已保存的屏幕位置 | Windows 按显示器 ID 记住上次屏幕；`AppWindow.Changed` 与 `DisplayAreaWatcher` 更新后重新计算物理像素矩形；拖动/缩放使用屏幕坐标 | A34 的 100/150/200% 合成布局检查通过；真实异 DPI 显示器、拔插、工作区变化仍待 GUI 验收 |

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
| 音源：电脑音频、麦克风、混合；授权状态和音频电平 | WASAPI loopback、麦克风及本地混音；只平均当前帧实际可用来源，缺帧补静音但不压低另一路；电平按来源 RMS 计算并平滑 | 功能存在但行为不同 | Mac `Audio/PCM16AudioPipeline.swift`、`SpeechViewModel.swift`；Windows `Services/AudioCapture.cs`、`SpeechSession.cs`、`Core/AudioLevelHistory.cs`。欠载混音见 A36，meter/波形见 A40；真实声卡、授权表达、睡眠和长时间双路验收仍待完成 |
| Soniox 临时字幕更新、最终字幕落定、翻译迟到、双语端点、说话人/语言元数据、强语境经济学/微积分词汇纠正及请求上下文 | Mac 每条响应整体替换 provisional 双语快照、累加 final；endpoint 在整条响应完成后最多定稿一次。同响应新建 row 时多个 speaker 合并并采用末尾元数据；已有 row 在下一响应遇到 final speaker 变化时分行。JSON 字段类型不符时以 Swift 可选转换回退/忽略 | A35 七条、A42 十三条和 A43 十八条生产对拍通过；云端真实负载待验 | A43 Actions run `37966271007` 的 Mac handler、Windows 对比和 Archive 回读均通过。A42 对齐畸形字段安全回退；A43 覆盖经济学上下文纠正、微积分词汇与中文联动、保守负例和 han/hand 上下文修正。来源与 artifact 见 A42/A43 底稿 |
| 设置识别模式、优先语言、严格限制、翻译、目标语言、说话人 | 设置页语言选项、自动识别、不翻译、严格语言和说话人开关；开始会话时冻结翻译行为 | 功能存在但行为不同 | Mac `TranscriptModels.swift`、`EchoMacApp.swift`；Windows `Core/Preferences.cs`、`MainPage.xaml(.cs)`、`MainPageViewModel.cs`。UI smoke 和当前会话不变行为仍需验 |
| Soniox 端点最大延迟、灵敏度、延迟级别、本地静音兜底、超长段兜底 | 按 Mac 默认值和范围保存；Soniox 请求发送三项端点参数；500ms 本地策略以相同词数/时长双阈值兜底，翻译开启时等待译文，语义端点优先 | 功能存在但行为不同 | Mac `macOS/Models/TranscriptModels.swift`、`SpeechViewModel.swift`、`SonioxRequestBuilder.swift`；Windows `Core/Preferences.cs`、`TranscriptSegmentationPolicy.cs`、`SonioxRequestBuilder.cs`、`MainPageViewModel.cs`。核心契约自动检查；见 A24 数据底稿，真实云端行为未验 |
| 归档选择、新建、续录、导出、清空、拆分已完成段 | Mac 载入列表按 `updatedAt` 降序；新存档名为“课程 MM-dd HH:mm”；多段 archive 按段开始日期导出 | Windows 按 `updatedAt` 降序载入并在保存后移动更新项；A45 将默认新存档名对齐为“课程 MM-dd HH:mm”；A32 Mac→Windows→Mac 生产归档往返已成功 | 部分实现 | Mac `Storage/TranscriptArchiveStore.swift`、`SpeechViewModel.swift`、archive models；Windows `Core/Transcript.cs`、`MainPageViewModel.cs`、`Echo.CoreChecks/Program.cs`；A32 runtime、A45 固定标题与 CI 通过。当前 HEAD `b4cdc61` 的 Mac/Windows/Archive CI 复验通过；仍缺用户历史归档广泛互操作验证；见 A17/A27/A32/A45 |
| 停止后生成 SRT；不默认保存原始音频 | Windows 停止后写 SRT，不保存原始音频 | 完全一致 | Windows `MainPageViewModel.cs`、`Core/Transcript.cs`、`Services/AudioCapture.cs`；格式细节见 A17 底稿 |
| 总结新增内容、当前段或完整归档；Markdown 呈现、展开/收起与复制；可选停止后自动总结；AI 校对和重译建议需人工采纳 | Windows 支持三种范围与停止后自动总结开关；总结 Markdown 按 Mac 标题、小标题、项目符号和段落规则展示，默认展开，可收起/展开和复制；面板按自动总结开关、已有总结或状态文字显示，状态在面板内反馈，录音中无总结时显示提示；校对请求固定 Mac 模型并要求人工确认，手动优先于待自动任务 | 主要逻辑已对齐，呈现待验 | Mac `Services/DeepSeekService.swift`、`SpeechViewModel.swift`、`EchoMacApp.swift`、`Views/TranscriptViews.swift`；Windows `Services/SubtitleCorrectionService.cs`、`MainPage.xaml(.cs)`、`MainPageViewModel.cs`、`Core/TranscriptSummaryMarkdown.cs`。A41 用纯逻辑检查复核可见条件并完成 Release 构建；真实 GUI 排版、自动总结生命周期及云端结果仍待验收；见 A28/A29/A41 |
| 全局透明悬浮双语字幕；位置/大小；锁定；点击穿透；多桌面与全屏空间 | 已实现独立 WinUI 窗口、Topmost、layered/DWM 透明窗口样式、无激活/无边框、点击穿透切换、调整时拖动和缩放；位置/宽度按规范化坐标、当前显示器 ID 持久化；显示器或 DPI 配置变化时重新布局。A38 对齐翻译关闭时强制显示原文及 Mac 两行尾部截断规则 | 尚未验证 | Mac `macOS/Views/DesktopSubtitleOverlay.swift`、`macOS/Models/DesktopSubtitleOverlayModels.swift`；Windows `windows/Echo.Windows/DesktopSubtitleOverlayWindow.xaml(.cs)`、`Core/DesktopSubtitleOverlayPlacement.cs`、`Core/DesktopSubtitleOverlay.cs`、`MainPage.xaml(.cs)`、`Core/Preferences.cs`。99 项 CoreChecks 含可见行和 XAML 行数契约；实际透明、点击穿透、异 DPI、多屏和全屏 GUI 行为仍须验收，见 A38 |
| 浮层显示临时识别；最终文本按保留时长消失；更新或更正重置倒计时 | Windows feed 直接投影当前 `Subtitle`，50ms 节流；final 后按配置隐藏；迟到译文/文字更正重置计时，相同文字不延长；不创建第二条采集或 Soniox 会话 | 部分实现 | Mac `macOS/Services/DesktopSubtitleOverlayFeed.swift`、`macOS/Models/DesktopSubtitleOverlayModels.swift`；Windows `Core/DesktopSubtitleOverlay.cs`、`ViewModels/MainPageViewModel.cs`、`DesktopSubtitleOverlayWindow.xaml.cs`；核心语义有 6 项自动检查，GUI 对拍尚未运行 |
| API Key 本机保护；录音音频不落盘 | Windows 以当前用户 DPAPI 保护密钥；音频缓冲仅用于流式发送 | 部分实现 | Windows `Core/Preferences.cs`、`Services/AudioCapture.cs`；还需对照 Mac Keychain 的错误恢复和设置更新时机 |
| macOS 桌面音频采集受系统屏幕与系统音频权限约束 | Windows 使用 WASAPI loopback 和麦克风权限 | 平台客观限制 | 权限弹窗、设备默认值和系统环回授权由平台决定；需在 Windows 上清楚显示授权和设备状态 |
| GitHub CI 及正式签名安装 | Windows 有 Release 构建、CoreChecks、历史自签名 MSIX 底稿；公众信任链、正式发布流水线未确认 | 尚未验证 | `windows/README.md`、`docs/sources/windows-a18-*`。签名、干净机器安装升级和回滚需要正式发布配置及设备验证 |

## 最高优先级缺口

1. P0：A35 七条、A42 十三条、A43 十八条 Mac production handler 对拍均通过。A39 对齐空响应静默计时，A42 对齐畸形字段的安全回退，A43 对齐纠正规则，A44 对齐 error/finished 控制帧。下一步核实真实用户历史 Archive 的脱敏互操作和断线收尾；真实云响应仍未覆盖。
2. P1：主界面语言菜单、滚动跟随行为、四类设置和分段配置已接入；A33 完成 12 项启动/控件 UIA smoke，但窄窗口、视觉布局、字幕滚动手感、键盘和 Narrator 仍未实测。
3. P1：A34 补上上次使用的显示器记忆、DPI/显示器配置变化重排以及跨 DPI 拖动基线；优先完成真实透明合成、点击穿透、多屏和全屏应用验收。悬浮字幕复用同一识别会话和当前字幕状态。
4. P2：总结面板已补齐 Mac 风格 Markdown 分块、折叠/展开、复制、状态反馈和显示条件；继续核验主题与窄窗口布局。
5. 发布：签名证书、CI 构建产物留存和干净 Windows 机器安装升级验证。

当前状态结合源码比对、A33 Windows 调试实例 UIA smoke 和 CI；未做 Mac/Windows 截图视觉对照、浮层真实透明/点击穿透、多 DPI、多屏、真实设备采集或正式签名安装验收。
