# A34 Windows 悬浮字幕多显示器与 DPI 布局

核验日期：2026-10-09
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 修改前基线：`feature/windows-mac-parity` `5f0bd0f0060a8807c10c421dbfa165e97a18b09d`
旧 Windows 预览分支核验值：`origin/feature/windows-preview` `64f2b897754c045adaa858be4ea3a5a0e2f19c88`

## Mac 源码行为底稿

读取未修改 Mac 源码：

- `macOS/Views/DesktopSubtitleOverlay.swift`：监听 `NSApplication.didChangeScreenParametersNotification`，屏幕配置变化后重新计算浮层 frame；按已存屏幕标识选择显示器，找不到时回退主屏；位置以可见区域中的归一化坐标保存。
- `macOS/Models/DesktopSubtitleOverlayModels.swift`：保存屏幕标识、归一化横纵坐标与宽度占比；重置位置会清除屏幕标识并回到默认屏幕；外观重置保留当前位置和启用状态。

本次重读的 Windows 基线代码：`DesktopSubtitleOverlayWindow.xaml.cs`、`Core/Preferences.cs`、`MainPage.xaml.cs`。此前窗口只在创建时读取一次 DPI，宽度调整和鼠标移动都依赖缓存缩放因子，未记住显示器 ID，也没有订阅显示器配置变化。

## 实现内容

- 新增 `Core/DesktopSubtitleOverlayPlacement.cs`，将屏幕工作区到物理像素窗口矩形的计算分离为纯函数；覆盖负屏幕原点、缩放系数、边缘夹取与小于最小字幕高度的工作区。
- 设置新增可选 `DisplayId`。窗口移动结束时保存屏幕 ID 和归一化位置；重开时优先找回该显示器，显示器不可用时回退主屏。旧设置不包含此字段时仍可正常读取。
- 监听 `AppWindow.Changed`；浮层移动到另一显示器或 DPI 改变后，根据新工作区和 DPI 重新计算尺寸，并保持归一化位置。
- 使用 `DisplayAreaWatcher` 监听显示区域新增、移除和配置更新；活动显示器的 DPI 或工作区改变后重新布置浮层并保存更新后的偏好。
- 移动/缩放改用 `GetCursorPos` 提供的屏幕物理像素位移；跨屏重排后重置当前拖动基线，避免继续沿用旧屏幕的 DPI 比例。
- 与 Mac 重置语义对齐：重置位置清除屏幕 ID，外观恢复默认保留当前显示器与坐标。
- 字幕仍由既有 `DesktopSubtitleOverlayFeed` 读取；没有新增音频采集、Soniox 连接或转写状态。

## 验证结果

本轮 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release`：93 项通过。新增合成布局检查覆盖：

| 场景 | 输入 | 期望 |
|---|---|---|
| 100% DPI | 1920×1080 工作区 | 1440×150 px，水平居中、底部留 9% |
| 150% DPI、左侧屏幕 | 原点 (-2560, 0)，2560×1440 | 1920×225 px，矩形保留负坐标并位于工作区内 |
| 200% DPI、窄矮工作区 | 原点 (1920, -200)，1280×1024 | 宽度和高度夹取至工作区，并限制在可见边界 |
| 旧偏好文件 | JSON 无 `DisplayId` 字段 | 反序列化仍成功，字段为空 |

Release x64 命令 `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：0 警告、0 错误。未启动 GUI，也未操作已安装的 Echo，因此真实多屏、DPI、透明像素和鼠标穿透行为仍未验收。

## 官方 API 数据来源

以下 Microsoft 文档于 2026-10-09 检索，保留本文中的摘要与 URL，供后续核对：

- [High DPI Desktop Application Development on Windows](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)：per-monitor DPI 应用需在 DPI 通知后重新评估尺寸与布局；指南建议在混合 DPI 显示器上测试跨屏、显示器缩放变化和主屏变化。
- [AppWindow.Changed](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.changed?view=windows-app-sdk-2.0)：窗口属性发生变化并进入稳定状态时触发。
- [DisplayArea.CreateWatcher](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayarea.createwatcher?view=windows-app-sdk-2.0) 与 [DisplayAreaWatcher](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayareawatcher?view=windows-app-sdk-2.0)：监听显示区域集合新增/移除及单个显示区域配置更新；必须在 UI 线程创建。
- [DisplayArea.DisplayId](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayarea.displayid?view=windows-app-sdk-2.0) 和 [DisplayId.Value](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.displayid.value?view=windows-app-sdk-2.0)：显示器 ID 为 `DisplayId`，其数值字段类型为 `ulong`。

## 文件清单

- `windows/Echo.Windows/Core/DesktopSubtitleOverlayPlacement.cs`
- `windows/Echo.Windows/Core/Preferences.cs`
- `windows/Echo.Windows/DesktopSubtitleOverlayWindow.xaml.cs`
- `windows/Echo.Windows/MainPage.xaml.cs`
- `windows/Echo.CoreChecks/Program.cs`
- `docs/windows/mac-parity-matrix.md`
- `docs/windows/ui-parity.md`
- `docs/windows/testing.md`
- `docs/roadmap/数据清单.md`

本底稿只含合成几何参数与公开 API 摘要，不含用户屏幕截图、字幕、录音或凭据。
