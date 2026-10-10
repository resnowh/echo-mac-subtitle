# A75 麦克风转换 PCM 健康监测

日期：2026-10-10

## Git 与源码基线

- Mac 只读基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
- Windows 分支：`feature/windows-mac-parity`
- Windows 起始提交：`b31c05a6764519d5f7f261a294e36554359e9239`
- 修改前已 fetch `main`、`feature/windows-preview` 和功能分支；PR #5 当时开放且现有检查全绿。
- Mac `SpeechViewModel.swift` SHA-256：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。本目录 `mac-microphone-conversion-reference.txt` 保存 2.5 秒 converter timeout、转换 PCM 成功标志和 timeout 处理摘录；摘录 SHA-256：`F6BE54C3A4572820CC5BE0BD6825F4C7BEF167E9257574D233B37CBCA3D7FCD0`。

## 行为实现

- 活跃麦克风首个 16 kHz 重采样输出帧完成时标记转换健康；首个原始回调仍由 A74 的 1.2 秒等待/最多两次重试处理。
- 原始回调存在但 2.5 秒内未出现转换输出时，`SpeechSession` 抛出明确的麦克风转换错误，录音启动中止。
- 录音中切换时，Windows 在提交候选源前用独立临时重采样器检查预缓冲。探测帧不会消费候选正式重采样器的状态；提交仍会清空候选预热音频，沿用现有单一 WebSocket。
- WASAPI `Silent` 标志代表有效静音数据包：它不会被误报为没有麦克风回调或转换超时。

## 自动验证

- `corechecks.log`：165 项通过，含合成 WebSocket 的转换超时错误路径与同一连接约束；SHA-256 `86E26A72BF6513D58B9E1DCD437CD65828776E3DE19D1CBC31C137B9745BFC1D`。
- `release-x64-build.log`：Release x64 成功，0 警告、0 错误；SHA-256 `0D079B5040CD910D4D2C2176E41E769635B23FB031658A5B4973BF8A83E34A26`。

## 修改源码 SHA-256

- `windows/Echo.Windows/Services/AudioCapture.cs`：`C602398503D0139587CCE09A3955E871235A4E652485A7D6907AC7BACC095DE6`
- `windows/Echo.Windows/Services/SpeechSession.cs`：`C855DD0761F52D2BE25C0360D956E9297E5E4BDA684A812B8D0D85FC3F1C933E`
- `windows/Echo.CoreChecks/Program.cs`：`9025743AEF1F9CD3D92141F53E024E09FC57B3124D5134FED374B63EF5C2B936`
- `windows/Echo.CoreChecks/Echo.CoreChecks.csproj`：`B207BA13C9A72BA142AC3F855F04F741FBB1B71181932AE6D4F7B3A6AD25FEEC`

## 验收边界

只运行合成采集器和本机 WebSocket；没有打开真实音频设备、调用 Soniox、保存音频或操作当前 Echo。CoreChecks 不模拟 NAudio 的实际回调线程、WASAPI 设备静音标志、硬件重采样异常或真实录音声学表现；这些仍需隔离硬件验收。Mac 源码未修改。
