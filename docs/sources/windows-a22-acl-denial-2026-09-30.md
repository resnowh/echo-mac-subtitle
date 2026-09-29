# A22 Windows ACL 拒绝写入检查

日期：2026-09-30
分支：`feature/windows-preview`
范围：在随机临时目录模拟当前用户没有权限创建存档临时文件。

## 验证环境

- Windows 11 build `10.0.26200`
- .NET SDK `10.0.401`
- `Echo.CoreChecks` Release

## 测试方法和结果

测试在临时目录先用正式 `AtomicWrite` 写入一份旧档案，然后只对该目录的 DACL 增加当前用户的 `CreateFiles` 拒绝规则。随后尝试写入新内容，并确认写入以 `UnauthorizedAccessException` 失败、旧档案文本不变且目录没有 `.tmp` 临时文件。测试退出前恢复原目录 ACL，并删除随机临时目录。

锁定依赖还原后运行 `dotnet run --project windows/Echo.CoreChecks -c Release --no-restore`，共 46 项检查通过。该用例不增加包依赖，也不修改系统目录或系统级 ACL。

## 限制

这验证的是创建临时文件时的 ACL 拒绝，不覆盖正式文件替换时的 ACL 拒绝、实际磁盘空间耗尽、存储设备故障或 Echo 应用本体被强制结束。没有写入用户档案、保存音频、调用云服务、改变证书信任或启动 Echo 窗口。
