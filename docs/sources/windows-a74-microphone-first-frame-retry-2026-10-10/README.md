# A74 麦克风首帧检测与有限重试

日期：2026-10-10

## 基线

- Mac 只读基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
- Windows 分支：`feature/windows-mac-parity`
- Windows 起始提交：`c101322cb786629696b721c61f34dbeb6afcfd35`
- 本轮开始前 `origin/feature/windows-mac-parity` 与本地分支一致；已 fetch `main`、`feature/windows-preview` 和当前功能分支。

## Mac 行为基准

`macOS/ViewModels/SpeechViewModel.swift` 设定首个麦克风原始回调超时 1.2 秒，启动重试延迟 450ms、最多重试 2 次。回调已到但转换后的 PCM 未到时另有 2.5 秒健康检查。本目录的 `mac-microphone-startup-reference.txt` 留存相关源码摘录；Mac 源码 SHA-256：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。摘录 SHA-256：`6B93394A32D32A28C5D58865E2AD7E0C1C889B33D59BCC7C3B4C9DA8EF94B190`。

## Windows 变更与验证

- `AudioCapture` 为话筒源登记首个非空数据回调，并在缓冲写入成功后标记首帧就绪。
- 初始话筒或混合模式在 1.2 秒内未收首帧时，间隔 450ms 最多重启采集两次；实际回调异常仍走采集错误提示。
- 录音中切换的候选话筒必须先送首帧才提交；超时放弃候选、保留旧采集和原识别连接。打开候选设备直接失败时立即保留旧源。
- CoreChecks 使用合成采集器与本机 loopback WebSocket，检查无首帧后的启动重试、默认话筒候选超时后重试、旧输入继续发送 PCM，且连接始终只有一个。
- `corechecks.log`：162 项通过；SHA-256 `F6426A0D6CC03AF57401A556E6C97EBA1E604E2C3CA7D38534B22D7AA447ED50`。
- `release-x64-build.log`：Release x64 构建成功，0 警告、0 错误；SHA-256 `F5BCA8EB091DC4338BFDD6D3553C15A655726876882A626313ABFC7B0F3FC20A`。

## 源文件 SHA-256

- `windows/Echo.Windows/Services/AudioCapture.cs`：`70EC4797A2E8FEFF120C6DA6EE6A3F74EFA6AB56C62707EF614022C74DFB2077`
- `windows/Echo.Windows/Services/SpeechSession.cs`：`A34C114F31490682188D735723DA1CAB7880742B6BAF060F759D467E0D779BBD`
- `windows/Echo.CoreChecks/Program.cs`：`A4D0ADA2E3D3CDF4F9BA2A5862092BB942112155D0CD10101746E203F22AAD98`

## 验收边界

没有打开麦克风、保存录音、调用 Soniox 或操作当前 Echo 进程。合成测试不验证 WASAPI 实际设备行为、权限拒绝/恢复、设备拔插或声音连续性。Mac 还对“原始输入回调正常但转换 PCM 未产生”单独设置 2.5 秒检测；Windows 尚无等价的独立转换健康监控，仍列为待完成差异。
