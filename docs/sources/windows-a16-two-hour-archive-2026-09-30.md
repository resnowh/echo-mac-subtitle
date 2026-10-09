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

这补齐一次完整大档案的原子落盘与恢复点检查，但不覆盖 UI 响应、持续资源曲线或强退时序。

## 2026-10-09 周期检查点积压补充

- 检查发现：录音期间每 3 秒创建一份完整 Archive 快照；旧串行队列会为每份快照保留一个 Task 和快照引用。若慢磁盘持续落后，等待中的完整快照会不断占用内存。
- 实现调整：同一存档的持久化仍保持串行；允许一个写入执行中，等待区仅保留最新完整快照。较早的待写快照被新完整快照覆盖时，其等待者共享最新快照的完成结果。UI 后台错误观察器也按共享 Task 去重，避免每次定时检查点附加一个等待任务。
- 故障注入：让第一个写入停在门闩处并最终抛出 `IOException`，然后快速排队 9,999 个后续完整快照。受控写入器只看到失败的第一个快照和成功的最后一个快照；最终完整快照带有 10,000 个累计条目的版本，`FlushAsync` 等待它成功，9,999 个排队调用共享同一完成 Task。
- 本轮复跑：`Echo.CoreChecks` 51 项通过；完整档案 `2,501,561` bytes，原子写入/刷盘/替换/回读 `87 ms`。Windows Release x64 构建 0 错误、10 条既存 NAudio 弃用警告。未启动应用或访问音频设备。
- GitHub Actions [37903408339](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37903408339) 的 Windows 与 macOS jobs 全通过，覆盖此前的单次落盘检查。
- 队列合并改动推送后，GitHub Actions [37904572734](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37904572734) 的 Windows 与 macOS jobs 全通过，包含 51 项核心检查、Release x64 和 Mac Debug/Release 构建。

该测试证明队列在受控慢写下将等待快照数量限制为一个，并验证失败后仍保存最新完整状态；不测真实磁盘长时延迟、UI 定时器与绘制、两小时持续资源曲线或真实录音。
