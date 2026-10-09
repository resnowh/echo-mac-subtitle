# Windows A16 两小时合成存档压力检查底稿

日期：2026-09-30  
范围：两小时量级字幕存档的内存快照、JSON 往返及 SRT 生成。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 在 `Echo.CoreChecks` 中创建一个 Segment，含 7,200 条合成双语字幕，每条跨度一秒，模拟两小时连续文本；调用生产 `TranscriptFiles.Snapshot`、JSON 序列化/`Parse` 和 `TranscriptFiles.Srt`。
- 使用 `Stopwatch` 记录从快照创建到 SRT 字符串完成的总耗时；不包含任何文件写入、音频设备、网络或 UI 窗口。

## 原始观测

| 指标 | 结果 |
|---|---:|
| 条目数量 | 7,200 |
| JSON UTF-8 字节 | 2,501,561 |
| SRT UTF-8 字节 | 565,473 |
| 快照 + JSON 序列化 + JSON 解析 + SRT 导出 | 71 ms |
| 尾条内容/时间码 | `Synthetic line 7199`；到 `02:00:00,000` |

测试确认解析后保留首条、尾条和 7,200 条总数，生成的 SRT 含最后文本及两小时终点时间码。最新全套 `Echo.CoreChecks --audio` 共 54 项通过。

## 边界

- 71 ms 只是该机器上的内存流水线单次观测，不是稳定性能基准、UI 响应时间或真实录音吞吐结论。
- 未包含磁盘刷新/替换、实时 `ObservableCollection` 更新、窗口绘制、校对队列、峰值内存测量或长时间运行资源曲线。
- A16 的真实两小时录音、UI 不冻结、队列有界与中断恢复仍待专门长测。

## 数据留存

合成文字与测试代码随仓库保留；没有真实用户文字、录音、API Key 或云端响应。

## 2026-10-09 落盘补充

- 环境：Windows 11 build `10.0.26200`；.NET SDK `10.0.401`。
- 在随机 `%TEMP%` 子目录运行生产 `TranscriptFiles.AtomicWrite`：先写一份含单条字幕的旧检查点，再用 7,200 条合成字幕的完整 JSON 原子替换；随后从磁盘回读并解析当前档案与 `.bak`。
- 结果：JSON `2,501,560` bytes；保存、刷新到磁盘、替换并回读共 `100 ms`。当前文件保留全部 7,200 条及尾条字幕；`.bak` 保留先前检查点；未遗留 `.tmp` 文件。临时目录在测试结束时删除。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：51 项通过。Windows Release x64 构建：0 错误、10 条 NAudio 弃用警告。未启动应用、未枚举或使用音频设备、未调用云服务。
- GitHub Actions [37903408339](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37903408339)：Windows 与 macOS jobs 全部通过；Mac PCM/loopback 检查及 Debug/Release 构建通过。

这补齐一次完整大档案的原子落盘与恢复点检查，但不证明真实两小时录音的周期性检查点队列有界，也不覆盖 UI 响应、持续资源曲线或强退时序。
