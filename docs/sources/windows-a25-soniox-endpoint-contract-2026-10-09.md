# A25 Soniox 端点与翻译事件协议底稿

核验日期：2026-10-09  
范围：当前官方 Soniox 实时转写、语义端点、手动 finalization 和实时翻译文档；只用于核对 Windows token assembler 的事件语义。没有调用云 API、捕获用户音频或修改 macOS 源码。

## 官方来源

| 文档 | 记录的协议事实 |
|---|---|
| [Real-time transcription](https://soniox.com/docs/stt/rt/real-time-transcription) | provisional token 可被替换；final token 只发送一次、后续不会变化。 |
| [Endpoint detection](https://soniox.com/docs/stt/rt/endpoint-detection) | 端点使该 segment 的前置 token 定稿；`<end>` 在段尾出现一次且始终为 final。 |
| [Manual finalization](https://soniox.com/docs/stt/rt/manual-finalization) | 客户端发送 `{"type":"finalize"}` 后，服务端定稿此前音频并返回 final 的 `<fin>` 标记。 |
| [Real-time speech-to-text translation](https://soniox.com/docs/translation/stt-translation/rt-translation) | 原文与译文共用同一 WebSocket token 流；转写 token 先到，译文 token 随后分块输出，二者不保证一对一。 |
| [WebSocket API](https://soniox.com/docs/api-reference/stt/websocket-api) | token 的 `translation_status` 是可选字段；`speaker` 是可选字符串；译文 token 不带音频时间戳。 |

## 对 Windows 实现的影响

- Mac `SpeechViewModel.handleTranscriptResponse` 对 `<end>` 和 `<fin>` 不检查 token lane；它记录本条 WebSocket 响应是否出现端点，在处理完整条响应后最多结束当前双语字幕一次。
- Windows `TokenAssembler` 采用相同的响应级结束语义；`translation_status` 不改变端点游标。同一响应里多次出现端点也只结束一次，响应中后续 token 仍聚合到该字幕行。
- 译文可以在原文 token 之后到达，但按官方实时翻译流顺序，端点标记属于整个定稿 segment 的尾部。若翻译在端点之后才到达，不属于该协议说明的标准顺序；Mac 当前实现也不会把这种 token 回填到前一条已结束字幕。
- 连续的空 `<end>`/`<fin>` 不应跳过字幕索引或生成空行；Windows 回归对此单独检查。
- Mac `SpeechViewModel.handleTranscriptResponse` 在 provisional token 上只填入尚为空的 speaker/language 标签；final token 才能更新已存在的标签。Windows 逐字幕行实现相同语言规则，并以 `en` provisional → `ja` provisional → `ja` final 的固定序列验证。
- Mac 在处理每条响应前都把 `partialEnglish` 与 `partialChinese` 同时清空，再由本次 `tokens` 建立新快照；Windows 也整体清空两 lane provisional 缓冲，防止只更新原文时把上一响应的临时译文继续显示。CoreChecks 对“原文+译文 → 仅新原文 → 新的原文+译文”序列验证。

关于“翻译结果在端点标记前完成”是根据官方所述的统一流、原文先于译文、`<end>` 位于 segment 末尾综合得出的实现推论；官方页面没有逐例展示带翻译的 `<end>` 响应全文。因此保留端到端 token fixture 对拍待验状态。

## 本轮验证与留存

- 采用固定合成 token，在 Windows CoreChecks 中验证原文后到达译文、带可选 lane 元数据的 endpoint、同一响应多端点只结束一次、跨响应下一句游标和连续空端点。
- 所有输入均为固定合成内容，不含用户数据或凭据；测试会随 `windows/Echo.CoreChecks/Program.cs` 保留。
- 本底稿只记录官方协议文字和实现推论；macOS/Windows 完整 token 输出对拍仍记为尚未验证。
