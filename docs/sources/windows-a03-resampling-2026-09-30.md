# Windows A03 采样率与输入错位验证底稿

- 日期：2026-09-30
- 命令：`dotnet run --project windows/Echo.CoreChecks -c Release -- --audio`
- 环境：Windows 11 家庭版中文版，版本 `10.0.26200`（Build `26200`）；.NET SDK `10.0.401`
- 样本：测试代码即时生成 1 秒常量幅度合成信号；44.1 kHz 双声道、48 kHz 单声道，以及含 150 ms 前置静音的 48 kHz 双声道。原始音频样本未写入文件；生成规则保存在 `windows/Echo.CoreChecks/Program.cs` 的 `FiniteToneSampleProvider`。
- 结果：三种重采样输出均为 16,000 个样本；150 ms 输入延迟测得起点 2,402 样本（理论值 2,400，误差 0.125 ms）。后续本机音频/本地模拟 WebSocket 检查全部通过，总计 41 项。

生产采集与测试共同调用 `AudioCapture.ToMono16k`，该函数负责声道折叠及 WDL 重采样。此合成检查证明率转换和延迟保留，不等于两台实际声卡之间的时钟/相位同步测量；不同物理设备的 44.1/48 kHz 组合和长时间误差仍待实机验收。未调用云端、未保存音频、未启动 Echo 窗口。

