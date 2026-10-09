# Windows 验证范围和证据

## 基线

- Mac 来源：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 本轮验证来源：当前 PR 分支 `feature/windows-mac-parity`。历史迁移基线为旧 `feature/windows-preview` 分支（本地快照 `d500bbb`，远端 `64f2b89`）；该旧分支混有后来 Mac 变更，当前工作只选择 Windows 子树及 Windows 证据文件。
- 当前任务不启动 Echo，不切换或操作用户已运行的应用。

## 现有自动验证

Windows 仓库已有 `windows/Echo.CoreChecks/Program.cs`，覆盖转写 token 组装、语言/说话人/翻译边界、人工纠正和撤销、归档格式与日期、SRT、增量总结选择、DPAPI round-trip、网络重试、WASAPI 帧规范化和恢复策略等纯逻辑/本机模拟场景。具体历史通过项和版本以 `docs/sources/windows-*` 记录为准，不能把历史数字当作本次 HEAD 的新结果。

`windows/README.md` 中记录曾在 Windows SDK/WinApp CLI 环境执行 Release x64 构建，亦有自签名 MSIX 底稿。自签名证书不等于公众信任的正式签名；历史包不代表此分支的当前构建产物。

## 本轮已运行

1. 2026-10-10 本机 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：111 项通过，含 A35/A42 Soniox 13 响应 fixture、双路 PCM 欠载混合、A38 浮层契约、A39 静默计时、A40 RMS/平滑/波形历史和 A41 总结面板显隐条件；不调用云端、不保存真实音频。A42 Mac production runtime fixture 和 Windows 对拍在 Actions runs `37964444866`、`37964454624` 通过；此前 95/97/99/100/104/105 项和历史 CI 结果仍以各自底稿为准。
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

## 跨平台 Archive runtime 对拍

- A32 在 Windows CI 增加 macOS fixture 生产与回读 job：使用未修改的 Mac 生产 ArchiveStore/SRTExporter 生成双段合成档案，Windows CoreChecks 解析并回写，随后 Mac 生产 decoder 再读并逐字段/SRT 校验。
- 当前提交的 GitHub Actions run `37948130637` 中，`generate-mac-archive-fixture`、`build-and-check`、`verify-mac-archive-roundtrip` 三个 job 均成功；PR 所需 Mac CI 与分发预检也通过。Mac 源 fixture、SRT 和 Windows round-trip JSON 已保存在 `docs/sources/windows-a32-runtime-fixtures-2026-10-09/`，文件长度与 SHA-256 登记在 `windows-a32-archive-runtime-pipeline-2026-10-09.md`。

## 尚未验证

- 透明渲染、悬浮窗口点击穿透/失焦、窗口位置和缩放的 GUI smoke。
- 新版矩阵覆盖每个 Mac 行为的自动化契约。
- 真实 Soniox/DeepSeek 云响应；本地模拟服务不等于云端验收。
- Windows GUI 的字体、窗口缩放、键盘、Narrator、高对比度与多 DPI，包括 125%、150%、200%。
- loopback、麦克风、设备切换、拔插、睡眠/唤醒等真实硬件行为。
- 真实 Mac 用户存档与 Windows 双向互操作，除非对应 A17 CI fixture 明确记录。
- 正式签名、干净机器安装升级、卸载和回滚。
