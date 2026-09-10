# GitHub Releases 发布准备

这是后续发布的检查清单，不代表当前仓库已经具备签名、 notarization 或安装包。

## macOS 目标流程

```text
Xcode Release build
  -> Developer ID Application signing
  -> Hardened Runtime
  -> Apple notarization
  -> staple
  -> .dmg
  -> GitHub Release
```

状态：

- 当前已完成：macOS Xcode 工程、Debug/Release 构建配置和本地运行所需权限声明。
- 当前未完成：Developer ID 证书签名、Hardened Runtime 发布配置复核、notarization、staple、DMG 制作和 GitHub Actions release workflow。
- 后续计划：先在干净机器验证权限与首次启动，再配置签名/公证和可重复的 DMG 构建。

## 未来 Windows 目标流程

```text
Windows build
  -> installer
  -> code signing
  -> GitHub Release
```

Windows 仍是未来计划；当前仓库没有 Windows 构建、安装器或签名代码。

## 密钥与 CI 安全

- Soniox API Key、DeepSeek API Key、Apple 证书、notary 凭据和 GitHub token 不得写入源码、Archive、测试文件、构建产物或日志。
- GitHub Actions Secrets 后续只用于 CI 构建所需的签名证书、证书密码、Apple notarization 凭据和 GitHub Release token。
- Release 日志应避免打印请求头、环境变量和完整错误响应中的敏感字段。
- 当前应用仍把用户 API Key 保存在本机 UserDefaults；正式分发前应单独评估迁移到 Keychain，并在发布前完成安全审查。

## Artifact 与版本约定

建议的 macOS artifact 命名：

`Echo-macOS-v1.0.0-arm64.dmg`

如果提供 Intel 构建，使用 `x86_64`；如果合并为 Universal，使用 `universal`。Git tag 使用 semantic version，例如 `v1.0.0`，预发布版本使用 `v1.0.0-rc.1`。Tag、Xcode 项目版本和 GitHub Release 标题应保持一致。
