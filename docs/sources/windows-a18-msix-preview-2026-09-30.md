# Windows A18 MSIX 预览打包与签名检查底稿

日期：2026-09-30  
目标：为当前 Windows Release x64 代码生成可复核的 MSIX 预览产物；不安装或启动应用。

## 构建与打包

- OS：Windows 11 build 10.0.26200；WinApp CLI 0.6.1。
- Release x64 构建命令：`BuildAndRun.ps1 Echo.Windows.csproj -SkipRun -ExtraArgs '/p:Configuration=Release'`。
- 构建结果：成功，0 错误、10 条既有 NAudio 弃用警告；`-SkipRun` 生效。
- 打包命令：`winapp package .\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\AppX --output ..\artifacts\Echo-Windows-x64-20260930.msix --cert ..\artifacts\Echo-preview.pfx`。
- 产物：`windows/artifacts/Echo-Windows-x64-20260930.msix`，122,703,308 bytes，SHA-256：`DFC905972FC2F164EB59706B07C4D5F0DBCE444869FB83B4214C55FB21FBF5EE`。

## 签名与包清单

- 包内存在 `AppxSignature.p7x`；WinApp CLI 报告 package signed 和 package creation completed。
- MSIX 清单 Identity：`B7582E49-F75A-4EFA-950C-C6754B9E496E`；版本 `1.0.0.0`；架构 `x64`；Publisher `CN=AppPublisher`。
- 签名者 `CN=AppPublisher`，证书 thumbprint `ADDF31C7C19756CF27C37A6D72FBC5FAA3D95B69`，与清单 Publisher 匹配。
- Windows `Get-AuthenticodeSignature` 返回 `UnknownError`，说明证书链终止于不受信任的根。该结果不能作为当前机器或其他机器已信任此发布者的证据。

## A17 当前候选包

2026-09-30 为 A17 的 Archive/SRT 兼容改动重新构建并另存新包，未覆盖上面的旧候选：

- 路径：`windows/artifacts/Echo-Windows-x64-a17-20260930.msix`
- 大小：122,703,308 bytes
- SHA-256：`7C6313A7BE226365215D9E5A9B246515FB21F0CF4341B546E7E350209946FB35`
- Identity/版本/架构/Publisher：与上一包相同（`B7582E49-F75A-4EFA-950C-C6754B9E496E` / `1.0.0.0` / `x64` / `CN=AppPublisher`）。
- 签名者 thumbprint 仍为 `ADDF31C7C19756CF27C37A6D72FBC5FAA3D95B69`，包内有 `AppxSignature.p7x`；Windows 状态仍为 `UnknownError`，原因仍是不受信任根。

## A20 过期打包尝试

2026-09-30 曾从遗留的 `AppX` 子目录打包并签名 `Echo-Windows-x64-a20-20260930.msix`（122,702,982 bytes，SHA-256 `98EE9197668E2617BEBE0D9F2E08713D6B9338F49AE72643B94BDD9192C28BAD`）。复核发现这个子目录上次生成于 2026-09-29，其 `Echo.Windows.dll` 与当日最新 Release 输出哈希不同。**此包已判为过期，不可用于分发或安装**，文件只保留在忽略目录中作为审计记录。

尽管该文件有测试证书签名且 `Get-AuthenticodeSignature` 能读取签名者，它没有通过当前构建 DLL 对拍，所以签名不能证明代码新鲜度。

## A21 当前候选包

MSIX 版本从 `1.0.0.0` 递增到 `1.0.1.0`，并以 Release 输出为主、合并 AppX 资源的方式生成新包：

- 路径：`windows/artifacts/Echo-Windows-x64-a21-20260930.msix`
- 大小：108,210,625 bytes
- SHA-256：`5FA2DAB7D945C3D5D06CBB62D40D26BB45010A5BC4C797BA9E2C2C96EE195C51`
- 清单：Identity `B7582E49-F75A-4EFA-950C-C6754B9E496E`，版本 `1.0.1.0`，架构 `x64`，Publisher `CN=AppPublisher`。
- SignTool 成功签名；签名者 thumbprint `ADDF31C7C19756CF27C37A6D72FBC5FAA3D95B69` 与清单 Publisher 匹配。Windows `Get-AuthenticodeSignature` 为 `UnknownError`，因为证书链终止于不受信任根。
- 使用 MakeAppx 解包后，`Echo.Windows.exe` 与 `Echo.Windows.dll` 均与最新 Release 输出 SHA-256 一致。
- 打包时 Release 输出来自 `3e74f1b83dc3f67a670783fb16b7be0b7e9d881c` 对应代码；包版本 `1.0.1.0` 当时刚在工作区递增、尚未提交，随后会与打包脚本一起入库。此候选包已通过解包与二进制对拍，仍应优先用已提交版脚本再生成一份可追溯包。

## A22 候选包与打包脚本验证

使用 `windows/package-preview.ps1` 重做 A21；脚本从 Release 输出覆盖遗留 AppX 目录中的旧载荷，检查清单与签名者一致，签名后再次解包并核对程序文件：

- 路径：`windows/artifacts/Echo-Windows-x64-a22-20260930.msix`
- 大小：108,210,624 bytes
- SHA-256：`46D4014438F1989CC7E663FFFC8B3608BCB25F2EB2D5C7606CBA77C7836DF1D4`
- 清单：Identity `B7582E49-F75A-4EFA-950C-C6754B9E496E`，版本 `1.0.1.0`，架构 `x64`，Publisher `CN=AppPublisher`。
- 打包及签名命令均成功；脚本签名后解包核验，exe/dll 与 Release 输出 SHA-256 相同。签名者 thumbprint `ADDF31C7C19756CF27C37A6D72FBC5FAA3D95B69`。Windows 信任状态仍为 `UnknownError`（不受信任根）。
- 本包生成时应用源代码来自 `3e74f1b83dc3f67a670783fb16b7be0b7e9d881c`；包版本递增与打包脚本改动在当前提交中保存。它是测试候选，不是可信发布包。

### A22 后台安装/升级尝试

- 升级前检测到当前用户已有开发注册包 `1.0.0.0`，状态 `Ok`，安装目录位于仓库 Release/AppX 目录；应用本地数据目录存在。
- 在 `%LOCALAPPDATA%\EchoWindowsUpgradeBackups` 创建了升级前数据备份。仅核对相对路径及文件大小清单：19 个文件、233,115 bytes，备份清单匹配；没有打开文件内容。备份保留在本机，不进入仓库。
- 对 A22 执行 `Add-AppxPackage -Path ...` 失败，HRESULT `0x800B0109`：签名证书链终止于不受信任根。未改变证书信任存储；现有 `1.0.0.0` 包仍处于 `Ok` 状态。应用未启动。
- 下一步只有在当前 Windows 用户明确允许信任该预览证书后，才能继续后台安装/升级验证；这仍不能代替干净机器测试或生产签名。

## A23 最新候选包

在 A21 原子写入代码已完成 Windows Release 构建后，使用已提交的 `windows/package-preview.ps1` 生成独立包。A22 ACL 用例只改动 CoreChecks 和文档，不进入应用包载荷。

- 路径：`windows/artifacts/Echo-Windows-x64-a23-20260930.msix`
- 大小：108,211,047 bytes
- SHA-256：`FB4CA0D77A49065AD1475A9061082EDB91C4952EC995EB1F49BD6C1BA1FD6DFE`
- 清单：Identity `B7582E49-F75A-4EFA-950C-C6754B9E496E`，版本 `1.0.1.0`，架构 `x64`，Publisher `CN=AppPublisher`。
- SignTool 成功签名；thumbprint `ADDF31C7C19756CF27C37A6D72FBC5FAA3D95B69` 与 Publisher 匹配。签名状态仍为 `UnknownError`，因为测试证书链不受信任。
- 脚本解包核验 `Echo.Windows.exe` 和 `Echo.Windows.dll`，两者 SHA-256 均与 Release 构建输出匹配。
- 此候选没有安装、导入或信任证书，也没有启动 Echo。A23 安装/升级仍等待当前用户对测试证书信任的答复。

## 边界与未完成验收

- 使用的是已有本地测试证书，不是正式个人/组织代码签名证书；本轮没有安装/信任该证书，没有安装或启动 MSIX，也未修改系统证书信任存储。
- 没有可信时间戳；正式分发前必须用长期有效的发布身份重新签名并带可信时间戳。
- 未在干净机器安装、升级、启动、填写 API Key、读取旧档或卸载回退。A18 仍未通过。
- 旧版、A17/A20/A21/A22/A23 预览包与 PFX 均保留在 `windows/artifacts` 忽略目录，不提交到 Git；A20 已明确作废，A23 为最新候选，哈希及可复核元数据写入本底稿。

## 数据留存

本机保留自签名 MSIX 原始文件；仓库保留构建命令、结果、清单/签名元数据和 SHA-256。未包含 PFX 私钥、密码、API Key、录音或云端响应。
