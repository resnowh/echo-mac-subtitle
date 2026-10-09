# Echo macOS 正式分发与验收

> 2026-10-08：此文档描述**准备中的发布流水线**。截至本文编写时，未完成真实 Developer ID 签名、公证、干净设备安装，也未创建公众 GitHub Release。不要把 CI 的 unsigned Release build 当作最终安装包。

## 首发范围与原则

- 优先 macOS 15+，Apple Silicon 与 Intel（Universal：arm64 + x86_64）。先采用 **BYOK**：用户自行提供 Soniox Key，DeepSeek 可选；不自建用户订阅或录音后端。
- 先发布 **0.1.0 Beta / Release Candidate**，通过验收后再正式公开。版本必须有严格递增的 build number，Bundle ID 暂保持现有 `local.echo.subtitle`，改变 Bundle ID 前先完成存档与 Key 迁移评估。
- Windows 在独立分支，不属于本轮 Mac 发行范围。
- 永远不要在用户正在录音的 Mac 上运行会自动 `open Echo.app` 的 `build_mac.command`。发布构建与签名必须位于 GitHub Actions 的独立 macOS runner。

## 当前构建情况

- 常规 CI `.github/workflows/ci.yml`：合成 PCM 与本地 WebSocket 测试、Debug/Release 无签名编译。
- `.github/workflows/macos-distribution-preflight.yml`：PR / 手动触发，验证脚本与权限 plist、隔离构建 Universal Release、临时设置版本元数据、制作与校验 **不受信任的 DMG**。**不上传该 DMG**，避免误传播。
- `.github/workflows/macos-distribution-release.yml`：仅 main 的手动 workflow_dispatch，使用 `macos-distribution` 环境（推荐配置 reviewers 审批），验证后导入临时 Developer ID 证书、签名、notarytool 公证、staple 与校验，上传 **仅供验收的 14 天保留 Artifact**；**不会自动创建公共 Release**。
- `scripts/macos-package.sh`：只从独立构建的 app 生成 DMG，拒绝覆盖同名文件，不负责启动/安装应用。

## 发布前必须由仓库所有人确认

1. Apple Developer Program 是否为有效付费成员、团队对 Developer ID Application 证书和 App Store Connect Notary API 凭据的实际权限。Xcode 中已有 Development Team ID **不代表有发行证书**。
2. 在仓库 Settings → Environments 创建 `macos-distribution`，设置合适的 required reviewers；避免未经授权即可触发签名。
3. 在该 Environment 作用域配置以下 secrets（**不要把凭据发给 ChatGPT、写入 PR/Issue 或提交到 Git**）：
   - `APPLE_DEVELOPER_ID_P12_BASE64`：Developer ID Application 的 P12 内容，Base64（只保存在受保护的 Secrets）。
   - `APPLE_DEVELOPER_ID_P12_PASSWORD`：P12 解密口令。
   - `APPLE_DEVELOPER_ID_APPLICATION`：对应证书完整身份，例如 Developer ID Application: ...
   - `APPLE_NOTARY_API_PRIVATE_KEY`：Apple 公证 API 的 P8 私钥文本。
   - `APPLE_NOTARY_KEY_ID`：API Key ID。
   - `APPLE_NOTARY_ISSUER_ID`：API Issuer UUID。
4. 验证本机 `local.echo.subtitle` 与未来正式 Bundle ID、更新路径、权限的连续性。改变签名团队 / Bundle ID 可能令麦克风或系统音频权限重新提示，也可能改变 Keychain 访问策略。
5. 发布前单独完成 Keychain 安全存储和从 UserDefaults 的**无损迁移**；当前 Key 仍存放 UserDefaults，不应夸大为安全存储。
6. 核对 App 图标、隐私披露、支持邮箱/问题报告方式、更新说明、第三方服务区域可用性及服务条款、第三方依赖许可证。当前仓库尚无 LICENSE 文件，不应称为已授权开源。
7. 检查 Hardened Runtime 下 microphone 与 ScreenCaptureKit 的实际权限与首次启动行为；提供 `com.apple.security.device.audio-input` entitlement **不代表系统音频权限一定正确**。若公证拒绝或权限失败，查看 Apple 日志并采用经过验证的最小权限集，绝不通过关闭 Hardened Runtime 绕过。

## 手动发布候选步骤

1. 把经过 CI 的代码合入 main；创建发行候选前确认当前 head 与变更记录。
2. GitHub → Actions → **macOS Signed Distribution Candidate** → Run workflow，选择 main，输入如 `0.1.0` 和 `1`。有环境审批时由授权人通过。
3. 工作流生成的是 **仅限维护者下载** 的签名公证 DMG 与 SHA-256；没有公开 Release。确认实际日志里签名验证、notarytool、stapler、checksum 全成功。
4. 在**非正在录音**的独立干净 Mac 上验证：下载安装、首次打开、麦克风和屏幕/系统音频授权、Soniox BYOK、翻译、停止与保存、多次录音、DeepSeek 可选、中文/日文/英语混合、全量 SRT。记录系统版本、CPU、各项结果。
5. 用上一候选版本验证覆盖安装以及旧 Archive/偏好保留，不应删除真实存档；验证权限拒绝、无网络、失效/欠费 API、睡眠唤醒、外设更换以及卸载后的本地数据说明。
6. 记录 known issues、SHA256、提交 SHA、实际 Developer ID Team ID、公证 request ID 与发布日期；所有验收通过后再手动创建 GitHub **Draft Release**，最后由维护者人工批准 Publish。

## 安全和可维护性

- CI runner 的证书/keychain/P8 均为临时资源；仓库永不存私钥或明文服务 API Key；Release 打包不含本地真实 Archive 或用户设置。
- 初期不添加静默强制更新；可以先显示版本和下载页面，用户在录音结束后自行安装。
- 更新失败应保留上一份经验证的发布包与 release note；切勿通过自动覆盖用户当前运行的 Echo.app 方式更新。
- 有公共下载前不要自动生成 GitHub Release、创建正式 tag 或推广未经签名公证的候选 DMG。

## 尚待验证清单

| 项目 | 状态 |
|---|---|
| Debug / Release CI（未签名） | 已通过历史 CI |
| Universal DMG 预检 | 等本 PR 工作流执行 |
| Developer ID Application 签名 | 未执行（缺用户证书） |
| Apple notarization + staple | 未执行（缺公证凭据） |
| Hardened Runtime + 真机权限 | 未执行 |
| 干净机器安装/更新 | 未执行 |
| 长时录音 / 异常恢复 | 待专门验收 |
| 公共 GitHub Release | 未发布 |
