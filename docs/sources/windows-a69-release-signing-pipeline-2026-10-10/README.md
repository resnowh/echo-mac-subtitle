# A69 Windows 正式签名候选包流程

日期：2026-10-10
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`
实现分支：`feature/windows-mac-parity`

## 变更

- 新增 `.github/workflows/windows-release-candidate.yml`：手动触发且仅 `main` 执行，构建 Release x64 后上传 30 天签名 MSIX artifact，不发布 GitHub Release。
- 新增 `windows/package-release-candidate.ps1`：只从 Actions 环境变量读取 Base64 PFX 和密码；检查私钥、Code Signing EKU、有效期和 Subject/Publisher；把证书临时导入当前用户证书库，签名后移除本步骤导入的证书及临时 PFX。
- 扩展 `windows/package-preview.ps1`：可请求 RFC 3161 HTTPS 时间戳，并可要求 Authenticode 信任状态为 `Valid` 和 SignTool `/pa` 验签；原本测试包默认路径不变。
- package-preview 会继续比较清单身份及包内 `Echo.Windows.exe` / `Echo.Windows.dll` 与刚完成的 Release build 哈希。

## 官方依据与数据底稿

- Microsoft 要求证书 Subject 与 MSIX manifest 的 Publisher 匹配，并建议生产环境使用受信任来源证书：[Create a certificate for package signing](https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing)。
- MSIX 签名使用与 package block map 一致的 SHA-256；RFC 3161 时间戳通过 SignTool `/tr` 与 `/td SHA256` 在签名时请求：[Sign an app package using SignTool](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool)、[SignTool reference](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)、[Timestamping Authenticode signatures](https://learn.microsoft.com/en-us/windows/win32/seccrypto/time-stamping-authenticode-signatures)。
- GitHub Actions secrets 限制及配置范围：[GitHub Actions secrets reference](https://docs.github.com/en/actions/reference/security/secrets)。单个 secret 最大 48 KB。
- 外部资料摘要、实现输入源哈希、PowerShell AST、YAML 与 diff 检查日志保存在本目录。

## 验证和限制

- `powershell-parse.log`：两个打包脚本均可由 PowerShell AST 解析。
- `workflow-yaml.log`：PyYAML 解析成功并确认 `workflow_dispatch` 与签名 job 存在；当前机器未安装 actionlint。
- `git-diff-check.log`：`git diff --check` 通过。
- `gh-secrets.log`：`gh secret list` 返回空数组，没有读取任何 secret 值。
- 未配置正式 PFX/密码，所以没有进行正式签名、可信时间戳、信任链、MSIX 交付或安装升级测试。
- 当前 `Package.appxmanifest` 的 Publisher 是占位 `CN=AppPublisher`。正式证书必须与该字符串完全一致；拿到证书后，应先在提交中固定实际发布者身份，再触发主线候选包流程。
- 需要注册 Actions secrets：`WINDOWS_SIGNING_PFX_BASE64` 和 `WINDOWS_SIGNING_PFX_PASSWORD`。PFX 不得进入仓库、artifact 或测试日志。

## 文件

- `powershell-parse.log`：打包脚本语法验证。
- `workflow-yaml.log`：工作流语法与关键 trigger 检查。
- `git-diff-check.log`：空白错误检查。
- `gh-secrets.log`：当前 secret 名称清单结果（无条目）。
- `source-hashes.txt`：相关实现、工作流和文档的 SHA-256。
