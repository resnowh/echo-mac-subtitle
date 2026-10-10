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
- Mac `SpeechViewModel.swift` SHA-256：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`；抽取器 `generate_soniox_runtime_harness.py` SHA-256：`492926021FED3F306C9B2C940252499531C76835844A1C1EFBC5B2BB7B813CE6`。
- Windows runtime helper SHA-256：`0327AC0C57D508A3C496790B2456578E000479DDC7B5136714A375DE91A05B67`；ViewModel SHA-256：`E8ADF5609277DA4C98F824BFD2B75FDC0B56F5C6DFAD03DC15129F64471725CB`；CoreChecks SHA-256：`8872E574827864328CA42C34CE282DD07CB5CF6863789C5AD59FA6B8BDE4DC6C`。
- 当前 workflow SHA-256：`5CA3187E9AF217ADC85C0EE7453EDEABF2D3A046E5C3B978654AF87E169609A6`。
- Mac 生产会话 artifact、Windows Actions 逐事件比较和本轮 PR 检查待提交后完成；成功后保存原始 artifact 并补录哈希。

## 尚未覆盖

这是固定 token/时间事件，不是实时墙钟等待或真实 Soniox 流。没有验证 500ms timer 的调度抖动、真实音频、服务端 endpoint 节奏、硬件时钟漂移或 UI 表现。Mac 源码未修改，PR 最终差异必须继续保持不含 `macOS/` 文件。
