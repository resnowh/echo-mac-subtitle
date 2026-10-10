# A92 悬浮字幕交互来源与验收底稿

日期：2026-10-10（Asia/Shanghai）

## 源码基线与 GitHub 状态

- PR #5：开放，`feature/windows-mac-parity` → `main`；刷新状态时检查均成功。
- PR #6：开放，`feature/windows-ui-mac-parity` → `feature/windows-mac-parity`；刷新状态时检查均成功。
- 本分支从 PR #6 的 `d5832dcdcab31d4a8525d47d83e02e6675287426` 开始。显式 fetch PR #6 的 head ref 后，分支相对其最新提交 ahead 1 / behind 0，无需拉取。
- `pr-5-baseline.json`、`pr-6-baseline.json` 是查询时的 GitHub PR 与 CI 原始 JSON；`source-baseline-git-blobs.txt` 记录开始改动前源码 blob，便于还原。

## 隔离 GUI 包

- 身份：`A92F3721-1586-4D09-9A92-79E7B8C95131`。
- 临时 checkout：`C:\Users\HENRY\AppData\Local\Temp\Echo-A92-IsolatedGui`。
- 单独 mutex：`Local\\EchoA92IsolatedGui`；运行 PID `84096`，可执行文件路径落在隔离 checkout 的 Debug MSIX 安装目录。
- 包数据：该身份独有的 LocalCache。配置中的 Soniox 与 DeepSeek API Key 均为空；未启动录音、麦克风采集、Soniox 或 DeepSeek 会话。
- fixture：只注入“Synthetic English caption for isolated UI test / 隔离验收用合成中文字幕”，不读用户字幕或存档。

## 验收结果

- 源码 Release x64 构建：0 警告、0 错误；CoreChecks：193 项通过。
- 一次完整 GUI 流程在隔离包中实测通过：工具栏/设置控件可访问、设置可保存、点击穿透改变字幕 HWND 的 `WS_EX_TRANSPARENT`、穿透时底层窗口命中、解锁拖动、锁定、主窗口应急恢复、主窗最小化后字幕仍可见、关闭重开 3 次且没有重复字幕 HWND。
- 单独的真实悬停检查确认移出字幕/工具条后工具条隐藏。
- 扩展后的复跑遇到 UI Automation 元素过期和拖动坐标时序不稳。`ui-interaction-results.json` 保留最近一次诊断结果，不能把它读成一份全绿报告；一次全流程通过的当时终端输出没有直接重定向成原始 JSON。
- 机器只有一台可用显示器，第二屏、异 DPI 热切换、任务栏位置变化、Narrator、长时间 GPU 资源稳定性仍未验证。
- `screenshots/` 只包含按单个窗口捕获的图。透明 HWND 截图被捕获工具用黑色合成；不作为桌面实际透明度的证明。未捕获全桌面。

## 文件目录

- `isolated-debug-build.log`：隔离包构建输出。
- `package-runtime.json`：临时包身份、安装路径及进程运行信息。
- `isolated-gui-fixture.txt`：合成 fixture、身份和 base commit。
- `ui-interaction-results.json`：最新一轮真实 UIA/交互诊断 JSON（含通过项和失败项）。
- `screenshots/`：单窗口捕获原始 PNG，不用于声称桌面合成透明效果。
