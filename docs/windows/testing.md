# Windows 验证范围和证据

## A80 当前主界面 UI 验证（2026-10-10）

工作树 `D:\ProgramData\WorkSpace\EchoWindowsUIParity`，分支 `feature/windows-ui-mac-parity`，起点 PR #5 最新 head `b52f68efafd7fd1b0d1671ded4fc3f37c9a701e7`；Mac 对照为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。A80 更新后的 CoreChecks 共184项通过，涵盖语言/音源菜单、目标“不翻译”常驻、录音提示、列表容器和行参数、日期头留白、Tabular时间数字、纠正按钮、存档右靠、深浅/高对比低强调表面和总结三范围菜单。Release x64 构建成功，0警告、0错误；仅构建，没有启动应用。

PR #6 当前代码 head `7056cdc25039a136c7adf0b3cce714e1fd1d0f07` 的8项 Actions 检查全部成功：Windows CI（含 CoreChecks、Release x64 与 Mac Archive 回读）、Mac CI 和 unsigned package preflight。本轮尊重用户明确的 GUI 使用安排，没有启动已安装或隔离的 Echo、没有做 UIA 或截图比较。目标任务附件目录无图片；此前两张图已检查为邮件界面，不能支持 Echo 视觉判断。Mac/Windows 实际截图并排验收尚未完成。详情、原始输出及源哈希见 [A80 底稿](../sources/windows-a80-main-ui-parity-2026-10-10/README.md)。

## A82 录音状态按钮尺寸回归（2026-10-10）

Mac 使用同一个切换按钮承载开始和停止录音。Windows 开始按钮此前继承 32 DIP 的通用工具栏最小高度，而停止按钮为 34 DIP；现将开始按钮明确设为 34 DIP，并让 CoreChecks 比较两种状态的最小宽度 126 DIP 与高度 34 DIP。CoreChecks 184 项通过，Release x64 构建 0 警告、0 错误。没有启动 GUI；控件实际布局和焦点仍待用户视觉验收。日志与哈希见 [A80/A82 底稿](../sources/windows-a80-main-ui-parity-2026-10-10/README.md)。

## 基线

- Mac 来源：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 验证来源：PR #5 分支 `feature/windows-mac-parity`。历史迁移基线为旧 `feature/windows-preview`，当前远端为 `d500bbb21bc9bfa4d811c614576f38c0476efc50`；该旧分支混有 Mac 变更，不能整体合并或 cherry-pick。
- A47 仅启动了带独立包身份、临时数据目录的 UI 验收副本；没有切换或操作用户已运行的 Echo 包。

## 现有自动验证

Windows 仓库已有 `windows/Echo.CoreChecks/Program.cs`，覆盖转写 token 组装、语言/说话人/翻译边界、人工纠正和撤销、归档格式与日期、SRT、增量总结选择、DPAPI round-trip、网络重试、WASAPI 帧规范化和恢复策略等纯逻辑/本机模拟场景。具体历史通过项和版本以 `docs/sources/windows-*` 记录为准，不能把历史数字当作本次 HEAD 的新结果。

`windows/README.md` 中记录曾在 Windows SDK/WinApp CLI 环境执行 Release x64 构建，亦有自签名 MSIX 底稿。自签名证书不等于公众信任的正式签名；历史包不代表此分支的当前构建产物。

## 按底稿追踪的历史验证

1. A55 时点本机工作区 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：130 项通过，含 A35/A42/A43 Soniox 18 响应 fixture、A51 音源模式路由、A52/A53 纠正历史/滚动视口源码契约、A54 语言请求契约、A55 术语限长/去重/数量边界，以及既有字幕纠正、归档、AI 请求、分段、浮层和音频处理检查；不调用云端、不保存真实音频。Windows Release x64 构建成功，0 警告、0 错误。官方页面 HTML 与 SHA-256 见 A54 底稿。此记录早于 A60，历史数量只适用于各自注明的版本；A60 当前本机验证见下文。
2. 2026-10-10 Windows Release x64 编译：执行 `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`，成功，0 警告、0 错误；编译没有启动应用。
3. 执行 `git diff --check`。本轮改动仅限 `windows/` 和 `docs/`，未改 `macOS/` 或 `tests/`。
4. Windows 调试实例已启动并完成 `windows/ui-smoke.ps1` 的 12 项 UIA 检查：录音页、停止按钮隐藏、电脑音频默认项、源/目标语言可读标签、设置入口、AI 服务分类和 Soniox Key 控件存在、模型输入不开放、返回主界面、导出菜单及字幕列表。没有点击录音、保存设置或调用云端；应用加载了 MSIX 包隔离目录中的既有存档，因此原始截图和 UIA 全树未留存，避免把本机字幕纳入仓库。仅留检查名称/结果。A33 还记录首次启动的 NullReferenceException 及修复。UIA 通过不等于视觉、浮层透明、键盘、Narrator、DPI 或多屏验收。
5. A34 本轮未启动 GUI 或已安装 Echo；显示器事件、透明像素与点击穿透在真实多屏设备上的行为仍待 UI 验收。
6. A35 的 macOS runtime harness 从当前 `macOS/ViewModels/SpeechViewModel.swift` 临时提取生产处理函数，并与生产 `TranscriptModels.swift` 一起编译；只使用合成文本，不启动 Echo、不采集音频、不连接服务。首轮 CI 暴露同响应多 speaker 的差异；Windows 修正后 run `37953208733` 的 Mac 夹具生成、Windows 93 项对拍/Release x64 构建及 Mac Archive 回读全部通过。PR 的 Mac CI 与 unsigned package preflight 也通过。
7. A37 参照 Soniox 官方 WebSocket、鉴权和端点文档，核实 Windows 使用 Bearer 握手而配置不携带 Key；本地模拟 WebSocket 覆盖握手后 error frame 的认证/配额/服务失败分类。官方资料摘录和 SHA-256 源清单见 [A37](../sources/windows-a37-soniox-auth-protocol-2026-10-10.md)；未调用真实 Soniox 服务。
8. A38 比对 Mac `subtitleLines` 后修正 Windows 浮层的语言可见规则。最初的行数检查只覆盖已退役的 XAML 浮层；A76 删除了这套旧控件，并改为检查实际 Win2D/DirectWrite 绘制路径的两行布局高度和尾部省略号。当前源码契约、166 项 CoreChecks 和 Release 构建通过；当时及本轮均未用真实字幕窗口验证字体截断和透明合成，仍需隔离 GUI 验收。
9. A39 对比 Mac `handleSonioxMessage` 与 Windows 接收路径，修正空有效响应期间的静默计时；CoreChecks 覆盖空 token、端点、结束和错误响应。数据底稿、源码 SHA-256 及控制流摘录见 [A39](../sources/windows-a39-soniox-quiet-activity-2026-10-10.md)。
10. A40 对比 Mac `rmsLevel`、`recordAudioLevel` 和 `WaveformView`，将 Windows 电平与波形改为按来源 RMS、20Hz 快攻慢放、48 个真实历史样本；CoreChecks 覆盖采样边界、平滑、容量和重置。合成逻辑与 XAML 编译通过，真实输入与动态视觉尚未实测，来源哈希见 [A40](../sources/windows-a40-audio-meter-parity-2026-10-10.md)。
11. A41 对比 Mac 摘要面板可见条件与状态提示：Windows 现在按自动总结开关、已有总结或状态文字决定显隐；状态和录音提示显示在面板中。105 项 CoreChecks 与 Release x64 构建通过，未启动应用、未调用 DeepSeek；真实 GUI 排版和自动总结生命周期仍待验。源码 SHA-256 与来源摘录见 [A41](../sources/windows-a41-summary-panel-parity-2026-10-10.md)。
12. A42 对照 Mac Swift 可选类型读取语义：Windows 遇到非字符串 text、类型不符的 is_final/translation_status/speaker/language、非数值时间戳或混入非对象的 token 数组时，不因 `JsonElement.Get*` 抛错；有效译文与 endpoint 后续响应仍可处理。仓库 fixture 下 111 项 CoreChecks、Release x64 构建 0 警告/错误通过。Mac production differential、Windows build/check 和 Mac Archive 回读在 Actions runs `37964444866`、`37964454624` 通过；没有真实服务、录音或 GUI 测试。生产输出哈希见 [A42](../sources/windows-a42-soniox-malformed-field-parity-2026-10-10.md)。
13. A43 扩展 Soniox 固定 fixture 至 18 条，把经济学、微积分和截断词规则纳入 Mac production handler 对拍；本机及 Actions 116 项 CoreChecks 通过，Windows Release x64 构建 0 警告、0 错误。Actions run `37966271007` 四个 job 全部成功。Mac 原始输出已保存并登记 SHA，见 [A43](../sources/windows-a43-correction-parity-2026-10-10.md)；未调用云端服务、未录制音频、未启动 GUI。
14. A44 补充 WebSocket 控制帧边界：无 `error_code` 字符串错误需触发非重试服务异常；字符串 `finished` 不应中断接收，后续布尔 true 正常完成 stop。119 项 CoreChecks 和 Release x64 构建通过；Actions runs `37967804957`、`37967811281` 的 Mac/Windows/Archive job、Mac CI `37967811103` 与 unsigned package preflight `37967811206` 均通过。Mac 生产源码 SHA 与本地模拟帧见 [A44](../sources/windows-a44-soniox-control-frames-2026-10-10.md)。
15. A45 对齐 Mac 新 Archive 的默认标题与本地日期格式。固定时间用例通过，CoreChecks 总计 120 项；本机 Release x64 0 警告/错误。初次 Actions runs `37969232352`、`37969238022` 的 Mac/Windows/Archive job、Mac CI `37969237990` 与 unsigned package preflight `37969238091` 全部通过；当前 HEAD `b4cdc61` 再运行的 Actions runs `37969777944`、`37969788314`、Mac CI `37969788362` 和 unsigned package preflight `37969788440` 也全部通过。Mac 源码 hash 和差异记录见 [A45](../sources/windows-a45-archive-title-parity-2026-10-10.md)。
16. A46 对齐设置 sheet 的受限窗口布局：Mac 常规目标尺寸为 520×560 DIP；Windows 继续以此为最大尺寸，在短/窄可用区域内收缩。CoreChecks 新增 XAML 契约，总计 121 项通过；Release x64 构建 0 警告、0 错误。Actions runs `37970740322`、`37970748525` 的 Windows build/check、Mac fixture/Archive 回读、Mac build `37970748522` 与 unsigned package preflight `37970748705` 全部成功。来源哈希见 [A46](../sources/windows-a46-responsive-settings-2026-10-10.md)。
17. A47 以隔离的唯一 Package Identity 和临时 DataRoot 首次实测设置页：144 DPI（150%）环境下窗口为 430×360 DIP，UIA 确认“完成”、分类栏可见；滚动至分段设置底部后，“恢复默认值”和“完成”同时可见。测试揭示原 `ScrollViewer` 位于纵向 `StackPanel`，无法取得有限视口；改为 `Auto,*` Grid 行后通过。4 项 UIA、121 项 CoreChecks、Release x64 构建（0 警告、0 错误）通过；截图与 JSON 结果、哈希及测试隔离信息见 [A47](../sources/windows-a47-responsive-settings-scroll-2026-10-10.md)。后续 PR checks 全部通过：Windows CI run `37972920269`、Mac CI `37972920240`、unsigned package `37972920232`，push run `37972914290` 的 Archive/Windows jobs 亦通过。测试实例正常关闭并注销，现有 Echo MSIX 包未运行且仍注册。其他 DPI、键盘、Narrator 和真实设备未覆盖。
18. A48 对齐 Mac 主窗口大小并复查字幕工具栏：144 DPI（150%）隔离副本默认 820×650 DIP，发起 600×450 请求后由 `OverlappedPresenter` 按 Mac 最小限制到 680×520 DIP；UIA 确认无新字幕时没有常驻“回到最新”动作。截图检查最小布局无裁切。4 项 UIA、123 项 CoreChecks 和 Release x64（0 警告、0 错误）通过；详细数据与原始截图见 [A48](../sources/windows-a48-main-window-size-2026-10-10.md)。实例已关闭并注销，正式包未触碰；Mac 并排截图、多显示器与其他 DPI 未覆盖。
19. A49 首次复现浮层启动崩溃：WinApp 调试器显示 `DisplayArea.FindAll()` 的 WinRT 枚举在 `IReadOnlyList.GetEnumerator()` 抛出 `InvalidCastException 0x80004002`。改为直接按 ID 获取显示器，并在显示器已移除/句柄失效时回退当前显示器。144 DPI 隔离副本的双语浮层启动后，UIA/Win32 共 7 项中 6 项通过：文字可见、窗口在主窗上方、可见、无激活/工具窗样式及点击穿透标志；`WS_EX_LAYERED` 检查失败，浮层截图为黑色不透明背景。完整日志、截图、JSON 和隔离说明见 [A49](../sources/windows-a49-overlay-ui-2026-10-10.md)。测试包已关闭注销，正式包保持安装且未运行。用户选择下一轮保留 WinUI 主界面并改用原生 Win32/Direct2D 透明浮层。

## 跨平台 Archive runtime 对拍

- A32 在 Windows CI 增加 macOS fixture 生产与回读 job：使用未修改的 Mac 生产 ArchiveStore/SRTExporter 生成双段合成档案，Windows CoreChecks 解析并回写，随后 Mac 生产 decoder 再读并逐字段/SRT 校验。
- 当前提交的 GitHub Actions run `37948130637` 中，`generate-mac-archive-fixture`、`build-and-check`、`verify-mac-archive-roundtrip` 三个 job 均成功；PR 所需 Mac CI 与分发预检也通过。Mac 源 fixture、SRT 和 Windows round-trip JSON 已保存在 `docs/sources/windows-a32-runtime-fixtures-2026-10-09/`，文件长度与 SHA-256 登记在 `windows-a32-archive-runtime-pipeline-2026-10-09.md`。

20. A50 在独立 MSIX 身份与临时数据目录运行原生 Win32/Win2D 浮层，未启动正式包、录音或调用云服务。隔离 UI smoke 10/10 通过：`WS_EX_LAYERED|TOOLWINDOW|NOACTIVATE`、Z 序位于主窗上方、可见、点击穿透、合成双语窗口名、缓冲区透明与可见像素、预乘 BGRA 正确；调整模式取消点击穿透且整窗命中，退出后恢复。原始缓冲统计为 1920×225，432,000 总像素、418,425 透明、13,575 可见、0 个非预乘像素。结果与哈希见 [A50 底稿](../sources/windows-a50-native-overlay-2026-10-10.md)。未保留桌面截图，因此未覆盖并排视觉、多显示器/DPI 切换、全屏与虚拟桌面。

21. A50 新增 `DisplayDeviceName` 首选显示器持久化与旧配置兼容检查；原有 `DisplayId` 字段保持可读。最终本机验证：CoreChecks 123 项通过且无警告，Release x64 构建 0 警告/0 错误，UI smoke PowerShell 语法通过，`git diff --check` 通过。x86 构建未通过：当前 `project.assets.json` 未包含 win-x86 目标；已在代码中按进程位数分别绑定 `Get/SetWindowLongPtrW` 与 x86 的 `Get/SetWindowLongW`，x86 运行时行为仍未实测。
22. A51 对照 Mac `AudioInputMode` 与 `SpeechViewModel.setInputMode`，补齐 Windows 录音中的模式切换和 Mac 默认话筒。126 项 CoreChecks、Release x64（0 警告、0 错误）通过。模式切换未连接真实 Soniox、录音设备或保存音频，故仅验证设置、来源路由计划和 XAML 切换事件；WASAPI 实机切换、授权拒绝、失败恢复与声音连续性仍待验。源码哈希和范围见 [A51 底稿](../sources/windows-a51-live-audio-mode-switch-2026-10-10.md)。
23. A56 复核 Mac `SynchronizedTranscriptView` 与 Windows 列表滚动。Windows 现在区分 Direct Manipulation/滚轮/键盘输入和被动内容布局变化，只有用户滚离底部才暂停实时跟随。134 项 CoreChecks 与 Release x64 构建（0 警告、0 错误）通过，`git diff --check` 通过。没有对前台隔离 A50 进程做 UIA；触控板惯性、鼠标滚轮与虚拟化列表仍待验。Microsoft Learn 两个官方页面原件、长度、SHA-256、Mac/Windows 源码哈希见 [A56 底稿](../sources/windows-a56-transcript-follow-2026-10-10.md)。
24. A57 对照 Mac `SpeechViewModel.saveAPIKey/saveSummarySettings` 和 Windows DPAPI。Mac 源码写入 UserDefaults；Windows 保持 DPAPI，并在解密失败时保留密文，防止用户只改其他设置就覆盖掉。138 项 CoreChecks 覆盖保留、替换、清空及 UI 接线；Release x64 构建 0 警告/0 错误。未读取或修改用户 Key；实际设置 UI 和故障账户环境仍待隔离验收。源码哈希和行为记录见 [A57 底稿](../sources/windows-a57-dpapi-secret-recovery-2026-10-10.md)。

## 尚未验证

- 原生 Win32/Direct2D 浮层的 Mac 并排视觉对照、多显示器/DPI、虚拟桌面、全屏应用、显示器移除恢复及真实字幕生命周期 GUI smoke。
- 新版矩阵覆盖每个 Mac 行为的自动化契约。
- 真实 Soniox/DeepSeek 云响应；本地模拟服务不等于云端验收。
- Windows GUI 的字体、窗口缩放、键盘、Narrator、高对比度与多 DPI，包括 125%、150%、200%。
- loopback、麦克风、设备切换、拔插、睡眠/唤醒等真实硬件行为。
- 真实 Mac 用户存档与 Windows 双向互操作，除非对应 A17 CI fixture 明确记录。
- 正式签名、干净机器安装升级、卸载和回滚。

## A58 原生浮层适配工作区变化（2026-10-10）

新增 CoreChecks 消息接线断言：确认 `WM_SETTINGCHANGE` 仅在 `wParam == SPI_SETWORKAREA` 时与 `WM_DISPLAYCHANGE` 一起进入浮层位置重算。该检查覆盖源码分支，不模拟任务栏或多屏系统事件；真实 GUI 验收仍待隔离环境执行。本轮 CoreChecks、Release x64 构建和 Actions 结果见 [A58 底稿](../sources/windows-a58-overlay-work-area-2026-10-10.md)。

## A59 音源切换失败回滚（2026-10-10）

CoreChecks 使用合成采集器与本地 WebSocket：开始话筒模式后模拟切换至电脑音频失败，检查旧输入恢复、`SpeechSession` 未发失败事件、恢复后继续发送 PCM，且全程只建立一个连接。本机 140 项 CoreChecks、Release x64 和 Actions run `37989494186` 的 Mac fixture、Windows 构建/检查与 Mac Archive 往返均通过。没有访问真实音频设备或 Soniox；真实切换连续性、权限拒绝与拔插仍待验收。源码基线见 [A59 底稿](../sources/windows-a59-audio-switch-rollback-2026-10-10.md)。

## A60 默认音频端点变化（2026-10-10）

延续 A59 的合成采集器和本机 loopback WebSocket：先模拟用户切换失败并恢复话筒，再注入 Windows 默认话筒变化事件。检查默认端点重启成功、活动设备 ID 更新、状态显示已跟随、后续 PCM 继续通过原 WebSocket，连接数仍为 1。完整原始输出见 [A60 测试日志](../sources/windows-a60-default-device-change-2026-10-10/test-results.txt)。本机 CoreChecks 141 项通过；Release x64 构建 0 警告、0 错误。Actions run `37991017788` 的 Mac Soniox/Archive fixture、Windows CoreChecks/Release x64 和 Mac Archive 回读全部通过。此测试不创建麦克风/扬声器采集器、不调用云端、不保存音频；真实 WASAPI 移除、权限拒绝、硬件断连及声学间隙仍未验收。Mac 参考源码和各文件 SHA-256、日志 SHA-256 见 [A60 底稿](../sources/windows-a60-default-device-change-2026-10-10.md)。

## A61 麦克风权限拒绝指引（2026-10-10）

CoreChecks 合成 `UnauthorizedAccessException`、包装后的 COM `E_ACCESSDENIED`，并验证话筒模式显示 Windows 隐私设置指引、非话筒模式给出设备权限消息、普通设备异常保留原始消息。本机 142 项 CoreChecks 通过；Release x64 构建 0 警告、0 错误。没有触发真实权限拒绝、打开麦克风、请求云服务或保存用户音频。Windows 官方指引原始页、测试/构建日志、源码哈希及实机验收边界见 [A61 底稿](../sources/windows-a61-microphone-permission-2026-10-10.md)。

## A70 录音中音源切换预启动（2026-10-10）

Windows 现在先启动候选 WASAPI 来源并暂存它们；新源成功后才停止旧源、发送旧源缓冲尾部并提交替换。提交时清除候选源启动阶段的重叠缓冲，避免旧源尾部和新源预热数据重复一段时间轴。新源在打开或启动阶段失败时，旧源从未停止，原 Soniox WebSocket 继续发送 PCM。候选源意外停止或缓冲溢出会阻止提交/触发采集失败处理。156 项 CoreChecks 验证预启动失败后原采集器保持运行、没有调用停止旧源、同一 WebSocket 继续接收音频，并检查预热缓冲在切换时清空；Release x64 构建 0 警告、0 错误。没有打开真实 WASAPI 设备、录音或调用云端；实际设备重叠、默认端点热切换、拔插和声学间隙仍待实机验收。完整日志、源码哈希和边界见 [A70 底稿](../sources/windows-a70-staged-audio-switch-2026-10-10/README.md)。

## A71 识别模式设置对齐（2026-10-10）

Windows 设置页以双项 SelectorBar 将“自动识别”和“优先语言”分开呈现；自动模式隐藏优先语言与严格限制，自动切换保留上次优先语言。旧配置从 `SourceLanguage` 迁移该值；语言菜单切换和设置页保存共用该偏好。CoreChecks 覆盖旧值迁移、自动模式保存、XAML 控件结构和事件接线；159 项通过。Release x64 构建 0 警告、0 错误，`git diff --check` 通过。没有启动 Echo 或执行 GUI smoke，因此实机布局、键盘、Narrator 与主题验收仍未完成。原始日志、源码哈希和 Git 基线见 [A71 底稿](../sources/windows-a71-source-language-mode-2026-10-10/README.md)。

## A72 原生透明浮层路线确认（2026-10-10）

用户选择 WinUI 主界面搭配原生 Win32/Direct2D 透明字幕浮层；该实现已存在于当前分支。本轮重新运行 159 项 CoreChecks、Release x64 构建和 PR #5 检查：全部通过，构建 0 警告、0 错误。没有重做 GUI smoke，也没有触碰当前 Echo 进程。实机异 DPI、多屏拔插、全屏/虚拟桌面、主窗口最小化和 Mac 并排视觉对照仍待隔离验收。原始日志、SHA-256 与 Git 基线见 [A72 底稿](../sources/windows-a72-native-overlay-verification-2026-10-10/README.md)。

## A62 丢弃过期的 AI 校对建议（2026-10-10）

CoreChecks 单独改变源语言、目标语言、翻译开关、严格限制与说话人选项，验证每一项都会使 `CorrectionRecognitionSnapshot` 失效；源码接线检查确认 `MainPageViewModel` 在请求前捕获快照、响应后比较配置并报告旧建议已忽略。144 项 CoreChecks 与 Release x64 构建（0 警告、0 错误）通过。未调用 DeepSeek；GUI 异步请求交互仍待隔离验收。结果原件和源文件哈希见 [A62 底稿](../sources/windows-a62-stale-correction-suggestions-2026-10-10.md)。

## A63 新内容总结的会话边界（2026-10-10）

CoreChecks 构造历史段与当前会话段，验证增量总结只返回起始 Segment 之后的候选，整个存档总结仍包含两段；源码接线检查确认开始录音前保存边界并传入生产选择逻辑。146 项 CoreChecks 与 Release x64 构建（0 警告、0 错误）通过。未调用 DeepSeek；多 Segment 断线恢复及 UI 工作流未实机验收。原始结果和哈希见 [A63 底稿](../sources/windows-a63-summary-session-scope-2026-10-10.md)。

## A64 AI 总结输入与提示词对齐（2026-10-10）

与 Mac `SpeechViewModel.requestAISummary` 和 `DeepSeekService.request` 对照后，Windows 总结文字稿加入 1 起始编号、真实本地时间、中英双语文本；时间沿用 Mac 的首条/跨日 `MM-dd HH:mm:ss`、同日 `HH:mm:ss` 规则。新增内容、当前段和全存档分别使用 Mac 对应提示词，并统一 system prompt。CoreChecks 验证跨午夜格式、Archive 时间回退、双语内容、speaker 不混入，以及三种 scope 的提示词接线。150 项通过；Release x64 构建 0 警告、0 错误。未调用 DeepSeek、未保存音频；长归档分块会保留原有 18,000 字符上限，真实云端响应仍需使用者自己的 API Key 验收。底稿与哈希见 [A64](../sources/windows-a64-summary-input-parity-2026-10-10/README.md)。

## A65 悬浮字幕锁定菜单与调整状态（2026-10-10）

Mac 主菜单可独立切换锁定/解锁位置，改变锁状态时相应进入或退出调整模式；外观设置刷新不擅自改变当前调整状态；隐藏浮层会退出调整。Windows 新增对应菜单项，并将调整状态变化限定到显式锁值变化，关闭浮层时清除调整状态。CoreChecks 检查菜单 AutomationId/事件、动态锁定文案、设置保存接线与隐藏行为。152 项通过；Release x64 构建 0 警告、0 错误。本轮未启动应用，因此未把源码契约检查描述为 GUI 验收；独立包实际操作、多屏/DPI 和主窗口最小化后呈现仍待验证。证据见 [A65 底稿](../sources/windows-a65-overlay-lock-menu-2026-10-10/README.md)。

## A66 原生浮层生命周期回归检查（2026-10-10）

Mac 使用不随应用失活隐藏的 floating panel；Windows 原生浮层创建时不设置 owner，并以 no-activate 方式置顶显示，因此主窗最小化不会通过 owner 关系自动隐藏浮层。主窗关闭路径会销毁 HWND、停止两个 dispatcher 定时器并取消 feed 订阅。CoreChecks 新增两项源码契约检查，154 项全部通过；Release x64 构建 0 警告、0 错误。没有启动应用或操作当前桌面，故“主窗最小化后仍可见”仍是源码和 Win32 所有权规则推断，不是 GUI 实测；多屏、全屏及真实资源释放仍待隔离 GUI 验收。源码哈希与测试日志见 [A66 底稿](../sources/windows-a66-overlay-lifecycle-2026-10-10/README.md)。

## A67 两小时合成 Soniox 字幕流压力（2026-10-10）

用生产 `TokenAssembler` 顺序处理 7,200 秒字幕时间轴：每秒一条 provisional 响应，随后一条双语 final 和 endpoint，共 14,400 条合成 WebSocket 响应。最终检查 7,200 条字幕各自仅落定一次，英文、中文、开始/结束时间、现实时间、speaker 和 language 均正确；再经 Archive JSON 序列化/解析与完整 SRT 导出，末尾时间达到 02:00:00。CoreChecks 155 项通过；该流处理、JSON/SRT 回读耗时 178ms（本机单次观测），Release x64 构建 0 警告、0 错误。此测试快速生成两小时跨度，不经过两小时墙钟时间、不采集音频、不连接 Soniox，也不覆盖 UI/内存曲线或真实网络故障。完整日志和源码哈希见 [A67 底稿](../sources/windows-a67-two-hour-token-stream-2026-10-10/README.md)。

## A68 两小时 Soniox 大序列 Mac/Windows 生产对拍（2026-10-10）

在 A67 合成输入上，GitHub macOS CI 将从当前 `SpeechViewModel.swift` 提取未修改的生产 handler，连续处理 14,400 条响应并只输出最终紧凑字幕数组；Windows CI 对生产 `TokenAssembler` 的 7,200 条结果逐条比较英文、中文、起止时间、speaker、language 和最终定稿数。Mac 期望结果作为 CI artifact 传递，不会产生 O(n²) 的逐响应快照。Windows 本机 155 项检查与 Release x64 构建通过；Actions run `37998251868` 已验证 Mac/Windows 7,200 行全字段对拍与 Mac Archive/SRT 回读。相同代码在当前 PR head 的 run `37998929503` 再次全绿。合成数据，不等待两小时、不调用云服务、不采集音频；单次性能观测不作为 SLA。可复现输入模式见 `windows/Echo.CoreChecks/Fixtures/soniox-two-hour-stress-mode.json`。

## A69 Windows 正式签名候选包流程（2026-10-10）

新增仅手动触发、仅允许 `main` 的 `Windows Signed Release Candidate`。流程从 Actions secrets 读取密码保护的 Base64 PFX，在临时 runner 上导入发布证书；校验证书 Code Signing EKU、有效期、Subject/清单 Publisher 一致，使用 SHA-256 与 RFC 3161 HTTPS 时间戳签 MSIX；随后要求 Authenticode 状态为 `Valid`、SignTool 验签通过，并对比包内 exe/dll 与 Release 构建。artifact 保留 30 天，不自动创建或发布 GitHub Release。

`package-release-candidate.ps1` 与打包脚本通过 PowerShell AST 解析；工作流通过 YAML 解析，`git diff --check` 通过。没有可用的正式 PFX/密码 secrets，因此本轮没有签名产物，也没有安装或发布。正式证书身份仍需替换清单占位 `CN=AppPublisher`；只有完成可信证书构建、可信时间戳和干净 Windows 安装/升级后，才可标记正式签名安装已验证。原始验证日志、源哈希与官方 Microsoft/GitHub 规则摘要见 [A69 来源底稿](../sources/windows-a69-release-signing-pipeline-2026-10-10/README.md)。

## A73 主界面 Narrator 控件名称（2026-10-10）

静态审查发现录音开始/停止按钮依赖子元素推导名称，本地存档列表无名称，实时录音状态原先只有 Polite live region，无上下文名称。Windows XAML 为四处添加明确 AutomationProperties.Name，并保留原 AutomationId 与 live setting。160 项 CoreChecks、Release x64 构建（0 警告、0 错误）和 `git diff --check` 通过。未启动 GUI 或运行 UIA/Narrator；键盘遍历、实际播报、高对比度和 Accessibility Insights 仍待验。原始输出和源码哈希见 [A73 底稿](../sources/windows-a73-narrator-accessible-controls-2026-10-10/README.md)。

## A74 麦克风首帧检测与有限重试（2026-10-10）

Mac `SpeechViewModel` 在麦克风安装后 1.2 秒无原始回调时，以 450ms 间隔重试两次；回调已到但转换 PCM 未到时另以 2.5 秒报错。Windows 新增首个非空 WASAPI 回调等待；初始话筒/混合模式若超时，最多重启两次；录音中切换的候选话筒若无首帧则丢弃候选并保留旧采集，正常设备打开错误仍立即失败。合成采集器 + 本地 WebSocket 验证首帧重试、默认话筒切换失败后重试、旧输入继续发 PCM、单一 WebSocket。162 项 CoreChecks 通过；Release x64 构建 0 警告、0 错误。未访问真实话筒、Soniox 或保存音频。Windows 尚无独立的转换 PCM 健康超时；权限、拔插和真实采集连续性仍待设备验收。原始日志、源文件哈希及 Git 基线见 [A74 底稿](../sources/windows-a74-microphone-first-frame-retry-2026-10-10/README.md)。

## A75 麦克风转换 PCM 健康监测（2026-10-10）

对照 Mac `SpeechViewModel`：首次回调后还需在 2.5 秒内产出转换后的音频；超时应明确报错并停止，不能把转换故障当作无输入而无限重试。Windows 以当前采集源的首个重采样输出作为 PCM 健康信号；热切换前用独立临时重采样器探测候选缓冲，不消耗提交后新会话的重采样状态。WASAPI Silent 标记按有效静音包处理。合成采集器 + 本机 WebSocket 验证原始输入存在但无转换输出时明确失败、单一识别连接，以及初始和热切换均需等待转换输出。165 项 CoreChecks 通过，Release x64 0 警告、0 错误。没有打开音频设备、调用 Soniox 或保存音频；真实转换异常和设备拔插仍待实机验收。原始输出、Mac 源码摘录、源码哈希及 Git 基线见 [A75 底稿](../sources/windows-a75-microphone-conversion-health-2026-10-10/README.md)。

## A76 原生浮层双行尾部省略（2026-10-10）

逐项复核 Mac `DesktopSubtitleOverlayView.subtitleText` 与 Windows 实际运行路径时发现，A38 的旧检查只读取已弃用的 WinUI XAML 浮层；当前原生 HWND/DirectWrite 绘制并未受该检查覆盖。现已删除不再实例化的 XAML 浮层和代码后置，并让原生渲染器将每条字幕布局高度限制为两行，使用字符级尾部省略号绘制文字与阴影。CoreChecks 改为检查原生绘制实现，166 项通过；Release x64 构建 0 警告、0 错误。未启动 Echo 窗口，也未保存桌面截图；真实字体行高、溢出文字视觉、RTL/复杂脚本、透明合成及 DPI 仍待隔离 GUI 验收。原始 Mac 代码摘录、构建/测试日志、文件哈希和边界见 [A76 来源底稿](../sources/windows-a76-overlay-two-line-rendering-2026-10-10/README.md)。

## A77 Mac/Windows 分段策略生产对拍（2026-10-10）

分段规则此前只有 Windows 本地断言。新增固定 JSON fixture，由 macOS CI 使用 `TranscriptModels.swift` 中的生产 `TranscriptSegmentationPolicy` 生成 13 个触发结果；Windows CI 下载这份实际输出，与 Windows `TranscriptSegmentationPolicy` 和固定预期逐项比较。覆盖语义端点对空内容/译文等待的优先级、静音最少词数及边界、译文到达、功能开关、长段词数/时长阈值、静音与长段同时满足时的优先级，以及 NBSP/全角空格词数。Windows 本机及 Actions run `38009024837` 的 167 项 CoreChecks、Mac 生产工件比较、Release x64 构建和 Mac Archive 回读均通过。当前对拍只比较纯策略触发器；生产会话时钟、段落 final 文本/时间戳和真实 Soniox 节奏仍未覆盖。原始输入、Mac 生产输出、日志/哈希及基线见 [A77 来源底稿](../sources/windows-a77-segmentation-policy-parity-2026-10-10/README.md)。

## A78：分段会话端到端生产对拍（2026-10-10）

Mac CI 从 `SpeechViewModel.swift` 抽取未修改的生产 `handleSonioxMessage` 和 `autoFinalizeIfNeeded`，用确定性时钟处理 9 个合成响应/timer 事件。Windows 使用生产 `TokenAssembler` 和 `MainPageViewModel` 调用的同一个 `TranscriptSegmentationRuntime`；逐事件比较 Mac 输出与 Windows 结果的最终字幕边界、双语文本、起止与现实时间、speaker、language 和定稿次数。覆盖译文迟到、静音等译文、空响应刷新静音计时、长段 89.999/90 秒边界和 endpoint。Mac harness 编译并生成 9 个事件快照；使用保存的 Mac 原始 artifact，本机 177 项 CoreChecks 全部通过，9 个事件逐项一致，Release x64 构建 0 警告、0 错误。修复测试读取生命周期后，提交 `0bb6bd0` 的 Actions run `38010496387` 中 Mac 工件生成、Windows CoreChecks/Release x64 与 Archive 往返全部通过。它不代表真实墙钟 timer、音频或 Soniox 网络验收。固定输入、原始 Mac harness、artifact、日志与哈希见 [A78 来源底稿](../sources/windows-a78-segmentation-session-parity-2026-10-10/README.md)。

## A79：Mac/Windows 生产音频转换对拍（2026-10-10）

固定 float32 输入覆盖 48 kHz mono、44.1 kHz stereo、150 ms 起音和反相双声道。Mac CI 从 `macOS/Audio/AudioCapture.swift` 提取未修改的生产 `MacMicrophoneCapture.convert`，以 1,024 帧块复用 converter，输出 16 kHz mono PCM16 参考；Windows 使用同一输入调用生产 `AudioCapture.ToMono16k`。首次生产对拍发现 Mac 立体声转单声道保留首声道、Windows 原实现平均所有声道；Windows 现按 Mac 保留首声道，并验证反相右声道时仍有信号。比较在 PCM16 量化后允许 0.1 帧步进对齐，以反映 AVFoundation/WDL 的分数采样延迟差异；输出帧数容差 8 帧（0.5 ms）、相关性至少 0.995、RMS 差异至多 3%。PR #5 的 Actions run `38012345830` 全部通过：Mac 生产 harness/artifact、Windows CoreChecks/Release x64 和 Mac Archive 往返；使用其保存的 Mac artifact 本机复跑 184 项全过，四组相关性 0.999864–0.999970，帧数差 6。日志及哈希见 [A79 来源底稿](../sources/windows-a79-audio-conversion-parity-2026-10-10/README.md)。该测试不替代真实设备时钟、WASAPI 欠载或长会话硬件验收。

## A83 主字幕对比度回归（2026-10-10）

静态对照 Mac `macOS/Views/TranscriptViews.swift` 的 Divider `.opacity(0.55)` 与 Windows `MainPage.xaml` 后发现，Windows 曾将透明度设在容纳 ListView、空态与新内容提示的父 Grid 上，整块内容会一起变淡。现改为仅让独立的 1 DIP 顶部分隔线使用 0.55 透明度；CoreChecks 检查父容器无 Opacity、透明度仅作用于细线。该检查是静态 XAML 契约，不替代深浅主题及显示器上的视觉验收。未访问用户字幕、录音、API Key 或截图；未启动 Echo；`macOS/` 未改。

## A84 录音工具栏窄窗布局（2026-10-10）

Mac `EchoMacApp.swift` 用 `ViewThatFits` 在横向空间不足时把连接状态放到录音按钮下方；Windows 原来一直固定在同一行。现增加 760 DIP `AdaptiveTrigger`：窄窗状态置于第二行，宽窗恢复右侧排列，状态内容与原 360 DIP 省略及 UI Automation 名称保持不变。CoreChecks 验证默认窄窗位置和宽窗触发/Setter。源码契约与构建不是运行态缩放验收；未启动 Echo，真实布局仍交由用户查看。

## A85 翻译列布局回归（2026-10-10）

CoreChecks 检查字幕行加载时读取统一显示状态、并在 ViewModel 通知后更新已实现的 ListView 项；原文 ColumnSpan 与译文 Visibility 随状态切换。检查 ViewModel 在录音时采用活动会话配置、闲置时采用保存配置；目标语言菜单和设置保存会刷新布局属性。这是静态 XAML/C# 契约；当前/下次会话切换、各主题渲染仍待 GUI 验收。

## A86 Speaker 元信息显示回归（2026-10-10）

以 Mac `SynchronizedTranscriptView.metadata(for:)` 对照 Windows `MainPage.xaml`：录音中使用会话开始时的 speaker 设置，闲置时使用保存设置；字段为空时 Speaker 和中点分隔符收起。为避开 WinUI DataTemplate 对嵌套静态方法参数的代码生成缺陷，已实现行的可见性由 `Loaded/Unloaded/DataContextChanged` 路径更新；Speaker 字段变化时刷新当前虚拟化行，避免容器复用保留旧状态。

- 隔离 worktree CoreChecks：最终源码 188 项通过；合成检查无云请求、未保存音频。最终日志 `a86-corechecks-final5.log`。
- 隔离 worktree Windows Release x64：最终 UI/XAML 源码构建成功，0 警告、0 错误；日志 `a86-build-final4.log`。
- `git diff --check` 通过。无 GUI、UIA 或截图运行；目标附件为无关邮件截图，当前仍无可用 Echo Mac/Windows 对照图。视觉、键盘/Narrator、浅/深/高对比和 125/150/200% DPI 运行态验收未完成。
- 早期隔离尝试的外置 MSBuild 中间目录会重复包含生成源码；另一次模板函数绑定会触发编译器错误。两类失败及最终成功原始日志保存在 [A86 来源底稿](../sources/windows-ui-refinement-2026-10-10/README.md)，最后采用独立 worktree 的常规构建目录完成验证。

## A87 新内容提示布局回归（2026-10-10）

- 将“有新内容”提示移入字幕 Grid 的独立 `Auto` 行，避免遮住字幕纠正按钮；CoreChecks 静态验证布局、折叠默认状态、`ScrollToLatest` AutomationId 和 transcript follow state 可见性接线。
- 隔离 Windows Release x64 构建：0 警告、0 错误；CoreChecks 189 项通过。首次运行的合成 WebSocket 测试遇到未完成关闭握手，原始失败日志已保留；隔离重跑完整通过。
- 没有启动 Echo，也没有做 UIA 或截图验收；真实提示高度、间距、DPI 和视觉效果仍由用户检查。未修改 `macOS/`。日志和哈希见 [A87 来源底稿](../sources/windows-ui-refinement-2026-10-10/README.md)。

## A88 摘要与字幕可用空间 GUI 检查（2026-10-10）

- 使用临时独立包身份和合成存档/摘要，在 144 DPI 下测试 Mac 理想窗口 820×650 DIP 与最小窗口 680×520 DIP；未启动正式安装包、未读用户存档/API Key、未录音或联网。
- 首次截图复现展开摘要将 `TranscriptList` 压至 0 高。最终实现摘要让位策略、120/140 DIP 字幕最小区，以及按稳定窗口宽度更新的录音工具栏布局。
- 最终 UIA：820×650 时字幕列表 140 DIP，连接状态和音源按钮同排；680×520 时列表 120 DIP、合成字幕完整可见，工具栏使用紧凑换行。最终深色截图和测量 JSON 见 [A88 底稿](../sources/windows-a88-gui-review-2026-10-10/README.md)。
- CoreChecks 191 项通过；Release x64 和隔离 Debug 包构建均 0 警告/错误。测试包关闭并注销，`macOS/` 未改。
- **仍待视觉验收**：Mac 并排截图、浅色/高对比、Narrator/键盘焦点、其他 DPI/宽度、录音权限和滚动状态。

## A89 UI Automation smoke 更新（2026-10-10）

旧 `windows/ui-smoke.ps1` 仍期待已移除的语言 ComboBox 和旧电脑音频默认值，且以窗口消息发送 Escape，无法可靠驱动 WinUI flyout。现改为检查当前模式的可访问名称、识别/翻译菜单选项、Settings 四分类、主题、存档菜单、字幕列表和纠正编辑器；Escape 使用 UIA 工具要求的 `send-input`。PowerShell AST 和脚本 UIA selector 与当前 XAML/C# 对照检查通过。本次没有启动应用；端到端结果须在下一次独立合成数据 GUI 批次中记录，不能把静态检查称为 UI smoke 通过。

## A90 隔离包 UIA smoke 实测（2026-10-10）

- **基线**：从 PR #6 分支 `feature/windows-ui-mac-parity` 的 `0d103514f98f3e549298859791add5dbde8303b2` 创建临时本地副本；测试包使用唯一身份 `041833B0-BB1F-458D-B149-AC9E04107D04` 和独立包数据目录。
- **验证**：隔离 Debug x64 包构建 0 警告、0 错误；`windows/ui-smoke.ps1` 在一次进程会话内通过 10/10 UIA 项，覆盖录音空闲状态、音源/语言标签、语言菜单幂等状态、无新内容提示、四个设置分栏及深浅主题、合成存档/导出、校对弹窗关闭、空存档状态。最终 JSON 与截图见 [A90 证据底稿](../sources/windows-a90-ui-smoke-2026-10-10/README.md)。
- **隔离与清理**：仅使用合成设置和字幕；未启动录音、访问真实存档/API Key、连接云服务或读取用户截图。测试进程结束后注销了该唯一开发包身份，确认不再注册；临时构建目录和数据证据保留。`macOS/` 未改。
- **验收边界**：UIA 和合成截图证明控件可访问、流程可操作，不替代 Mac 与 Windows 并排视觉验收、Narrator/键盘全路径、多显示器或其他 DPI 的人工检查。

## A91 目标截图场景覆盖审计（2026-10-10）

下表只认隔离合成包的运行态截图或 UIA 回执；静态 XAML/CoreChecks 不替代视觉截图。用户负责最终视觉评估。

| 目标场景 | 当前证据 | 状态 |
|---|---|---|
| 多条长字幕 | A88 合成存档有 18 条字幕，最长英文 197 字符；最终截图未显示长句完整换行 | 未完成截图验收 |
| 短字幕 | A88 最终宽窗和最小窗截图含短字幕双栏 | 已截图 |
| 无字幕 | A90 空存档截图；UIA 未发现字幕纠正行 | 已截图 |
| 录音中 | 没有开启真实音频或 Soniox；未构造纯视图模拟状态 | 未验证 |
| 未录音 | A88 820×650 与 680×520 DIP 截图显示空闲/未录音状态 | 已截图 |
| 长列表滚动 | A88 数据有 18 条；UIA 见 `TranscriptList` 垂直滚动能力，但没有滚动过程或滚动后截图 | 部分证据，未完成视觉验收 |
| 新内容提示出现 | A48/A90 检查无新字幕时不显示常驻入口；新增内容状态的出现、避让和点击没有运行态截图 | 部分证据，未完成视觉验收 |
| 字幕校对窗口 | A90 合成字幕校对弹窗截图，UIA 打开并关闭且未编辑文本 | 已截图 |
| 设置页面 | A90 覆盖常规/识别/分段/AI 服务 UIA；保留深色常规及浅色常规截图 | 已截图，尚非 Mac 并排对照 |
| AI 总结展开 | A88 最终宽窗截图展示展开总结及可见字幕列表 | 已截图 |
| 最小窗口尺寸 | A88 在 144 DPI 实测 680×520 DIP，字幕列表高 120 DIP并保留工具栏/字幕 | 已截图/UIA 测量 |
| 深浅主题 | A88 主窗口深色；A90 浅色只覆盖常规设置页，主窗口浅色未截图 | 部分证据 |

额外 DPI 方面，A88 的 144 DPI 对应 150% 缩放；125% 和 200% 主窗口截图仍未做。当前没有可用的 Echo Mac 同尺寸截图，因此任何 Windows 截图都不代表完成了像素级或并排视觉验收。真实音频设备、键盘完整路径和 Narrator 也未运行。

## A92 悬浮字幕交互重做（2026-10-10）

- **基线**：先检查 PR #5 (`b52f68e`) 与 PR #6 (`d5832dc`)，两者均开放且当时 CI 全绿；新分支 `feature/windows-overlay-interaction` 从 PR #6 实际 HEAD `d5832dc` 快进切出。隔离目录为 `%LOCALAPPDATA%\Temp\Echo-WindowsOverlayInteraction`。没有改 `macOS/`，没有接触已运行的 Echo。
- **实现**：保留透明 Win2D/Direct2D 字幕 HWND；加入独立 WinUI 工具条/设置 HWND、90ms 光标轮询、锁/穿透分离、设置实时预览与延时保存，以及主窗口紧急恢复菜单。没有全局鼠标 Hook。详见 [悬浮字幕交互设计](overlay-interaction.md)。
- **自动化验证**：隔离 Release x64 构建 0 警告、0 错误；CoreChecks 193 项通过，其中新增断言检查主菜单仅保留开关/恢复、工具栏和设置控件 AutomationId、字幕穿透状态和窗口分层接线。
- **GUI 记录校正**：当时的一次全流程通过只有控制台摘要，没有保存原始 JSON；另一次 hover 检查单独通过。扩展复跑遇到 UIA 元素过期和拖动坐标不稳，原始结果在 [A92 GUI JSON](../sources/windows-a92-overlay-controls-2026-10-10/ui-interaction-results.json)，其中 6 项均失败。故 A92 只能描述为“曾有一次未留原始回执的控制台通过记录，后续复跑失败”，不能称作可重复 GUI 通过。
- **未验场景**：本机只有一个显示器，第二屏切换、异 DPI 切换、任务栏位置变化、Narrator、GPU 长时间运行/资源泄漏及真实会议字幕仍未验收。不能把单屏窗口移动测试描述成多屏验收。
- **边界**：仅使用本地代码与合成数据；不启用录音、不访问 API Key、不调用 Soniox/DeepSeek。真实第二显示器、DPI 热切换、Narrator 和长时间资源检查若无法在本机执行，应列为待验。

## A93 悬浮字幕紧凑设置面板与锚点修复（2026-10-10）

- **修改**：工具栏调整为 184×40 DIP、统一 30×30 DIP 图标按钮；设置面板调整为 304×416 DIP，固定标题/底栏、中部有界滚动区，常用项默认可见，保留时间/阴影/锁定/穿透/位置重置/默认值收纳到“更多设置”。默认值修改复用原控件，避免重建导致滚动位置或设置窗闪动。Escape、完成和窗口关闭会先 flush 延迟写入。
- **定位**：设置面板使用工具栏 HWND 作为锚点；`OverlayPopoverLayout.Place` 先比较上下可用空间，空间不足再试左右，夹在 monitor work area 内并在短屏幕收缩。位置缓存以锚点矩形、工作区和 DPI 为键，稳定 hover 时不重复 resize/SetWindowPos。
- **自动化验证**：Release x64 构建成功，0 警告/0 错误；CoreChecks 195 项通过，新增屏幕四边、负坐标、短工作区和 100/125/150/200% DPI 布局检查。`windows/ui-overlay-interaction-smoke.ps1` 已更新为检查常用/折叠高级设置结构，PowerShell AST 解析通过，GUI 脚本未执行。
- **PR CI**：提交 `507d885a3f8ba6b28bc57854efab9cb71bdb1b5c` 的 Windows build、build-and-check、4 项 Mac fixture、Mac Archive 往返与 unsigned package preflight 共 8 项全部通过，原始状态见 A93 `post-push-pr-checks.txt`。
- **真实 GUI 边界**：遵照不控制用户鼠标的要求，没有启动应用、调用 UIA、移动指针或截图。当前没有确认的独立 VM：Hyper-V 可见性标志为 true，但没有 `Get-VM`/`vmrun` 命令或运行中 VM 进程。因此全屏面板显示、控件实际边界、鼠标 hover、键盘焦点、透明合成和交互状态尚未验收；该限制不能由 CoreChecks 代替。
- **数据底稿**：[A93 来源目录](../sources/windows-pr7-overlay-popover-2026-10-10/README.md) 保存 PR/基线查询摘要、构建/CoreChecks日志、脚本 AST 结果、源文件 SHA-256 和 GUI 环境核查结果；A92 原失败 JSON 保持原样。
- **影响范围**：只改 Windows 代码、Windows 文档与 smoke 脚本；没有启动识别会话、访问真实字幕/存档/API Key、连接 Soniox/DeepSeek，也没有修改 `macOS/`。
