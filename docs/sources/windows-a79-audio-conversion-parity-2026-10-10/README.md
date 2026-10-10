# A79 Mac/Windows 生产音频转换对拍

日期：2026-10-10

## 范围与输入

对照 Mac `macOS/Audio/AudioCapture.swift` 的生产 `MacMicrophoneCapture.convert` 与 Windows `AudioCapture.ToMono16k`。Mac 应用源码保持未修改；CI 从 Mac 源码抽取生产方法生成独立 Swift harness，以固定 float32 PCM 输入输出 16 kHz、单声道 PCM16 little-endian 参考。Windows 对同一输入调用生产转换器。没有真实录音、用户 Echo 进程或云服务参与。

固定合成输入有四组：48 kHz mono 440 Hz；44.1 kHz stereo 660 Hz（左右声道幅度不同）；48 kHz mono 523 Hz 且延迟 150 ms 起音；48 kHz 997 Hz 反相 stereo。原始输入 fixture 在 `windows/Echo.CoreChecks/Fixtures/audio-conversion-inputs.json`，SHA-256 `AB56DBCAB16A61CE5ADEEF2E826AA2D170DE3C1ED7CE6E0982851B3D027B5170`。

## Mac 基线发现与修复

首次生产对拍的 Windows CoreChecks Actions run `38011642331` 暴露了真实行为差异：Mac 转 mono 时保留第一个输入声道，Windows 原实现平均所有声道。44.1 kHz stereo 例中 Mac RMS 为 `0.127276`，Windows 平均实现与 Mac 的相对 RMS 差异达 `122.020%`；反相双声道的 Mac 输出 RMS 为 `0.212119`，表明 Mac 没有把两声道相消。此原始 Mac 输出保存在 `failed-run-38011642331/mac-audio-conversion.json`，SHA-256 `9FECB77BBC80B96AD13D51F17877F9D9543CD5051A2FC7C6CE91C02DFC195BED`。

Windows `ToMono16k` 现对多声道输入取首声道，再以 WDL 重采样；CoreChecks 另外断言不等幅 stereo 选左声道，且右声道反相不会抵消左声道信号。比较时把 Windows 样本按生产 PCM16 输出规则量化，并以 0.1 帧步进做最佳时序对齐，降低 AVFoundation/WDL 不同滤波器群延迟对波形相关系数的影响。接受条件为总帧数差不超过 8 帧（0.5 ms）、最佳偏移不超过 4 帧、相关系数至少 0.995、相对 RMS 差异不超过 3%；延迟起音另检查 150 ms 时间位置。该对齐改善了测量精度，不放宽转换结果的幅度合同。

## 当前本机验证

使用上次失败 Actions 保存的 Mac 生产 artifact 对照当前 Windows 生产实现：184 项 CoreChecks 全部通过。四组 Windows 输出均为 16,000 帧，Mac 为 15,994 帧；最佳分数偏移分别为 1.2、1.2、1.2、1.3 帧；相关系数分别为 0.999970、0.999951、0.999955、0.999864；RMS 差异为 0.251%、0.640%、0.354%、1.698%。常规本机 CoreChecks 179 项通过。Release x64 构建成功，0 警告、0 错误。完整日志保存在本目录。

## 来源、哈希与复现

- Mac 参考方法：`macOS/Audio/AudioCapture.swift`；抽取器 `windows/MacParityChecks/generate_audio_conversion_harness.py` SHA-256 `D96DB3AC3C841B87DDBEAA20D4BF57F65020707614D4B1C753130ED44F587D35`。
- 确定性输入生成器 `windows/MacParityChecks/generate_audio_conversion_inputs.py` SHA-256 `95F334CB9D7CD61EDA52D05DD7026B6084E7913856F8EDEEB15AFA01A9DCDCA5`；Swift harness 模板 SHA-256 `D226474F9FE3AA7419A792F0D106EC24608F2F66811317A876895D2478B9E174`。
- 抽取的 Mac 生产 harness `mac-production-conversion-harness.swift` SHA-256 `03D6FA55D22EDEC32136146B5800600BA426B73B22B7351F66AAF3B6545939F6`。
- Windows `AudioCapture.cs` SHA-256 `79F576CF207CD28C43531A8316256A91B9386E64255E4344CF8E1FE86AF6B5D2`；CoreChecks `Program.cs` SHA-256 `B18D4CB856EB455B15DF92B550ADAC1B5AD6E2ADA378AAF30C8AA5798857BF64`；workflow SHA-256 `437A39F0DA7DC77C9BEF18398295D030859B76A174B89A5BA26DE677AF5F6CC6`。
- 常规 CoreChecks 日志 SHA-256 `7A09CB826CA3CCD5A23805012E72A5135F7753026C5E8D45FEFF0B2A9926F0EF`；Mac artifact 对拍日志 SHA-256 `D1390F89010A149D2A8B8C83DCE51CAF6A06AACA08A1DC3F357436A3449ACFB7`；Release 构建日志 SHA-256 `37913F2385FA347676276B0757A73CE6942B26B7DFA96F1C22C140C3616EB260`。
- 当前提交 `e2a773569ca7b36885f0321e05fe6cca9dc9fbcd` 的 Windows CI run [`38012345830`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38012345830) 全部通过：Mac converter 编译及 artifact 生成、Windows CoreChecks/Release x64、Mac Archive 往返。该 run 的 Mac 原始 artifact 保存在 `ci-run-38012345830/mac-audio-conversion.json`，SHA-256 `1880F30A0F206E324A9C78338E281C847C420F3D2A9477E3644D3235582CEC8F`；用该 artifact 本机复跑 184 项全过，日志 SHA-256 `D0866FF31FCE49C9A923F9BE9FEC64A2B9F7DD73CC3FE480ED71C4E58FD94886`。旧的失败 artifact 仍保留为差异证据。

该验证只证明固定合成输入下的转换行为和输出接近程度；不代表真实设备时钟同步、WASAPI 欠载/热拔插、长时间双路稳定性或实际 Soniox 网络会话已验收。Windows 主机没有 Swift 编译器，Mac harness 由 GitHub macOS runner 编译。
