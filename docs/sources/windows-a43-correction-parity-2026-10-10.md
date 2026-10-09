# Windows A43 词汇纠正规则 Mac 生产对拍

核验日期：2026-10-10。Mac 唯一基准为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。本轮仅扩展 Windows 检查 fixture 与文档；Mac 源码只读。没有连接 Soniox、采集音频或启动 GUI。

## 输入底稿和来源清单

| 来源 | 版本/长度 | SHA-256 | 用途 |
|---|---:|---|---|
| 修改前 Soniox fixture：`input-before.json` | A42 13 响应，8,922 bytes | `9D0F4E94967C931C4513046EFA50AFEDF276BC9159913702E8D2D5AD3C94BE78` | 原始输入底稿，保留于本目录 |
| Mac `macOS/ViewModels/SpeechViewModel.swift` | `origin/main` 基线 | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | 生产 handler 与纠正规则来源 |
| Windows `windows/Echo.Windows/Core/Transcript.cs` | 本轮未修改 | `65E6C42B36C1659652CAD1DCFA2CADEC16D02A48ABDCD904EBFC7A8E18CD7B51` | Windows `TokenAssembler` 和纠正规则 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 本轮未修改 | `DB94D7E50A9E1075DE235AB1A09664669897F0322D62D3F4CDCB204D47EDB7FB` | 逐响应比较字幕字段、时间戳及定稿数 |
| 新 Soniox fixture：`windows/Echo.CoreChecks/Fixtures/soniox-mac-parity-sequence.json` | 18 响应，25,116 bytes | `A200086383B3FFD711F8AB20C06DEF9473E44108D18E0400C3C4A07C596D38AB` | Windows 静态期望和 macOS CI 输入 |

输入 fixture 由虚构的英文与中文字幕组成，不包含用户内容、录音、Key 或服务端响应。追加的五种场景是：

1. `micro economic theory and macroeconomic model`：标准化拼写并依微观语境纠正。
2. `macroeconomic growth with a microeconomic class`：宏观语境纠正。
3. `The first duty and duty function` 搭配 `税收关税`：英文导数和中文译文联动修正。
4. `The duty is owed by Han`：普通语境保留原文，确认不会过度纠正。
5. `on the other han`：只在指定短语语境修复截断词。

## 验证

- 本机 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：116 项通过；18 条固定响应逐项比较字幕、speaker、language、时间戳和定稿次数。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：0 警告、0 错误。
- A43 Mac production handler 对拍需要本轮提交后由 GitHub Actions macOS runner 执行。运行结果与其原始 JSON artifact 会在本文件和数据清单中补记。

本轮未编辑 `macOS/`，未执行云端请求或真实音频验收。合成 fixture 检查只证明所列确定性输入的处理结果一致。
