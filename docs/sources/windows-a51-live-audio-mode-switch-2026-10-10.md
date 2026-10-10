# A51 录音中切换音源模式

日期：2026-10-10

## 基线

- Mac 产品基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 本次起始提交：`54707fcc035c7dbe01d8d6408dc3d1aae69e3d1b`。
- 旧迁移分支远端：`feature/windows-preview` `d500bbb21bc9bfa4d811c614576f38c0476efc50`。该分支包含 Mac 修改，只作为历史参考，没有合并或摘取。

## Mac 当前行为

Mac `AudioInputMode` 顺序是电脑音频、话筒、电脑音频和话筒；`SpeechViewModel` 在录音中允许变更模式，并将偏好保存到 `UserDefaults`。切换时，缺少的采集源先启动，启动成功后再移除不需要的源。麦克风或系统音频授权/启动失败时保留旧模式与正在进行的会话。Mac 未设置值时默认话筒。

Windows 原来把模式 ComboBox 绑定到 `CanEdit`，录音中禁用；只能在当前模式内切换物理设备。这与 Mac 实际行为不同。

## Windows 变更

Windows 音源菜单现在录音中仍可操作，顺序与 Mac 相同；新设置及旧设置文件中缺失该字段时均以话筒为默认，并将选择保存至本地设置。模式变更调用当前 `SpeechSession` 的切换事务：暂时暂停发送、排空有界音频缓冲尾部、重启所需 WASAPI 来源；Soniox WebSocket、token assembler 和当前字幕段不重建。若新来源启动失败则恢复旧模式和设备；若原来源也无法恢复，沿现有恢复策略保存字幕并停止录音。单独的“切换设备”对话框仍用于选取特定播放/录音设备。

## 数据与验证

Mac 源文件 SHA-256：

- `macOS/Models/TranscriptModels.swift`：`CAEAB4EBA2797901B91C212F535B343FFE0AB77730E4A8BD73ABBF6464AC8E16`
- `macOS/ViewModels/SpeechViewModel.swift`：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`

Windows 源文件 SHA-256：

- `Core/Preferences.cs`：`3F8986E7671D6384B08D89176422E0D4563B5E4F9FB946C7A6B49CBF5123DC8B`
- `Core/AudioInputModeSelection.cs`：`D7FD17641E64079F7B8886817A04E9DDA508E4C8FD75F13B5F07E5C5E33674D7`
- `Services/SpeechSession.cs`：`35B7CE60BF64D6F7EC5958D9874DF0F29ED105034E4E4005F82D40AC57EDCAC3`
- `ViewModels/MainPageViewModel.cs`：`B80A56A2C107E14EA802CD813224C9C1072629D90C76254E5F8988CE152FCEAA`
- `MainPage.xaml`：`A8CDFD6AD4EC6ACAB89E6434AA23B753923508D51993109DE222A77EEBCE7688`
- `MainPage.xaml.cs`：`35A40413F5083C7468DC4AE9F32169A20B0BCD46C5936142B54FA10EC72AB28F`
- `Echo.CoreChecks/Program.cs`：`412DEEFB9779959EB6F6518A35B7B31B4B543E504FC84BCAC8FAC21AACC74116`

本机 CoreChecks 共 126 项通过，新增覆盖：Mac 话筒缺省及旧设置兼容、偏好序列化、电脑/话筒/混合模式对应的采集路由、XAML 中模式菜单始终依据 `CanChangeAudioMode` 开放且接入切换事件。Windows Release x64 构建 0 警告、0 错误。PR #5 在 A50 提交 `54707fc` 上的 Windows build/check、Mac build、Archive round-trip 和 unsigned package checks 均通过；A51 代码的 PR CI 尚需推送后运行。

## 验收限制

没有启动正式 Echo、触碰 Mac 应用、打开麦克风/扬声器、创建录音或连接 Soniox。检查证明选择及代码事务路径契约，不证明真实设备切换时没有可听间隙，也未覆盖 Windows 麦克风授权拒绝、独占占用设备、混合模式时钟漂移、设备移除/默认设备变化和回滚硬件行为。这些仍需隔离 GUI 与真实音频设备验收。
