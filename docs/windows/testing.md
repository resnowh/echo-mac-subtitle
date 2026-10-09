# Windows 验证范围和证据

## 基线

- Mac 来源：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 本轮验证来源：当前 PR 分支 `feature/windows-mac-parity`。历史迁移基线为旧 `feature/windows-preview` 分支（本地快照 `d500bbb`，远端 `64f2b89`）；该旧分支混有后来 Mac 变更，当前工作只选择 Windows 子树及 Windows 证据文件。
- 当前任务不启动 Echo，不切换或操作用户已运行的应用。

## 现有自动验证

Windows 仓库已有 `windows/Echo.CoreChecks/Program.cs`，覆盖转写 token 组装、语言/说话人/翻译边界、人工纠正和撤销、归档格式与日期、SRT、增量总结选择、DPAPI round-trip、网络重试、WASAPI 帧规范化和恢复策略等纯逻辑/本机模拟场景。具体历史通过项和版本以 `docs/sources/windows-*` 记录为准，不能把历史数字当作本次 HEAD 的新结果。

`windows/README.md` 中记录曾在 Windows SDK/WinApp CLI 环境执行 Release x64 构建，亦有自签名 MSIX 底稿。自签名证书不等于公众信任的正式签名；历史包不代表此分支的当前构建产物。

## 本轮已运行

1. `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release`：91 项通过，包括 Mac 主题默认/循环顺序、fixed model 请求、DeepSeek 校对/重译请求语义、手动优先队列和必需响应字段、响应级双语端点、五响应 Soniox parity fixture、Mac 规则的总结 Markdown 分块、字幕纠正/撤销、Soniox 上下文与 Unicode 术语、Archive 顺序/时间线、音频转换与恢复策略。Soniox fixture 期望值按 Mac 源码静态推导，未运行 Mac handler。检查不调用云端，不保存真实音频。Soniox 协议依据见 `docs/sources/windows-a25-soniox-endpoint-contract-2026-10-09.md`；上下文与术语基线见 `docs/sources/windows-a26-soniox-context-2026-10-09.md`；Archive 依据与边界见 `docs/sources/windows-a27-archive-order-multisegment-2026-10-09.md`；AI 请求依据见 `docs/sources/windows-a28-ai-correction-2026-10-09.md`；总结呈现底稿见 `docs/sources/windows-a29-summary-panel-2026-10-09.md`；Soniox 多响应 fixture 见 `docs/sources/windows-a30-soniox-stream-parity-fixture-2026-10-09.md`；默认主题底稿见 `docs/sources/windows-a31-theme-parity-2026-10-09.md`；分段参数底稿见 `docs/sources/windows-a24-soniox-segmentation-2026-10-09.md`。
2. Windows Release x64 编译：执行 `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`，最新源码成功，0 警告、0 错误；编译没有启动应用。
3. 执行 `git diff --check`，检查提交路径确保无 `macOS/`、`tests/` 文件。
4. Windows 调试实例已启动并完成 `windows/ui-smoke.ps1` 的 12 项 UIA 检查：录音页、停止按钮隐藏、电脑音频默认项、源/目标语言可读标签、设置入口、AI 服务分类和 Soniox Key 控件存在、模型输入不开放、返回主界面、导出菜单及字幕列表。没有点击录音、保存设置或调用云端；应用加载了 MSIX 包隔离目录中的既有存档，因此原始截图和 UIA 全树未留存，避免把本机字幕纳入仓库。仅留检查名称/结果。A33 还记录首次启动的 NullReferenceException 及修复。UIA 通过不等于视觉、浮层透明、键盘、Narrator、DPI 或多屏验收。

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
