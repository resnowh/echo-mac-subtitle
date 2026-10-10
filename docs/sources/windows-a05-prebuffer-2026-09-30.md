# Windows A05 建连缓冲验证底稿

- 日期：2026-09-30
- 命令：`dotnet run --project windows/Echo.CoreChecks -c Release -- --audio`
- 环境：Windows 11 家庭版中文版 `10.0.26200`（Build `26200`）；.NET SDK `10.0.401`；NAudio 3.1.0（依赖版本见 `windows/Echo.Windows/packages.lock.json`）
- 测试结果：初次 45 项全部通过；A06 元数据改动后再次运行共 46 项全部通过；当前 Release x64 构建通过，0 错误、5 条 NAudio 弃用警告。

## 原始用例与观测

1. `BoundedAudioPrebuffer` 使用 16 kHz、16-bit、单声道格式时容量为 2.5 秒（80,000 bytes）；再写入 640 bytes 会抛出 `AudioPrebufferOverflowException`，错误信息说明录音将停止以避免静默丢失。
2. 本机麦克风采集与本地模拟 WebSocket 握手：服务端延迟接受 1 秒；`StartAsync` 返回时测得 0.98 秒输入音频缓冲，首段音频在握手前已采集。
3. 连接成功后 200 ms，预缓冲从 0.98 秒下降至 0.11 秒（重复运行约 0.11～0.12 秒），发送任务已追上大部分建连期间缓存，没有留下永久的一秒字幕延迟。
4. 服务端延迟 3.2 秒时，2.5 秒预缓冲先触发错误，连接启动及时失败，错误可见；不会继续无提示地丢弃新音频。
5. WebSocket 成功后先快速发送已缓存帧，再按实时节奏发送新帧；该行为通过同一运行中的本地模拟会话验证。

底稿代码为 `BoundedAudioPrebuffer`、`AudioCapture.ToMono16k` 和 `Echo.CoreChecks` 中的本地 WebSocket 延迟用例。未调用云服务、未保存 PCM 或设备 ID、未启动 Echo 窗口。此结果只代表当前机器与本地模拟服务；其他声卡、持续网络退化和设备切换中的缓冲恢复仍待验证。
