# Changelog

## Unreleased

### Added

- 支持 Soniox speaker diarization，并在字幕、Archive 和 SRT 中保留匿名 Speaker 编号。
- 增加自动识别/指定源语言、语言提示、严格语言限制和可配置翻译目标的 `RecognitionConfig`。
- 为 Soniox 建连前增加约 2.5 秒有界音频缓冲。
- 增加“总结新内容”“重新总结当前录音段”“总结整个存档”三种 AI Summary 范围。

### Changed

- 电脑音频和话筒现在先按统一 PCM 格式在本地按时间轴混音，再发送一条 Soniox 音频流。
- Archive、SRT 和总结使用现实开始时间处理跨录音段内容，同时保留 session-relative 时间。
- 将模型、音频采集/处理、服务、存储和字幕视图拆分为独立文件，保留现有 macOS SwiftUI 使用方式。

### Fixed

- 修复双输入模式把两路 PCM 直接拼接造成的时间轴破坏。
- 修复 WebSocket 尚未 ready 时首句音频被丢弃，以及 stop/clear/failure 后旧 session 音频残留的问题。
- 修复整存档 SRT 在多段录音接续时可能时间倒退的问题。
