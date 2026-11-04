# A65 悬浮字幕锁定菜单与调整状态对齐底稿（2026-10-10）

## Mac 来源

当前基线 `origin/main`：`ae0359dc90da0ccb5e526a275da1747954a49a4f`。

- `macOS/EchoMacApp.swift:81-98`：主菜单包含启用/关闭、调整位置/大小、独立锁定/解锁与点击穿透；锁值改变后调用 `setAdjusting(unlock)`。
- `macOS/Views/DesktopSubtitleOverlay.swift:94-105,150-160`：调整状态由 controller 保持；普通设置刷新更新窗口交互但不重设调整状态；锁定只在调整模式下决定可移动性。
- `macOS/Views/DesktopSubtitleOverlay.swift:84-92`：隐藏浮层时退出调整并清理 panel 引用。

## Windows 对齐

- `windows/Echo.Windows/MainPage.xaml` 新增带 `AutomationId=ToggleSubtitleOverlayLock` 的主菜单锁定/解锁项，复用 Mac 文案顺序。
- `windows/Echo.Windows/MainPage.xaml.cs` 增加独立锁定处理和动态文案；通用外观设置保存不再按锁值强制切换调整；仅锁值变化时才切换模式；应用初次载入时更新菜单状态。
- `windows/Echo.Windows/NativeDesktopSubtitleOverlayWindow.cs` 隐藏窗口时结束调整模式，避免之后重新显示时保留旧的可拖动状态。

## 验证与边界

`corechecks.log` 共 152 项通过。新增源码契约检查菜单事件/AutomationId/动态文案、设置保存保持调整模式，以及隐藏浮层退出调整。`release-build.log`：Release x64 成功，0 警告、0 错误。

本轮没有启动 Echo GUI、录音、调用云端或访问用户存档。GUI 点击、实际拖动/锁定、最小化主窗口后的合成、多屏/DPI与全屏行为尚未验收；不能把 CoreChecks 的事件接线检查当作视觉或真实桌面交互验收。

对照源码和实现源码的 SHA-256 见 `source-hashes.txt`。
