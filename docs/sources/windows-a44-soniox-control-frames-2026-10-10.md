# Windows A44 Soniox 控制帧边界对齐

核验日期：2026-10-10。Mac 基准为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。仅检查 Mac Swift handler；变更限于 Windows core、WinUI 传输层、合成测试及文档。未启动 Echo、未连接服务、未采集真实音频。

## 输入底稿与来源清单

| 来源 | 版本/状态 | SHA-256 | 用途 |
|---|---|---|---|
| Mac `macOS/ViewModels/SpeechViewModel.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | `handleSonioxMessage` 对错误与 finished 帧的判断 |
| Windows `windows/Echo.Windows/Services/SpeechSession.cs` | 本轮修改后 | `A1FED7F4180B8C8C14C348A9C04BBFAF03099DF00C79C2CB44E131F858D96C59` | WebSocket 接收、服务错误通知和 stop 等待 |
| Windows `windows/Echo.Windows/Core/SonioxResponseControl.cs` | 本轮新增 | `5E1D89C50CB021FE03B074089B87E0EF54F5DDBCC5CCEF6C42514EF334B18BFD` | 统一读取字符串错误消息与严格布尔 finished |
| Windows `windows/Echo.Windows/Core/SonioxResponseActivity.cs` | 本轮修改后 | `6BB27FBD4E013BD30BFBB27E82E0CAA75B335FA745D7D3FDBA63ED6C1D22FBDA` | 静默计时复用相同控制帧分类 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 本轮修改后 | `2B14A1A0A16A4E2D939B5FF470FD33D39FF5614FD5BA8F4B7F2808A9300A8B82` | 本地模拟 WebSocket 控制帧场景与纯解析断言 |

### 合成输入底稿

新输入均作为确定性字符串内嵌于 `Program.cs`，没有来自真实用户或云端的数据：

```json
{"error_message":"Synthetic service failure","request_id":"message-only-request"}
{"finished":"true"}
{"finished":true}
```

## Mac 与 Windows 行为差异

Mac `handleSonioxMessage` 对 `error_message` 做 Swift `as? String` 转换；字符串存在时报告服务错误并立即返回。它不要求帧必须同时带 `error_code`。Mac 仅在 `finished` 可转换为布尔 `true` 时才定稿、保存并关闭会话；字符串、数字等错误类型按未完成响应处理。

Windows 原接收器只在存在 `error_code` 时抛出服务错误，并对 `finished` 直接调用 `JsonElement.GetBoolean()`。因此无 `error_code` 的字符串错误会被当成普通响应，而字符串 `finished` 会抛 `InvalidOperationException`，提前把停止路径变成失败。

新增 `SonioxResponseControl` 统一规则：`error_code` 字段存在时保留 Windows 服务错误处理；否则仅字符串 `error_message` 识别为服务错误；`finished` 仅 JSON 布尔 `true` 才完成接收。错误帧在事件分发前被拦截，不进入字幕解析。静默计时也共用这套规则。

## 验证结果

- 新增纯逻辑矩阵：有效字符串错误、error-code-only、错误类型的 error_message、布尔/字符串 finished。
- 新增本地 loopback WebSocket：仅含字符串 `error_message` 的响应产生带 request ID 的非重试 `SpeechServiceException`，且不触发字幕消息事件。
- 新增停止握手集成检查：字符串型 `finished` 被忽略，后续 `finished: true` 正常完成 stop，期间没有 Failure 事件。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：119 项通过。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：0 警告、0 错误。
- GitHub Actions runs [`37967804957`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37967804957) 与 [`37967811281`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37967811281) 全部通过，含 Mac fixture 生成、Windows CoreChecks/Release x64 与 Mac Archive 回读。Mac CI build `37967811103` 和 unsigned package preflight `37967811206` 也通过。PR #5 当前检查全部通过，状态 CLEAN；Windows 分支仍未合并。

Mac 源码未改。合成网络测试不等于真实 Soniox 服务端兼容性或云端验收。
