# A68 两小时 Soniox Mac/Windows 生产 handler 对拍（2026-10-10）

## 目的

A67 已让 Windows 生产 `TokenAssembler` 跑完两小时合成时间轴，但没有让 Mac handler 跑同一序列。本轮把大序列模式加入现有 macOS CI harness，使用当前 `macOS/ViewModels/SpeechViewModel.swift` 提取 `handleSonioxMessage`，再交 Windows CI 对比 Mac 最终结果。

## 数据流

- 输入模式文件：`windows/Echo.CoreChecks/Fixtures/soniox-two-hour-stress-mode.json`，只含生成模式标识，避免提交数 MB 的重复 JSON 输入。
- Mac harness 每秒发送一个 provisional 英文响应，再发送英文 final、中文 translation final 与 `<end>`，共 14,400 条响应；最终 artifact 只包含 7,200 条字幕与累计定稿数，避免保存所有逐响应快照造成 O(n²) 膨胀。
- Windows 对生产 `TokenAssembler` 的 7,200 行逐条比较英文、中文、start/end、speaker、language 和总定稿数。Windows 本地缺少 Mac artifact 时仍运行 A67 自身测试；PR Actions 必须跑 Mac 生产对拍。

## 当前提交前验证

- Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`；Mac Swift 源码只读，未改动。
- Windows CoreChecks：155 项通过；包含 14,400 条响应 / 7,200 条双语字幕的本地压力、JSON 往返和 SRT 导出。该本机运行没有 Mac artifact，所以不算 A68 跨端对拍通过。
- Windows Release x64 构建：成功，0 警告、0 错误。
- `.github/workflows/windows-ci.yml` 通过 PyYAML 解析；本机未安装 `actionlint`，Swift harness 由 macOS GitHub Actions 编译执行。
- PR #5 新提交的 Windows/macOS Actions 是 A68 Mac/Windows 对拍的权威验收来源；报告状态以推送后的运行结果补充。

## 复现和边界

PR workflow 会提取未修改的 Mac production method、运行 stress mode、上传 `echo-mac-soniox-stress-fixture` artifact，再由 Windows job 消费并逐行比较。数据完全合成，不经过两小时墙钟时间，不采集音频、不联网、不触发 DeepSeek，也不代表真实云端或设备验收。

- `corechecks.log`：Windows 本机全量输出。
- `release-build.log`：Windows 本机 Release x64 构建输出。
- `source-hashes.txt`：Mac 基线与 harness、工作流、Windows 比较逻辑和文档的 SHA-256。