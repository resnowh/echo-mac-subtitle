# A32 Mac/Windows Archive runtime fixture pipeline

核验日期：2026-10-09
Mac 源码基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
范围：在 Windows-only 工作流中用 Mac 生产类型生成两段 Archive，Windows 导入/重写，再由 Mac 生产类型解码并比较 SRT。所有数据均固定合成内容。

## 数据来源与执行链

1. `windows/MacParityChecks/ArchiveParityChecks.swift` 与未修改的 Mac `TranscriptModels.swift`、`TranscriptArchiveStore.swift`、`SRTExporter.swift` 一起编译。生成步骤通过生产 `TranscriptArchiveStore.save/load` 写出两段 Archive 和通过生产 `SRTExporter.archiveText` 生成 SRT。
2. Windows CI 将 Mac artifact 传给 `windows/Echo.CoreChecks/Program.cs` 的 `ECHO_MAC_ARCHIVE_FIXTURE` 路径。Windows 解析 Archive、序列化并解析回存档，比较实际 SRT 与 Mac 生产 SRT，再上传 Windows 回存档。
3. 后续 macOS CI job 通过生产 `TranscriptArchiveStore.load` 解码 Windows JSON，逐字段比较 Archive/Segment/Subtitle/校对历史和日期，并要求生产 SRT 与 Mac 原始 SRT 完全相同。

固定数据包含原有 A17 校对历史/Apple epoch/可选字段案例，以及相隔 100 秒的第二段、speaker、language 和 recordedAt。CI artifact 可下载；可复跑脚本和工作流都保存在当前 PR。该合成 fixture 不代表所有历史用户存档或真实设备数据。

## 状态与验证边界

- Windows 本机可运行 CoreChecks 和 Release 构建，但当前主机没有 Mac Swift runtime；生产生成/解码由本次 GitHub macOS runner job 验证。
- GitHub Actions 当前提交结果：待本轮推送后运行并记录。
- 未读取用户 Archive、未启动 Echo、未改 Mac app 源码、Xcode 工程或 `tests/`。
