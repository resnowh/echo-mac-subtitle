# A35 Soniox 生产处理器运行时对拍

核验日期：2026-10-09

macOS 源码基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 起始提交：`466b25c2fffde785d5e8d6aa89721becdcad8c85`

## 缺口

A30 的五响应 Soniox fixture 由 Mac 源码静态推导，Windows CoreChecks 只运行 Windows `TokenAssembler`。它不能证明 Mac 生产 `SpeechViewModel.handleSonioxMessage` 对同一输入会产生相同字幕状态和定稿次数。

## 本轮方案

- Mac CI 在临时目录生成一个只读适配壳：从当前检出的 `macOS/ViewModels/SpeechViewModel.swift` 提取 `handleSonioxMessage`、entry 更新/定稿、时间和纠错方法原文；只移除访问级别并把定稿调用包一层计数器。
- 适配壳使用真实 `macOS/Models/TranscriptModels.swift` 中的 `SubtitleEntry`、`RecognitionConfig`、`TranscriptSegmentationPolicy` 与纠错逻辑；音频、网络、归档、UI 和用户偏好均为不执行的 stub。
- 将 `windows/Echo.CoreChecks/Fixtures/soniox-mac-parity-sequence.json` 的合成响应交给 Mac 处理器，Mac 真实输出成为 CI artifact。
- Windows CoreChecks 从该 artifact 读期望值，再对相同响应运行 `TokenAssembler`；逐响应比较原文、译文、speaker、language、时间戳和累计定稿次数。离线运行时仍可使用版本库中的历史静态 fixture。

新增 GitHub Actions job `generate-mac-soniox-runtime-fixture` 在 `macos-latest` 用 `xcrun swiftc` 编译上述 harness；`build-and-check` 等待该 job，将 artifact 交给同一组 Windows CoreChecks。

原始 macOS 文件不修改、不启动 Echo App，也不发起真实 WebSocket 或录音。生成的 fixture 只含本仓库虚构文本。

## 验证与底稿

Mac runtime artifact 将由提交后 GitHub Actions 生成并上传；首轮通过后，需下载该 artifact，将机器可读输出及 SHA-256 保存在本目录，再补记生成 run、源码 SHA 与字段级差异。生成器已通过 Python 语法检查，workflow YAML 可解析；Mac Swift 编译及生产输出需要以 Actions 结果验证。

当前 Windows CoreChecks 仍只证明 Windows 行为符合旧静态 fixture。Mac runtime fixture 与 CI job 尚待首轮 Actions 验证。
