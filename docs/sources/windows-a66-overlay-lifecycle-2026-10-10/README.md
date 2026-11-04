# A66 原生浮层生命周期回归底稿（2026-10-10）

## 目的

保护 Mac 浮层行为对应的 Windows 窗口生命周期：悬浮字幕使用独立顶层窗口，不因主窗口最小化而按 owner 关系隐藏；应用关闭时完整销毁原生窗口并释放订阅与定时器。

## 对照来源

- Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Mac `macOS/Views/DesktopSubtitleOverlay.swift`：`TransparentSubtitlePanel` 不成为 key/main window；controller 以 floating level 展示，并设置 `hidesOnDeactivate = false`。
- Windows `NativeDesktopSubtitleOverlayWindow.cs`：`CreateWindowEx` 的 parent/owner 参数为 0，窗口包含 `WS_EX_NOACTIVATE`；展示时使用 `SW_SHOWNOACTIVATE` 和 `HWND_TOPMOST`。
- Windows `MainWindow.xaml.cs`：主窗关闭后调用 `MainPage.CloseSubtitleOverlay()`；浮层关闭释放 expiry/publish 定时器、feed 事件订阅和 HWND。

## 验证结果

- `dotnet run --project windows/Echo.CoreChecks -c Release`：154 项通过。新增两项源码契约检查覆盖无 owner/置顶/非激活显示，以及主窗关闭清理。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`：成功，0 警告、0 错误。
- `git diff --check`：通过。
- 没有启动 Echo GUI、录音或操作用户桌面。主窗最小化后的实际合成表现仍未 GUI 验收；源码契约和 Win32 owner 规则不是实机证据。

## 保留数据

- `corechecks.log`：完整 CoreChecks 输出。
- `release-build.log`：完整本机 Release x64 构建输出。
- `source-hashes.txt`：Mac/Windows 源码与更新后对照文档 SHA-256。