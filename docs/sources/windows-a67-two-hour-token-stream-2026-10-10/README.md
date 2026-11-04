# A67 两小时合成 Soniox 字幕流压力底稿（2026-10-10）

## 目标与来源

A16 已验证 7,200 条、每秒一条的两小时 Archive 快照/JSON/SRT 流水线；A67 补上此前未覆盖的实时 token 合并器路径。Mac 唯一行为基线仍为 `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`，处理器见 `macOS/ViewModels/SpeechViewModel.swift` 的 Soniox message handler。A67 不修改 Mac，也没有把 Mac 生产 handler 纳入这次两小时回放；固定小样本的 Mac/Windows 运行时对拍仍由 A35/A42/A43 覆盖。

## 合成方法

`Echo.CoreChecks` 生成 7,200 个一秒时间片。每个时间片先向 Windows 生产 `TokenAssembler` 输入 provisional 英文响应，再输入英文 final、中文 translation final 和 `<end>` endpoint，共 14,400 个 JSON 响应。时间戳连续从 0 秒推进到 7,200 秒；使用虚构英文/中文、speaker `1`、language `en`。随后执行生产 Archive snapshot、JSON 序列化/解析和 SRT 导出。

## 结果

- CoreChecks：155 项全部通过。
- 新增流处理 + JSON 往返 + SRT 单次耗时：178ms。
- 14,400 条响应最终得到 7,200 个字幕；每行恰有预期英中内容、连续起止时间、RecordedAt、Speaker 与 Language；每行仅创建和定稿一次。
- JSON：2,587,961 bytes；SRT：659,071 bytes；最后时间码达到 `02:00:00,000`。
- Release x64：成功，0 警告、0 错误。

## 边界与可复现材料

这会快速合成两小时字幕时间轴，不等待两小时墙钟时间，不捕获音频、不联网、不调用 Soniox/DeepSeek，也不覆盖 UI 更新频率、内存曲线、磁盘压力、休眠恢复或真实网络故障。计时为本机一次观测，不是性能 SLA。

- `corechecks.log`：全套 CoreChecks 输出及 A16/A67 耗时/大小。
- `release-build.log`：本机 Release x64 构建输出。
- `source-hashes.txt`：Mac 行为基线、Windows 生产/测试源码和更新文档 SHA-256。