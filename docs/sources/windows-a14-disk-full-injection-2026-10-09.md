# A14 存档部分写入时磁盘满故障注入

日期：2026-10-09
分支：`feature/windows-preview`
范围：验证原子存档写入在临时文件写入阶段遇到磁盘满错误时的恢复；仅操作随机临时目录，未启动 Echo。

## 方法与结果

- 为 `TranscriptFiles.AtomicWrite` 增加仅程序集内可见的写入委托重载；生产默认仍通过原子写入路径写入字节并 `Flush(true)`。
- CoreChecks 先写入一份完整旧存档，再让临时文件写入委托写入前 64 字节并抛出 `IOException`，HResult 设为 `0x80070070`（Windows `ERROR_DISK_FULL`）。
- 检查原存档仍是旧内容、没有生成 `.bak`、部分临时文件已清理，且错误文案为“磁盘空间不足，存档未保存。请释放磁盘空间后重试。”
- `MainPageViewModel` 的异步保存、等待保存和 Flush 失败状态统一使用此错误映射；普通 I/O 错误仍保留原错误详情。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：50 项通过。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release --no-restore /p:Platform=x64 /p:RuntimeIdentifier=win-x64`：成功，0 错误、10 条既有 NAudio 弃用警告。

## 未覆盖

本测试只注入真实 Windows 磁盘满错误码，不会实际耗尽卷空间；无法验证文件系统/驱动在实际空间耗尽时的全部行为。真实磁盘耗尽和恢复仍待后续实机验收。
