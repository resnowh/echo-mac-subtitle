# Echo Windows

原生 WinUI 3 / C# 实现，与 `macOS/` 并列维护。当前交付目标是 Windows 11 x64；Mac 实现未修改。Android/iOS 暂未实现。

## 当前开发基线

- Windows 分支：`feature/windows-mac-parity`；PR [#5](https://github.com/resnowh/echo-mac-subtitle/pull/5) 尚未合并；A65 当前本机验证记录在来源底稿中。分支起点和迁移历史见 PR 及对照矩阵。
- Mac 产品基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- 2026-10-10：A45 后本地 CoreChecks 120 项通过；A46/A47 设置页布局契约和 A48 窗口布局后为 123 项通过，Release x64 构建 0 警告、0 错误。A45 对齐 Mac Archive 默认标题，Mac/Windows/Archive 检查和 unsigned package preflight 通过，证据见 [A45](../docs/sources/windows-a45-archive-title-parity-2026-10-10.md)。A46 保留 Mac 520×560 DIP 设置面板目标并允许收缩；A47 修复长设置页滚动，见 [A47](../docs/sources/windows-a47-responsive-settings-scroll-2026-10-10.md)。A48 对齐主窗口 820×650 DIP 理想尺寸及 680×520 DIP 最小尺寸，移除空字幕时常驻的回底按钮；144 DPI 隔离 UIA 实测通过，见 [A48](../docs/sources/windows-a48-main-window-size-2026-10-10.md)。A44 对齐 Soniox 控制帧，见 [A44](../docs/sources/windows-a44-soniox-control-frames-2026-10-10.md)。截图并排、其他 DPI、无障碍及真实设备验收仍待进行。
- A50 将字幕层改为原生 Win32 layered HWND，并用 Win2D/Direct2D 透明像素通过 `UpdateLayeredWindow` 呈现；隔离 GUI 10 项通过，1920×225 缓冲区确认透明像素和预乘 Alpha。多屏/DPI、全屏及虚拟桌面仍待验收；记录见 [A50](../docs/sources/windows-a50-native-overlay-2026-10-10.md)。
- A76 复核浮层行数规则时发现旧 CoreChecks 只覆盖已弃用的 XAML 浮层。已删除旧实现，并将当前 DirectWrite 布局约束为最多两行、尾部省略；166 项 CoreChecks 和 Release x64 构建通过。A50 的桌面像素 smoke 来自先前渲染版本，当前文本布局仍需隔离 GUI 复验；见 [A76](../docs/sources/windows-a76-overlay-two-line-rendering-2026-10-10/README.md)。
- A77 用 Mac `TranscriptSegmentationPolicy` 生产输出与 Windows 规则进行 CI 对拍；Windows 本地 167 项 CoreChecks 通过。当前覆盖纯策略阈值，不代表生产会话时间戳或真实云端节奏已验，见 [A77](../docs/sources/windows-a77-segmentation-policy-parity-2026-10-10/README.md)。
- A78 把 Mac 生产 token handler 与自动分段定稿接入固定会话 fixture，并用 Windows 实际的 `TokenAssembler` 和 `TranscriptSegmentationRuntime` 对拍 9 个事件的字幕文本、边界/现实时间、speaker、language 和定稿次数；本机 177 项 CoreChecks、Release x64 和 Actions run `38010496387` 全部通过。见 [A78](../docs/sources/windows-a78-segmentation-session-parity-2026-10-10/README.md)。
- A79 对拍发现并修正 Windows 立体声转单声道行为：Mac 保留第一个声道，Windows 现在也保留第一个声道。使用 Mac 生产 artifact 的 184 项本机检查及 Release x64 构建通过；新 CI 运行待完成。此项不代替真实设备测试，见 [A79](../docs/sources/windows-a79-audio-conversion-parity-2026-10-10/README.md)。
- A51 对齐 Mac 音源模式顺序与默认话筒；录音中可在电脑音频、话筒和混合模式间切换，复用同一 Soniox 会话，失败时按原模式回滚。当前 130 项 CoreChecks 与 Release x64 构建通过；实际 WASAPI 设备和授权恢复尚未验收，见 [A51](../docs/sources/windows-a51-live-audio-mode-switch-2026-10-10.md)。A52/A53 为字幕纠正编辑器补上可折叠修订历史和受限滚动视口；A55 加入面板内校对术语并即时保存；动态 GUI 行为仍待隔离验收，见 [A53](../docs/sources/windows-a53-correction-editor-scroll-2026-10-10.md) 和 [A55](../docs/sources/windows-a55-inline-correction-term-2026-10-10.md)。A54 复核了自动/指定识别语言请求与 Soniox 当前协议，见 [A54](../docs/sources/windows-a54-soniox-refresh-2026-10-10.md)。
- A56 修正字幕跟随状态：区分用户滚动和字幕内容扩展，避免查看历史时被增量更新拉回末尾；134 项 CoreChecks、Release x64 构建通过。鼠标/触控板真实滚动和列表虚拟化仍待 GUI 验收，详见 [A56](../docs/sources/windows-a56-transcript-follow-2026-10-10.md)。
- A57 保持 Key 使用 DPAPI 加密；当当前用户无法解密现存密文时，保存其他设置不会清空它。替换值仍加密保存，正常 Key 可按空字段清除；138 项 CoreChecks 和 Release x64 构建通过，见 [A57](../docs/sources/windows-a57-dpapi-secret-recovery-2026-10-10.md)。
- A60 追加默认话筒变化的合成会话验收：模拟端点事件后采集器重启、活动端点更新，并在同一 WebSocket 上继续发送 PCM；连同 A59 失败回滚，本机 141 项 CoreChecks 和 Release x64（0 警告/错误）通过，Actions run `37991017788` 通过。测试没有打开音频设备；真实设备拔插与授权仍待验收，原始日志和源码基线见 [A60](../docs/sources/windows-a60-default-device-change-2026-10-10.md)。
- A61 在访问被拒绝时提示 Windows 麦克风隐私设置路径，覆盖开始录音、会话失败和音源切换；142 项 CoreChecks 与 Release x64 构建（0 警告/错误）通过。真实隐私开关行为未实测，见 [A61](../docs/sources/windows-a61-microphone-permission-2026-10-10.md)。
- A62 使 AI 校对响应在源/目标语言、翻译、严格限制、说话人或术语在请求期间变化时失效；144 项 CoreChecks 与 Release x64 构建（0 警告/错误）通过。未调用 DeepSeek，见 [A62](../docs/sources/windows-a62-stale-correction-suggestions-2026-10-10.md)。
- A63 将“总结新增内容”限定为本次录音开始后的 Segment，旧存档中未总结的历史字幕不会混入；当前段/全存档总结范围保持独立。146 项 CoreChecks 与 Release x64 构建（0 警告/错误）通过，见 [A63](../docs/sources/windows-a63-summary-session-scope-2026-10-10.md)。
- A64 将 AI 总结输入与 Mac 对齐：编号、本地真实时间、中英双语文字稿，以及增量/当前段/全存档提示词。150 项 CoreChecks 与 Release x64 构建（0 警告、0 错误）通过；未调用 DeepSeek，见 [A64](../docs/sources/windows-a64-summary-input-parity-2026-10-10/README.md)。
- A65 补齐 Mac 悬浮字幕菜单中的锁定/解锁操作，设置外观时保留当前调整状态，隐藏浮层时退出调整。152 项 CoreChecks 与 Release x64 构建（0 警告、0 错误）通过；本轮未做 GUI 实测，见 [A65](../docs/sources/windows-a65-overlay-lock-menu-2026-10-10/README.md)。
- A66 为原生浮层的无 owner/置顶生命周期与主窗关闭清理增加回归检查。154 项 CoreChecks 与 Release x64 构建（0 警告、0 错误）通过；未启动 Echo，主窗最小化后的真实桌面呈现仍待 GUI 验收，见 [A66](../docs/sources/windows-a66-overlay-lifecycle-2026-10-10/README.md)。
- A67 用生产 token assembler 快速回放 7,200 秒跨度的双语 provisional/final 字幕，共 14,400 条合成响应；验证字幕和时间戳、Archive JSON 往返、SRT 末尾时间。155 项 CoreChecks 与 Release x64 构建通过。该测试不等同于两小时真实运行或 Soniox 云端验收，见 [A67](../docs/sources/windows-a67-two-hour-token-stream-2026-10-10/README.md)。
- A68 将同一大序列加入 macOS CI，使用 Mac 生产 handler 生成 7,200 条最终状态，再由 Windows CI 逐条对拍生产 token assembler 的文本、时间和元数据；Mac artifact 与 Windows 检查结果见该提交的 GitHub Actions。尚待远端检查完成，且不代表真实云端或两小时墙钟测试。
- Soniox 实时模型为 `stt-rt-v5`。Windows 在 WebSocket 握手发送 Bearer API Key，配置 JSON 不重复包含密钥；Mac 当前代码仍把密钥放在起始配置中。按项目约束不改 Mac，Windows 保持官方推荐的握手鉴权。官方迁移时间与测试证据见 [A37 Soniox protocol baseline](../docs/sources/windows-a37-soniox-auth-protocol-2026-10-10.md)。
- 正式发行尚未就绪：真实浮层/多屏/音频验收、受信任发布者签名、干净机器安装升级和隐私政策仍待完成。Soniox 官方建议客户端使用临时 Key；当前项目没有签发临时 Key 的后端，Windows 采用个人自行填写的 Key 与本机 DPAPI 存储。Android/iOS 不在当前交付范围。

## 与 macOS 的当前差异

macOS 对照基线、逐项现状和优先级见 [Mac parity matrix](../docs/windows/mac-parity-matrix.md)。当前 Windows 已覆盖录音、Soniox 转写、双语字幕、存档、导出和 AI 辅助等主路径；音源模式可在录音中切换。悬浮字幕已使用原生透明层，仍需验收真实音频切换、多屏/DPI、全屏行为和 Mac 并排视觉差异。矩阵区分源码、自动检查和真实设备验收，不能将旧分支历史记录当成本分支验证。

UI 方案见 [ui-parity.md](../docs/windows/ui-parity.md)，功能计划见 [functional-parity.md](../docs/windows/functional-parity.md)，测试边界见 [testing.md](../docs/windows/testing.md)。

## 开始使用

1. 当前开发机可通过工作区根目录的 **启动 Echo Windows.lnk**，或开始菜单的 **Echo.Windows** 打开。不要移动或删除编译输出目录。
2. 点击右上角齿轮打开设置，在“识别”中选择“自动识别”或“优先语言”；优先语言模式可指定语言并选择是否严格限制。默认英语优先识别、中文翻译；主界面语言菜单也可快速切换到自动识别。填写自己的 Soniox API Key 后点击“完成”。
3. 设置面板关闭后，选择电脑音频、麦克风或双输入，点击“开始录音”。电脑音频来自所选播放设备；麦克风需要 Windows 隐私设置允许桌面应用访问。
4. 点击“停止录音”。下次可选择原存档继续录音。总结会保存在当前存档；使用“导出 SRT”或“导出存档”保存到指定位置。
5. 可选：填写 DeepSeek Key 后使用“AI 总结”。自动总结默认关闭；开启后会在正常停止录音时发送当前段文字稿。手动总结也会发送选定范围的文字稿。

## 已实现

- WASAPI 播放设备回环采集、麦克风采集、本地双路混合；转换为 16 kHz 单声道 PCM16。
- Soniox WebSocket 转写和单向翻译；临时文本替换、最终文本追加、端点分段、Speaker 编号。
- Soniox 请求带有与 Mac 一致的经济学领域上下文、识别术语与中英译词；自定义术语按行清理并按 Unicode 文本元素截断，避免拆开组合字符或 emoji。
- 本地多段存档、接续、JSON 导入/导出、整份存档 SRT、停止后自动单段 SRT。
- DeepSeek 总结新增内容、当前段、全存档；结果按 Mac 的 Markdown 标题、段落和项目符号规则显示，可展开/收起并复制原文。
- 设置中的停止后自动总结默认关闭；AI 服务说明会提示文字发送和费用影响。
- Soniox 端点最大延迟、灵敏度、延迟等级、本地静音兜底和超长段兜底参数按 Mac 默认值/范围保存；当前会话使用冻结参数，更新在下一次连接生效。
- Mac 默认深色、浅色/深色/跟随系统主题循环，窗口置顶、录音波形、重复启动保护。波形基于实际输入来源的 RMS 电平，使用 Mac 同款 20Hz 平滑和 48 点历史；静音时不会凭空起伏。真实设备表现仍需验收。
- API Key 用 Windows DPAPI 当前用户加密；不保存原始音频。
- 字幕逐条手动纠正、撤销及原始识别稿保留；可展开查看带时间的修订历史，编辑器支持滚动长内容并可直接添加校对术语。可按需请求 DeepSeek 校对/重新翻译建议，并编辑后再应用。设置可选择开启分句后的自动语境校对；默认关闭，只生成待人工确认的建议。
- 存档可在存档菜单中重命名；新名称立即写入现有存档文件。
- 刚完成的录音段可经确认拆到独立存档；原存档保留 `.bak` 备份。
- 存档可经二次确认移入本地 `Deleted/` 回收区；将 JSON 移回 `Archives/` 可恢复。

数据目录：`%LOCALAPPDATA%\EchoWindows`。安装方式可能使系统对路径重定向，以应用“打开数据目录”按钮打开的位置为准。

- `Archives/`：JSON 存档，包含字幕、该存档的 AI 总结及增量总结去重记录；每 3 秒检查点保存，替换时保留上一份 `.bak`。
- `Deleted/`：从应用中移除的存档及备份；将 `.json` 文件移回 `Archives/` 可恢复。
- `Exports/`：每次停止后的单段 SRT。
- 配置文件：语言、主题、加密后的 Key；服务模型固定遵循当前 Mac 版本，旧配置中的模型字段仅为兼容保留。

自动存档使用有序后台队列：较早的检查点失败不会阻塞后续完整快照；退出前的保存排空会等待最新快照结果，失败时界面提示导出备份。

存档继续采用 Mac 的 `english` / `chinese` 字段及 2001-01-01 日期基准。字段兼容已有本地测试，但尚未用用户真实 Mac 存档进行双向实机验证。

## 构建与测试

工具链：.NET SDK 10、WinApp CLI 0.6.1、WinUI 模板；开发运行需启用开发者模式。当前验证版本为 SDK 10.0.401；无 Visual Studio 也已构建成功。

```powershell
cd windows/Echo.Windows
./BuildAndRun.ps1 -Project Echo.Windows.csproj -SkipRun -ExtraArgs '/p:Configuration=Release'
winapp run Echo.Windows.csproj -c Release --no-build --detach --json

# 在仓库根目录执行；--audio 会短暂打开本机音频设备，仅发送到本地模拟服务，不保存音频
dotnet run --project windows/Echo.CoreChecks -c Release -- --audio
./windows/ui-smoke.ps1 -AppPid <运行进程号> -ResultsPath <结果JSON路径>
```

NuGet 版本记录在 `packages.lock.json`。GitHub Actions 在 Windows runner 上以 `dotnet restore --locked-mode` 还原依赖，运行不访问云端且不打开音频设备的核心检查，并构建 Release x64；该任务不签名、不安装、不启动应用。WASAPI 输入和回环现在使用 NAudio 3 的 `WasapiRecorder`；采集回调的临时 span 会立即复制进有界预缓冲。CI 与本机 Release 构建不验证具体声卡、loopback、设备拔插或睡眠恢复。

## 测试签名安装包

历史测试候选：A26 `Echo-Windows-x64-a26-20261009.msix`（版本 1.0.3.0，108,214,565 bytes）曾在本机生成；该文件当前不在工作区，且不是当前 PR 源码的构建产物。历史 SHA-256 和签名信息见 [A18 打包底稿](../docs/sources/windows-a18-msix-preview-2026-09-30.md)。它是自签名测试包，不是公众发行证书，也不是商店上架版本。

其他测试机器需要先审查并信任该测试发布者证书，再安装 MSIX；普通用户直接双击可能被不受信任证书拦截。历史记录显示 A26 证书链终止于未受信任根，且没有在另一台干净机器验证安装。不要把本机的 `.pfx` 私钥发给测试用户或提交到 Git。安装包和私钥均被 Git 忽略。

在当前 Release 构建输出、Windows SDK Build Tools 与测试签名证书已就绪后，可用打包脚本生成新包。输出路径必须唯一；脚本会校验清单、签名者和包内 exe/dll 与当前构建一致，且不会启动或安装应用：

```powershell
.\windows\package-preview.ps1 `
  -BuildOutputPath windows/Echo.Windows/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64 `
  -OutputPath windows/artifacts/Echo-Windows-x64-<build>.msix `
  -SignerThumbprint <test-certificate-thumbprint>
```

生产发布仍需确定发布者身份、正式代码签名或商店发行、安装升级测试、隐私政策及长时间稳定性验证。

## 正式签名候选包

仓库提供手动触发的 `.github/workflows/windows-release-candidate.yml`，只允许从 `main` 构建并上传 30 天有效的签名 MSIX artifact，不会创建公开 Release。触发前需将发布版本提交到 `Package.appxmanifest`，并在仓库 Actions secrets 设置 `WINDOWS_SIGNING_PFX_BASE64`（密码保护的 PFX 文件 Base64）与 `WINDOWS_SIGNING_PFX_PASSWORD`。PFX 对应证书必须含 Code Signing 用途，且 Subject 与清单 `Publisher` 完全一致；当前清单仍是占位值 `CN=AppPublisher`，需要用正式证书身份更新后才能出包。

工作流使用 SHA-256 和 RFC 3161 HTTPS 时间戳，要求 Windows runner 将签名链判为 `Valid`，再通过 SignTool 验签并比较签名包中的 exe/dll 与 Release 构建。签名密钥只在打包步骤注入，临时 PFX 和导入到当前用户证书库的证书在步骤结束时清理；工作流不会把证书加入信任根。GitHub 单个 Secret 上限为 48 KB；若证书包超限，应改用受支持的托管签名服务，而不要把证书放进仓库。

工作流与脚本尚未使用正式证书执行；当前 Actions secrets 未配置，公众信任链、可信时间戳、干净机器安装/升级仍未验收。证书 Subject 必须匹配 Publisher 的要求见 [Microsoft MSIX 证书说明](https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing)；时间戳和签名算法依据见 [Microsoft SignTool 文档](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool) 与 [MSIX 签名指南](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide)。GitHub 密钥配置规则见 [GitHub Actions secrets](https://docs.github.com/en/actions/reference/security/secrets)。

## 本次验证与边界

2026-09-30：A15 无障碍语义修正后的 Windows Release x64 输出生成签名候选 A24 `windows/artifacts/Echo-Windows-x64-a24-20260930.msix`（108,211,492 bytes）。打包脚本已在签名前后核对清单和 exe/dll 哈希；包 SHA-256、签名状态与安装限制见 `docs/sources/windows-a18-msix-preview-2026-09-30.md`。自签名链仍不受信任，未安装、导入证书或启动 Echo。

2026-10-09：为包含 NAudio `WasapiRecorder` 迁移的源码，将包版本递增至 1.0.3.0，并曾生成 A26 测试 MSIX（108,214,565 bytes）。WinUI analyzer 启用时 Release x64 构建成功，打包脚本完成签名后解包，exe/dll 哈希与当时 Release 输出相同。测试证书根不受信任；历史记录显示未安装、信任证书或启动 Echo。包哈希和二进制哈希见 `docs/sources/windows-a18-msix-preview-2026-09-30.md`。该旧包文件目前不在工作区，不能用作当前源码安装包。

2026-09-17：Release 构建成功；14 项本地核心/采集/模拟协议检查通过；9 项 UI 冒烟检查通过；另通过实际文件选择器完成示例 JSON 导入及 SRT 导出，核对三段原文、译文与时间戳。已实际打开窗口并检查主界面、设置界面截图，修复高 DPI 下字幕区域高度问题。按用户选定的 Mac 风格重做双栏字幕、底部控制、薄荷绿主题和居中设置面板；原界面已备份。窗口中的“示例”存档为人工测试文字，不是真实语音识别结果。

尚未验证：真实 Soniox/DeepSeek Key 的云端端到端结果、网络波动长测、其他 Windows 版本、ARM64、不同声卡、Mac 实机存档互通、正式安装升级与签名信任。

首版限制：

- 录音期间可切换当前模式使用的麦克风/播放设备，并保持 Soniox 会话与当前字幕段；跟随系统默认设备时会监听默认端点变化并重建采集，短暂静音期间保留同一转写连接。固定设备拔除或停用时会显示设备名称、停止并保存已收到的文字，不会静默切换。蓝牙模式切换及真实拔插仍待硬件验收。睡眠前会检查点保存并结束当前段，唤醒后等待 1 秒再尝试在原存档中新段恢复；若睡前未在录音、用户手动开始/停止或窗口关闭，均不会自动恢复。电源广播和状态机已实现，真实睡眠唤醒循环尚未实测。网络断开后最多重连两次（1 秒、3 秒间隔），401、配额和固定设备故障不重试；网络恢复失败时保存已收到的文字，需手动开始新段。
- 双设备按各自缓冲占用进行有界速率补偿（每路 ±0.5%），并非硬件时钟同步；60 分钟漂移目标尚未实机测量，长会议仍建议先用单输入验证。
- 导入单个 JSON 限 32 MB。
- 启动读取/解析、自动存档序列化与写盘、导出序列化均在后台；保存前仍会在 UI 线程复制一致性快照，超长存档的复制可能短暂占用 UI 线程，尚未压力测试。
- 界面当前以简体中文为主，尚未完成完整本地化。高对比度颜色映射、设置面板键盘焦点进出、标题层级与状态播报已补齐静态支持，但仍未用真实对比主题、键盘、Narrator/读屏工具实测；多 DPI 验收也待完成。

代码维护在隔离分支 `feature/windows-mac-parity`，PR #5 当前保持未合并；PR 差异不包含 `macOS/` 文件。提交和验证状态以 GitHub 当前 PR 为准。


2026-09-29：进一步参照 macOS/EchoMacApp.swift 和 TranscriptViews.swift，完成双栏字幕表头、逐行分隔、底部录音/存档/波形三行操作、顶部主题切换及设置覆盖面板。Release 构建与 9 项 UI 冒烟检查通过；截图保留在工作区 docs/sources/windows-mac-ui-*.png。


当前 A17 自签名预览包 SHA-256：`7C6313A7BE226365215D9E5A9B246515FB21F0CF4341B546E7E350209946FB35`。Windows 当前将证书链判为未信任根；本轮没有安装或启动该包，跨机器安装仍待验证。旧候选包哈希保留在 `docs/sources/windows-a18-msix-preview-2026-09-30.md`。

2026-09-29 同步并补齐：当前分支已合入 `origin/main` 至 `c7b9e37`，包含 Mac 端新增的字幕响应优化、可撤销手动编辑、可选 AI 校对建议及语义边界拆分。Windows 已增加逐条字幕编辑、撤销、查看原始识别稿、手动/可选自动 AI 校对、重新翻译及校对术语设置。自动校对默认关闭；开启后在分句或说话人切换边界排队生成建议，仍需人工应用。人工修改按字段锁定，后续识别不会覆盖锁定字段；字幕发生变化时旧建议会失效。最终识别 token 的说话人变化会拆成独立字幕行。校对数据使用 Mac 兼容字段；总结保存在当前存档的可选 `summary` 字段，增量总结签名也随存档保存，长文字稿按不超过 18,000 字符的块顺序总结。Release x64 后台构建通过，0 错误；核心检查覆盖说话人分行、单次分句回调、校对锁定、撤销、存档总结往返与 Unicode 分块。未启动应用窗口。仍待 Mac 实机双向存档验证与跨机器安装验证。

2026-09-29：自动存档写盘、导出序列化和启动时存档加载/解析改为后台处理；保存采用有序快照队列。录音时可切换当前采集模式使用的输入/播放设备，保持同一个转写连接和字幕段；切换失败时尝试恢复旧设备，恢复失败则保存已有文字并停止。Release x64 构建通过；后台本地采集/模拟服务检查共 29 项通过，覆盖三种音频模式热重建、无效设备回滚和 WebSocket 会话保持。未启动 Echo 窗口、未调用云服务、未保存音频。仍待不同声卡/长时间会议实测，断网恢复仍需手动重新开始录音。

2026-09-29：补上 A13 的运行中断网恢复：对临时 WebSocket/超时故障最多重试两次，1 秒和 3 秒间隔；服务拒绝、401/配额和采集设备故障不重试。断线前后的文字保留在同一存档的独立录音段；重连期间停止按钮可取消，断线段和每次失败后的内容先落盘。Release x64 构建通过，核心检查 25 项、含本机音频与模拟 WebSocket 检查 33 项通过。未启动 Echo 窗口、未调用云服务、未保存音频。睡眠恢复、真实断网切换及长时间网络波动仍待验证。

2026-09-29：为双输入采集加入缓冲反馈漂移补偿，各路每 20 ms 最多增减 2 个 16 kHz 样本，并对小数调整累计以减小舍入漂移。核心检查验证快慢方向、±0.5% 限制及长序列帧数；另以 ±500 ppm 模拟独立时钟运行 60 分钟，队列均保持在 1 秒安全范围内。含本机采集/模拟 WebSocket 检查共 35 项通过。Release x64 构建通过，0 错误、10 条 NAudio 弃用警告；未启动 Echo 窗口、未保存音频。双声卡同步信号的 60 分钟实机测量仍待完成。

2026-09-29：存档原子写入改用同目录唯一临时文件，写完执行磁盘刷新再替换，并在失败时清理临时文件；替换保留上一份完整存档为 `.bak`。核心检查覆盖创建、替换、备份回读和失败清理；核心 28 项及含本机采集/模拟 WebSocket 检查 36 项通过。Release x64 构建通过，0 错误、10 条 NAudio 弃用警告；未启动 Echo 窗口、未保存音频。真实磁盘写满、断电和强退恢复仍待专门实测。

2026-09-30：补上 Windows 音频端点状态监听。系统默认多媒体端点变化时在同一 Soniox 会话中刷新采集，切换前发送缓冲尾帧；用户固定选择的设备被移除或停用时显示设备名称并停止，保存已收到的字幕。核心 29 项、包含本机音频与模拟 WebSocket 的检查 37 项通过；端点策略覆盖默认跟随、固定设备及 Communications 角色隔离。Release x64 构建通过，0 错误、10 条 NAudio 弃用警告；未启动 Echo 窗口、未调用云服务、未保存音频。真实拔插、默认路由变化和蓝牙模式变化仍待硬件验收。

2026-09-30：接入 Windows `WM_POWERBROADCAST` 睡眠/唤醒通知和恢复状态机。睡眠时先保存字幕检查点并结束当前段；仅睡前正在录音的会话会在唤醒等待设备稳定后于原存档新建段恢复。用户开始/停止、未完成的连接启动及关闭窗口都会取消自动恢复意图。核心检查 30 项通过，包含本机音频和模拟 WebSocket 的检查 38 项通过；Release x64 构建通过，0 错误、10 条 NAudio 弃用警告。未启动 Echo 窗口、未调用云服务、未保存音频。状态机测试不替代真实睡眠唤醒循环，A11 的 10 次循环和睡后手动停止场景仍待设备验收。

2026-09-30：改善 A15 静态无障碍支持：高对比度资源改为系统对比画刷映射；设置面板打开时从字幕界面移出焦点并将焦点送入设置，关闭后恢复焦点；为字幕语言标题标注层级、状态加入 Polite 实时区域，并从 UIA 隐藏纯装饰波形。修正“跟随系统”主题：仅在用户固定选择浅色/深色时覆盖页面主题，默认模式清除局部主题值以继承 Windows 外观。Release x64 构建通过，0 错误、10 条 NAudio 弃用警告；XAML 解析及主题资源/焦点/实时区域断言通过。未启动 Echo 窗口。高对比主题、键盘与 Narrator 仍需实机验收，多 DPI 也尚未覆盖。

2026-09-30：扩展 A14 原子存档失败检查：模拟 `.bak` 路径被目录占用导致最终替换失败，确认旧存档内容保持不变且唯一临时文件清理；原目录目标冲突用例也继续通过。核心检查 30 项通过，Release x64 后台构建通过，0 错误、10 条 NAudio 弃用警告。未启动 Echo 窗口；真实磁盘写满、权限拒绝、断电及强退恢复仍待实机验收。

2026-09-30：扩展 A02 后台本机采集验收：在当前 Windows 11 机器上检查播放、麦克风、双输入三种模式，并在单个麦克风识别会话中交替切换两个活动输入端点 20 次。临时字幕在切换前送达，最终字幕保持为单条；无效输入设备仍能回滚恢复。`Echo.CoreChecks --audio` 共 39 项通过，仅连接本机模拟 WebSocket，未保存音频、未调用云端、未启动 Echo 窗口。环境与结果底稿见 `docs/sources/windows-a02-audio-switch-2026-09-30.md`。

2026-09-30：扩展 A03 可重复采样率检查，生产采集和测试共同使用 `AudioCapture.ToMono16k`。即时合成 44.1 kHz 双声道、48 kHz 单声道及 150 ms 延迟的 48 kHz 双声道输入；输出各 16,000 样本，延迟实测 2,402 样本，相对 2,400 样本目标误差 0.125 ms。Release x64 构建通过，`Echo.CoreChecks --audio` 共 41 项通过；未保存合成 PCM、未启动应用。真实双声卡时钟/相位同步仍待实测。底稿见 `docs/sources/windows-a03-resampling-2026-09-30.md`。

2026-09-30：补齐 A05 建连前采集与有界缓冲。每个输入在 WebSocket 握手期间先采集，最多缓存 2.5 秒；连接后先追发送缓存帧，再恢复实时节奏。缓存超限会报明确错误并停止，避免静默缺口。后台本机测试在 1 秒握手延迟下缓存 0.98 秒，连接后 200 ms 降至约 0.11～0.12 秒；3.2 秒延迟时在缓冲上限处明确失败。A06 完成后的 `Echo.CoreChecks --audio` 共 46 项通过，Release x64 构建通过。未保存音频、未启动 Echo 窗口；其他硬件及运行中网络退化仍待验收。底稿见 `docs/sources/windows-a05-prebuffer-2026-09-30.md`。

2026-09-30：补齐 A06 识别语言元数据在字幕行和 Archive 中的保留。字幕行订阅语言属性变更，并在有检测值时显示语言代码；A17 对齐后 SRT 采用 Mac 的说话人独占行格式，检测语言仍保存在 Archive 但不写入 SRT。合成最终 token 覆盖 Speaker 变化与 en/ja 绑定；Archive JSON 往返保留 Speaker/语言。底稿见 `docs/sources/windows-a06-speaker-language-2026-09-30.md`。

2026-09-30：补齐 A07 停止收尾超时提示。会话正常停止等待服务端最终识别结果；若 8 秒内未收到结果，返回明确“最后识别结果超时，最后结果可能不完整”异常，界面继续保存已收到文字并提示不完整。后台本机模拟 WebSocket 分别验证正常最终响应和收到结束标记后故意不回最终结果的超时路径；全套 `Echo.CoreChecks --audio` 共 47 项通过，Release x64 构建通过、0 错误。未启动 Echo 窗口、未调用云服务、未保存音频。真实网络延迟分布待后续观测。底稿见 `docs/sources/windows-a07-stop-final-2026-09-30.md`。

2026-09-30：扩展 A08 跨段 SRT 时间轴回归：合成 Archive 故意按倒序放置两个录音段，并让第二个时间段覆盖第一条字幕的结束时间。SRT 仍按段开始时间排序，并将重叠 cue 起点推进到前一 cue 的结束处；断言完整校验 `00:00:00–00:00:05`、`00:00:05–00:00:08`。最新 `Echo.CoreChecks --audio` 全套 48 项通过；本项仅使用合成字幕文本，未写真实音频或启动应用。大存档导出性能和实际播放器兼容仍待验证。底稿见 `docs/sources/windows-a08-archive-export-2026-09-30.md`。

2026-09-30：补齐 A09 旧版字段与跨日时间回归。构造缺少 speaker/language 的旧 JSON，确认字段保持 null、不推断语种；段落从 2024-01-01 23:59:59 UTC 开始、字幕起点为 2 秒，解析后仍对应 2024-01-02 00:00:01 UTC，SRT 则以该档最早字幕为 0 秒正确导出。全套 `Echo.CoreChecks --audio` 共 49 项通过；仅使用合成 JSON、未写真实录音或启动应用。通过静态检查确认导入以 JSON 文本解析并写入应用自己的档案路径，但本轮没有比较源文件导入前后的字节哈希。底稿见 `docs/sources/windows-a09-legacy-dates-2026-09-30.md`。

2026-09-30：补齐 A10 三种总结范围的可重复选择测试，并将纯选择逻辑用于生产总结流程。增量模式排除签名未变的旧条目、包含被修改和新加入的文字；当前段仅取末段；全文范围包含两段所有条目。既有 Unicode 长文分块检查仍通过。Release x64 构建通过，0 错误、10 条 NAudio 弃用警告；`Echo.CoreChecks --audio` 共 50 项通过。未配置或调用 DeepSeek、未启动窗口。真实总结响应、超大存档完整请求及总结期间录音并行仍需后续验收。底稿见 `docs/sources/windows-a10-summary-selection-2026-09-30.md`。

2026-09-30：扩展 A11 睡眠恢复状态机检查，模拟 10 次“录音意图保留→睡眠→唤醒→恢复→完成”循环，且单独验证唤醒等待期间用户停止后不会启动恢复。`Echo.CoreChecks --audio` 共 51 项通过；未触发系统睡眠、未启动应用。真实设备睡眠/唤醒、音频设备稳定等待及新录音段行为仍待人工验收。底稿见 `docs/sources/windows-a11-sleep-cycle-2026-09-30.md`。

2026-09-30：扩展 A12 音频端点通知策略矩阵：默认多媒体端点变化会请求跟随；固定设备失联不切到其他设备；活动/未知设备、无关 flow、Console 角色和未变化的默认 ID 均忽略。全套 `Echo.CoreChecks --audio` 共 52 项通过。未触发设备拔插或蓝牙 profile 变化；真实端点与路由变化仍待硬件验收。底稿见 `docs/sources/windows-a12-endpoint-policy-2026-09-30.md`。

2026-09-30：扩展 A13 活动会话断线回归：本机模拟服务先接受转写配置并收到音频帧，再主动关闭 WebSocket；客户端将缺少 finished 的断开报告为 `IOException`，符合有限重试分类。401 不重试、503 可重试及连接期间用户取消用例仍通过。`Echo.CoreChecks --audio` 全套共 53 项通过；未连接云端、未保存音频、未启动窗口。ViewModel 恢复编排路径已静态检查，但真实断网、恢复新段与错误提示尚未端到端实测。底稿见 `docs/sources/windows-a13-network-recovery-2026-09-30.md`。

2026-09-30：为当前 Release x64 构建生成自签名预览 MSIX `windows/artifacts/Echo-Windows-x64-20260930.msix`。WinApp CLI 0.6.1；包身份版本 1.0.0.0、架构 x64；签名发布者与清单 `CN=AppPublisher` 匹配，包内有 `AppxSignature.p7x`。SHA-256 与文件大小见 `docs/sources/windows-a18-msix-preview-2026-09-30.md`。Windows 证书链状态为未受信任根，因此该包只用于后续显式信任后的预览安装；本轮未安装、未导入/信任证书、未启动应用。正式证书、时间戳、干净机器安装升级和回退仍待完成。

2026-09-30：A17 对照 macOS `TranscriptArchiveStore`、`TranscriptModels` 与 `SRTExporter`，修正 Windows SRT 的说话人位置、英文条目筛选、全条目零点和毫秒截断；校对历史 `Date` 写为 Apple epoch 秒，读取时兼容旧 ISO-8601 Windows 日期。Swift Codable 形状样本、Windows JSON 往返及同格式 SRT 断言通过；Release x64 构建 0 错误、10 条既有警告，`Echo.CoreChecks --audio` 共 56 项通过。当时本机没有 Swift/macOS runtime，尚未进行实机双向对拍。该未完成项于 2026-10-09 由 GitHub macOS runner 补齐：Mac 生产 encoder → Windows 导入/回写 → Mac 生产 decoder 回读通过；三份合成产物哈希及 CI 记录见 `docs/sources/windows-a17-mac-contract-parity-2026-09-30.md`。为 A17 代码另生成的自签名预览包 `windows/artifacts/Echo-Windows-x64-a17-20260930.msix` 哈希与签名状态见 A18/A17 底稿。未安装或启动应用，未导入证书。

2026-09-30：扩展 A16 两小时规模数据路径检查：即时合成 7,200 条双语字幕（每秒一条），执行 Archive 快照、JSON 序列化/解析和完整 SRT 导出。2,501,561-byte JSON 与 565,473-byte SRT 共保留全部 7,200 条，尾部时间码到 02:00:00；内存流水线用时 71 ms。A16 当时的全套 `Echo.CoreChecks --audio` 共 54 项通过；A17 对齐后全套升至 56 项。未进行磁盘 I/O、UI 渲染、真实录音、内存曲线或两小时运行；见 `docs/sources/windows-a16-two-hour-archive-2026-09-30.md`。

2026-10-09：A15 静态复审修正字幕行重复的 AutomationId，改按字幕 UUID 生成稳定唯一标识；为运行时创建的设备切换与校对对话框控件补齐 AutomationId。Windows Release x64 后台构建 0 错误，10 条既有 NAudio 弃用警告；CoreChecks 51 项通过。未启动 Echo 或 UI smoke；键盘、Narrator、高对比度和多 DPI 仍需实机验收。见 `docs/sources/windows-a15-accessibility-static-review-2026-09-30.md`。

2026-10-09：首次 WinUI 调试实例启动发现 `MainPage` 构造时读取尚未赋值的 `App.Window`，导致 `NullReferenceException`；关闭浮层的订阅已移至主窗口关闭事件。UI smoke 另发现主界面语言菜单显示 C# 对象调试文本，现绑定 `Title`。修复后 Debug x64 构建 0 警告/错误，Windows UIA smoke 12 项通过，启动与关闭正常；不含字幕内容的结果和故障依据见 `docs/sources/windows-a33-ui-smoke-2026-10-09.md`。未开始录音、保存设置或调用云端；真实视觉对照、透明浮层、多 DPI、键盘/Narrator 和音频硬件仍待验收。
