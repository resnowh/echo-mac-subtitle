# Windows 验证范围和证据

## 基线

- Mac 来源：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 本轮验证来源：当前 PR 分支 `feature/windows-mac-parity`。历史迁移基线为旧 `feature/windows-preview` 分支（本地快照 `d500bbb`，远端 `64f2b89`）；该旧分支混有后来 Mac 变更，当前工作只选择 Windows 子树及 Windows 证据文件。
- A47 仅启动了带独立包身份、临时数据目录的 UI 验收副本；没有切换或操作用户已运行的 Echo 包。

## 现有自动验证

Windows 仓库已有 `windows/Echo.CoreChecks/Program.cs`，覆盖转写 token 组装、语言/说话人/翻译边界、人工纠正和撤销、归档格式与日期、SRT、增量总结选择、DPAPI round-trip、网络重试、WASAPI 帧规范化和恢复策略等纯逻辑/本机模拟场景。具体历史通过项和版本以 `docs/sources/windows-*` 记录为准，不能把历史数字当作本次 HEAD 的新结果。

`windows/README.md` 中记录曾在 Windows SDK/WinApp CLI 环境执行 Release x64 构建，亦有自签名 MSIX 底稿。自签名证书不等于公众信任的正式签名；历史包不代表此分支的当前构建产物。

## 本轮已运行

1. 2026-10-10 本机 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：120 项通过，含 A35/A42/A43 Soniox 18 响应 fixture、五类 Mac 词汇纠正路径、A44 本地 WebSocket error/finished 控制帧、A45 新 Archive 标题、双路 PCM 欠载混合、A38 浮层契约、A39 静默计时、A40 RMS/平滑/波形历史和 A41 总结面板显隐条件；不调用云端、不保存真实音频。A43 的 Mac production differential、Windows 比较、Release 构建和 Mac Archive 回读在 Actions run `37966271007` 全部通过；A44 同类 job 在 runs `37967804957` 与 `37967811281` 通过，Mac CI 和 unsigned package preflight 也全部通过。A45 CI 待推送后核对。此前 95/97/99/100/104/105/111/116/119 项和历史 CI 结果仍以各自底稿为准。
2. 2026-10-10 Windows Release x64 编译：执行 `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`，成功，0 警告、0 错误；编译没有启动应用。
3. 执行 `git diff --check`。本轮改动仅限 `windows/` 和 `docs/`，未改 `macOS/` 或 `tests/`。
4. Windows 调试实例已启动并完成 `windows/ui-smoke.ps1` 的 12 项 UIA 检查：录音页、停止按钮隐藏、电脑音频默认项、源/目标语言可读标签、设置入口、AI 服务分类和 Soniox Key 控件存在、模型输入不开放、返回主界面、导出菜单及字幕列表。没有点击录音、保存设置或调用云端；应用加载了 MSIX 包隔离目录中的既有存档，因此原始截图和 UIA 全树未留存，避免把本机字幕纳入仓库。仅留检查名称/结果。A33 还记录首次启动的 NullReferenceException 及修复。UIA 通过不等于视觉、浮层透明、键盘、Narrator、DPI 或多屏验收。
5. A34 本轮未启动 GUI 或已安装 Echo；显示器事件、透明像素与点击穿透在真实多屏设备上的行为仍待 UI 验收。
6. A35 的 macOS runtime harness 从当前 `macOS/ViewModels/SpeechViewModel.swift` 临时提取生产处理函数，并与生产 `TranscriptModels.swift` 一起编译；只使用合成文本，不启动 Echo、不采集音频、不连接服务。首轮 CI 暴露同响应多 speaker 的差异；Windows 修正后 run `37953208733` 的 Mac 夹具生成、Windows 93 项对拍/Release x64 构建及 Mac Archive 回读全部通过。PR 的 Mac CI 与 unsigned package preflight 也通过。
7. A37 参照 Soniox 官方 WebSocket、鉴权和端点文档，核实 Windows 使用 Bearer 握手而配置不携带 Key；本地模拟 WebSocket 覆盖握手后 error frame 的认证/配额/服务失败分类。官方资料摘录和 SHA-256 源清单见 [A37](../sources/windows-a37-soniox-auth-protocol-2026-10-10.md)；未调用真实 Soniox 服务。
8. A38 比对 Mac `subtitleLines` 后修正 Windows 浮层：翻译关闭时强制展示原文，原文/译文/阴影层均设为最多两行并在尾部省略；CoreChecks 断言语言可见组合与四个 XAML TextBlock 的限制。Release 构建验证 XAML 可编译；没有启动窗口，真实透明、截断排版和多屏行为仍待 GUI 验收。
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

## 尚未验证

- Mac 所需的透明渲染尚未实现；当前 WinUI 浮层实测黑底。点击穿透只检查了 HWND 扩展样式，鼠标命中/失焦仍待 GUI 验收。
- 原生 Win32/Direct2D 替换后的多显示器/DPI、虚拟桌面、全屏应用及实际字幕生命周期 GUI smoke。
- 新版矩阵覆盖每个 Mac 行为的自动化契约。
- 真实 Soniox/DeepSeek 云响应；本地模拟服务不等于云端验收。
- Windows GUI 的字体、窗口缩放、键盘、Narrator、高对比度与多 DPI，包括 125%、150%、200%。
- loopback、麦克风、设备切换、拔插、睡眠/唤醒等真实硬件行为。
- 真实 Mac 用户存档与 Windows 双向互操作，除非对应 A17 CI fixture 明确记录。
- 正式签名、干净机器安装升级、卸载和回滚。
