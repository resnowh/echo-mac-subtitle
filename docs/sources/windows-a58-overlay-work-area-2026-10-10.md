# A58 悬浮字幕响应系统工作区变化

日期：2026-10-10

## 基线与数据来源

- Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：`97f8caecee37fea712dac227ddfbdc53d6d13942`。
- Windows 原生浮层源码 SHA-256：`B9C49228E049354CB3DBA4642D95E37835776C192CBBC41B03D9EEE14E7D8E9A`。
- `windows/Echo.CoreChecks/Program.cs` SHA-256：`7A61A77640AAE9744D50737B6FC4B2058E3F5DBE1305B4C65EDDE9757C196CC6`。
- 数据范围：Windows 原生悬浮窗源码、CoreChecks 结果和 Release 构建输出；不读取用户数据、屏幕内容、API Key 或音频。

## 变更

Windows 已处理 `WM_DISPLAYCHANGE` 和 `WM_DPICHANGED`，但未处理工作区范围变化。任务栏移位或自动隐藏设置改变可用工作区后，浮层可能继续保留旧的底部位置。现在仅在 `WM_SETTINGCHANGE` 的 `wParam == SPI_SETWORKAREA` 时调用现有显示器重算路径；其他系统设置变更不会触发重排。原生 HWND、透明合成及 Mac 源码均未改。

## 验证与限制

- CoreChecks 新增 1 项消息接线检查，确认 `SPI_SETWORKAREA` 与 `WM_DISPLAYCHANGE` 各自进入同一重排处理。
- 本机 139 项 CoreChecks 通过；Windows Release x64 构建成功，0 警告、0 错误；`git diff --check` 通过。
- GitHub Actions Windows CI run `37988196258` 全部成功：Mac Soniox fixture、Mac Archive fixture、Windows CoreChecks/Release x64、Windows Archive 往返及 Mac production decoder/SRT 回读。
- GUI 测试没有运行：真实任务栏位置变化、显示器拔插、异 DPI 和全屏窗口行为仍待隔离验收；源码检查不等于系统 GUI 实测。
- 真实任务栏位置变化、显示器拔插、异 DPI 和全屏窗口 GUI 行为仍待隔离验收；源码检查不等于系统 GUI 实测。

## 文件

- `windows/Echo.Windows/NativeDesktopSubtitleOverlayWindow.cs`
- `windows/Echo.CoreChecks/Echo.CoreChecks.csproj`
- `windows/Echo.CoreChecks/Program.cs`
- `docs/windows/mac-parity-matrix.md`
- `docs/windows/ui-parity.md`
- `docs/windows/testing.md`
- `docs/roadmap/数据清单.md`
