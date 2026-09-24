# Changelog

## Unreleased

### Added

- 支持 Soniox speaker diarization，并在字幕、Archive 和 SRT 中保留匿名 Speaker 编号。
- 增加自动识别/指定源语言、语言提示、严格语言限制和可配置翻译目标的 `RecognitionConfig`。
- 为 Soniox 建连前增加约 2.5 秒有界音频缓冲。
- 增加“总结新内容”“重新总结当前录音段”“总结整个存档”三种 AI Summary 范围。
- 增加 macOS 睡眠/唤醒后的录音自动恢复，以及音频设备配置变化恢复。

### Changed

- 字幕采用惰性行布局，移除逐次拼接全部历史文本的滚动观察；波形 UI 更新限制为每秒 20 次。
- 控制区在窄窗口自动换行，存档提示单独显示，总结展开区域限制高度。
- Soniox PCM 串行发送，积压限制为 160 KB（约 5 秒音频）；发送或建连超时会明确失败，不无限积压。

- 电脑音频和话筒现在先按统一 PCM 格式在本地按时间轴混音，再发送一条 Soniox 音频流。
- Archive、SRT 和总结使用现实开始时间处理跨录音段内容，同时保留 session-relative 时间。
- 将模型、音频采集/处理、服务、存储和字幕视图拆分为独立文件，保留现有 macOS SwiftUI 使用方式。
- 睡眠前结束当前 TranscriptSegment，唤醒后在同一 Archive 中创建新的 session/segment。

### Fixed

- 消除混音热路径中逐采样点重复解码整块 PCM 的开销。
- 连接回调校验对应的 WebSocket，避免旧连接失败影响新连接；结束标记排在已接受音频之后。
- 文字稿保存节流提前到历史文本复制之前，避免未到保存周期时重复遍历。

- 修复双输入模式把两路 PCM 直接拼接造成的时间轴破坏。
- 修复 WebSocket 尚未 ready 时首句音频被丢弃，以及 stop/clear/failure 后旧 session 音频残留的问题。
- 修复整存档 SRT 在多段录音接续时可能时间倒退的问题。
- 修复 Mac 合盖睡眠后 UI 仍显示录音但底层话筒/系统音频 callback 已失效的问题；旧 Soniox 连接和音频状态不会带入唤醒后的新 session。
