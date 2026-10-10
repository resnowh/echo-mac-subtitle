# A48：主窗口尺寸与字幕回底操作对齐 Mac

核验日期：2026-10-10。Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。本轮只修改 Windows 主窗口尺寸策略、主界面操作和对应自动检查/文档；未修改 `macOS/`。

## 数据底稿与来源清单

| 来源 | 版本/状态 | SHA-256 | 用途 |
|---|---|---|---|
| Mac `macOS/EchoMacApp.swift` | Mac 基线 | `CB2ED238D2EE30643BE2F4621E18599D7102ADBECFDF898D0EC52204E7802D87` | 主窗口 `.frame(minWidth: 680, idealWidth: 820, minHeight: 520, idealHeight: 650)` |
| Windows `windows/Echo.Windows/MainWindow.xaml.cs` | A48 修改后 | `5C61A4208467E3263C6DE5188D6DEC1083C889A8F2A588EE0AF1ED2273EAF091` | 设置窗口 min-track 与 Mac 理想起始尺寸 |
| Windows `windows/Echo.Windows/Core/MainWindowSizePolicy.cs` | A48 新增 | `128EFA1CE3A9EDD46F7162D684520241157659782E16868ED284D28B29147D98` | DPI 物理像素换算及工作区裁定 |
| Windows `windows/Echo.Windows/MainPage.xaml` | A48 修改后 | `25F85DC44FA81B0AFD17BD56C91FE3B775CD720E5839CAF9D13301E752D32A04` | 移除常驻的“回到最新”按钮；保留有新内容时出现的提示按钮 |
| Windows `windows/Echo.CoreChecks/Program.cs` | A48 修改后 | `2BF97721994AEC28665A1DDF440233549633778D63AC975E11EC6AE1244E5548` | 理想/最小尺寸、DPI/工作区和常驻按钮静态契约 |
| `windows/ui-main-window-size.ps1` | A48 新增 | `7AB4B46F1F2670C89C20BE6A13E6415DAB9BAAF7EE98030D1BC63A8ED015503D` | 隔离包身份、临时数据目录、真实窗口尺寸/按钮 UIA 验收 |
| UIA 结果 `test-results.json` | 4 项全部通过 | `B490AF084ECFCD220E59A26C0E08EC8004BF11548383EF802FCE094B7B9E2D51` | 保存在本目录 |
| 最小窗口截图 `minimum-window.png` | 本轮原始截图，34,399 bytes | `6C56F4A35F09EE599714AB2F855F056C642BF2EA03553BDFCCC7A00AB53199FC` | 独立测试副本在最小尺寸下的实际主界面 |

## 差异与实施

Mac 把主窗口最小可用尺寸定为 680×520 DIP，理想尺寸为 820×650 DIP。Windows 原先以 920×720 DIP 启动，也没有等效最小跟踪尺寸。Windows 现将 Mac 的最小值换算成当前 DPI 的物理像素，设为 `OverlappedPresenter.PreferredMinimumWidth/Height`；初始尺寸取 Mac 理想值，并在工作区更小时收缩到可用范围。Windows App SDK 为 overlapped presenter 提供首选最小尺寸属性，见 [Microsoft 官方 API 说明](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.overlappedpresenter?view=windows-app-sdk-1.8)。

隔离主窗口截图还发现工具栏常驻显示“回到最新”，而 Mac 仅在用户滚离底部并有新内容时显示提醒。Windows 移除了额外常驻动作，保留字幕区的新内容提醒和返回最新交互。

## 验证

- 本机 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：123 项通过。
- Release x64 构建：0 警告、0 错误。
- Windows UIA 在 144 DPI（150%）隔离副本中验证：默认 820×650 DIP；UIA 不再发现常驻“回到最新”；请求窗口缩至 600×450 DIP 后实际窗口为 680×520 DIP；4 项检查全部 PASS。
- 检查截图显示最小尺寸下主控件无裁切且字幕区保留可用空间。截图为 1,000×770 物理像素。
- 测试使用唯一临时 Package Identity 和 `%TEMP%` DataRoot；副本通过 UIA Close 正常退出并注销，原有 Echo 包仍安装且未运行。

本轮验证只覆盖当前 144 DPI 显示器与主窗口空字幕状态；多显示器工作区变化、其他缩放比例、键盘/Narrator、真实音频和 Mac/Windows 并排截图仍待验。没有录音、API Key 或云端请求。
