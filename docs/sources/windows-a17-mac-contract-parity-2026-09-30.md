# Windows A17 Mac 存档与 SRT 契约对照底稿

日期：2026-09-30  
范围：Swift Codable Archive 结构、Date 数值编码及存档 SRT 输出行为。

## 比对依据

- `macOS/Models/TranscriptModels.swift`：`TranscriptArchive`、`TranscriptSegment`、`ArchivedSubtitle`、`SubtitleCorrection` 与历史 revision 的 Codable 字段。
- `macOS/Storage/TranscriptArchiveStore.swift`：存档直接使用默认 `JSONEncoder` / `JSONDecoder`，无自定义 Date 策略。
- `macOS/Services/SRTExporter.swift`：archive SRT 对条目排序、时间原点、speaker 行、英文空行过滤及毫秒转换的实现。
- 本机未安装 Swift/macOS runtime，未获得由 Mac 程序实际写出的样本。测试 JSON 按上述 Swift Codable 字段手工构造；结论是源代码驱动的 Windows 兼容回归，不是双平台 runtime 对拍。

## 发现并修复

1. **校对历史日期编码不兼容。** Swift 默认 `Date` 编码为 Apple reference date（2001-01-01 UTC 起的秒数）；Windows 原先把 `SubtitleRevision.Date` 写成 ISO 字符串且无法读 Swift 数字。Windows 现在对 `DateTimeOffset` 写 Apple epoch 数值，读取数值时转回 Apple epoch；同时接受旧 Windows ISO-8601 字符串以保留已存档数据兼容性。
2. **存档 SRT 行为差异。** Windows 改为让 `[Speaker N]` 独占一行，不导出仅中文条目，以全部字幕条目（包括无英文原文的条目）最早时间作为零点，并截断到毫秒；与 `SRTExporter.archiveText` 算法保持一致。检测语言继续保存在 Archive，Windows UI 仍展示检测值，但不写进 SRT。

## 验证

- CoreChecks 使用 Swift Codable 字段形状 JSON，包含 Apple epoch 日期、可选字段、校对锁定与 history revision；Windows 成功解析、快照、序列化后再解析，ID、RecordedAt 与 history Date 保持一致。
- 同一固定数据集含一条只有中文的较早记录，以及一条起点 2.3456 秒的英文/中文记录。Windows 输出与源代码预期完全一致（统一 LF 比较）：

  ```text
  1
  00:00:02,345 --> 00:00:03,580
  [Speaker 1]
  Hello
  你好
  ```

- 另验证旧 Windows ISO 字符串历史日期仍可读取。
- `dotnet run --project windows/Echo.CoreChecks -c Release -- --audio`：56 项全部通过；Release x64 `BuildAndRun.ps1 ... -SkipRun` 构建通过，0 错误、10 条既有 NAudio 弃用警告。未启动应用、未调用云服务或保存音频。

## 发布候选

修改后的 Release x64 另打包为 `windows/artifacts/Echo-Windows-x64-a17-20260930.msix`，122,703,308 bytes，SHA-256 `7C6313A7BE226365215D9E5A9B246515FB21F0CF4341B546E7E350209946FB35`。包签名者与 Publisher 一致，但 Windows 报证书链终止于未信任根；不属于生产可信签名。

## 未完成项

- 需要在 Mac runtime 上让实际 Swift encoder 生成 JSON，再由 Windows 解析、改写并让 Mac 重新读取；也要在两端运行同一 SRT fixture 对拍。
- 当前证明没有覆盖用户现存 Mac Archive 的全部版本或实际迁移数据。
- 未测试 MSIX 安装、窗口 UI、云服务或跨机器升级。

## 数据留存

测试 JSON 字段和值位于 `windows/Echo.CoreChecks/Program.cs`，均为固定合成内容。未收集用户档案、私钥、API Key 或 Mac 文件。
