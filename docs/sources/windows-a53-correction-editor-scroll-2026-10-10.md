# A53 字幕纠正编辑器滚动与历史面板

日期：2026-10-10

## 基线与数据来源

- Mac 权威基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows PR 远端基线：`91bb68bbb19057509ac0b77c6adbd6b94e5fe39d`（A52）；本记录在其后工作区修改上验证，尚未提交。
- Mac 对照 `macOS/Views/TranscriptViews.swift` SHA-256：`805C242A93A45830F355AD14A91DF567E166BDF1F36D697FB2A3D9D2538140E4`。
- Windows `windows/Echo.Windows/MainPage.xaml.cs` SHA-256：`FE8E8D0F45A821191E32D154273EE0A0ABF601C36024264BF43800D6D4E067D9`。
- Windows `windows/Echo.CoreChecks/Program.cs` SHA-256：`C44A5AA4A68A8281B7A5A814F00E5ADD8018E1738F0BDBDCF1B83BC5A433A8A5`。

## 发现与改动

Mac 的 `SubtitleCorrectionEditor` 把编辑字段放在 `ScrollView`，窗口保留底部保存操作；历史版本另置于可展开的 `DisclosureGroup`。Windows A52 已加入历史展开面板，但编辑对话框把全部内容放入竖向 `StackPanel`，没有受限滚动视口。长字幕或展开较多修订时，内容可能挤出操作区域。

Windows 现在把编辑内容装入 `ScrollViewer`，纵向滚动条按需显示，最大高度 480 DIP；保存/关闭按钮继续由对话框承载。修订面板设置 `AutomationId=CorrectionHistory`，原始识别和每条修订继续显示并可复制。

## 验证边界

- 当前工作区 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：127 项通过；新增/更新源码契约检查历史面板 ID、原始文字、各修订字段/本地时间及滚动视口配置。
- 当前工作区 Release x64 构建通过，0 警告、0 错误；`git diff --check` 通过。
- 本轮机器上有一个位于 `%TEMP%\EchoWindowsA50UiTest-*` 的前台隔离浮层预览进程。没有对该进程或正式 Echo 执行交互、关闭、重启或替换，因此修订面板展开、窄窗口滚动、键盘导航尚未动态 UIA 验收。
- PR #5 远端 A52 HEAD 在本次本地改动前；A53 需提交后重新运行 CI。

本轮未修改 `macOS/`、用户存档或 API Key，未采集音频或调用云服务。结果仅证明源码契约与编译通过，不能替代窗口视觉及交互验收。
