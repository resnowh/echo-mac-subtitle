# Windows A40 音频波形与电平对齐

核验日期：2026-10-10。Mac 基准为 GitHub `origin/main` 的 `ae0359dc90da0ccb5e526a275da1747954a49a4f`。底稿保留 Mac 的 RMS、平滑和波形显示源码依据，以及 Windows 本轮实现哈希和自动验证范围；不含真实音频样本。

## 数据底稿与来源清单

| 来源 | 版本 | SHA-256 | 核验范围 |
|---|---|---|---|
| Mac `macOS/ViewModels/SpeechViewModel.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | RMS 放大系数、20Hz 更新、快攻慢放平滑、最多 48 个历史样本 |
| Mac `macOS/Views/TranscriptViews.swift` | 同上 | `805C242A93A45830F355AD14A91DF567E166BDF1F36D697FB2A3D9D2538140E4` | 3pt 圆角条、52pt 波形高、样本到高度映射、活动/静止透明度 |
| Windows `windows/Echo.Windows/Core/AudioLevelHistory.cs` | 本轮新增 | `364D4386EE490DA9EFCF7507F5FACF5689F68204D84E9BBD208FE8354158EE0C` | RMS、20Hz 节流、快攻慢放与固定容量历史 |
| Windows `windows/Echo.Windows/Services/AudioCapture.cs` | 本轮修改后 | `68E66FAC87ABB24B1B5586D9324DAFBA1442E4F3A0AFE498409F9A0B4D9BAFAC` | 对各路有效重采样样本分别测 RMS |
| Windows `windows/Echo.Windows/Services/SpeechSession.cs` | 本轮修改后 | `6B796123496F38ABB25511D62100EFD00F55BBAD0F5AFDE9214C57683715B31C` | 向 UI 提供足够频率的电平采样，计时节流由共享历史 helper 完成 |
| Windows `windows/Echo.Windows/ViewModels/MainPageViewModel.cs` | 本轮修改后 | `7D6BEF6A8CB3A6A853734872C066E9528434F81F8B6D54EE37415861C4A33211` | 会话开始/结束重置、20Hz 采样和波形样本通知 |
| Windows `windows/Echo.Windows/MainPage.xaml.cs` | 本轮修改后 | `0C7EE1FC5FA2EDBE1598530B2F1E87D3C2C4DE375BE1DF345F944FE859351F30` | 48 个实际样本控制条形高度与透明度，不再用正弦动画 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 本轮修改后 | `25DF10D5CE51303E2C44DA2C7311897B9F212F539DE3936174017B6E0C19429F` | 合成 RMS、节流、攻击/回落、最新 48 点顺序与重置断言 |

## Mac 行为摘录

Mac 将电平计算为 `sqrt(sum(sample * sample) / frameCount) * 7.5`，并限制在 0 到 1。`recordAudioLevel` 最快每 50ms 更新一次：输入增加时取 `previous * 0.25 + level * 0.75`，下降时取 `previous * 0.82 + level * 0.18`；平滑值加入历史，最多保留 48 点。SwiftUI 每点映射成 `max(2, min(46, 3 + sample * 44))` 的圆角条，活动状态使用 mint 85% 不透明度，静止状态使用 secondary 35% 不透明度。

## 差异与修正

Windows 原来用混合帧的最大绝对振幅作电平，并每 100ms 上报一次；界面根据一个正弦函数给 64 个条形赋予动画高度，因此即便静音时也会持续出现与输入无关的起伏。

Windows 现在对每个实际有效输入帧分别计算 Mac 同系 RMS 和 7.5 倍缩放，取当前来源的较大电平，以避免双路相加改变 meter 增益。共享 `AudioLevelHistory` 按 20Hz 节流并应用 Mac 攻击/回落系数，保留 48 个值。UI 的 48 根圆角条按每个样本绘制，开始和结束录音时把历史恢复为 48 个零值。

## 验证

- 固定合成样本验证 RMS 仅包含有效帧、20Hz 节流、攻击和回落系数、48 点上限以及会话重置。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：104 项通过；无云调用，也没有采集或保存真实音频。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：成功，0 警告、0 错误；未启动 Echo。
- 尚未验证麦克风电平和视觉动态的真实 GUI 表现。Mac 在混合模式下分别接收麦克风与系统音频回调，Windows 则每个 20ms 混合周期取各有效来源 RMS 的较大值；这一调度差异在静态逻辑上有界，但仍需实机听测/视觉确认。
