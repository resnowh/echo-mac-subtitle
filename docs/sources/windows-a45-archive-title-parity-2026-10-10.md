# Windows A45 新存档标题对齐

核验日期：2026-10-10。Mac 基准为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。仅检查 Mac 新建 Archive 标题生成方式；只修改 Windows 默认标题与 CoreChecks。没有打开存档文件或启动 Echo。

## 输入底稿与来源清单

| 来源 | 版本/状态 | SHA-256 | 用途 |
|---|---|---|---|
| Mac `macOS/ViewModels/SpeechViewModel.swift` | `origin/main` 基线 | `BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D` | 生产新建 Archive 的标题和日期格式 |
| Windows `windows/Echo.Windows/Core/Transcript.cs` | A45 前 | `65E6C42B36C1659652CAD1DCFA2CADEC16D02A48ABDCD904EBFC7A8E18CD7B51` | 原默认标题 `录音 MM-dd HH:mm` |
| Windows `windows/Echo.Windows/Core/Transcript.cs` | A45 修改后 | `DA29A58A39023D6CC8A004BB2262EB1F78C8AA5D5A68878577C3BB32EB5616E5` | 新标题格式化 helper 与默认值 |
| Windows `windows/Echo.CoreChecks/Program.cs` | A45 修改后 | `9C4B864C89DC24F681F1CCCEAF6D13B6D4802B2D23BE3327DE0324B7ACBB3EC6` | 固定本地时间标题契约检查 |

Mac 的 `prepareArchiveForRecording()` 新建存档时使用中文标题“课程”，并以 `zh_CN` locale 和 `MM-dd HH:mm` 显示本地日期时间。Windows 的 `NewArchive()` 使用 `new Archive()`，原默认值却是“录音”，因此新建按钮会产生不同的可见标题。

Windows 现通过 `Archive.NewTitle(DateTime localTime)` 生成相同的“课程 MM-dd HH:mm”，默认 Archive 用本机当地时间调用该格式化函数。修复没有改变 Archive JSON 字段或时间编码。

## 验证

- 固定输入 `2026-10-10 09:05`，期望标题为 `课程 10-10 09:05`。
- 本机 `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：120 项通过。
- Release x64 本机构建：0 警告、0 错误。
- GitHub Actions runs [`37969232352`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37969232352) 和 [`37969238022`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37969238022) 的 Mac fixture、Windows CoreChecks/Release 构建与 Mac Archive 回读均通过；Mac CI build `37969237990` 和 unsigned package preflight `37969238091` 通过。
- 在后续文档提交 HEAD `b4cdc61058095be1a149afa2c870882aced5cde4` 上重新运行：本机 CoreChecks 120 项和 Release x64 构建（0 警告、0 错误）通过；Actions runs [`37969777944`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37969777944)、[`37969788314`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37969788314) 的 Mac fixture、Windows 构建/检查、Mac Archive 回读通过；Mac CI build `37969788362` 与 unsigned package preflight `37969788440` 通过。
- Mac 源文件只读，未触碰 Echo 或真实存档。PR #5 检查通过，Windows 分支未合并。
