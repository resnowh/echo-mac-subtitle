# Windows A09 旧版存档与跨日时间验证底稿

日期：2026-09-30  
范围：旧版 Echo JSON 缺字段、UTC 跨日时间以及 SRT 相对时间导出。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 使用 `Echo.CoreChecks --audio` 中的合成 JSON 和生产 `TranscriptFiles.Parse` / `TranscriptFiles.Srt`；未打开 Echo 窗口。
- 合成档案包含有效 ID、标题、创建/更新时间及字幕段，但旧版字幕没有 `speaker`、`language` 字段。

## 输入与结果

- 档案以 Apple reference date 秒数保存 UTC 绝对时间；录音段开始于 `2024-01-01T23:59:59Z`，字幕相对起点为 2 秒，对应 `2024-01-02T00:00:01Z`。
- 解析后 `StartedAt` 与 `Start` 保持原值，组合绝对时间仍为次日 `00:00:01Z`；未受当前 Windows 本地时区影响。
- 缺失的 `Speaker`、`Language` 解析为 null。输出 SRT 的单条 cue 为 `00:00:00,000 --> 00:00:01,000`，没有自动添加语言标签。
- `dotnet run --project windows/Echo.CoreChecks -c Release -- --audio`：49 项全部通过。没有云端调用、真实录音或音频文件落盘。

## 未覆盖项

- 本轮没有执行应用界面的文件导入，也没有测量导入前后源文件字节哈希；因此不能声称该哈希验收已通过。
- 未使用真实历史 Mac 存档样本；兼容结论只适用于当前合成的旧字段结构。

## 数据留存

只保留测试代码合成结构与本说明；不包含用户存档、设备 ID、API Key 或个人文字稿。
