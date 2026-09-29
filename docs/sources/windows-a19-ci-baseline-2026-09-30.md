# A19 Windows CI 基线

日期：2026-09-30  
分支：`feature/windows-preview`  
变更范围：`.github/workflows/ci.yml`、Windows README、路线验收与数据清单。

## 本机验证环境

- Windows 11 build `10.0.26200`
- .NET SDK `10.0.401`
- 架构：x64
- 使用本地锁文件 `windows/Echo.CoreChecks/packages.lock.json` 和 `windows/Echo.Windows/packages.lock.json`

## 执行与结果

| 命令/阶段 | 结果 |
|---|---|
| `dotnet restore windows/Echo.CoreChecks/Echo.CoreChecks.csproj --locked-mode` | 通过 |
| `dotnet run --project windows/Echo.CoreChecks -c Release` | 43 项通过；无云服务调用、无音频保存；本次未打开真实音频设备 |
| `dotnet restore windows/Echo.Windows/Echo.Windows.csproj --locked-mode /p:Platform=x64 /p:RuntimeIdentifier=win-x64` | 通过；锁定还原阶段包含 Release 构建所需的 ReadyToRun runtime pack |
| `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release --no-restore /p:Platform=x64 /p:RuntimeIdentifier=win-x64` | 通过，0 错误、10 条 NAudio 旧采集接口弃用警告 |

## 自动门禁范围

GitHub Actions `Desktop CI` 对 `main`、`feature/windows-preview` 的 push 及 pull request 运行 macOS 和 Windows jobs。Windows job 安装 .NET 10，执行上述锁定还原、无设备核心检查和 Release x64 构建。它不配置签名凭据，不生成发布 MSIX，不安装或启动 Echo。

首次 GitHub Actions 运行暴露了一个环境差异：若 Release 的 ReadyToRun 构建使用 `win-x64`，锁定还原也必须带相同 RID；本机已有缓存时未能暴露缺失的 runtime pack。workflow 已按此修正，下一次远端运行用于确认修复。

本底稿记录本机执行证据；当前远端 A19 尚未通过。该流水线只证明代码可构建和核心规则可回归，不替代 A02～A18 中的硬件、UI、跨 Mac 文件实测、签名信任、干净机器安装与升级验收。测试期间没有读取、保存用户音频或 API Key。
