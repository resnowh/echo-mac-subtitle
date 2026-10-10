# A80 Windows 主界面 Mac 信息层级对齐

日期：2026-10-10

Windows 分支：`feature/windows-ui-mac-parity`

起始提交：PR #5 head `b52f68efafd7fd1b0d1671ded4fc3f37c9a701e7`

Mac 参考：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`

## 源码依据

- Mac 主界面和菜单：`macOS/EchoMacApp.swift`
- Mac 字幕行、纠正按钮、信息留白及总结视图：`macOS/Views/TranscriptViews.swift`
- Windows 主界面：`windows/Echo.Windows/MainPage.xaml`、`MainPage.xaml.cs`、`App.xaml`
- 静态和逻辑回归：`windows/Echo.CoreChecks/Program.cs`
- 对照参数和源文件 SHA-256：本目录 `source-sha256.txt`

参考参数摘要：Mac 双列语言菜单之间留白 14 DIP；字幕双列也用 14 DIP 列距，字幕垂直留白 7 DIP，元信息到正文 5 DIP，正文行间距 3；音源控件最小宽 148 DIP，录音按钮最小宽 126 DIP；波形 48 条、宽 3 DIP、条间距 3 DIP、容器高 52 DIP；总结表面圆角 10、内边距 12、四级背景色 35% 不透明。对应依据是上述 SwiftUI 当前实现，没有从非相关附件推导设计。

## Windows 修改

- 顶部识别语言和翻译目标改为 Button + MenuFlyout，动态显示当前语言和勾选项；“不翻译”保留在目标位。录音中菜单注明下次录音生效，原设置对象和保存流程不变。
- 音频来源改成轻量菜单，但保留隐藏的 `Mode` 状态选择器及原 `AudioMode_SelectionChanged`，选择后继续走同一实时切换/持久化逻辑；保留三种模式、设备切换及原 AutomationId。
- 字幕列表项清掉 WinUI 容器的默认边距/内边距；每行上下留白 6 DIP，正文 15 DIP，元信息 12 DIP。纠正变成低强调铅笔按钮，保留基于字幕 ID 的 AutomationId。
- 底部常用按钮收紧到至少 32 DIP 高，录音按钮最小宽 126 DIP；停止录音用系统危险色语义资源。总结头部采用透明表面，同时保留范围、状态、复制/展开和现有生成逻辑。
- 沿用当前 Echo 主题字典、窗口尺寸、无障碍名称和波形 48 点/52 DIP 尺寸；未改 `macOS/`。

## 验证

原始输出保存在 `corechecks.log` 和 `build.log`。

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-build`：182 项通过。新增断言覆盖语言菜单接线、目标不翻译常驻、录音提示、三种音频模式热切换、列表容器 padding、纠正图标和总结表面。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore -p:PublishReadyToRun=false`：成功，0 警告、0 错误。此前带默认发布设置的同一 Release x64 构建也成功，0 警告、0 错误。
- `git diff --check`：通过。
- 没有启动 Echo 或 GUI、没有点击录音/访问用户存档、没有请求云服务、没有读取或保存真实音频。

## 未完成的视觉验收

目标消息里的两张附件经本机查看是邮件撰写/回复 UI，不含 Echo 主界面；出于隐私也没有将其复制到仓库。当前没有可用的 Mac/Windows Echo 对照截图，因此本底稿只证明源代码参数、构建和静态/逻辑接线，**不证明实际窗口视觉一致**。用户明确保留视觉评估，本轮未启动窗口；Mac/Windows 相同字幕内容、主题、窗口尺寸和 DPI 的并排截图验收等待用户反馈。
