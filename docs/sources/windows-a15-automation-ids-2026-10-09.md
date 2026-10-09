# A15 WinUI 自动化标识清理

日期：2026-10-09
分支：`feature/windows-preview`
范围：仅调整 `MainPage.xaml` 中 UI Automation 的 `AutomationId`；未启动 Echo。

## 数据底稿

- 检查前执行 `git fetch origin`，并通过 GitHub API 核对分支：`feature/windows-preview` 为 `07b5b3ed07b21812736a8253c92e72c441d6fbdc`，与本地 HEAD 一致；`main` 为 `6ed817df8edd31500ef85a9c40250970ca1cda40`，已包含在当前分支，没有待拉取提交。
- 静态审查 `windows/Echo.Windows/MainPage.xaml` 与 `windows/ui-smoke.ps1`。发现存档菜单、导出菜单、总结区域和设置控件使用 `Control1`～`Control21` 一类位置型 ID；其中 20 个实际控件标识改为稳定语义名称，例如 `ArchiveNew`、`ExportSrt`、`SummaryScope`、`SourceLanguage`、`RefreshAudioDevices`。
- 现有 UI smoke 脚本依赖的 `SonioxModel` 等定位符保持不变。没有改动可见文案、布局、操作流程或脚本行为。

## 验证

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：47 项通过；没有云端调用或音频保存。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release --no-restore /p:Platform=x64 /p:RuntimeIdentifier=win-x64`：Release x64/XAML 编译成功，0 错误、10 条既有 NAudio 弃用警告。
- `git diff --check` 通过；静态搜索未发现残留的 `AutomationId="ControlN"`。

## 未覆盖

没有启动应用或执行 `windows/ui-smoke.ps1`。AutomationId 便于稳定定位，不代表读屏名称或键盘流程已被实际验证；Narrator、键盘与焦点顺序、高对比度主题、100%～200% 缩放、多显示器 DPI 仍需实机验收。
