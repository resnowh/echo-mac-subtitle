# A23 Windows ACL 拒绝原子替换检查

日期：2026-10-09
分支：`feature/windows-preview`
范围：验证正式存档文件的删除权限被拒绝时，原子替换能否保留旧档案。

## 验证环境

- Windows 11 build `10.0.26200`
- .NET SDK `10.0.401`
- `Echo.CoreChecks` Release，锁定依赖还原

## 测试过程与结果

1. 只在随机系统临时目录创建档案文件并先写入旧内容。
2. 使用 `icacls.exe` 对该单个测试文件增加当前用户的 Delete 拒绝 ACE；目录及其他文件权限不变。
3. 通过 `AtomicWrite` 的内部检查点确认新内容已写入临时文件并完成 `Flush(true)`，随后在目标文件原子替换阶段发生访问拒绝。
4. 移除测试文件上的显式拒绝 ACE，再核对旧档案内容未变、没有 `.bak`，并且原子写入的 finally 已清理临时文件。

锁定依赖还原后运行 `dotnet run --project windows/Echo.CoreChecks -c Release --no-restore`，47 项检查全部通过。该用例不依赖新增 NuGet 包，不访问 Echo 用户档案，也不改系统级 ACL。

提交 `c424d331e9131cefbe02547ff588ee26c271e3e5` 的 [GitHub Actions 运行 37899690548](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37899690548) 全部通过：Windows 锁定还原、47 项 CoreChecks、Release x64 构建，以及 Mac transport checks 与 Debug/Release 构建均成功。

## 边界

仅验证一个临时档案的 ACL 拒绝；没有填满磁盘、修改真实存档目录权限、模拟存储硬件故障或启动 Echo。真实磁盘空间耗尽和 Echo 本体强退仍待后续验收。
