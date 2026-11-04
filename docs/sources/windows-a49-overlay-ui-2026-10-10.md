# A49：悬浮字幕运行时检查与显示器查找崩溃修复

核验日期：2026-10-10
Windows 代码基线：PR #5 分支 `feature/windows-mac-parity`，A48 提交 `bd25f9860911ed71c47b45c3f3916965519de8a3`，本轮修复位于 [`DesktopSubtitleOverlayWindow.xaml.cs`](../../windows/Echo.Windows/DesktopSubtitleOverlayWindow.xaml.cs)。
macOS 对照基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。

## 发现与修复

首次实际创建悬浮窗时，`DisplayAreaWatcher_Changed` 会调用 `SavedOrCurrentDisplayArea`。原实现枚举 WinRT `DisplayArea.FindAll()`；本机运行时在 `IReadOnlyList.GetEnumerator()` 抛出 `System.InvalidCastException`（HRESULT `0x80004002`，“句柄无效”），导致 WinUI dispatcher 崩溃。WinApp 调试日志记录了完整调用栈，保存在 [`crash-before-fix-debug.log`](windows-a49-overlay-ui-2026-10-10/crash-before-fix-debug.log)，SHA-256：`D6CB2648A2469C6C68373C32F356F34E9370F0C28F54ED14092B825C6CDB84CC`。

修复后用 `DisplayArea.GetFromDisplayId` 按已保存 ID 直接查找；遇到显示器已拔除或 WinRT 句柄失效时，回退到当前窗口所在显示器。这样不再枚举该 WinRT 列表，也不会让过期显示器 ID 使浮层事件回调终止应用。

## 隔离 GUI 检查

- 测试项目副本位于 `%TEMP%\EchoWindowsA49UiTest-b381e96b1f2a4783bf5cb68ef9752152\Echo.Windows`，包身份 `AD3A6701-5B62-4B86-A2B1-0D1BC1D8F2A9`，数据根目录 `%TEMP%\EchoWindowsA49UiTestData-2e4c727bf0504c8792c65024d794fb26`。副本只注入合成英文/中文字幕，未填 API Key、未录音、未访问云端。
- 在 144 DPI（150%）环境用 `winapp run` 启动；测试实例出现主窗口和独立悬浮窗。浮层在主窗 Z 序之上；UIA 能读取两路合成字幕；非调整状态含 `WS_EX_TRANSPARENT` 点击穿透标志、`WS_EX_NOACTIVATE` 与 `WS_EX_TOOLWINDOW`。该实例关闭并注销后，正式包身份 `B7582E49-F75A-4EFA-950C-C6754B9E496E` 仍安装且没有被启动。
- 7 项隔离 GUI 断言中 6 项通过：无激活/工具窗样式、浮层位于主窗之上、窗口可见、点击穿透、英文与中文文本可访问、仅浮层截图已保存。`WS_EX_LAYERED` 实际未出现在窗口扩展样式中，相关断言失败。
- 浮层截图显示黑色不透明矩形遮住后方内容；WinUI XAML 根元素透明并不代表 HWND 输出透明。参考 Microsoft 对 WinUI 3 透明 XAML 内容限制的说明：[`microsoft-ui-xaml#11134`](https://github.com/microsoft/microsoft-ui-xaml/issues/11134) 和 [`microsoft-ui-xaml#2956`](https://github.com/microsoft/microsoft-ui-xaml/issues/2956)。因此矩阵将透明表现记为部分实现，不把源码上的 DWM/Layered 调用当作视觉成功。
- 用户已选择后续采用“主 UI 保持 WinUI、悬浮层改为原生 Win32/Direct2D 透明窗”，以恢复 Mac 的透明字幕效果。该架构改动不属于 A49。

## 证据文件

| 文件 | 内容 | SHA-256 |
|---|---|---|
| [`overlay-production-v3.png`](windows-a49-overlay-ui-2026-10-10/overlay-production-v3.png) | 仅悬浮窗区域的实机截屏；可见不透明黑底及合成双语字幕 | `2CA2D80C706773A24D83CB72B5D91749871859F2B5DBC59515B6B1DC1E522ED9` |
| [`test-results-production-v3.json`](windows-a49-overlay-ui-2026-10-10/test-results-production-v3.json) | 7 项 UIA/Win32 结果，6 PASS、1 FAIL（layered style 缺失） | `B690E520446F33657B56A5EB1BF5282B225350AD31C04CA3F5F64D87993D120E` |
| [`crash-before-fix-debug.log`](windows-a49-overlay-ui-2026-10-10/crash-before-fix-debug.log) | 修改前 WinApp stowed-exception 调试输出 | `D6CB2648A2469C6C68373C32F356F34E9370F0C28F54ED14092B825C6CDB84CC` |

可复用检查脚本：[`ui-overlay-window.ps1`](../../windows/ui-overlay-window.ps1)。它只截取指定隔离包的悬浮窗 HWND，不捕获主屏幕或其他应用。

## 自动检查

- Windows Release x64：0 警告、0 错误。
- CoreChecks：123 项通过。
- PowerShell 检查脚本：语法解析通过。
- 修改源码和文档的 `git diff --check`：通过；原始调试日志保留工具原始输出（字节未改），包含工具写入的行尾空格，因此不纳入该项格式检查。
- GitHub PR 检查在本轮开始时全部通过、PR #5 合并状态为 `CLEAN`；本轮推送后需重新核对新 CI。
