# A59 录音中音源切换失败回滚检查

日期：2026-10-10

## 基线与源代码

- macOS 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：`cfb702e6fb7bd7eb9772b832a0b18634f2393a04`。
- Mac `macOS/ViewModels/SpeechViewModel.swift` SHA-256：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。
- Mac `macOS/Models/TranscriptModels.swift` SHA-256：`CAEAB4EBA2797901B91C212F535B343FFE0AB77730E4A8BD73ABBF6464AC8E16`。
- Windows `windows/Echo.Windows/Services/AudioCapture.cs` SHA-256：`191D2FEAA86312E271F06E31974608F32C282BAD183E83F30ADA0E56CC73CBBA`。
- Windows `windows/Echo.Windows/Services/SpeechSession.cs` SHA-256：`D520AC5EF721A5A9B09D6CB4F687D578040568B085100318610BA5BF46F0CF39`。
- Windows `windows/Echo.CoreChecks/Program.cs` SHA-256：`F356F4552470E95A3CD0810B49BF96D3545F2CF4E0A20AB995CC236436B10884`。

## 行为和变更

Mac `setInputMode` 会先启动新的采集源；成功后移除不再需要的旧源。麦克风授权或来源启动失败时保留原模式和当前录音。Windows 的 `SpeechSession.SwitchDevicesAsync` 暂停音频发送并排空尾部，再重启 WASAPI 来源；新来源失败时重新启动旧模式，恢复失败才报告不可继续的设备错误并停止录音。

Windows 现将 `SpeechSession` 的采集依赖表示为 `ISpeechSessionCapture`，正常应用仍默认创建原有 `AudioCapture`。CoreChecks 注入合成采集器，模拟切换到电脑音频时启动失败，并检查旧话筒恢复、没有会话 `Failure`、恢复后仍发送 PCM，且只建立一个本地 WebSocket。该测试覆盖实际 session 事务和发送路径，不只是策略函数。

## 验证与限制

- 本机 140 项 CoreChecks 通过；Windows Release x64 构建成功，0 警告、0 错误；`git diff --check` 通过。
- 没有打开真实麦克风/扬声器、保存音频或调用 Soniox；WebSocket 仅绑定本机 loopback。
- A59 代码提交后的 Windows CI、Mac CI 与 unsigned package preflight 待运行。
- 合成回滚不证明真实 WASAPI 声学连续、系统授权、独占设备占用或拔插行为；仍需隔离硬件验收。

## 文件

- `windows/Echo.Windows/Services/AudioCapture.cs`
- `windows/Echo.Windows/Services/SpeechSession.cs`
- `windows/Echo.CoreChecks/Program.cs`
- `docs/windows/functional-parity.md`
- `docs/windows/mac-parity-matrix.md`
- `docs/windows/testing.md`
- `docs/roadmap/数据清单.md`
