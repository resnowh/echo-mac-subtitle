# A79 Mac/Windows 生产音频转换对拍

日期：2026-10-10

## 范围与约束

对照 Mac `macOS/Audio/AudioCapture.swift` 中 `MacMicrophoneCapture.convert` 与 Windows `AudioCapture.ToMono16k` 实际使用的 NAudio/WDL 重采样路径。Mac 应用源码不修改；Mac CI 从源码提取原始 `convert` 方法到独立测试 harness，并为固定的 float32 PCM 输入生成 Mac PCM16 输出。Windows 使用同一份字节级输入和生产转换方法对照 Mac 工件。

合成输入包含 48 kHz 单声道 440 Hz、44.1 kHz 双声道不等幅 660 Hz、含 150 ms 前置静音的 48 kHz 单声道 523 Hz，以及 48 kHz 反相立体声抵消。数据不含语音、录音或用户内容。

## 比较合同

Mac 使用目标格式 16 kHz、单声道、交错 PCM signed 16-bit little-endian，与应用目标格式一致；Mac 生产方法以 1,024 帧输入块和同一个持续 converter 处理流。Windows 使用生产 `AudioCapture.ToMono16k` 输出浮点采样。比较包含输出帧数（容差 8 帧，即 0.5 ms）、最佳相对偏移（±4 帧）、边缘滤波区域以外的相关系数（至少 0.995）、相对 RMS 幅度差（至多 3%）、延迟起点（约 2,400 帧）和反相双声道静音。

重采样器由 AVFoundation 与 WDL 分别实现，滤波系数和边缘采样不要求逐字节相同；有限流的 converter priming/tail 也可能带来数个输出帧差异，因此帧数限差为半毫秒。合同对齐时间、声道折叠、幅度与信号形态。Mac 实际输出和详细差异数据由 CI artifact 归档；阈值只有在 CI 运行完成后才视为已验证。

## 来源与重现

- 输入：`windows/Echo.CoreChecks/Fixtures/audio-conversion-inputs.json`；确定性生成器：`windows/MacParityChecks/generate_audio_conversion_inputs.py`。
- Mac harness 模板：`windows/MacParityChecks/AudioConversionParityChecks.swift`；`windows/MacParityChecks/generate_audio_conversion_harness.py` 将 `MacMicrophoneCapture.convert` 原方法体提取到模板，不复制或改写转换逻辑。Mac 录音以 1,024 帧回调重用同一个 AVAudioConverter，harness 也以相同大小分块处理。
- Windows 对照：`windows/Echo.CoreChecks/Program.cs` 通过 `ECHO_MAC_AUDIO_CONVERSION_FIXTURE` 读取 Mac PCM16 artifact，并调用生产 `AudioCapture.ToMono16k`。
- GitHub Actions：`.github/workflows/windows-ci.yml` 的 `generate-mac-audio-conversion-fixture` 任务生成 Mac 生产输出；Windows `build-and-check` 下载工件并执行四种信号对照。
- fixture `audio-conversion-inputs.json` SHA-256：`AB56DBCAB16A61CE5ADEEF2E826AA2D170DE3C1ED7CE6E0982851B3D027B5170`。
- 输入生成器 `generate_audio_conversion_inputs.py` SHA-256：`95F334CB9D7CD61EDA52D05DD7026B6084E7913856F8EDEEB15AFA01A9DCDCA5`。
- 抽取器 `generate_audio_conversion_harness.py` SHA-256：`D96DB3AC3C841B87DDBEAA20D4BF57F65020707614D4B1C753130ED44F587D35`；Mac harness 模板 SHA-256：`D226474F9FE3AA7419A792F0D106EC24608F2F66811317A876895D2478B9E174`。
- 抽取后的原始 harness 保存在 `mac-production-conversion-harness.swift`，SHA-256：`03D6FA55D22EDEC32136146B5800600BA426B73B22B7351F66AAF3B6545939F6`。
- Windows CoreChecks 修改后 `Program.cs` SHA-256：`FD09B7916E43573CFF22D8BB1F1D5DAD0E0D814CE01AB6102308974AED6E0FE6`；workflow SHA-256：`437A39F0DA7DC77C9BEF18398295D030859B76A174B89A5BA26DE677AF5F6CC6`。
- 本机 CoreChecks：177 项通过，完整日志 `windows-corechecks.log` SHA-256：`62F71C86383B491DE350250ADB659A9F0EB3F497D256F83C382520A2F919B7B9`。Release x64 构建 0 警告、0 错误，日志 `release-build.log` SHA-256：`C9F14472D9C6089BC7259011CD375B8728ECC28450B7F4EB7E5616D043A39207`。

## 验证状态

首次 Actions run `38011310648` 成功编译并运行 AVAudioConverter harness，但 Windows CoreChecks 显示单次输入 buffer 的 Mac 结果少 6 帧；调查发现测试 harness 没有模拟 Mac 每 1,024 帧复用 converter 的实际生命周期。已修正 harness 为分块流式转换，并将输出长度限差记录为 8 帧（0.5 ms）；修复后的 Actions 待验证。本机 Windows CoreChecks 177 项通过，Release x64 构建 0 警告/错误；Windows 主机没有 Swift 编译器。没有真实音频设备、用户 Echo 窗口或云端服务参与。转换样本对拍不能证明真实设备时钟同步、WASAPI 欠载/拔插和双路长时间稳定性。
