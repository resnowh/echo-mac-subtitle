# Windows A39 Soniox 静默计时活动对齐

核验日期：2026-10-10。Mac 基准为 GitHub `origin/main` 的 `ae0359dc90da0ccb5e526a275da1747954a49a4f`。本底稿保留发现差异时的生产源码摘录、源文件校验和以及 Windows 修复与验证记录；不包含用户字幕或音频。

## 数据底稿与来源清单

| 来源 | 版本 | SHA-256 | 核验范围 |
|---|---|---|---|
| Mac `macOS/ViewModels/SpeechViewModel.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | 错误/finished 早退、所有其他有效响应更新时间、静默时长计算 |
| Windows `windows/Echo.Windows/ViewModels/MainPageViewModel.cs` | 本轮修改后 | `AD2F1F09356048EA1393C5420578319864B601AB82C4386C5015BF1AC3751499` | 响应时间更新点及 segmentation timer 的静默时长读取 |
| Windows `windows/Echo.Windows/Core/SonioxResponseActivity.cs` | 本轮新增 | `3DDBF6025520A42FE903277DEF899C6DAAC8F1C7EDEB93C61BBFF3D44C925C0C` | valid response、error、finished 分类 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 本轮修改后 | `88BF870F87B3AEECBA373C267449D5768A3C354C691612F19C3021E3982EC9BB` | 空 token、端点标记、finished、error 四种合成响应断言 |

## 保留的 Mac 行为摘录

Mac `handleSonioxMessage` 先对无效 JSON 和 `error_message` 返回；`finished == true` 时保存并结束会话后返回。其余有效响应会清空两侧 provisional 文本，并在解析 token 数组前执行：

```swift
partialEnglish = ""
partialChinese = ""
lastTokenReceivedAt = Date()
var reachedEndpoint = false
if let tokens = response["tokens"] as? [[String: Any]] {
```

因此有效的空 token 响应和仅含 `<end>`/`<fin>` 的响应同样刷新后备静默计时。端点本身会立即按语义规则结束字幕；结束服务响应和错误响应不刷新计时。后备计时为当前时间减去该响应时间。

## 差异与修正

Windows 原先仅在 `HasSpeechToken` 识别到非空普通文本 token 时更新 `lastTokenReceivedAt`。当服务持续返回空响应时，Mac 会认为转写仍有活动，Windows 却可能因旧时间戳达到本地静默阈值提前切句。

Windows 现以 `SonioxResponseActivity.ShouldResetQuietTimer` 判断有效响应：JSON 对象刷新计时；错误响应和 `finished: true` 不刷新。接收路径仍在 `SpeechSession` 解析/抛出服务错误之后触发，因此不会把 WebSocket 错误帧当作有效活动。计时字段改名为 `lastResponseReceivedAt`，强调其语义是响应到达时间。

## 验证

- 固定 JSON 检查：`{"tokens":[]}` 与仅含 `<end>` 的响应刷新计时；`{"finished":true}` 与 `{"error_message":"unavailable"}` 不刷新。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：100 项通过；不调用云服务，不采集或保存音频。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：成功，0 警告、0 错误；没有启动 Echo。
- 只修改 Windows 代码和文档；Mac 生产源码未修改。真实 Soniox 行为与长时间静默/重连压力仍需后续验收。
