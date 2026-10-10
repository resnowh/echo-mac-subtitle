# A76 原生浮层双行尾部省略

日期：2026-10-10

## 基线与目的

- Mac 唯一基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 修改前：`feature/windows-mac-parity` `04c6718ed9b2c67f052106457bcdf6fcf5003f0b`。
- 对照 Mac `macOS/Views/DesktopSubtitleOverlay.swift`：每行 `lineLimit(2)` 且 `truncationMode(.tail)`。
- 复核发现此前 CoreChecks 读取 `DesktopSubtitleOverlayWindow.xaml`，但应用入口实际创建 `NativeDesktopSubtitleOverlayWindow`。因此旧断言没有保护活动的 DirectWrite 绘制路径。

## 修改

- 删除未被实例化的 `DesktopSubtitleOverlayWindow.xaml` 与代码后置，避免留下另一套黑底 WinUI 浮层实现。
- 当前原生渲染器使用 `CanvasTextLayout`，按字体行距将每条字幕高度限定到两行，并配置字符级尾部省略号；文字和阴影复用相同布局。
- CoreChecks 改为检查活动的原生绘制源码及两行高度计算。

## 验证及范围

- `corechecks.log`：166 项 CoreChecks 通过。测试覆盖当前渲染器的两行布局、尾部省略设置以及既有透明 HWND 生命周期等源码/逻辑合同；不等价于实际字体的 GUI 对拍。
- `release-x64-build.log`：Release x64 构建通过，0 警告、0 错误。
- 本轮没有启动任何 Echo 窗口、采集音频、请求云服务或保存桌面截图。A50 隔离 GUI 测试证明 HWND 样式与当时的像素透明性；它运行早于本次布局变更，不能替代当前文字渲染的 GUI 验收。
- 真实超长原文/译文的 DirectWrite 断行、末尾省略符、复杂脚本/RTL、DPI 与桌面透明合成仍待隔离 GUI 实测。

## 原始来源

- `mac-overlay-reference.txt` 保留 Mac `subtitleText` 的原始代码行。
- 原始摘录 `mac-overlay-reference.txt` SHA-256：`4688C7011E828BA65AFA180F1377C21C3092A6D01951EA72E5C632B53682A7C9`。
- Mac `DesktopSubtitleOverlay.swift` SHA-256：`F7743B67EDE0BCEF8CDE0424A76C66BB2E7C769725A1DB4E0309B8D01389D0B9`。
- `NativeDesktopSubtitleOverlayWindow.cs` SHA-256：`48160C67BC210DC9DA3242C48102F0C0DDB9B04A96D45EFA1623DDB424453286`。
- `Echo.CoreChecks/Program.cs` SHA-256：`A0DAFD7C18236CAA61C5405A98E0ACB7AFC211005672CE1243450DC6CB780E03`。
- `Echo.CoreChecks.csproj` SHA-256：`BF7FCFE1398FDF4EBA85B04AB52DB451A5C2A7314A1F33AF029E4B866D2073A0`。
- `corechecks.log` SHA-256：`78282574F51BA19EDA27B627134432C0754F2C26E568DF7A84E17DDA4BE82AD7`。
- `release-x64-build.log` SHA-256：`275E29DE8EBA844B2BF773BDB695E5B67BCA913A18D04688368CDB01B52814E2`。
