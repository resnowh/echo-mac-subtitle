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

## 边界与未完成验收

- 使用的是已有本地测试证书，不是正式个人/组织代码签名证书；本轮没有安装/信任该证书，没有安装或启动 MSIX，也未修改系统证书信任存储。
- 没有可信时间戳；正式分发前必须用长期有效的发布身份重新签名并带可信时间戳。
- 未在干净机器安装、升级、启动、填写 API Key、读取旧档或卸载回退。A18 仍未通过。
- 预览包与私钥均保留在 `windows/artifacts` 忽略目录，不提交到 Git；哈希及可复核元数据写入本底稿。

## 数据留存

本机保留自签名 MSIX 原始文件；仓库保留构建命令、结果、清单/签名元数据和 SHA-256。未包含 PFX 私钥、密码、API Key、录音或云端响应。
