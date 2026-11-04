# A63 新内容总结限定为当前录音会话（2026-10-10）

## Mac 基线与差异

Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。Mac `SpeechViewModel` 在开始录音时保存 `sessionEntriesStartIndex = entries.count`；“总结新增内容”从该索引之后选择本次录音产生的条目，再按本次总结签名排除未变化的文字。当前段总结仍取本次录音段，整存档总结读取所有段。

Windows 原先把“总结新增内容”的候选范围设为整个选中 Archive，再排除存档签名已记录的条目。旧存档中的未总结历史文字会被错误地纳入当前会话的增量总结。

## Windows 修复

Windows 在 Archive 被选中及开始录音前记录 `newContentStartSegmentIndex`。Scope 0 只枚举该 Segment 索引之后的文字，再应用已有签名去重；录音重连产生的后续 Segment 仍包含在本次会话范围。Scope 1 继续只取最后一个 Segment，Scope 2 仍覆盖整个 Archive。开始一场新录音时重设边界，选择旧 Archive 时把当前末尾设为边界，避免把历史当作新内容。

## 验证与限制

- CoreChecks 使用一个未总结的历史 Segment 和一个新会话 Segment，验证 scope 0 只返回新会话文字；scope 2 仍返回两段。源码接线检查确认 ViewModel 传递起始索引。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：146 项通过；无云请求，无音频保存。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`：Release x64 成功，0 警告、0 错误。
- 原始输出保存在本目录 `test-results.txt`、`build-results.txt`。
- 没有调用 DeepSeek 或启动 UI；多 Segment 自动重连期间实际生成的总结输入尚未用运行时 UI 验收。

## SHA-256

| 文件 | SHA-256 |
|---|---|
| Mac `SpeechViewModel.swift` | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` |
| Windows `MainPageViewModel.cs` | `DA6DA66C243B293ED472AB72C0676FCC66F5F9AEEE8CB1CCBB0198A98A87B42A` |
| Windows `Core/Transcript.cs` | `9E6CAC82D8461589644D804A3B28D9D100E98B0EF1ED70884DE38CEC721A1594` |
| Windows `Echo.CoreChecks/Program.cs` | `89ED10022FE4A9A43581F214A91E4F11CD6B703905761018AC3B84112B4E79EE` |
| `test-results.txt` | `D80D3FBCA0E5062C3D2A55F249337BE7F4FE4CF5D672E031C3F58F3D20CB7F89` |
| `build-results.txt` | `AAC6D0AFA8D770123F5CC1B7443B0B65ED0BB2BA848C994FDEFC526D48265FBD` |
