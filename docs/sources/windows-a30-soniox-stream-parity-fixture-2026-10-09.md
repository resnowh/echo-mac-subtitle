# A30 Soniox 多响应字幕对齐 fixture

核验日期：2026-10-09
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 起始提交：`97dfc4f2a9c882eb3b934833a8a7960529d14a01`
输入底稿：`windows/Echo.CoreChecks/Fixtures/soniox-mac-parity-sequence.json`

## 来源与覆盖

期望字幕逐响应按 Mac `macOS/ViewModels/SpeechViewModel.swift` 中的 `handleSonioxMessage`、`ensureCurrentEntry`、`updateCurrentEntry` 和 `finalizeCurrentEntry` 静态推导。固定序列覆盖：

1. provisional 原文/译文快照；下一响应只更新原文并清空过期 provisional 译文，同时不改已初始化的临时语言/Speaker。
2. 原文 final 后先独立留在当前字幕；译文在后续响应到达，再以响应级 `<end>` 和 `<fin>` 完成字幕。
3. 一条响应含两个 final 原文 speaker turn、两条译文及 `<end>`；变化的 speaker 结束上一行，整个响应仍按 Mac 的当前顺序归并字幕。
4. 对每个响应检查完整行列表、原文、译文、Speaker、语言、起止秒数与 finalization 回调数。

此输入和期望输出均为人工编写的合成数据，不含用户录音或字幕。CoreChecks 实际执行 Windows `TokenAssembler`，并与根据 Mac 生产代码静态推导的期望状态逐条核对。**Windows 主机没有运行 Mac `SpeechViewModel.handleSonioxMessage`；这不是两端进程的 runtime differential test，不能据此标为端到端完全对拍。**后续仍需在合法的 Mac 构建环境为生产 handler 建立可调用测试 seam 或录制生产输出，再复用此 fixture 验证实际 Mac 输出。

## 验证记录

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release`：89 项通过，其中此序列 5 个响应状态逐条通过；无云端调用、无真实音频。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64`：0 警告、0 错误。
- Mac app 与 `macOS/`、`tests/` 文件未修改。
