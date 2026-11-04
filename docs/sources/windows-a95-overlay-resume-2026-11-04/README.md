# A95 Windows 浮层交互恢复（2026-11-04）

本轮继续 PR #7（head `640c3c7ffb03302c465b163fca21ff5d9014fb97`），未改主线或自动合并。旧临时 checkout 已清理，使用工作区内独立 `overlay-interaction` worktree 恢复同一分支。

## 来源与原始记录

- `pr-7-baseline.json`：GitHub connector 原始 PR 元数据，包含历史 PR 描述；历史 GUI 表述应结合 A92 失败 JSON 与 A93 更正阅读。
- `ci-baseline.json`：暂停提交的三个 GitHub Actions workflow 均成功；不是本轮新提交的 CI。
- `git-baseline.txt`：fetch 后三个 Windows 分支的实际 SHA 和本地 HEAD。
- `gui-environment.txt`：未找到 `Get-VM`/`vmrun`/`VBoxManage` 或运行中虚拟机进程；只能判定未确认可用独立 GUI 环境，不能断言机器绝无虚拟化能力。
- `release-build.log`：首次构建缺少 Input 命名空间导致 FocusManager 无法解析及后续 XAML 级联错误；`release-build-final.log`、`release-build-complete.log`：修正后最终 Release x64，0 警告/错误。
- `corechecks.log`：旧源码字符串契约因新增结束拖动调用失败；`corechecks-final.log`：修正契约后运行至临时文件原子替换时遭沙箱拒绝；`corechecks-unsandboxed.log`：相同合成测试获准在沙箱外完整运行，197 项通过，无云请求/真实音频。
- `smoke-script-validation.txt`：PowerShell AST 和缺少独立桌面声明时的保护入口实际测试。GUI 脚本正文未运行。
- `source-sha256.txt`：本轮 Windows 修改文件 SHA-256。

## 实现与审查

1. `OverlayInteractionWindows.cs`：Button 已处理的指针事件通过 AddHandler 接收；手柄保留 WinUI CapturePointer，结束/取消时调用字幕移动结束路径。键盘操作保持工具栏可见；Escape 的 PreviewKeyDown 返回齿轮焦点。验证后的语言开关状态同步回原控件，重置位置保留显示器。
2. `NativeDesktopSubtitleOverlayWindow.cs`：锁定配置作为移动判断；显示/应用设置时恢复状态；隐藏、关闭、取消捕获时结束并持久化，只有字幕 HWND 自己持有捕获时才 ReleaseCapture。
3. `OverlayPopoverLayout.cs`：短屏回退不再按矩形面积牺牲宽度，先缩短垂直视口；新增真实可复现几何输入 600×500 工作区、中间 184×40 工具栏，旧算法会选择 200px 窄侧条，新算法保持 304px 宽。
4. `ui-overlay-interaction-smoke.ps1`：新增 HWND/工作区/UIA/滚动视口测量与四个设置/工具栏截图点，重取 UIA 元素，默认拒绝用户桌面鼠标测试。开关只是独立环境声明，不提供隔离。

按 winui-code-review 复核了事件回调、焦点、捕获重入、销毁路径及命名控件。改变均位于浮层与测试层，未修改采集、Soniox、DeepSeek、存档业务逻辑或 macOS 文件。现有硬编码深色材质、高对比/主题和 Narrator 仍需 GUI 验收，不能凭编译判断可访问性通过。

平台依据：Microsoft 的 [WM_CAPTURECHANGED](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-capturechanged) 说明释放捕获也会触发该消息，故先清除操作状态以避免重入重复保存；[WinUI focus 设计说明](https://github.com/microsoft/microsoft-ui-xaml/blob/main/docs/design-notes/focus.md) 和 [键盘事件文档](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/input/keyboard-events.md) 用于复核焦点和隧道路由。

## GUI 状态

没有启动 Echo 或 GUI 测试，没有控制鼠标、截图或读取用户字幕/API Key。本目录没有新 GUI 通过证据。P1/P2 的实际拖动、锁定/穿透恢复、Escape/焦点、设置裁切、多屏/DPI 热切换、窗口/GPU 稳定性均待独立 VM/测试桌面验收。极小工作区仍可能小于标题/底栏最低可用尺寸，不能把几何夹取称为完整控件可用证明。
