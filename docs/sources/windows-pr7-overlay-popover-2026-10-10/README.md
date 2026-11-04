# A93 PR #7 悬浮字幕面板修复底稿

日期：2026-10-10（Asia/Shanghai）

## 代码与 PR 基线

- PR #7: `https://github.com/resnowh/echo-mac-subtitle/pull/7`
- 基线 branch: `feature/windows-overlay-interaction`
- 修复前 GitHub head: `fda2aa5db4b44d2f2e38ce581ba450803e963063`; 本地显式 fetch `refs/pull/7/head` 后一致。
- 修复提交：`507d885a3f8ba6b28bc57854efab9cb71bdb1b5c`，已推送至 PR #7，PR 保持 OPEN。
- PR base: `feature/windows-ui-mac-parity`; PR 未合并。
- 变更只涉及 Windows 实现、Windows 测试脚本、Windows 文档与数据清单。`macOS/` 未修改。

## 来源与证据

- `pr-7-state.json`：执行本轮修复前读取的 PR 状态、基线分支和 head SHA。
- `release-build.log`：Windows Release x64 构建输出。
- `corechecks.log`：CoreChecks 本地输出，包含完整测试名称与汇总。
- `smoke-script-ast.log`：PowerShell 解析器验证 UIA smoke 脚本语法的结果。该脚本未运行。
- `precommit-checks.log`：提交前 `git diff --check` 结果。
- `pre-push-pr-checks.txt`：修复前 PR #7 头提交的 GitHub 检查状态。
- `post-push-pr-checks.txt`：修复提交 `507d885a3f8ba6b28bc57854efab9cb71bdb1b5c` 的 GitHub 检查状态，8 项全部通过。
- `gui-environment-check.json`：只读检查 VM 管理工具与运行中 VM 进程；没有连接或启动 GUI。
- `icon-glyph-source.md`：微软官方 Segoe MDL2 glyph 对照与本次改正的错误码点。
- `source-sha256.txt`：提交前 Windows 源码、脚本和本底稿文档的 SHA-256。
- A92 原始失败回执保持在 `../windows-a92-overlay-controls-2026-10-10/ui-interaction-results.json`，没有覆盖或重写。

## 结果摘要

- 工具栏为 184×40 DIP，图标按钮 30×30 DIP；设置面板为 304×416 DIP，固定标题和底栏，常用项默认显示，更多设置折叠，主体在有界滚动区中。
- 新增纯函数屏幕放置器，基于工具栏锚点与 monitor work area 在上/下择位、空间不足时再尝试左/右，尺寸不足时收缩；锚点、工作区和 DPI 未变化时跳过重复 resize/move。
- 核验目标：屏幕四边、负坐标副屏、短工作区，100%、125%、150%、200% 布局换算。
- GUI 验收未执行：用户要求不操控其桌面鼠标；本机没有确认可用的独立 VM/test desktop。不能声称真实窗口视觉、UIA 矩形或交互已通过。CoreChecks 和构建不能替代 GUI 验收。
- 没有启动 Echo、录音或任何识别会话；没有读取真实字幕、归档或 API Key，没有调用 Soniox/DeepSeek。
