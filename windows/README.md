# Echo Windows 首个预览版

原生 WinUI 3 / C# 实现，与 `macOS/` 并列维护。当前交付目标是 Windows 11 x64 本机试用；Mac 实现未修改。Android/iOS 暂未实现。

## 开始使用

1. 当前开发机可通过工作区根目录的 **启动 Echo Windows.lnk**，或开始菜单的 **Echo.Windows** 打开。不要移动或删除编译输出目录。
2. 点击右上角齿轮打开“服务设置”，填写自己的 Soniox API Key，点击面板右上角“完成”。默认英语识别、中文翻译；源语言留空可自动识别。
3. 设置面板关闭后，选择电脑音频、麦克风或双输入，点击“开始录音”。电脑音频来自所选播放设备；麦克风需要 Windows 隐私设置允许桌面应用访问。
4. 点击“停止录音”。下次可选择原存档继续录音。总结会保存在当前存档；使用“导出 SRT”或“导出存档”保存到指定位置。
5. 可选：填写 DeepSeek Key 后使用“AI 总结”。只有手动点击总结时才发送文字稿。

## 已实现

- WASAPI 播放设备回环采集、麦克风采集、本地双路混合；转换为 16 kHz 单声道 PCM16。
- Soniox WebSocket 转写和单向翻译；临时文本替换、最终文本追加、端点分段、Speaker 编号。
- 本地多段存档、接续、JSON 导入/导出、整份存档 SRT、停止后自动单段 SRT。
- DeepSeek 总结新增内容、当前段、全存档；结果可选中复制。
- 浅色/深色/系统主题、窗口置顶、录音波形、重复启动保护。
- API Key 用 Windows DPAPI 当前用户加密；不保存原始音频。
- 字幕逐条手动纠正、撤销及原始识别稿保留；可按需请求 DeepSeek 校对/重新翻译建议，并编辑后再应用。设置可选择开启分句后的自动语境校对；默认关闭，只生成待人工确认的建议。
- 存档可在存档菜单中重命名；新名称立即写入现有存档文件。
- 刚完成的录音段可经确认拆到独立存档；原存档保留 `.bak` 备份。
- 存档可经二次确认移入本地 `Deleted/` 回收区；将 JSON 移回 `Archives/` 可恢复。

数据目录：`%LOCALAPPDATA%\EchoWindows`。安装方式可能使系统对路径重定向，以应用“打开数据目录”按钮打开的位置为准。

- `Archives/`：JSON 存档，包含字幕、该存档的 AI 总结及增量总结去重记录；每 3 秒检查点保存，替换时保留上一份 `.bak`。
- `Deleted/`：从应用中移除的存档及备份；将 `.json` 文件移回 `Archives/` 可恢复。
- `Exports/`：每次停止后的单段 SRT。
- 配置文件：模型、语言、主题、加密后的 Key。

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

NuGet 版本记录在 `packages.lock.json`。正式 CI 可用 `dotnet restore --locked-mode`。构建当前仍有 NAudio 旧采集接口弃用警告，迁移新接口前保留本机已验证的调用路径。

## 测试签名安装包

本地交付位置：`windows/artifacts/Echo-Windows-x64-mac-ui-preview.msix`，约 117 MiB，包含运行时。附带 `Echo-preview.cer` 公钥证书。**这是自签名测试包，不是公众发行证书，也不是商店上架版本。**

其他测试机器需要先审查并信任该测试发布者证书，再安装 MSIX；普通用户直接双击可能被不受信任证书拦截。当前机器已通过开发注册启动，尚未用另一台干净机器验证此 MSIX 的安装。不要把本机的 `.pfx` 私钥发给测试用户或提交到 Git。安装包和私钥均被 Git 忽略。

可复现打包命令（已构建并通过 `winapp run` 生成 AppX 目录后）：

```powershell
winapp cert generate --manifest windows/Echo.Windows/Package.appxmanifest --output windows/artifacts/Echo-preview.pfx --export-cer
winapp package windows/Echo.Windows/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/AppX --output windows/artifacts/Echo-Windows-x64-mac-ui-preview.msix --cert windows/artifacts/Echo-preview.pfx
```

生产发布仍需确定发布者身份、正式代码签名或商店发行、安装升级测试、隐私政策及长时间稳定性验证。

## 本次验证与边界

2026-09-17：Release 构建成功；14 项本地核心/采集/模拟协议检查通过；9 项 UI 冒烟检查通过；另通过实际文件选择器完成示例 JSON 导入及 SRT 导出，核对三段原文、译文与时间戳。已实际打开窗口并检查主界面、设置界面截图，修复高 DPI 下字幕区域高度问题。按用户选定的 Mac 风格重做双栏字幕、底部控制、薄荷绿主题和居中设置面板；原界面已备份。窗口中的“示例”存档为人工测试文字，不是真实语音识别结果。

尚未验证：真实 Soniox/DeepSeek Key 的云端端到端结果、网络波动长测、其他 Windows 版本、ARM64、不同声卡、Mac 实机存档互通、正式安装升级与签名信任。

首版限制：

- 录音期间可切换当前模式使用的麦克风/播放设备，并保持 Soniox 会话与当前字幕段；设备重建期间可能出现短暂静音，尚未在多种真实声卡上实测。网络中断或采集故障会结束当前段并保存已收到的文字，需要手动开始新段；睡眠恢复无保证。
- 双设备按采样率转换后混合，尚无长期时钟漂移补偿；长会议建议先用单输入验证。
- 导入单个 JSON 限 32 MB。
- 启动读取/解析、自动存档序列化与写盘、导出序列化均在后台；保存前仍会在 UI 线程复制一致性快照，超长存档的复制可能短暂占用 UI 线程，尚未压力测试。
- 界面当前以简体中文为主，尚未完成完整本地化与高对比/屏幕阅读器验收。

代码维护在 `feature/windows-preview` 分支；先前方案文档已在主分支发布。


2026-09-29：进一步参照 macOS/EchoMacApp.swift 和 TranscriptViews.swift，完成双栏字幕表头、逐行分隔、底部录音/存档/波形三行操作、顶部主题切换及设置覆盖面板。Release 构建与 9 项 UI 冒烟检查通过；截图保留在工作区 docs/sources/windows-mac-ui-*.png。


最新自签名包的 SHA-256：`0131219EEAC805FE5C6E502F85AF931A06B76686695B561F75EAD43E45763C98`。开发机通过包身份运行与设置面板滚动检查已通过；跨机器安装仍待验证。

2026-09-29 同步并补齐：当前分支已合入 `origin/main` 至 `c7b9e37`，包含 Mac 端新增的字幕响应优化、可撤销手动编辑、可选 AI 校对建议及语义边界拆分。Windows 已增加逐条字幕编辑、撤销、查看原始识别稿、手动/可选自动 AI 校对、重新翻译及校对术语设置。自动校对默认关闭；开启后在分句或说话人切换边界排队生成建议，仍需人工应用。人工修改按字段锁定，后续识别不会覆盖锁定字段；字幕发生变化时旧建议会失效。最终识别 token 的说话人变化会拆成独立字幕行。校对数据使用 Mac 兼容字段；总结保存在当前存档的可选 `summary` 字段，增量总结签名也随存档保存，长文字稿按不超过 18,000 字符的块顺序总结。Release x64 后台构建通过，0 错误；核心检查覆盖说话人分行、单次分句回调、校对锁定、撤销、存档总结往返与 Unicode 分块。未启动应用窗口。仍待 Mac 实机双向存档验证与跨机器安装验证。

2026-09-29：自动存档写盘、导出序列化和启动时存档加载/解析改为后台处理；保存采用有序快照队列。录音时可切换当前采集模式使用的输入/播放设备，保持同一个转写连接和字幕段；切换失败时尝试恢复旧设备，恢复失败则保存已有文字并停止。Release x64 构建通过；后台本地采集/模拟服务检查共 29 项通过，覆盖三种音频模式热重建、无效设备回滚和 WebSocket 会话保持。未启动 Echo 窗口、未调用云服务、未保存音频。仍待不同声卡/长时间会议实测，断网恢复仍需手动重新开始录音。
