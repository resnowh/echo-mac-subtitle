# Windows A36 双路 PCM 欠载混音对齐

核验日期：2026-10-10

## 数据底稿与来源清单

| 来源 | 版本 | SHA-256 | 用途 |
|---|---|---|---|
| Mac 生产混音器 `macOS/Audio/PCM16AudioPipeline.swift` | `origin/main` / Mac 基线 `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `AA1A4F48926B8441E8EA9610E32EC0DA557CC994D3331B77E744A5DB093CBDAC` | 判断 PCM 样本按时间位置对齐，且每个输出采样只平均当时确有数据的来源 |
| Windows `windows/Echo.Windows/Services/AudioCapture.cs` | 修改前 `83df9a8ddd4e29a43aa6a31eef89fa154c62bb6f`；本轮工作树源码见 PR 分支 | `665633AF613F3F2663954CF45568E8CB4FE7746EC1FABE9F5B5940FA81EC1106` | 检查 WASAPI 预缓冲、重采样、每路有效帧计数与混音实现 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 同上 | `20E5E392D2041A46A631425E30FB99D0105DAC471C74CDD83AC3D5FDBE2ACC48` | 保存欠载与混音的确定性合成检查 |

上述源码都保留在仓库检出中；本轮没有从用户设备采集或保存音频，也没有更改 `macOS/`。

## 差异与修正

Mac 的 `PCM16TimelineMixer` 依帧位置混合两个来源。对某一采样点，若两路都存在则求平均；只有一路存在时直接采用该路；两路都没有时输出静音。

Windows 原先使用 `BufferedWaveProvider.ReadFully = true`，欠载时以补零伪装成完整帧，然后无论一路是否有数据都除以总来源数。双路采集中若一路临时无帧，另一路会被错误衰减。

本轮将预缓冲设为只返回已捕获样本，由每路 `ReadOutputFrame` 保留实际有效帧数，再让 `AudioFrameMixer` 按每个采样点的有效来源数求平均。缺失样本仍输出静音，不会被当作真实样本；无有效来源时输出静音。普通重叠帧继续平均并限幅。

Windows 仍通过每个 WASAPI 缓冲队列和漂移控制器逐帧读取，未改造成 Mac 的绝对帧时间轴混音器。现有 150ms 延迟源合成检查测得起音 2402 帧（预期 2400，容差 800 帧）。因此 A36 只修复欠载时的振幅差异，不代表真实设备时序已完全对齐。

## 验证

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：97 项通过；包含重叠平均、一路欠载保留另一路音量、双路均缺样本输出静音、限幅，以及缓冲区空读返回 0。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：成功，0 警告、0 错误；未启动应用。
- `git diff --check`：通过。
- 未调用云服务、未录制或保存音频、未运行真实 WASAPI 设备验收。

## 后续验收

真实麦克风与 loopback 双路采集、设备切换/拔插、长时间时钟漂移，以及欠载后的恢复仍需在 Windows GUI 和实际声卡上验证。矩阵与验证汇总见 [mac-parity-matrix.md](../windows/mac-parity-matrix.md) 和 [testing.md](../windows/testing.md)。
