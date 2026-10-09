# Mac API Key Keychain 迁移基线

日期：2026-10-09

## 官方资料

| 页面 | 核验内容 |
|---|---|
| [Apple TN3137: On Mac keychain APIs and implementations](https://developer.apple.com/documentation/technotes/tn3137-on-mac-keychains) | macOS `SecItem` API 默认访问传统文件型登录钥匙串；设置 `kSecUseDataProtectionKeychain` 才改用 Data Protection Keychain。 |
| [Apple `kSecClassGenericPassword`](https://developer.apple.com/documentation/security/ksecclassgenericpassword) | 通用密码条目的匹配标识包含 service 与 account；macOS 上 `kSecAttrAccessible` 仅在启用 Data Protection Keychain 或同步时生效。 |

页面于 2026-10-09 查询。没有下载外部文件；本文保存用于实现的官方事实摘要和链接。

## 实现与兼容策略

- Soniox 与 DeepSeek Key 写入当前用户的 macOS 登录钥匙串，使用稳定 service `local.echo.subtitle.credentials` 和独立 account。
- 读取时先查钥匙串；若不存在旧值，则从旧 `UserDefaults` 项迁入。只有写入后再次读取确认一致，才删除旧项。
- 迁移失败时保留旧 `UserDefaults` 值并在设置页显示提示。新 Key 保存失败时不清除钥匙串中的旧值，设置面板保持打开，输入值仍留在字段中，用户可以重试。
- 清空 Key 时先成功删除钥匙串条目，再清理旧设置副本。API Key 不写入 Archive、导出、日志或 CI 工件。
- 测试使用随机 Keychain service、合成 Key 和隔离的 `UserDefaults` suite；退出时清理测试条目，不连接 Soniox/DeepSeek，不启动 Echo。

钥匙串实际授权提示、应用正式签名身份下的持久读取、旧安装升级迁移仍需在 Mac 上验收。自动化检查只验证当前用户上下文中的 Keychain API 与迁移逻辑。

## 验证结果

GitHub Actions [37914459700](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37914459700) 全部 3 个 job 成功。macOS 日志明确输出 `PASS: Keychain credential create/update/read/delete and verified legacy migration`；Mac Debug/Release 构建通过。Windows 核心检查与 Release x64 构建通过，macOS 生产 decoder 也成功读取 Windows 生成的 Archive。

Keychain 检查使用一次性 service 和合成数据，完成 Soniox 账户条目的创建、更新、读取、删除；完成 DeepSeek 旧设置迁移、迁移后的更新和清除，并检查已存在的钥匙串值优先于旧设置。不读取用户 Key、不连接服务、不启动 Echo。CI 结果证明当前 runner 用户会话中的钥匙串调用和迁移实现可用；正式签名身份下的首次授权提示、退出重启后读取、旧安装覆盖升级仍需在 Mac 实测。
