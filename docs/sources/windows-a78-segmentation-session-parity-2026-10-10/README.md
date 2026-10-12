# A78 分段会话端到端 Mac/Windows 对拍

日期：2026-10-10

## 基线与范围

- Mac 行为基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 起始提交：`3a1b8b66630f204c37ac7e951b58a68e1f9362f4`。
- Mac 来源：`macOS/ViewModels/SpeechViewModel.swift` 的生产 `handleSonioxMessage`、`autoFinalizeIfNeeded`、`ensureCurrentEntry`、`updateCurrentEntry`、`finalizeCurrentEntry` 与 `elapsedSinceSessionStart`；模型/策略为 `macOS/Models/TranscriptModels.swift`。
- Windows 生产路径：`TokenAssembler` 与 `TranscriptSegmentationRuntime.TryFinalizeCurrent`；`MainPageViewModel.AutoFinalizeIfNeeded` 已改为调用此入口。

## 固定合成会话

`segmentation-session-parity.json` 使用固定起始时间和 9 个事件：六词原文、译文迟到、译文未到时静音不得定稿、4.5 秒静音定稿、80 词长段、空 Soniox 响应刷新安静计时、89.999 秒不定稿、90 秒定稿、带 `<end>` 的下一句立即定稿。每个事件都比较当前最终字幕集合、原文、译文、起止时间、现实时间、speaker/语言和定稿次数。

Mac 侧复用 production handler 和 auto-finalizer；测试适配器只注入确定性时钟，并将定稿调用记录到 harness。Windows 使用生产 token assembler 和当前 ViewModel 调用的相同 runtime helper。无音频、云端连接或用户数据。

## 本机验证与底稿

- CoreChecks：177 项通过；完整日志 `core-checks.log`，SHA-256 `833F1C905819424161B258E1A04869427DC7DB4BA1B0DB37E833BF6F83EA0E0A`。
- Release x64：0 警告、0 错误；完整日志 `release-build.log`，SHA-256 `D3DCA2C81F858E5E86F5EDFC71443C87F560DA66FDACCC8DA3DFD579367101F9`。
- 固定输入 SHA-256：`085FC06EA0A7B57EE97B6233DC89CD13F918A5A6ADE4AE429C69501CD72B7C67`。
- Mac 生产方法测试 harness `mac-production-session-harness.swift` SHA-256：`92C18E3B7B728B103CD96CADFD354D917F9D6629FF10D885A1E3F333F10A1E6E`。
- Mac `SpeechViewModel.swift` SHA-256：`1844D63F0025243F3891A5AAB732C099614BEAD18C35948ECEC0DED60C97E0F9`；抽取器 `generate_soniox_runtime_harness.py` SHA-256：`492926021FED3F306C9B2C940252499531C76835844A1C1EFBC5B2BB7B813CE6`。
- Windows runtime helper SHA-256：`0327AC0C57D508A3C496790B2456578E000479DDC7B5136714A375DE91A05B67`；ViewModel SHA-256：`E8ADF5609277DA4C98F824BFD2B75FDC0B56F5C6DFAD03DC15129F64471725CB`；CoreChecks SHA-256：`8872E574827864328CA42C34CE282DD07CB5CF6863789C5AD59FA6B8BDE4DC6C`。
- 当前 workflow SHA-256：`5CA3187E9AF217ADC85C0EE7453EDEABF2D3A046E5C3B978654AF87E169609A6`。
- Mac CI run `38010148281`（push，提交 `e2c1f9a`）生成并上传生产会话 artifact；Mac 抽取 harness 编译及 9 个事件均通过。下载的原始 JSON 保存在 `mac-production-output/mac-segmentation-session.json`，SHA-256：`E2C9A5DC6CD0729FB92D14631FB7C31C18CDFEBA93F6628C750A8311389E069E`；artifact 内记录 source commit `e2c1f9a8aae285fa6fcac119ca85a8d46a767de6` 和上述 Mac 源码哈希。
- 使用该原始 Mac artifact 在 Windows 本机重跑 CoreChecks：177 项通过；日志 `core-checks-mac-artifact.log` SHA-256：`B1FFEE246C099F12C6AC24BC110378D911DD18B44FF9B09480E6585A5B89A125`。其中 9 个逐事件快照全部匹配，比较最终行数、双语文本、起止时间、现实时间、speaker、language、定稿数及触发时刻。
- 首次 Windows Actions run `38010148281` 在同一环境读取 Mac JSON 时发现测试代码保留了已释放 `JsonDocument` 的元素；已改为克隆快照元素，防止文档释放后访问。修复后本机复测全绿；提交 `0bb6bd0` 的 Actions run `38010496387` 中 Mac 工件生成、Windows CoreChecks/Release x64 和 Mac Archive 往返均通过。
- 同一提交 PR 的 Windows 构建 run `38010152583` 和 macOS 无签名打包检查 run `38010152551` 均通过；这些检查不包含失败的 CoreChecks 步骤。

## 尚未覆盖

这是固定 token/时间事件，不是实时墙钟等待或真实 Soniox 流。没有验证 500ms timer 的调度抖动、真实音频、服务端 endpoint 节奏、硬件时钟漂移或 UI 表现。Mac 源码未修改，PR 最终差异必须继续保持不含 `macOS/` 文件。
