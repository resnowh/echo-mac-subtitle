# A60 默认音频端点变化的会话检查

日期：2026-10-10

## 基线与读取范围

- macOS 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：PR #5 当前已发布 HEAD `b003bb631ca2f4a4a85f1fb797ad1dc03b8d38a4`；本次改动只扩展合成 CoreChecks，不改 macOS 源码。
- Mac 参考：`macOS/ViewModels/SpeechViewModel.swift`，当前 SHA-256 `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。读取 `setInputMode` 及来源启动/失败处理，确认 Mac 在会话中启动新来源，只有成功后才移除旧来源。
- Windows 参考：`AudioCapture.cs` SHA-256 `191D2FEAA86312E271F06E31974608F32C282BAD183E83F30ADA0E56CC73CBBA`；`SpeechSession.cs` SHA-256 `D520AC5EF721A5A9B09D6CB4F687D578040568B085100318610BA5BF46F0CF39`。
- 新增测试的 `windows/Echo.CoreChecks/Program.cs` SHA-256 `8F210290FF09A21E7DFEF35356862DF86B7F9253F630A0A5F3C4EB16D0E7A071`。

## 覆盖行为

A59 的测试先让合成话筒会话切换到电脑音频失败，再确认旧话筒恢复、同一个 loopback WebSocket 继续收到 PCM、会话没有发出失败事件。A60 复用该会话，向注入的合成采集器触发 `DataFlow.Capture` 默认设备变化；随后检查强制重启成功、合成活动端点 ID 改变、会话状态报告跟随系统默认设备，并且仍由唯一的原 WebSocket 接收音频。

这项检查覆盖 `SpeechSession` 的默认设备事件接线和切换事务；它不调用 NAudio 的真实系统设备通知，也不证明真实 WASAPI 设备的声学连续性、拔插时序或权限表现。

## 验证与原始结果

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release`：141 项通过。日志原件位于 `windows-a60-default-device-change-2026-10-10/test-results.txt`，SHA-256：`A20936792B7C145668F90F2AA81D312A6E04AACC8762CBA6BCC59F98D318AAD1`。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`：成功，0 警告、0 错误。日志原件 SHA-256：`A6305EA8D0C7D01C6DEE421B2B16846310F7F6F00B501DEDED2A256F1CE2B14D`。
- 未启动或安装 Echo，不访问麦克风/扬声器、Soniox、用户存档、字幕或 API Key；loopback WebSocket 仅绑定本机。
- 本次尚未推送，GitHub Actions 尚未验证本次测试增量。

## 后续验收

需要在隔离的 Windows 环境验证真实默认录音设备切换、拔除当前设备、无默认设备、明确选择设备后设备失联、麦克风权限拒绝，以及混合模式中一路失联时另一路的持续性。不得影响用户当前运行的 Echo。
