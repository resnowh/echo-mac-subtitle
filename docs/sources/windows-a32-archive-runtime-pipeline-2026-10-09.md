# A32 Mac/Windows Archive runtime fixture pipeline

核验日期：2026-10-09
Mac 源码基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
范围：在 Windows-only 工作流中用 Mac 生产类型生成两段 Archive，Windows 导入/重写，再由 Mac 生产类型解码并比较 SRT。所有数据均固定合成内容。

## 数据来源与执行链

1. `windows/MacParityChecks/ArchiveParityChecks.swift` 与未修改的 Mac `TranscriptModels.swift`、`TranscriptArchiveStore.swift`、`SRTExporter.swift` 一起编译。生成步骤通过生产 `TranscriptArchiveStore.save/load` 写出两段 Archive 和通过生产 `SRTExporter.archiveText` 生成 SRT。
2. Windows CI 将 Mac artifact 传给 `windows/Echo.CoreChecks/Program.cs` 的 `ECHO_MAC_ARCHIVE_FIXTURE` 路径。Windows 解析 Archive、序列化并解析回存档，比较实际 SRT 与 Mac 生产 SRT，再上传 Windows 回存档。
3. 后续 macOS CI job 通过生产 `TranscriptArchiveStore.load` 解码 Windows JSON，逐字段比较 Archive/Segment/Subtitle/校对历史和日期，并要求生产 SRT 与 Mac 原始 SRT 完全相同。

固定数据包含原有 A17 校对历史/Apple epoch/可选字段案例，以及相隔 100 秒的第二段、speaker、language 和 recordedAt。该合成 fixture 不代表所有历史用户存档或真实设备数据。

## 留存数据底稿

本次 GitHub Actions 成功 run：`37944556906`，提交 `faf85d014c7dd3503a1e2e4287c01d93140354f7`。三个 job 均成功：Mac 生成 fixture、Windows CoreChecks + Release x64、Mac 生产 decoder/SRT 反向验证。原始 Actions artifacts：`echo-mac-archive-fixture`（artifact SHA-256 `8aa337afb4b2c96c9971cf258d1e4d5d4f05472d3f14bcc0a6d6c9d37d187d92`）和 `echo-windows-archive-roundtrip`（artifact SHA-256 `b2a89ab2ae82e8cc561cd30eb43fc2ae9a43ec2cfc6ae9585c5a413d89262fba`）。下载留存文件在 [`windows-a32-runtime-fixtures-2026-10-09`](windows-a32-runtime-fixtures-2026-10-09/)：

| 文件 | 字节 | SHA-256 | 内容摘要 |
|---|---:|---|---|
| [`mac-archive.json`](windows-a32-runtime-fixtures-2026-10-09/mac-archive.json) | 1,687 | `F69742375142917CA1AC264E8C8808C2A5A1AD4D5E40E2F1DF3BD691CBAAA574` | Mac 生产 ArchiveStore 保存的两个 segment、三个 subtitle；含 correction history、speaker/language、Apple epoch date 和空可选字段 |
| [`mac-archive.srt`](windows-a32-runtime-fixtures-2026-10-09/mac-archive.srt) | 127 | `585A65DC5E41DF1D8EB1D80DA97925F4AD5C24DA3E18984AB209C7072BDDE36F` | Mac 生产 SRTExporter 基准输出 |
| [`windows-roundtrip.json`](windows-a32-runtime-fixtures-2026-10-09/windows-roundtrip.json) | 1,917 | `EA247A13B79A96B935857C62A9D9B0361AE3667564D299493585B3EEA044E3CB` | Windows CoreChecks 导入并重写的同一 Archive；通过 Mac 生产 decoder/SRT 逐字段回读 |

fixture 仅含固定合成文本，不含用户 Archive、录音、凭据或云端响应。数据清单也已登记这些文件和来源 run。

## 状态与验证边界

- Windows 本机可运行 CoreChecks 和 Release 构建，但当前主机没有 Mac Swift runtime；生产生成/解码由本次 GitHub macOS runner job 验证。
- GitHub Actions run `37944556906` 三个 job 全部成功；artifact 与文件哈希见上方留存清单。
- A33 修复后的当前提交再次通过 run `37948130637` 三个 Archive parity job；同一 PR 的 Mac CI 与 unsigned-package preflight 也通过。此轮不重新下载相同的合成样本，底稿继续以首个留存 run 的 artifact 和哈希为准。
- 未读取用户 Archive、未启动 Echo、未改 Mac app 源码、Xcode 工程或 `tests/`。
