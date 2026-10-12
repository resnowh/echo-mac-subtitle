# Windows A42 Soniox 畸形字段处理对齐

核验日期：2026-10-10。Mac 唯一基准为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`；Windows 起始提交为 `6f40d48a85d5029dfb0dd4e7ac16168e1b25194d`。本轮只读取 Mac handler，改动 Windows JSON 消费兼容层和合成 fixture；没有修改 macOS 源码、连接 Soniox 或保存音频。

## 数据底稿与来源清单

| 来源 | 版本 | SHA-256 | 用途 |
|---|---|---|---|
| Mac `macOS/ViewModels/SpeechViewModel.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | 生产 Soniox handler 的字段类型转换及回退行为 |
| Mac production runtime fixture `windows-a42-soniox-runtime-fixture-2026-10-10/mac-soniox-runtime.json` | Actions run `37964444866`，输入提交 `d5f018c671d21c865b0d41bc7b6452c2f057e693` | `B80AA9C474F703F0A65D6F5DFD9F328F43465590B7BEE9F7C3BC27A36A59BC7A` | Mac 生产 handler 处理 13 条响应的完整原始输出，14,824 bytes |
| Windows `windows/Echo.Windows/Core/Transcript.cs` | 本轮修改后 | `65E6C42B36C1659652CAD1DCFA2CADEC16D02A48ABDCD904EBFC7A8E18CD7B51` | 生产 TokenAssembler 的防御式字段读取 |
| Windows `windows/Echo.CoreChecks/Fixtures/soniox-mac-parity-sequence.json` | 本轮扩展后 | `9D0F4E94967C931C4513046EFA50AFEDF276BC9159913702E8D2D5AD3C94BE78` | 13 个固定响应及本机历史 Mac 期望状态 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 基线未改 | `DB94D7E50A9E1075DE235AB1A09664669897F0322D62D3F4CDCB204D47EDB7FB` | 每条响应比较字幕字段、时间戳和定稿次数；GitHub CI 会优先使用运行时 Mac artifact |

Mac 当前生产 handler 的字段读取关键行为：

```swift
guard let tokens = response["tokens"] as? [[String: Any]] else { return }
for token in tokens {
    guard let tokenText = token["text"] as? String, !tokenText.isEmpty else { continue }
    let translationStatus = token["translation_status"] as? String ?? "none"
    let isFinal = token["is_final"] as? Bool ?? false
    if let start = token["start_ms"] as? Double { ... }
    if let end = token["end_ms"] as? Double { ... }
}
```

## 差异与修正

Windows 原来直接对 `JsonElement` 调用 `GetString()`、`GetBoolean()` 和 `GetDouble()`。字段缺失、数值类型错误或 `text` 不是字符串时会抛出类型异常，而 Mac 对同类字段使用可失败转换：无效 text 被跳过，invalid bool/translation status 回退默认值，invalid speaker/language/timestamps 被忽略；整个 `tokens` 数组不符合字典数组形状时不会进入 token 遍历。

Windows `TokenAssembler` 现仅接受正确 JSON 类型：

- text 必须为字符串；非字符串 token 被跳过。
- is_final 只有 JSON boolean `true` 才为 final；其他类型按 false 处理。
- translation_status、speaker、language 只有字符串才读取；无效 translation status 视为普通 source token。
- start_ms/end_ms 只有 JSON 数字才修改时间轴；无效值不覆盖此前有效值。
- token 数组含非对象元素时按整体 token 解码失败处理，并保留 Mac 对既有 final 内容的刷新行为。

A42 将同一 fixture 从 7 条扩展到 13 条，覆盖非字符串 text、错误类型的 final/translation status、speaker/language、start/end 时间、混有非对象项的 token 数组，以及无效 token 后继续接收最终译文和 endpoint。所有输入均为仓库固定合成内容。

## 验证和限制

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：111 项通过，包含本地静态 fixture 13 条逐响应对拍。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：0 警告、0 错误。
- GitHub Actions run [`37964444866`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37964444866) 和重复触发的 [`37964454624`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37964454624) 均通过 Mac production fixture、Windows 111 项 CoreChecks/Release x64 和 Mac Archive 回读；PR build 与 unsigned package preflight 亦通过。Mac runtime 原件已保存在 [`windows-a42-soniox-runtime-fixture-2026-10-10/mac-soniox-runtime.json`](windows-a42-soniox-runtime-fixture-2026-10-10/mac-soniox-runtime.json)，14,824 bytes，SHA-256 `B80AA9C474F703F0A65D6F5DFD9F328F43465590B7BEE9F7C3BC27A36A59BC7A`。输出内记录输入提交 `d5f018c671d21c865b0d41bc7b6452c2f057e693`；CI 的逐响应比较均成功。
- 未调用云端、未录制/保存音频、未启动 Echo GUI；不能据此宣称真实 Soniox 只会发送这些畸形值或其所有异常恢复行为已验收。
- 更新 `docs/windows/mac-parity-matrix.md`、`functional-parity.md`、`testing.md`、`windows/README.md` 与 `docs/roadmap/数据清单.md`。没有修改 `macOS/`。
