# Echo 当前架构

本文只描述当前仓库已经实现的 macOS 版本，不把 Windows 或发布自动化写成现成功能。

## 整体数据流

```text
Audio Source
  -> macOS Capture
  -> 16 kHz / mono / PCM s16le conversion
  -> timeline mixer or bounded pre-buffer
  -> Soniox WebSocket
  -> token processing
  -> SubtitleEntry
  -> Archive / SRT / SwiftUI
  -> optional DeepSeek summary
```

- **话筒链路**：`MacMicrophoneCapture` 使用 `AVAudioEngine` 输入节点采集 PCM。`SpeechViewModel` 用 `AVAudioConverter` 转成 16 kHz、单声道、交错的 signed 16-bit little-endian PCM，再交给音频串行队列。
- **电脑音频链路**：`MacSystemAudioCapture` 使用 ScreenCaptureKit 的音频流。收到的 `CMSampleBuffer` 被转换成同一目标格式，再进入音频串行队列。捕获电脑音频需要系统的“屏幕与系统音频录制”权限。
- **双输入混音**：两路采集独立进行，以各自的 PCM frame position 放入 `PCM16TimelineMixer`。混音器按重叠 frame 对齐，多个输入的 signed 16-bit sample 取平均并限制在 Int16 范围内；不会把两段 PCM 直接首尾拼接。单输入模式绕过混音器，仍发送一条连续 PCM 流。输入源切换时保留当前 Soniox 会话和文字。
- **建连前缓冲**：`PCM16Prebuffer` 最多保留约 2.5 秒的完整 PCM 块。WebSocket 配置发送成功并标记 ready 后，按 FIFO 顺序 flush；新 session、停止、失败和清空都会清理它。
- **Soniox token 处理**：WebSocket 返回的 finalized token 与 provisional token 分开累积。`<end>`/`<fin>`、句末标点、长度和静默时间共同触发一条字幕完成，避免字幕长期只更新一行。翻译 token 写入中文字段，原文 token 写入英文字段；同一字幕中 speaker 改变时先完成前一条。
- **Speaker / language**：请求开启 speaker diarization 和 language identification。token 的 `speaker` 保存为 `Speaker 1` 形式，`language` 保存为语言代码，随后进入 `SubtitleEntry` 和 `ArchivedSubtitle`。当前只使用匿名编号，不推断真人身份。
- **Archive**：录音开始时可新建存档或选择已有存档接续。每次 session 是一个 `TranscriptSegment`，识别到的非空字幕会定期写入 `Application Support/Echo/Archives/*.json`；旧存档缺少新增可选字段时仍可加载。
- **SRT**：当前 session 使用 session-relative `start/end` 导出。整存档导出时按 segment 的 `startedAt` 和 entry 的 `recordedAt` 建立跨 session 的相对 timeline，并强制保持单调递增；字幕正文可带 `[Speaker 1]` 前缀，但不会改变标准 SRT 时间行。
- **AI Summary**：可选的 DeepSeek 服务只接收文字稿，不接收音频。主界面分别支持总结新增内容、重新总结当前录音段、总结所选存档；总结提示中使用字幕的现实本地开始时间，英文原文优先于机器翻译，并要求对识别/翻译错误保持保守修正。

## 模块职责

- `Models/TranscriptModels.swift`：平台无关的字幕、存档、输入源、主题和识别配置模型。
- `Audio/AudioCapture.swift`：音频采集抽象；`AudioCaptureSource` 描述 PCM 话筒源，`SystemAudioCaptureSource` 描述系统音频源。
- `Audio/AudioCapture.swift` 中的 `MacMicrophoneCapture`：macOS `AVAudioEngine` 话筒实现。
- `Audio/MacSystemAudioCapture.swift`：macOS ScreenCaptureKit 系统音频实现。
- `Audio/PCM16AudioPipeline.swift`：统一 PCM 块表示、双输入按 frame 对齐混音、以及有界 pre-buffer。该文件不依赖 SwiftUI 或 ScreenCaptureKit。
- `Services/SonioxRequestBuilder.swift`：把 `RecognitionConfig` 映射到 Soniox request 字段，包括语言提示、严格限制、翻译、语言识别和 speaker diarization。
- `Services/SonioxWebSocketClient.swift`：只负责 Soniox WebSocket 的建立、配置发送重试、接收循环、PCM 发送和关闭。
- `Services/MacLifecycleObserver.swift`：集中注册和清理 macOS 睡眠、唤醒及 `AVAudioEngine` 配置变化通知，不承载录音业务。
- `Models/LifecycleRecoveryState.swift`：平台无关的睡眠/唤醒恢复状态机，防止重复恢复并区分用户停止与系统生命周期事件。
- `ViewModels/SpeechViewModel.swift`：协调录音 session、采集生命周期、WebSocket、token 状态、存档和 UI 发布状态。
- `Services/SRTExporter.swift`：只负责 session 或 archive 的 SRT 时间线与正文格式化。
- `Storage/TranscriptArchiveStore.swift`：负责 Archive JSON 的目录、编码、保存和读取。
- `Services/DeepSeekService.swift`：构造 DeepSeek Chat Completions 请求；不依赖 SwiftUI，也不持有总结状态。
- `Views/TranscriptViews.swift`：字幕双栏滚动、Speaker/时间显示、Markdown 总结显示、输入源标签和波形视图。
- `EchoMacApp.swift` 中的 `ContentView`/`SettingsView`/`WindowAccessor`：macOS SwiftUI 界面、设置入口和窗口置顶行为；核心业务由 `SpeechViewModel` 提供。

依赖方向是：采集实现 → 音频抽象/PCM pipeline → `SpeechViewModel` → Soniox transport / token processing / 存储 / SwiftUI。Soniox request 和 transport 已独立于 View；session 生命周期与 token 业务协调仍由 `SpeechViewModel` 负责。

## 平台边界

- **macOS-specific**：`MacMicrophoneCapture`、`MacSystemAudioCapture`、`AVAudioConverter`/`AVAudioEngine`、ScreenCaptureKit、`NSWindow` 置顶、NSSavePanel 以及当前 Xcode app target。
- **平台无关**：`TranscriptModels`、`PCM16TimelineMixer`、`PCM16Prebuffer`、`SRTExporter`、`TranscriptArchiveStore` 的数据和纯逻辑部分、`RecognitionConfig` 到 request 字典的映射、DeepSeek request 构造。
- **未来 Windows**：实现与 `AudioCaptureSource`/`SystemAudioCaptureSource` 等价的 Windows 采集适配器即可接入相同的格式转换、混音、WebSocket、字幕和存储层；电脑音频预计使用 WASAPI。当前仓库没有 Windows 实现，也不承诺 Windows 编译。

## 并发与生命周期

- 每次录音拥有 `activeSessionID`。采集回调、WebSocket 接收和延迟重试都携带 session ID；回调发现 ID 不再匹配时直接丢弃，防止旧连接污染新 session。
- 音频数据进入 `local.echo.soniox-audio` 串行队列。混音器、PCM frame cursor 和 pre-buffer 的写入集中在这条队列；UI 状态更新回到主线程。
- recording session 开始时重置 speaker/language、token 累积、时间基准、frame cursors、mixer、pre-buffer 和当前字幕状态。
- WebSocket 先发送配置，配置成功后才置 `socketReady` 并 flush pre-buffer；ready 之前的音频不会被静默丢弃。
- stop 会停止采集、完成最后一条字幕和文件保存，处理混音器尾块，然后关闭 WebSocket；pre-buffer/mixer/cursors 必须清空。failure 和 clear 也停止采集、取消连接并清空这些 session 状态。
- `MacLifecycleObserver` 在主线程集中接收 `NSWorkspace` 的 will-sleep/did-wake 和 `AVAudioEngine.configurationChangeNotification`。睡眠前若正在录音，会保存并结束当前字幕/segment、停止两路采集、刷新并清空 mixer/pre-buffer、取消旧 WebSocket，再使旧 `activeSessionID` 失效；唤醒后等待短暂的音频设备恢复窗口，使用原输入源和 Archive 建立新 session/segment。睡眠期间未录音时，唤醒不会自动开始录音。
- 音频配置变化不等同于业务上的 `isRecording`：若录音仍应继续且话筒 tap/engine 已中断，ViewModel 会串行、限频地重装话筒捕获；失败时停止录音并显示恢复失败。ScreenCaptureKit 的旧 stream 不复用，双输入必须等待话筒和电脑音频都恢复后才回到正常状态。所有延迟恢复任务和旧回调都通过 session ID、生命周期状态及取消逻辑防止重复或污染新 session。
- Archive 的 segment 可以在一个录音结束后保留，供后续录音接续；这不会复用旧 session 的音频 buffer 或 Soniox connection。
