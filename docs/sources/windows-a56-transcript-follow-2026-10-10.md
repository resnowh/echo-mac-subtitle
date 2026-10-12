# A56 字幕滚动跟随状态

日期：2026-10-10

## 基线和来源

- Mac 权威基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：`7c51842683b079d45185bfb409dbc295c89e8042`。
- Mac `macOS/Views/TranscriptViews.swift`：SHA-256 `805C242A93A45830F355AD14A91DF567E166BDF1F36D697FB2A3D9D2538140E4`。
- Windows `windows/Echo.Windows/MainPage.xaml.cs`：SHA-256 `D1D90E104429E31B4EB43FD4F014628F6A04A6860739E78DD592622DC776169F`。
- Windows `windows/Echo.Windows/Core/TranscriptFollowState.cs`：SHA-256 `93AB38D2AE4D6979650FB251A6D9CB7FCD78502ADA15098F0501BD6C8EBF9117`。
- Windows `windows/Echo.CoreChecks/Program.cs`：SHA-256 `6F11DEF34CAC4952AB268D84F1CE51B111E905BCB78D77056BCB09C0EF25F66F`。
- Microsoft Learn `ScrollViewer` 文档：<https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.scrollviewer?view=windows-app-sdk-1.8>；抓取原件 `scrollviewer.html`，320,701 bytes，SHA-256 `0D50621B7A4980C3F03BB8045E257D56F7BD8AE0C59405361D49DABE417493FA`。
- Microsoft Learn `DirectManipulationStarted` 文档：<https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.scrollviewer.directmanipulationstarted?view=windows-app-sdk-2.0>；抓取原件 `direct-manipulation-started.html`，51,691 bytes，SHA-256 `81D5F5DC27C6CB6CA36C15BF3F1B24DD9EB0DF9D480046BFD02987CBA7137CEE`。

## 差异和处理

Mac `SynchronizedTranscriptView` 用滚动阶段识别用户交互。只有用户正在滚动时才因离开底部而暂停跟随；内容增长导致的滚动几何变化不能误当成人工滚动。Windows 之前仅在 `ViewChanged` 中判断距末尾是否超过 32 DIP，这可能在字幕增量更新时误停自动跟随。

Windows 现记录 Direct Manipulation 开始/结束，并覆盖滚轮和键盘滚动输入。跟随状态独立处理视口变化和内容变化：被动布局变化不会改变是否跟随；用户滚离末尾后新内容出现“有新内容”；回到底部会清除提示并恢复跟随。

## 验证与限制

- CoreChecks 134 项通过，其中包括跟随状态转换以及 WinUI 输入事件接线契约。
- Windows Release x64 构建成功，0 警告、0 错误；`git diff --check` 通过。
- 未运行 UIA：前台仍有另一隔离测试包中的 A50 浮层进程，本轮未向该进程发送交互。真实鼠标滚轮、触控板惯性、键盘及虚拟化列表的 GUI 行为仍待隔离 UI 验收。
- 没有启动正式 Echo、连接 Soniox/DeepSeek 或采集音频；未修改任何 `macOS/` 文件。

完整官方页面原件保存在本目录。该数据只用于确定 WinUI 事件/API 语义；具体行为仍以当前 WinUI 版本编译和后续 GUI 验收为准。
