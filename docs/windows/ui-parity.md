# Windows UI 对齐方案

目标：保留 Windows 原生 WinUI 控件和可访问性，同时把 Mac 的信息层级和交互节奏对齐。Mac 视觉参考固定为 `origin/main` 的 `ae0359dc90da0ccb5e526a275da1747954a49a4f`，不追求逐像素复制 SwiftUI。

## 主窗口

- 顶栏保留 ECHO、悬浮字幕菜单、设置、主题和置顶，动作数量和位置接近 Mac。
- 中央区域优先给字幕；双列原文/译文，保留说话人、时间、纠正入口和日期分隔。
- 已实现：用户向上查看历史时冻结自动滚动，新字幕到达时出现“有新内容”入口；点击后回到最新字幕。仍需 UI smoke 实机确认 ListView 虚拟化与触控滚动手感。
- 底部保留音源选择、开始/停止、连接/音频状态、存档选择、新建/导出、更多操作和波形；窄窗口可折叠次要动作。
- 总结结果使用可折叠面板，可复制；提供新增内容、当前段、整个存档三种范围。
- 默认窗体尺寸与布局根据 Mac 680×520 最小、820×650 理想尺寸做相近的可读性验证，不照搬不可适用的像素尺寸。

## 设置

已按 Mac 的四个可切换分类实现：

1. **常规**：主题、音频设备选择、文字稿及音频保存说明。
2. **识别**：自动/指定源语言、翻译目标、严格限制、说话人。
3. **分段**：端点最大延迟、灵敏度、延迟等级、本地静音和超长段兜底；控件范围与 Mac 一致。
4. **AI 服务**：Soniox/DeepSeek Key 和模型、校对开关与术语表，并明确哪些文字会发送。

源语言和目标语言已改用选项菜单，不要求用户记忆语言代码。分段参数按 Mac 默认值/范围校验并立即存本机，每次建连冻结一份配置，当前会话不会被设置改动影响；API Key 仍经 Windows DPAPI 加密。Windows 主界面在录音时提示语言修改下次生效。各设置项视觉和辅助技术仍需 GUI 验收。

## 透明悬浮字幕

Windows 当前分支已实现独立顶层窗口，显示原文与译文两行，可分别显示/隐藏。Mac 默认外观及关键交互：透明背景、白字和阴影、底部居中、宽度占可用屏幕约 75%、非调整状态点击穿透、可拖动调整并记忆位置、可锁定。Windows 设置默认值为 5 秒保留、原文 26 pt、译文 24 pt，字号/透明度/宽度/阴影范围与 Mac 一致；位置采用当前显示区域内的规范化坐标。

窗口使用 WinUI `Window` + `AppWindow`，通过受支持的 Win32 extended styles 控制 layered、no-activate 和点击穿透，并扩展 DWM frame；XAML root 保持透明。主菜单可开关、进入/退出调整，设置面板可改两种文字显示、字号、透明度、宽度、保留时间、阴影、点击穿透和位置锁定，也可恢复默认或重置位置。

窗口只订阅当前活动字幕 entry：临时识别持续更新；最终文本开始倒计时；有意义的迟到翻译或更正重置倒计时；相同文本更新不重置。禁止为浮层另建录音、音频采集器或 Soniox 连接。

当前只完成源码、编译和字幕状态语义检查；尚未启动应用验证实际透明像素、鼠标命中、失焦、DPI、多显示器、虚拟桌面或全屏应用行为。AppWindow 是 WinUI Window 对应的顶层 HWND 抽象，`Show(false)` 用于显示而不激活；Win32 文档规定 layered window 可做 alpha 混合，layered + `WS_EX_TRANSPARENT` 可把鼠标事件传给下层窗口。真实呈现仍必须在 GUI smoke 阶段验证，源码和编译不能证明桌面透明效果。

实现参考（Microsoft 官方文档，读取日期：2026-10-09）：[WinUI Windowing overview](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview)、[AppWindow API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow?view=windows-app-sdk-1.8)、[Win32 extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)、[layered window behavior](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)、[SetLayeredWindowAttributes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes)。

## 视觉与无障碍验收

- 深浅色与系统主题；对比度、字体缩放和高对比度主题可读。
- Tab 顺序覆盖所有控件，Narrator 能读出控件用途和字幕语言。
- 不依赖颜色单独表达录音、连接或错误状态。
- 主窗口缩放、窄宽布局、100/150/200% DPI、多个显示器分别验收。
- UI 截图对照必须注明 Windows 版本、分辨率、DPI、主题和窗口尺寸；未运行应用的静态检查不能标作视觉验收通过。
