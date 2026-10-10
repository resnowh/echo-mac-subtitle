# Windows A37 Soniox WebSocket 鉴权协议基线

核验日期：2026-10-10。此记录用于保证 Windows 适配当前协议，同时保护 Mac 源码不被 Windows 开发带入或修改。

## 官方协议数据底稿

来源为 Soniox 官方文档，访问日期 2026-10-10：

- [Speech-to-Text WebSocket API](https://soniox.com/docs/api-reference/stt/websocket-api)：实时端点 `wss://stt-rt.soniox.com/transcribe-websocket`；当前示例模型 `stt-rt-v5`；raw PCM 需配置 `audio_format`、`sample_rate`、`num_channels`。`api_key` 配置字段已标为 deprecated；仅在起始配置中携带密钥的旧连接从 **2027-01-15** 起会被拒绝。
- [WebSocket authentication](https://soniox.com/docs/guides/websocket-authentication)：握手使用 `Authorization: Bearer <key>`，配置 JSON 不放密钥；认证错误可在 WebSocket 建立后作为 error frame 到达。客户端应用推荐使用临时 API Key；长期 Key 应保留在可信后端。
- [Endpoint detection](https://soniox.com/docs/stt/rt/endpoint-detection)：`<end>` 表示当前语义端点，且最终化前序 tokens；端点设置与 speaker diarization 存在准确率权衡。

本轮摘录并留存的协议结论：Windows 应将密钥放在握手头、配置里排除密钥，并解析连接成功后收到的服务错误帧。只保存了相关规范摘要和官方链接；没有存储带用户凭据的网络流量或密钥。

## 代码来源清单

| 源码 | Commit/版本 | SHA-256 | 观察 |
|---|---|---|---|
| Mac `macOS/Services/SonioxWebSocketClient.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `A3A2658DB5823A5DF3D2C83D7FDB4B19D2A7431F5F329192AFE29489289B65FD` | 打开 WebSocket，不设置 Bearer header |
| Mac `macOS/ViewModels/SpeechViewModel.swift` | 同上 | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | 起始配置加入 `api_key` 与 `stt-rt-v5`，仍在官方截止日期之前兼容 |
| Windows `windows/Echo.Windows/Core/SonioxRequestBuilder.cs` | 本轮开始核验时 HEAD `76356a978226619f4653e2bd8344bfb9815cb040` | `3AC871F17F8E1D4A498CD36120FC4731BCD16ECFE3875104EDFF9A7148EA77E3` | 使用 `stt-rt-v5` 和 16 kHz mono `pcm_s16le`，配置不含密钥 |
| Windows `windows/Echo.Windows/Services/SpeechSession.cs` | 同上 | `49602B5ACE46E083B2B3BBEFBA345907D5148CFA05386CA331AB22EF0C37C9E5` | 握手设置 Bearer header；识别连接后的 error frame 并分类重试 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 同上 | `20E5E392D2041A46A631425E30FB99D0105DAC471C74CDD83AC3D5FDBE2ACC48` | 本地 WebSocket 模拟检查 header、无 api_key 配置及服务错误响应 |

## 判定与安全边界

Mac 现有请求在官方迁移截止日期前仍可用，但它已采用待迁移方式。项目明确禁止修改 Mac Swift 或 Xcode 工程，因此保持 Mac 原状；Windows 按当前推荐方式实现，属于记录过的协议行为差异，不是兼容失败。不要把 Mac 起始配置的旧字段复制回 Windows。

Windows 目前由用户在本机填写长期 Soniox Key，并以 DPAPI 当前用户范围保护静态设置；Key 仍会在进程内存和 WebSocket 握手中使用。若面向更多用户正式发布，应评估使用可信服务端签发的短期 Soniox Key；当前项目没有该后端，正式产品安全模型仍待决策。

## 可重复验证

`windows/Echo.CoreChecks/Program.cs` 的本地模拟 WebSocket 检查断言：握手头包含合成 Bearer 值、配置 JSON 不包含 `api_key`，且连接建立后的 401/402/403/429 错误不重试，503 和 413 `max_duration_reached` 按有限策略重试。该检查不发送真实 API Key、不调用 Soniox 云端。2026-10-10 整体 CoreChecks 为 97 项通过；真实云 Key 的端到端响应未做验证。
