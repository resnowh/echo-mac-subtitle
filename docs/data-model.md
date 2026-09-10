# Echo 数据模型与持久化

## 核心模型

### `SubtitleEntry`

实时 UI 使用的字幕条目。字段如下：

- `id: UUID`：条目稳定标识，运行时使用；不单独决定时间。
- `start` / `end: TimeInterval`：当前 session 内相对录音开始的秒数。实时 Soniox token 有时间时优先使用 token 的 `start_ms/end_ms`；否则使用本地 session 经过时间。不是现实世界时间。
- `recordedAt: Date?`：条目开始时刻对应的本地现实时间。新条目会由 `sessionStartedAt + start` 生成；旧数据可能为空。
- `english` / `chinese: String`：英文原文与翻译文本。实时期间会包含 provisional 内容，完成后保留为最终内容。
- `speaker: String?`：可选的匿名显示值，如 `Speaker 1`。
- `language: String?`：Soniox 返回的 detected language code，如 `en`；没有返回时为空。

`SubtitleEntry` 本身是运行时模型，不直接 Codable。

### `ArchivedSubtitle`

`SubtitleEntry` 的 Codable 存档版本，字段语义相同，包含 `id/start/end/recordedAt/english/chinese/speaker/language`。它保留 session-relative 时间，同时尽量保留现实开始时间，供恢复 UI、总结和跨段 SRT 使用。

### `TranscriptSegment`

一次录音 session 在一个 Archive 中的持久化单元：

- `id`：segment 标识。
- `startedAt`：该 session 的现实本地开始时间。
- `updatedAt`：该 segment 最近一次保存时间。
- `entries`：该 session 的 `ArchivedSubtitle` 列表。

同一 Archive 可有多个 segment；每段的 `start/end` 都可能从 0 开始，不能跨段直接拼接。

### `TranscriptArchive`

可由多次录音接续的存档：

- `id`、`title`：存档标识与 UI 标题。
- `createdAt`、`updatedAt`：存档现实时间元数据。
- `segments`：按时间排序后用于恢复、总结和全量 SRT 导出。

### `RecognitionConfig`

识别请求配置，不是识别结果：

- `sourceLanguageMode`：`automatic` 或 `specified`。
- `specifiedSourceLanguage`：指定源语言 code，默认 `en`。
- `languageHints`：自动模式下传给 Soniox 的语言提示数组；提示不是识别结果。
- `strictLanguageRestriction`：是否将指定语言限制作为严格限制传给 Soniox。
- `translationEnabled`：是否请求翻译，默认开启。
- `targetTranslationLanguage`：翻译目标 language code，默认 `zh`。
- `speakerDiarizationEnabled`：是否请求 speaker diarization，默认开启。

语言列表集中在 `LanguageOption.supported`，当前 UI 提供 English、简体中文、日本語、한국어、西班牙语、法语、德语、意大利语、葡萄牙语、俄语、阿拉伯语和印地语，后续可继续扩展。

## Speaker 与 Language

- Soniox 的 speaker ID 只是匿名编号。Echo 将其显示/保存为 `Speaker 1`、`Speaker 2` 等，不判断“老师”“我”或具体真人身份。
- 不做跨 session 的真人身份绑定；同一个数字也不应被解释为跨 session 的同一个人。
- `detected language` 来自 Soniox token 的 `language` 字段，随字幕条目保存。
- `specified source language`、`language hints`、`strict language restriction` 属于 `RecognitionConfig`，描述请求意图，不等于最终 detected language。
- `target translation language` 属于请求配置；翻译文本本身保存在 `chinese` 字段（字段名保持兼容，即使目标语言被用户改成其他语言）。

## 时间模型

Echo 同时保留三种时间概念：

1. **session-relative `start/end`**：每次 WebSocket/录音 session 从零开始的相对秒数，适合该段的实时显示与单段 SRT。
2. **现实本地 `recordedAt`**：字幕开始时刻的本地 `Date`，适合 UI 显示真实时钟和跨段排序。
3. **Archive 导出 timeline**：`SRTExporter.archiveText` 以 Archive 中最早的现实时间为参考零点，将每条字幕映射到连续 timeline，并用 `lastEnd` 保证单调递增。

因此不能直接把多个 session 的 `start/end` 拼成整段 SRT：每段都可能从 0 重新计时，直接拼接会造成时间倒退或不同段字幕重叠。缺失 `recordedAt` 的旧条目使用 `segment.startedAt + entry.start` 回退计算。

## 持久化与向后兼容

- Archive JSON 写入 `~/Library/Application Support/Echo/Archives/`，由 `TranscriptArchiveStore` 统一读取/保存。
- 当前录音的 SRT 会自动写到用户的“下载”文件夹；界面的“导出全部字幕”使用保存面板另存。默认不保存音频。
- `speaker`、`language` 和 `recordedAt` 都允许为空。旧 Archive 没有这些字段时，Swift Codable 对可选字段按 `nil` 解码，旧条目仍可读取；导出与 UI 会使用 segment 时间或可用的相对时间回退。
- UserDefaults 中已有的 Soniox/DeepSeek Key、主题、输入源和总结开关 key 保持不变；新的识别设置使用独立 key，缺失时采用 English → 简体中文的默认行为。
- SRT 是导出文本，不作为 Archive 恢复源；老 SRT 不需要迁移，重新从 Archive 导出即可获得跨 segment 的新 timeline。
