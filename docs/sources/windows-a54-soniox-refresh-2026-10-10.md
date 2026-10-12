# A54 Soniox 当前协议与语言模式复核

核验日期：2026-10-10

## 官方协议数据底稿

直接下载并保留 Soniox 官方页面 HTML，访问 URL 和 SHA-256 如下：

| 页面 | URL | 原始文件 SHA-256 |
|---|---|---|
| WebSocket API | https://soniox.com/docs/api-reference/stt/websocket-api | `9567D6D0CED3314B9B9BFB93BF34E72E0D8DDF92CF66C3D359A229E596098E9D` |
| Language hints | https://soniox.com/docs/stt/concepts/language-hints | `4E00EF538FE600DB82705EB552028A65B93541EFA82F520B809919F406C2FB50` |
| Language restrictions | https://soniox.com/docs/stt/concepts/language-restrictions | `72102319BBE9C758E5C4032F93D3C7913DE64F633617FBD28B16013E36A4D819` |
| WebSocket authentication | https://soniox.com/docs/guides/websocket-authentication | `6DA540E1426952CA359F14920E2442170E0525C8AA519DE55715A70D8284BC72` |

原始 HTML 位于本目录。文档确认实时模型 `stt-rt-v5` 仍有效；WebSocket 支持连接时 Bearer 鉴权，旧 start-request `api_key` 将于 2027-01-15 被拒绝；`language_hints` 仅偏向预期语种，`language_hints_strict` 才强烈限制结果；自动检测在没有提示时可直接工作。

## Mac 与 Windows 配置语义

源码哈希：

- Mac `macOS/Services/SonioxRequestBuilder.swift`：`48ACF849817D42CDDD89EF52BD0E865BFCF0B02BF325C8586D5A84120997B226`。
- Mac `macOS/Models/TranscriptModels.swift`：`CAEAB4EBA2797901B91C212F535B343FFE0AB77730E4A8BD73ABBF6464AC8E16`。
- Mac `macOS/ViewModels/SpeechViewModel.swift`：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。
- Windows `windows/Echo.Windows/Core/SonioxRequestBuilder.cs`：`3AC871F17F8E1D4A498CD36120FC4731BCD16ECFE3875104EDFF9A7148EA77E3`。
- Windows `windows/Echo.Windows/Core/Preferences.cs`：`3F8986E7671D6384B08D89176422E0D4563B5E4F9FB946C7A6B49CBF5123DC8B`。
- Windows `windows/Echo.CoreChecks/Program.cs`（本次更新后）：`EFEA30F17DBB3A8D683EDF84F08220477E464D3C2189D0D694CD5C964ACBF90D`。

对照发现 Windows 自动识别模式省略 `language_hints`，但这与 Mac 一致：Mac `RecognitionConfig.load` 在 automatic 下初始化空 hints，切换到 automatic 时也清空；请求 builder 同时移除 hints 和 strict 字段。指定模式向请求发送一个所选语言，strict 开关状态随请求发送。Windows 用空 `SourceLanguage` 表示自动模式，保持 `Strict` 偏好；构建请求时省略该模式下的两个字段，回到指定语言时再发送 hints 与 strict。此前把两者描述为差异的怀疑已由实际源码排除，没有改产品行为。

## 验证

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：128 项通过。新增用例覆盖 automatic 移除 hints/strict、指定语言发送单项 hint 与 strict、切回 automatic 后仍移除两字段。
- Windows Release x64 构建成功，0 警告、0 错误；`git diff --check` 通过。
- 没有调用 Soniox 云端、采集或保存音频；官方页面 HTML 原样保存，未保存任何密钥或用户转写。
- Mac 基线提交：`ae0359dc90da0ccb5e526a275da1747954a49a4f`。未修改 `macOS/`。
