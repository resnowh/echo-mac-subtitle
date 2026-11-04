# Windows A41 摘要面板状态对齐

核验日期：2026-10-10。Mac 基准为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。本轮只读 Mac 源码，对齐 Windows 摘要面板何时显示、任务状态显示位置和录音提示；没有启动 Echo、调用 DeepSeek 或接触真实转写内容。

## 数据底稿与来源清单

| 来源 | 版本 | SHA-256 | 用途 |
|---|---|---|---|
| Mac `macOS/EchoMacApp.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `CB2ED238D2EE30643BE2F4621E18599D7102ADBECFDF898D0EC52204E7802D87` | 面板显示条件、录音提示和状态文本布局 |
| Mac `macOS/ViewModels/SpeechViewModel.swift` | 同上 | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | 总结任务状态文案和状态生命周期 |
| Windows `windows/Echo.Windows/MainPage.xaml` | 本轮修改后 | `0C65EFEF79285A0C2A43E43DA8521C0029345CA6397ADDA04763CF39D10F3546` | 面板、状态文本和录音提示的 XAML 绑定 |
| Windows `windows/Echo.Windows/MainPage.xaml.cs` | 本轮修改后 | `B54CCFA02EB75B7AD3DFD7AE6FDC3489B6CBE6B0081E94F1E29E6C843AF1F18E` | 录音提示可见性及 UI 刷新 |
| Windows `windows/Echo.Windows/ViewModels/MainPageViewModel.cs` | 本轮修改后 | `D09FBEBBFC2AB41DCF371D758B7F53AB30BE0F8CD7757131CC182545B1E6A3E7` | 摘要面板派生可见状态和任务反馈 |
| Windows `windows/Echo.Windows/Core/SummaryPanelPresentation.cs` | 本轮新增 | `9F4702D7DE59FC49DA4579086B18CE203AD610A383C55CE05B9D995369CE6084` | 独立且可重复测试的显隐条件 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 本轮修改后 | `DB94D7E50A9E1075DE235AB1A09664669897F0322D62D3F4CDCB204D47EDB7FB` | 自动检查覆盖三个显示条件 |

Mac 视图源码摘录：

```swift
if model.isSummaryEnabled || !model.summaryText.isEmpty || !model.summaryStatus.isEmpty {
    VStack(alignment: .leading, spacing: model.summaryText.isEmpty ? 6 : 8) {
        if model.summaryText.isEmpty, model.isRecording {
            Text("录音中，可随时生成")
        }
        if !model.summaryStatus.isEmpty {
            Text(model.summaryStatus)
        }
    }
}
```

Mac 总结状态包括选档/API Key/无可总结文字提示、运行进度、失败和“已生成”；成功收尾设置为 `已生成`。Windows 仍把生成后的摘要保存在 Windows Archive 中，Mac/Windows 在摘要持久化方式上的既有差别不属于本轮改动。

## Windows 修正

- 面板现在且仅在自动总结启用、已有总结或有状态文字时显示；设置保存后主动刷新派生可见状态。
- 将总结状态放到面板内，避免只在通用录音状态行反馈；手动总结的空文字、运行、分段进度、存档切换、成功和错误均有对应状态。
- 录音中且尚无总结时显示 Mac 同款“录音中，可随时生成”提示；换存档时清空上个存档的状态。
- 新增纯逻辑 `SummaryPanelPresentation.ShouldShow`，覆盖三个条件分别为 true 以及全为 false 的情况。

## 验证边界

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：105 项通过；新增检查验证 Mac 面板显示条件。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：成功，0 警告、0 错误；验证 XAML 编译和绑定生成，不启动窗口。
- 未运行 GUI smoke，未连接 DeepSeek，未保存真实录音或转写；面板实际布局、窄窗口和自动总结完整异步生命周期仍待 GUI/云端测试。
- 更新 `docs/windows/mac-parity-matrix.md`、`functional-parity.md`、`ui-parity.md`、`testing.md`、`windows/README.md` 和 `docs/roadmap/数据清单.md`。没有修改 `macOS/`。
