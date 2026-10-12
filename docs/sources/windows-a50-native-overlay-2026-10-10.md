# A50 原生透明字幕浮层

日期：2026-10-10
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 起始提交：`e4903e88107d4ceb89c1810b9a812efd1cabc8e8`

## 变更

保留 WinUI 3 主界面，将字幕浮层换为原生 Win32 layered HWND。Win2D 1.4.0（Direct2D/DirectWrite）绘制 premultiplied BGRA 缓冲区，使用 `UpdateLayeredWindow` 和 `ULW_ALPHA` 呈现。普通状态支持点击穿透和不激活；调整状态临时取消点击穿透并填充命中区域。保留现有字幕 feed、计时/保留策略及设置入口，不为浮层创建新的音频或网络会话。显示器偏好记录设备名与规范化位置；旧 `DisplayId` 配置继续兼容。

## 官方 API 依据

- [UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)：layered window 的像素表面通过 `ULW_ALPHA` 和 `BLENDFUNCTION` 提交。
- [Win2D premultiplied alpha](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/premultiplied-alpha)：Win2D 默认使用预乘 Alpha；RGB 通道应不大于 Alpha。
- [Win2D](https://microsoft.github.io/Win2D/)：Direct2D/DirectWrite 的 Windows Runtime 图形封装。

## 原始测试数据

隔离 MSIX 包使用唯一临时身份和临时 DataRoot；注入的字幕为合成字符串。没有启动已安装的正式包、采集音频、请求云服务或保存真实字幕。测试窗口缓冲区为 1920×225。

`render-pixels.json` 是从隔离测试进程导出的原始渲染统计：432,000 像素中 418,425 个完全透明，13,575 个可见；alpha 格式为 `premultiplied-bgra8`，非预乘像素计数为 0。原件 SHA-256：`6F283F17030D9E1E4C3896694A6E5108DF376E9F2328DE46B7A08A5560F6DCC0`。

`test-results.json` 中 10 项 UI smoke 全部通过，包含 layered/tool/no-activate 样式、与主窗口的 Z 序、窗口可见、点击穿透、可访问窗口名、透明和可见像素、预乘 Alpha、调整模式接受鼠标输入及退出后恢复点击穿透。原件 SHA-256：`D0D6CB308620FC64D178FA2CA43CD56E4C9E12B150A567D96BFBC21BDA606D2A`。

源码 `windows/Echo.Windows/NativeDesktopSubtitleOverlayWindow.cs` SHA-256：`1596BFC80C5C820427821973F9696F84C400E87DE5FC28E4F9D1A63A91B75652`。测试脚本 `windows/ui-native-overlay-window.ps1` SHA-256：`59DA47A0D4DED14BA9D900B73F98BF2BA34B4F6CCA9F66F5A09A61183C92A490`。

## 未覆盖范围

没有保留画面截图：首次屏幕截图包含测试窗口后方其他桌面应用内容，故不作为仓库证据。像素统计证明 render target 中 alpha 值和不变量，不证明桌面合成器在所有设备上呈现正确，也不构成 Mac/Windows 并排视觉验收。多显示器/DPI 切换、全屏应用、虚拟桌面、显示器断开/恢复、Narrator 与真实会议字幕仍需后续验收。
