# Windows 验证范围和证据

## 基线

- Mac 来源：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 本轮验证来源：当前 PR 分支 `feature/windows-mac-parity`。历史迁移基线为旧 `feature/windows-preview` 分支（本地快照 `d500bbb`，远端 `64f2b89`）；该旧分支混有后来 Mac 变更，当前工作只选择 Windows 子树及 Windows 证据文件。
- 当前任务不启动 Echo，不切换或操作用户已运行的应用。

## 现有自动验证

Windows 仓库已有 `windows/Echo.CoreChecks/Program.cs`，覆盖转写 token 组装、语言/说话人/翻译边界、人工纠正和撤销、归档格式与日期、SRT、增量总结选择、DPAPI round-trip、网络重试、WASAPI 帧规范化和恢复策略等纯逻辑/本机模拟场景。具体历史通过项和版本以 `docs/sources/windows-*` 记录为准，不能把历史数字当作本次 HEAD 的新结果。

`windows/README.md` 中记录曾在 Windows SDK/WinApp CLI 环境执行 Release x64 构建，亦有自签名 MSIX 底稿。自签名证书不等于公众信任的正式签名；历史包不代表此分支的当前构建产物。

## 本轮已运行

1. `dotnet run --project windows/Echo.CoreChecks -c Release --no-restore`：71 项通过，包括 Mac 经济学/微积分识别纠正规则的正反例、字段级纠正/撤销、跨本地午夜日期分隔、覆盖层和分段默认值、Soniox 请求字段、端点优先、翻译等待及本地 final 化索引。Windows 10.0.26200、.NET SDK 10.0.401；检查不调用云端，不保存真实音频。分段设置数据底稿见 `docs/sources/windows-a24-soniox-segmentation-2026-10-09.md`。
2. Windows Release x64 编译：执行 `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`，最新源码成功，0 警告、0 错误；编译没有启动应用。
3. 执行 `git diff --check`，检查提交路径确保无 `macOS/`、`tests/` 文件。
4. UI smoke 未运行：本轮不启动应用，因此 UI、Narrator、DPI、多屏、录音硬件均未验。

## 尚未验证

- 透明渲染、悬浮窗口点击穿透/失焦、窗口位置和缩放的 GUI smoke。
- 新版矩阵覆盖每个 Mac 行为的自动化契约。
- 真实 Soniox/DeepSeek 云响应；本地模拟服务不等于云端验收。
- Windows GUI 的字体、窗口缩放、键盘、Narrator、高对比度与多 DPI，包括 125%、150%、200%。
- loopback、麦克风、设备切换、拔插、睡眠/唤醒等真实硬件行为。
- 真实 Mac 用户存档与 Windows 双向互操作，除非对应 A17 CI fixture 明确记录。
- 正式签名、干净机器安装升级、卸载和回滚。
