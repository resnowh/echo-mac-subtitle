# A20 Windows 自动存档队列故障恢复

日期：2026-09-30  
分支：`feature/windows-preview`  
范围：`OrderedPersistenceQueue<T>`、存档 ViewModel 调用点、后台检查与文档。

## 验证环境

- Windows 11 build `10.0.26200`
- .NET SDK `10.0.401`
- x64 Release

## 故障注入结果

使用临时 `TaskCompletionSource` 阻塞第一份检查点快照，再排入第二份快照；第一份随后抛出模拟 `IOException`。检查确认：

1. 第二次写入在第一份结束前不会开始，执行顺序为 `[1, 2]`。
2. 第一份任务保留失败结果，便于现有 UI 失败提示路径报告错误。
3. 第一份失败不会阻塞后续完整快照，第二份成功进入持久化回调。
4. `FlushAsync` 等待队尾任务并在第二份成功后完成。

`dotnet restore windows/Echo.CoreChecks/Echo.CoreChecks.csproj --locked-mode` 后运行 `dotnet run --project windows/Echo.CoreChecks -c Release --no-restore`，共 44 项检查通过。Release + `win-x64` 锁定还原及 Release x64 build 通过，0 错误、5 条既有 NAudio 弃用警告。

提交后的 [GitHub Actions 运行 36611067984](https://github.com/resnowh/echo-mac-subtitle/actions/runs/36611067984) 通过：Windows 锁定还原、44 项核心检查与 Release x64 build 成功，Mac Debug/Release build 也成功。

本项验证的是队列调度与错误隔离，不模拟磁盘空间耗尽、ACL 拒绝、断电或强制结束进程。没有打开 Echo 窗口、写入用户档案、保存音频或调用云服务。
