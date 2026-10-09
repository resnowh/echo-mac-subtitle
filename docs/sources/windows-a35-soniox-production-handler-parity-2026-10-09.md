# A35 Soniox 生产处理器运行时对拍

核验日期：2026-10-09

macOS 源码基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
Windows 起始提交：`466b25c2fffde785d5e8d6aa89721becdcad8c85`
Mac runtime 生成提交：`8ed340d946b575630ebed2689e038ba09140ad55`
Windows parity 修正提交：`fdb21da03ce662b39d23ab24ade389695b06262f`

## 缺口

A30 的五响应 Soniox fixture 由 Mac 源码静态推导，Windows CoreChecks 只运行 Windows `TokenAssembler`。它不能证明 Mac 生产 `SpeechViewModel.handleSonioxMessage` 对同一输入会产生相同字幕状态和定稿次数。

## 本轮方案

- Mac CI 在临时目录生成一个只读适配壳：从当前检出的 `macOS/ViewModels/SpeechViewModel.swift` 提取 `handleSonioxMessage`、entry 更新/定稿、时间和纠错方法原文；只移除访问级别并把定稿调用包一层计数器。
- 适配壳使用真实 `macOS/Models/TranscriptModels.swift` 中的 `SubtitleEntry`、`RecognitionConfig`、`TranscriptSegmentationPolicy` 与纠错逻辑；音频、网络、归档、UI 和用户偏好均为不执行的 stub。
- 将 `windows/Echo.CoreChecks/Fixtures/soniox-mac-parity-sequence.json` 的合成响应交给 Mac 处理器，Mac 真实输出成为 CI artifact。
- Windows CoreChecks 从该 artifact 读期望值，再对相同响应运行 `TokenAssembler`；逐响应比较原文、译文、speaker、language、时间戳和累计定稿次数。离线运行时仍可使用版本库中的历史静态 fixture。

新增 GitHub Actions job `generate-mac-soniox-runtime-fixture` 在 `macos-latest` 用 `xcrun swiftc` 编译上述 harness；`build-and-check` 等待该 job，将 artifact 交给同一组 Windows CoreChecks。

原始 macOS 文件不修改、不启动 Echo App，也不发起真实 WebSocket 或录音。生成的 fixture 只含本仓库虚构文本。

## 发现与验证

GitHub Actions run [`37952567838`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37952567838) 的 Mac job 成功编译并运行生产 handler，artifact 已保存到 [`mac-soniox-runtime.json`](windows-a35-soniox-runtime-fixture-2026-10-09/mac-soniox-runtime.json)：4025 bytes，SHA-256 `A432ED4A29FD6AEF8B0D5B89A3A92DE8C4F731BE41FA6DDB97BA1AF74971C1B8`。运行输入只有本仓库五条虚构 Soniox 响应，累计定稿计数为 `0, 0, 0, 1, 2`。

首轮 Windows CI 在第五响应发现真实差异：该响应从无活动行开始，包含两个说话人。Mac 在整个 token 循环结束后才创建/更新当前 entry，因此把两段 source 和两段 translation 合并成一条，保留最后的 `Speaker 2` / `ja`；Windows 原先在响应内拆成两条。Windows 已改为只对响应开始时已存在的活动行处理说话人切分，并按生产 Mac handler 的规则累计起点/终点。Mac 源码未修改。

用下载的 Mac runtime artifact 本机运行 Windows CoreChecks：93 项通过；该运行直接覆盖上述五条逐响应对拍。无环境变量时的仓库 fixture 同样通过，Release x64 构建为 0 警告、0 错误。

修正提交 `fdb21da` 的 GitHub Actions run [`37953208733`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37953208733) 四个 job 全部成功：Mac Archive fixture、Mac Soniox production fixture、Windows CoreChecks/Release x64、Mac Archive 回读。该提交的 PR 检查中 Mac CI、unsigned package preflight 和另一轮 Windows CI 也全部成功。Soniox 对拍基于五条固定合成响应；真实云、音频、GUI 和完整 Soniox 序列仍未覆盖。

该结果只证明固定合成输入下的生产处理器对拍，不代表真实云响应、音频、GUI 或所有 token 序列都已验收。
