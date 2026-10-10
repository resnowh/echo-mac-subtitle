# A52 字幕纠正历史查看

日期：2026-10-10

## 数据基线

- Mac 产品基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：`54ee9285a6467141c3b2077dfb4829e496c9b25b`。
- 对照文件 `macOS/Views/TranscriptViews.swift` SHA-256：`805C242A93A45830F355AD14A91DF567E166BDF1F36D697FB2A3D9D2538140E4`。
- Windows `MainPage.xaml.cs`（本次修改后）SHA-256：`17F20FEA30B91593E6EEE8C503CDE122EFCAD2426CAB23E88D8F5A29EF8C0810`。
- Windows `Echo.CoreChecks/Program.cs` SHA-256：`F727A844E60A548B12D6CC2890FDCEE72AA101B57ABF8CE59B614D942E2622A8`。
- Windows `Echo.CoreChecks/Echo.CoreChecks.csproj` SHA-256：`95E3021BE7B65DD38B935A6F329740DE15A5B27359590D88E4E9930092F7EF26`。

## 差异与实现

Mac 的“识别稿与修改前版本”折叠组列出原始识别文字和每个修订版本的时间、原文、译文。Windows 原先只显示原始文字和可撤销次数。Windows 现以折叠 `Expander` 提供同样信息，时间按本机区域格式显示，历史文本可选择复制；编辑与保存流程不变。

## 验证

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：127 项通过，无云端请求、未保存音频。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：成功，0 警告、0 错误。
- `git diff --check`：通过。
- PR #5：Mac build、Windows build、unsigned package check 通过；本轮记录时 archive round-trip job 尚在运行。

本次比对仅读取固定 Mac 源码和本地 Windows 源码，没有复制用户字幕或音频数据。该 UI 与 Mac 行为的源码契约及 Windows 构建已检查；本轮没有新增真实应用截图或 UI 自动化运行。
