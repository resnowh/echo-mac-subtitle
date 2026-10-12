# Windows A10 总结范围选择验证底稿

日期：2026-09-30
范围：增量总结、当前录音段总结、全文总结的输入条目选择。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 使用 `Echo.CoreChecks --audio` 中的合成 Archive，调用生产 `TranscriptSummarySelection.Select`；未启动 Echo 窗口。
- 输入为两个录音段中的四条合成英文字幕，其中一条未变化、一条内容发生变化、一条未总结、一条位于末段且已总结。

## 观测结果

| 范围 | 期望输入 | 观测 |
|---|---|---|
| 增量 | 变化与新增条目 | 选择 `edited`、`new`；排除签名未变的 `unchanged` 与 `last segment` |
| 当前段 | Archive 最后一个 Segment | 只选择 `last segment`，不包含早期段落 |
| 全文 | 所有非空英文条目 | 选择全部 4 条 |

新增选择逻辑已由 `MainPageViewModel.SummarizeAsync` 使用。Unicode 长文分块原有测试仍验证每块字符上限及 300 个汉字均未丢失。全套 `Echo.CoreChecks --audio` 共 50 项通过；Release x64 构建通过，0 错误、10 条 NAudio 弃用警告。

## 未覆盖项

- 未调用 DeepSeek 或其他云端总结服务；响应质量、超时、限流及费用不在本轮结论内。
- 未模拟录音期间的真实并发总结请求，也未测超大 Archive 的端到端分块请求时长和内存占用。
- 未启动界面；本底稿只确认输入选择逻辑及 Release 编译。

## 数据留存

仅保留合成字幕、摘要签名、断言和本说明。无用户文字稿、API Key、云端请求或音频文件。
