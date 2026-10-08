# Desktop Subtitle Overlay

Echo 的桌面悬浮字幕是视频字幕式的实时显示层，适合观看视频、线上课程或使用其他应用时查看双语字幕；它不是滚动歌词模式。

## 用户可见行为

- 在主窗口右上角打开「悬浮字幕」菜单，可启用/关闭悬浮窗、进入位置与大小调整、锁定位置、设置点击穿透，或打开独立设置。
- 默认在当前显示器底部居中，窗口透明、无标题栏和卡片背景，显示白色原文及译文，文字带轻微阴影；原文默认 26 pt、译文 24 pt、宽度约为屏幕可用宽度的 75%。
- 调整模式可拖动窗口，右下角调整手柄改变宽度；锁定后窗口不接收鼠标事件（点击穿透默认开启），下方应用仍可操作。任何时候都可从 Echo 主窗口重新进入调整模式。
- 默认同时显示原文和译文；至少保留一种显示语言。字号、透明度、宽度、定稿保留时间、阴影、点击穿透和位置会即时保存到本机 UserDefaults，不需要重连 Soniox。默认关闭悬浮字幕。

## 字幕同步与隐藏

`SpeechViewModel` 将当前字幕 entry 投影到独立的 `DesktopSubtitleOverlayFeed`。provisional token 更新替换同一 entry 的完整文本，不拼接重复片段；译文只显示同一 entry 的结果，译文迟到时更新原字幕。下一条字幕创建新 ID，不会沿用上一条译文。若当前 session 关闭翻译，只显示原文。手动修正当前显示的 entry 时同步展示修正版，不改变纠错历史、revision 或存档逻辑。

收到现有字幕定稿后默认保留 5 秒再隐藏；保留时间可设为 1–15 秒。新修订会重置隐藏计时。停止录音、开始新 session、启动失败或清空字幕会立即清除悬浮实时状态；隐藏文字不清除主窗口字幕历史。应用启动时不会回放旧字幕。

## 架构边界

- `DesktopSubtitleOverlayModels.swift`：纯配置、显示状态和 reducer。
- `DesktopSubtitleOverlayFeed.swift`：低频只读显示 feed，不观察音量/波形，不做识别、网络或持久化转写。
- `SpeechViewModel`：沿用现有识别结果和 entry 生命周期，只在 entry 更新/定稿/纠正/清理时更新 feed。
- `DesktopSubtitleOverlay.swift`：SwiftUI 字幕与设置界面、AppKit `NSPanel` controller。每个 controller 仅管理一个面板，关闭时移除面板内容并释放 feed/model 引用。

悬浮窗不创建第二个 `SpeechViewModel`、Soniox WebSocket 或音频采集器。它不修改字幕 entry，也不增加音频或网络请求。

## 已知限制

- 全屏覆盖为尽力支持：面板使用公开的 `.canJoinAllSpaces` 和 `.fullScreenAuxiliary` 行为，但系统/目标应用可能限制 Space 或全屏覆盖；受保护界面不保证可覆盖。
- 外接显示器变化时会重新选择可用屏幕并把窗口夹回可见区域。真实插拔、多个 Space 和全屏应用仍需真实 GUI 验收。
- ViewModel、设置 store 和唯一 panel controller 由 SwiftUI `App` 生命周期持有；主窗口最小化、遮挡或关闭时，只要 macOS 仍保持 Echo 进程运行，悬浮层即可继续显示。退出 Echo 后不会继续显示。
- 长文本最多显示两行并截断尾部，避免溢出屏幕；不会逐字高亮或自动重新分句。

## 后续可扩展

可在真实 macOS 验收后考虑描边样式、独立显示器选择和每显示器布局预设；当前版本不承诺这些额外能力。
