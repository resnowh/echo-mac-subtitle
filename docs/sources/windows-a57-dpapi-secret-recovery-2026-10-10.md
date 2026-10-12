# A57 API Key 解密失败时保留 DPAPI 密文

日期：2026-10-10

## 对照基线与源码

- Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：`5572627ff5286b521095eda6cd41953a8530d9ff`。
- Mac `macOS/ViewModels/SpeechViewModel.swift`：SHA-256 `BD79AA7588FF43E4F3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。
- Mac `macOS/EchoMacApp.swift`：SHA-256 `CB2ED238D2EE30643BE2F4621E18599D7102ADBECFDF898D0EC52204E7802D87`。
- Windows `windows/Echo.Windows/Core/Preferences.cs`：SHA-256 `2D59B1F79469649D4CAE2057DA4BA607F2606068A896356EAB4BB2056D4876EC`。
- Windows `windows/Echo.Windows/MainPage.xaml.cs`：SHA-256 `307946969DE670955FBB635C207CC78B438C5657F0F49901903D564544088250`。
- Windows `windows/Echo.CoreChecks/Program.cs`：SHA-256 `F096F4FCC67F6278E13CB6B19881C68E81E444BF72775C5371CF698BB90D8F1F`。

## 发现与行为

Mac `SpeechViewModel.saveAPIKey()` 和 `saveSummarySettings()` 将非空、trim 后的值写入 `UserDefaults`，空值则删除对应键；当前实现并非 Keychain。Windows 保留当前用户 DPAPI，继续在设置文件中保存密文。

Windows 之前在主界面初始化时用同一个 `try` 解密 Soniox 与 DeepSeek Key；一个密文失败会跳过另一个，且设置保存会把空输入重新加密为空值，覆盖原密文。现在两项独立解密并提示。若某项无法解密且用户未输入替代值，保存其他设置时保留原密文；用户输入新 Key 后仍使用 DPAPI；可正常解密的 Key 清空后仍按原行为删除。

## 验证与限制

- 138 项 CoreChecks 通过，覆盖当前用户 DPAPI round-trip、不可解密密文保留、替换值重新加密、正常密钥清空和设置页面接线。
- Windows Release x64 构建成功，0 警告、0 错误；`git diff --check` 通过。
- 没有读取、复制或改写用户 Key，没有发起云请求、采集音频或修改 `macOS/`。
- 未运行设置 GUI，也未模拟另一 Windows 用户/损坏 DPAPI profile；这些情形仍需隔离 UI 验收。
