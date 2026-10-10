# A77 Mac/Windows 分段策略生产对拍

日期：2026-10-10

## 固定基线

- Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 修改前：`feature/windows-mac-parity` `0655ac2144c59dc093a0c0ceefccc5d1311ca1c5`。
- 行为来源：Mac `TranscriptSegmentationPolicy`，路径 `macOS/Models/TranscriptModels.swift`；Windows 实际策略为 `windows/Echo.Windows/Core/TranscriptSegmentationPolicy.cs`。

## 本轮新增

- 固定输入 `segmentation-policy-parity.json` 覆盖 13 个边界：语义 endpoint 优先、空文本/译文等待、静音最少词数与时间阈值、译文迟到、静音和长段开关、长段词数/时长边界、两种兜底同时满足时的优先级、NBSP/全角空格分词。
- `windows/MacParityChecks/SegmentationPolicyChecks.swift` 与 macOS 生产 `TranscriptModels.swift` 同模块编译，逐条运行固定输入并产出 Mac trigger 结果及源码 commit/SHA 元数据。
- Windows `Echo.CoreChecks` 校验固定期望；CI 下载 Mac 生产输出后逐项比较 ID 和触发器，并核对 Mac artifact 来源元数据。

## 当前验证边界

- Windows 本机 CoreChecks：167 项通过，完整输出保存在 `core-checks.log`，SHA-256 `506E930A159FBDD84E6BE796C0134D224E46EC36BCB870A29FA20AEF212E3739`。
- Windows Release x64 构建成功，0 警告、0 错误；完整输出 `release-build.log`，SHA-256 `548FF3E5F7C66E91E7A6F3D56AF67E248A8020DBA15C614311B46F64E0D2CFBE`。
- GitHub CI 尚待提交后验证 Mac harness 成功编译运行，以及 Windows 下载的 13 项生产输出一致。成功后应把 Mac 原始 JSON artifact 保存在本目录并补记 SHA-256。
- 固定输入 SHA-256：`7EDAF11A97B33BE3E3B1639490A370F07C2DBA6A4F9459E675CA23942D883C69`；Mac harness SHA-256：`A51FD25427D79A276C8E606B2844BDF71CE62E93B4A58DFD7B7CB0BE0FB8D93E`。
- Mac 生产策略源码摘录 `mac-production-policy.swift.txt` SHA-256：`0BD5C479C6ABABD753B86EA64F73C65A5A0CAB33091378DB1D680C0057DD1515`；完整 Mac 源文件 SHA-256：`CAEAB4EBA2797901B91C212F535B343FFE0AB77730E4A8BD73ABBF6464AC8E16`。
- Windows CoreChecks 修改 SHA-256：`3AA549D9E79E601C26A63902C84F623330DCA82EA5D9C657CA809EB56057B56D`；workflow SHA-256：`5F32ACE794BAB191A6E982FAAB1D59C2812FEA77E8C30138C7D8D5CD9D60D432`。
- `git diff --check` 通过；本机未安装 `actionlint`，因此尚无本地 GitHub Actions 专用 lint 结果。
- 这是纯 `TranscriptSegmentationPolicy` 的生产函数对拍；不覆盖运行中 Timer 的调度抖动、会话时钟来源、实际 token handler 的最终字幕/时间戳全链路或真实 Soniox endpoint 频率。
- Mac app 源码没有修改；Windows 开发分支对 `macOS/` 的 diff 必须保持为空。
