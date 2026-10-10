# NAudio 3.1.0 WASAPI 采集接口依据

核对日期：2026-10-09。该记录固定本轮迁移采用的上游接口事实，便于后续复查；项目依赖锁定于 NAudio `3.1.0`，不以 upstream `main` 代替锁定版本。

## 来源

- [NAudio v3.1.0：Recording Audio with WasapiRecorder](https://github.com/naudio/NAudio/blob/v3.1.0/Docs/WasapiRecorder.md)：构造器、设备与 loopback 选项、事件及停止行为。
- [NAudio v3.1.0：WasapiRecorderBuilder.cs](https://github.com/naudio/NAudio/blob/v3.1.0/src/NAudio.Wasapi/WasapiRecorderBuilder.cs)：`Build()` 对指定设备的构造路径及自动路由限制。
- [NAudio v3.1.0：WasapiRecorder.cs](https://github.com/naudio/NAudio/blob/v3.1.0/src/NAudio.Wasapi/WasapiRecorder.cs)：事件缓冲区、静音标记和同步/异步释放实现。
- 本仓库 `windows/Echo.Windows/packages.lock.json`：直接依赖请求范围 `[3.1.0, )`，实际解析版本 `3.1.0`；本机 NuGet XML 文档位于 `naudio.wasapi/3.1.0/lib/net9.0/NAudio.Wasapi.xml`。

## 保存的接口基线

- NAudio 3.1.0 将 `WasapiRecorder` / `WasapiRecorderBuilder` 作为新 WASAPI 采集接口，替代旧 `WasapiCapture` 与 `WasapiLoopbackCapture`。
- 麦克风通过 `WithDevice(captureEndpoint).Build()` 创建；系统播放回采通过 `WithDevice(renderEndpoint).WithLoopbackCapture().Build()` 创建。`WaveFormat` 在构建后可读；`StartRecording`、`StopRecording`、`RecordingStopped` 和 `StoppedEventArgs.Exception` 保留停止/错误观察路径。
- `DataAvailable` 的委托接收 `ReadOnlySpan<byte>`、WASAPI flags、device position 和 QPC position。span 只在回调期间有效，缓冲层必须在回调返回前复制数据。
- `WasapiRecorder` 的静音包路径明确提供与包长度相同的真实零字节，避免把未定义的 WASAPI 缓冲区内容暴露给采集回调。
- `Dispose` 会停止采集并等待采集线程退出；`DisposeAsync` 提供非阻塞等待选项。本轮继续沿用已有同步生命周期和端点通知流程，不改为默认设备自动路由。
- 自动默认设备路由需异步 `BuildAsync()` 且不能同时指定 `WithDevice`，因此不用于当前显式端点/默认端点重建策略。

## 本轮实现边界

本轮只替换采集对象与回调签名，仍将捕获字节同步复制进原有 2.5 秒有界预缓冲；重采样、双输入混合、设备变化策略、错误处理和保存链不改。CoreChecks 增加 span→预缓冲 PCM 往返断言。源码构建不能证明具体声卡、loopback、静音包、拔插或睡眠恢复行为；这些仍需后续设备验收。

## 本轮验证结果

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：52 项通过；覆盖合成 PCM span 复制及既有存档、队列、音频策略和生命周期用例。未启用 `--audio`，未打开音频设备、未访问云服务、未保存音频。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release --no-restore /p:Platform=x64 /p:RuntimeIdentifier=win-x64`：通过，0 警告、0 错误；应用没有启动。
- `WasapiRecorder` 的 WASAPI 设备运行、静音真实设备包、设备热插拔和用户 UI 仍未验收。需要后续受控设备检查后，才可把迁移记为硬件验收通过。
