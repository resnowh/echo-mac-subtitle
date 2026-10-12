# A64 摘要输入与提示词对齐底稿（2026-10-10）

## 对照结论

Mac `macOS/ViewModels/SpeechViewModel.swift` 的 `requestAISummary` 会把每条候选内容编码为编号、按本地真实开始时刻格式化的时间、英文原文和中文翻译。Mac `macOS/Services/DeepSeekService.swift` 为 AI 总结提供固定 system prompt。Windows 旧实现只传 `[TimeLabel] Speaker English`，而且所有范围共用通用提示词；长归档分块本身已存在。

Windows 本轮调整 `TranscriptTextChunks`，优先使用字幕自身 `RecordedAt`，缺失时根据其所属 Segment 的 `StartedAt + Start` 计算 Apple epoch 时间；首条和本地跨日首条用 `MM-dd HH:mm:ss`，同日后续行用 `HH:mm:ss`。每行包含编号、英文和中文，speaker 不进入总结资料。`TranscriptSummaryPrompt` 为新增内容与当前段/全存档分别生成 Mac 对应提示词；请求 system prompt 对齐 Mac。原有每块 18,000 字符上限保留。

## Mac 来源摘录

`macOS/ViewModels/SpeechViewModel.swift:2038-2080` 的原文结构包含：

```text
\(index + 1). [\(timestamp)]
英文：\(original)
中文：\(translated)
```

同一文件 `:2053-2110` 定义完整总结与新增内容两套中文提示词。`macOS/Services/DeepSeekService.swift:61-68` 提供 system/user 消息，system 内容为“你是一个专业的会议和演讲总结助手。请用简体中文回答。”

## Windows 实现与检查

- `windows/Echo.Windows/Core/Transcript.cs:182-302`：按 Archive 计算真实本地时间、生成双语分块与 scope 提示词。
- `windows/Echo.Windows/ViewModels/MainPageViewModel.cs:887-917`：总结请求调用分块器、选择提示词并提交 Mac system prompt。
- `windows/Echo.CoreChecks/Program.cs:76-79,819-850`：接线契约、跨午夜编号/双语格式、Archive 时间回退、speaker 排除以及 scope 0/1/2 提示词检查。
- `corechecks.log`：150 项全部通过；末行确认未调用云服务、未保存音频。
- `release-build.log`：Release x64 成功，0 警告、0 错误。

没有调用 DeepSeek、读取用户存档、采集或保存音频，也未启动 Echo。真实云端摘要措辞、UI 流程和自动总结生命周期尚未实测。

## 来源源码哈希（SHA-256）

见同目录 `source-hashes.txt`。哈希针对本轮检查时工作树中的 Mac 对照文件与 Windows 源码/测试文件。
