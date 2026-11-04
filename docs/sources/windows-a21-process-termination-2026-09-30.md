# A21 Windows 存档强退恢复检查

日期：2026-09-30
分支：`feature/windows-preview`
范围：存档原子写入及死进程临时文件清理；只在 CoreChecks 独立子进程和临时目录验证。

## 验证环境

- Windows 11 build `10.0.26200`
- .NET SDK `10.0.401`
- x64 Release

## 强退注入

检查程序先写入旧存档，再启动同一个 CoreChecks 程序作为隐藏子进程。子进程将新内容写入带 PID 的唯一临时文件并调用 `Flush(true)`；测试回调写出就绪标记后，父进程立即强制结束子进程，确保终止点位于临时文件刷盘完成、正式文件原子替换之前。

验证确认：

1. 子进程到达刷盘后的强退边界。
2. 正式存档仍是旧的完整内容；强退只留下该子进程的临时文件。
3. 下一次正常保存识别该 PID 已退出，删除孤儿临时文件，再原子替换存档。
4. 新存档完整，`.bak` 保留原存档，目录中不再残留该测试写入的临时文件。

临时文件名现在含写入进程 PID。清理时仅匹配当前目标档案前缀；若 PID 仍运行或进程状态无法确认，则保留候选文件，不冒险删除。

## 结果与限制

`dotnet restore windows/Echo.CoreChecks/Echo.CoreChecks.csproj --locked-mode` 和 `dotnet run --project windows/Echo.CoreChecks -c Release --no-restore` 通过，共 45 项检查。Windows x64 Release 锁定还原与构建通过，0 错误；输出 10 条 NAudio `WasapiCapture`/`WasapiLoopbackCapture` 弃用警告。

这是对原子写入边界的进程终止故障注入，不代表实际磁盘满、ACL 拒绝、断电、文件系统损坏或 Echo 应用进程的手工强退已验收。测试数据仅位于系统临时目录并在用例结束后清理；没有写入用户档案、保存音频、访问云服务或启动 Echo 窗口。
