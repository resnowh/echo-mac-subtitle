# A90 隔离包 UIA Smoke 底稿

日期：2026-10-10。此底稿只对应 Windows 分支，不修改 `macOS/`。

## 隔离方式

- 从 `feature/windows-ui-mac-parity` 提交 `0d103514f98f3e549298859791add5dbde8303b2` 创建临时 clone，避免运行正式 Echo。
- 生成唯一 MSIX Package Identity `041833B0-BB1F-458D-B149-AC9E04107D04`，并将应用 mutex 也改为唯一测试名。包数据位于该 Package Family Name 的独立 LocalCache。
- 仅复制合成设置、合成字幕存档和空存档到此隔离包目录；副本详见本文件夹的 `synthetic-*.json`。未录音、未使用真实 API Key、未连接云服务、未读取正式 Echo 包或用户文件。
- Debug x64 构建完成后，通过 AUMID 启动 UIA 测试进程 PID 29960。测试完成后结束该进程并注销上述 Package Identity；注销回执确认身份已移除。未删除临时 clone，以保留可复查构建目录。

## 结果

`ui-smoke-results.json` 为结构化最终回执，`ui-smoke-run.log` 为同一份结果的可读摘要。`windows/ui-smoke.ps1` 在一个隔离进程会话内通过 10/10：

1. 主录音页空闲状态下可开始录音，停止操作隐藏。
2. 音源模式有可访问名称。
3. 识别语言和翻译目标按钮读出当前选择。
4. 识别语言保持自动识别；脚本对已选状态安全重复运行。
5. 翻译目标保持不翻译；脚本对已选状态安全重复运行。
6. 没有新字幕时不显示“回到最新”操作。
7. 识别、分段、AI 服务、常规设置页可达，浅色/深色主题可切换并恢复深色。
8. 可切换合成有字幕存档，列表和导出入口可达。
9. 校对弹窗可打开并关闭，未更改合成文本。
10. 空存档显示空态且没有字幕行。

首轮在初始合成设置（English / 简体中文）上实际选择了自动识别和不翻译，两项均在 UIA 回执中通过；最终 10 项重跑检查脚本在这两个已选状态下可安全重复运行。

在修正脚本时，实测发现设置页容器没有独立 UIA 节点（子标签正常）；UIA search 对空结果会返回有效 JSON 但使用非零退出码；主题 ComboBox 需展开后选中列表项。脚本已按可见控件和真实退出码处理。最终 10 项均通过。

## 截图索引

- `screenshots/settings-check.png`：常规设置深色。
- `screenshots/settings-recognition-check.png`：识别设置。
- `screenshots/settings-segmentation-check.png`：分段设置。
- `screenshots/settings-services-check.png`：AI 服务设置；合成 Key 为空。
- `screenshots/settings-general-light.png`：常规设置浅色。
- `screenshots/correction-editor-dark.png`：合成字幕纠错弹窗。
- `screenshots/empty-archive-dark.png`：空存档状态。

图片均为隔离测试包的合成界面，不包含用户数据。原始构建日志见 `isolated-build.log`；测试身份、AUMID、数据目录和注销核验见 `package-identity.txt`、`launch.json`、`test-data-path.txt`、`cleanup-verification.json`。各文件 SHA-256 见 `source-sha256.txt`。

## 限制

UIA 通过只证明这些控件可访问和流程可操作。Mac/Windows 并排视觉验收、键盘全路径、Narrator、多显示器、其他 DPI 和真实录音设备检查仍需分别完成；由用户进行最终视觉评估。
