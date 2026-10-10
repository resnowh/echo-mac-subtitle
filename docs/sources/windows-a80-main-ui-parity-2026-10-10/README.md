# A80 Windows 主界面 Mac 信息层级对齐

日期：2026-10-10

Windows 分支：`feature/windows-ui-mac-parity`

起始提交：PR #5 head `b52f68efafd7fd1b0d1671ded4fc3f37c9a701e7`

Windows 改动已先提交为 `f3326a06aab1541789b3c212f36bb455cc030c16`；本目录与后续改动随当前分支 head 一并提交。

Mac 参考：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`

## 源码依据

- Mac 主界面和菜单：`macOS/EchoMacApp.swift`
- Mac 字幕行、纠正按钮、信息留白及总结视图：`macOS/Views/TranscriptViews.swift`
- Windows 主界面：`windows/Echo.Windows/MainPage.xaml`、`MainPage.xaml.cs`、`App.xaml`
- 静态和逻辑回归：`windows/Echo.CoreChecks/Program.cs`
- 对照参数和源文件 SHA-256：本目录 `source-sha256.txt`

参考参数摘要：Mac 主视图 padding 20、纵向 spacing 18；双列语言菜单间距 14 DIP；字幕 LazyVStack spacing 0、行垂直留白 7 DIP、元信息/正文 spacing 5、正文行间距 3；日期头上下 12/4 DIP；时间数字为 monospacedDigit。录音控制 HStack spacing 10，音源最小宽 148、录音按钮最小宽 126；存档工具栏 spacing10，存档最大宽260，Spacer 将导出/更多推至右侧；波形48条、宽/间距3、活动高3+样本×44并裁切至46、容器高52、水平padding12；总结面板 padding12、圆角10、四级背景色35%不透明、摘要滚动最大高220。对应依据是上述 SwiftUI 当前实现。

## Windows 修改

- 顶部识别语言和翻译目标改为 Button + MenuFlyout，动态显示当前语言和勾选项；“不翻译”保留在目标位。录音中菜单注明下次录音生效，原设置对象和保存流程不变。
- 音频来源改成轻量菜单，但保留隐藏的 `Mode` 状态选择器及原 `AudioMode_SelectionChanged`，选择后继续走同一实时切换/持久化逻辑；保留三种模式、设备切换及原 AutomationId。
- 主内容 margin 调为20 DIP、主行距18 DIP；语言两列间距14。菜单按钮透明、min-height30、padding 0×3，动态显示当前选择和勾选，AutomationId 按来源/目标区分。
- 字幕容器 padding/margin/min-height 清零；日期头上下12/4 DIP，每行上下6 DIP、元信息/正文间距4 DIP，正文15 DIP、元信息12 DIP且时间使用 Tabular numeral；空原文显示Mac相同省略号，空译文保留一行；纠正为低强调铅笔按钮，保留逐字幕 AutomationId。
- 录音控制最小宽148/126 DIP，A80 最初开始/停止高度为32/34；A82 将两者统一为34 DIP，保留 Mint/危险色语义。存档行首列弹性占宽，标题左靠、导出/更多右靠。未改录音、存档或总结业务处理。
- 波形保留48点 RMS 与52 DIP尺寸，背景改为弱表面：深色白色6%不透明、浅色黑色4%、高对比度使用系统 Window 色。AI 总结改为padding12/圆角10的轻表面，标题右侧菜单含三种总结范围；保留状态、220 DIP滚动、展开/复制与原 ViewModel 调用。
- 沿用 Echo Accent、窗口尺寸和无障碍语义；未改 `macOS/`。Tabular numeral 使用 WinUI `Typography.NumeralAlignment`，[Microsoft 文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.documents.typography.numeralalignment?view=windows-app-sdk-1.8)。

变更文件：`windows/Echo.Windows/MainPage.xaml`、`MainPage.xaml.cs`、`App.xaml`；`windows/Echo.CoreChecks/Program.cs`、`Echo.CoreChecks.csproj`；`docs/windows/ui-parity.md`、`testing.md`、`mac-parity-matrix.md`；`docs/roadmap/数据清单.md`；以及本目录中的原始日志、哈希和说明。

## 验证

原始输出保存在 `corechecks.log` 和 `build.log`。

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release`：184 项通过。新增断言覆盖语言菜单、目标不翻译常驻、录音提示、三态音源实时切换、字幕容器/行参数/日期留白/tabular数字/空文本占位、纠正入口、存档右对齐、深浅/高对比表面资源和总结范围菜单。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore -p:PublishReadyToRun=false`：成功，0 警告、0 错误。该构建涵盖 WinUI XAML 编译，没有启动 GUI。
- PR #6 当前代码提交 `7056cdc25039a136c7adf0b3cce714e1fd1d0f07` 的 Actions 8 项全部通过：Windows CI run [38023773238](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38023773238)、Mac CI run [38023773203](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38023773203)、分发预检 run [38023773159](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38023773159)。
- 后续对当前 PR 头 `40a5a9ab3fdffbfba7dba22ed0c5fd1eeee85baf` 复验：CoreChecks 184 项通过，Release x64 构建 0 警告、0 错误；完整输出保存在 `corechecks-followup.log`、`build-followup.log`。当前头部 8 项 Actions 检查全部成功，原始 PR 状态保存在 `pr-checks-40a5a9a.json`；Windows run [38024058026](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38024058026)、Mac run [38024058025](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38024058025)、分发预检 run [38024058229](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38024058229)。
- 本次复验日志和 PR 快照的 SHA-256 见 `followup-sha256.txt`。
- `git diff --check`：通过。
- 没有启动 Echo 或 GUI、没有点击录音/访问用户存档、没有请求云服务、没有读取或保存真实音频。

## 未完成的视觉验收

目标任务附件目录仅包含目标说明；此前随消息提交的两张图实际是邮件撰写/回复 UI，不含 Echo 主界面，未复制进仓库。当前没有可用的 Mac/Windows Echo 对照截图，因此本底稿证明源代码参数、构建和静态/逻辑接线，**不证明实际窗口视觉一致**。用户明确保留视觉评估，本轮未启动窗口；同字幕内容、主题、窗口尺寸和 DPI 的并排截图验收等待用户反馈。

## A81 Mac 语义色层级复核

- Mac 基线仍为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`；Windows 起始头为 `776df24804c4a0a1c39d45d1077dd5ca8bf9e3bc`。
- 对照 `TranscriptViews.swift` 中的 `.tertiary`，将 Windows 两个语言菜单箭头及录音中提示改为 `TextFillColorTertiaryBrush`；语言名称和字幕元信息保留 secondary 层级。视觉规范表同时记录系统语义色、当前 Windows HEX 值、字号、间距、圆角及控件状态，并注明源码无法替代截图核验。
- CoreChecks 184 项通过；Release x64 构建 0 警告、0 错误。日志为 `a81-corechecks.log`、`a81-build.log`，对应文件哈希见 `a81-sha256.txt`。
- tertiary brush 的官方依据链接与摘要保存在 `a81-microsoft-theme-source.md`。
- 未启动 Echo/GUI、未连接服务或访问用户数据；`macOS/` 未修改。实际焦点/hover 与深浅主题的屏幕效果继续交由用户视觉验收。

## A82 录音按钮高度统一

- Mac 基线 `ae0359dc90da0ccb5e526a275da1747954a49a4f` 在 `EchoMacApp.swift` 使用一个录音切换按钮，最小宽度 126；开始与停止共享相同的原生控件高度。
- Windows 起始提交 `a2e8f0308b1813d84c9aa33a10d8e703e69fa9b9` 中开始按钮继承通用工具栏 32 DIP 最小高度，停止按钮为 34 DIP。开始按钮现显式设为 34 DIP；CoreChecks 同时断言开始/停止宽 126 DIP、高 34 DIP，避免两种状态跳动。
- CoreChecks 184 项通过，Release x64 构建 0 警告、0 错误。原始日志 `a82-corechecks.log`、`a82-build.log`，源码及日志哈希 `a82-sha256.txt`。
- 本次未启动 GUI；实际的控件边界、键盘焦点和截图验收仍由用户反馈。`macOS/` 未修改。
