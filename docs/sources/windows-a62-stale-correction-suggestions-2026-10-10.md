# A62 丢弃过期的 AI 校对建议（2026-10-10）

## 目的与基线

Mac 基线为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。Mac `SpeechViewModel.runNextCorrection()` 保存发起请求时的 `RecognitionConfig`，响应返回时要求该配置仍相等；源语言、目标语言、翻译开关、严格限制或说话人选项变动后，旧建议不会进入当前字幕编辑器。Windows 原先只核对文本修订、存档、generation 和校对术语，缺少识别设置快照校验。

本次只修改 Windows 源码、检查和文档，没有改动 `macOS/`，没有调用 DeepSeek 或启动 Echo。

## 实现

新增 `CorrectionRecognitionSnapshot`，捕获 Windows `Preferences` 中的源语言、目标语言、翻译开关、严格限制和说话人设置。`MainPageViewModel.ExecuteCorrectionJobAsync` 在发出 DeepSeek 请求前捕获该快照；响应返回后若识别设置或校对术语变化，则不缓存旧建议，并在状态栏与当前字幕校对状态显示“识别设置或校对术语已变化，旧建议已忽略。”

## 验证

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：144 项通过；新增五种识别配置独立变化用例，以及 ViewModel 快照捕获、响应检查和过期提示接线检查。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`：构建成功，0 警告、0 错误。
- 原始输出保存在本目录 `test-results.txt` 与 `build-results.txt`。

## 源码与日志 SHA-256

| 文件 | SHA-256 |
|---|---|
| Mac `SpeechViewModel.swift` | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` |
| Mac `TranscriptModels.swift` | `CAEAB4EBA2797901B91C212F535B343FFE0AB77730E4A8BD73ABBF6464AC8E16` |
| Windows `MainPageViewModel.cs` | `CEB8641166BBE397CB2C6DC9C3B644F670DBC590AE4F3BE915E5E08DBA2348FF` |
| Windows `CorrectionRecognitionSnapshot.cs` | `9FA8ECB32978E57ACF6621142C1019009B29ABCA7F411FEB3297B73816A84360` |
| Windows `Echo.CoreChecks/Program.cs` | `FB082171D3D26EE86B68D1304F7B866990BA3215FAF40BB332F61C49C0B0B925` |
| Windows `Echo.CoreChecks.csproj` | `1AC00B81B5E19A338F1F16202FD9BF2229C5C009D668E962A75ACB8C428E83CE` |
| `test-results.txt` | `30A01961CFA048515B9F0844BE133DAA71B287B3A5D1DBA9800735ADDA83E28A` |
| `build-results.txt` | `2175926B0420DAD3AD95A1F89C5685FAEB81E454521A939732D5BCDFF0F3E781` |

这些检查验证纯配置快照比较和生产代码接线；没有对 DeepSeek 进行网络调用或端到端延迟模拟。编辑器 GUI 状态表现仍待隔离 UI 验收。
