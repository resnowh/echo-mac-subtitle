# A15 Windows 无障碍静态审查与修正

日期：2026-09-30
分支：`feature/windows-preview`
范围：字幕列表和设置控件的 UI Automation 语义；未启动 Echo。

## 审查发现和变更

静态检查发现三处读屏语义不完整：双栏字幕正文没有把“原文/译文”角色并入可访问名称；检测语言的固定名称会隐藏动态语言值；纠正按钮固定读作“纠正字幕”，即使它当前显示的是“查看校对”。

已作如下修正：

1. 字幕原文、译文和检测语言的 UI Automation 名称随绑定文本更新，例如“原文：Hello”“译文：你好”“检测语言：ja”。空译文名称保持为空，不播报一个不存在的值。
2. 纠正按钮由 WinUI 从其动态可见内容生成名称，使“纠正”和“查看校对”能够准确表达当前动作。
3. 元数据分隔符“·”从 UI Automation Control View 隐藏；可视外观不变。
4. 2026-09-30 将 Soniox 模型设置的 AutomationId 从通用 `Control10` 改为 `SonioxModel`，相应更新既有 UI smoke 脚本定位符。2026-10-09 将仍存在的通用控件标识统一改为语义化 ID，详见 [本轮底稿](windows-a15-automation-ids-2026-10-09.md)。

## 静态主题和构建检查

`App.xaml` 的 Light/Dark 使用用途明确的画刷；HighContrast 映射到 Windows 系统窗口、文字、强调色画刷。设置表单位于可滚动容器内。没有改主题颜色和录音状态表达。

`dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release --no-restore /p:Platform=x64 /p:RuntimeIdentifier=win-x64` 成功：0 错误、10 条既有 NAudio 弃用警告。A22 CoreChecks 仍为 46 项通过。Release XAML 编译证明绑定语法和代码生成有效，但不能替代运行时 UI Automation 检查。

提交 `9f68c8516b5d380e7885bd9ad38a56ac8b296954` 的 [GitHub Actions 运行 36617852437](https://github.com/resnowh/echo-mac-subtitle/actions/runs/36617852437) 全部通过：Windows 锁定还原、46 项 CoreChecks、Release x64 构建，以及 Mac transport checks 与 Debug/Release 构建均成功。

## 未覆盖

没有启动应用或运行 `windows/ui-smoke.ps1`。Narrator 实际播报、键盘全流程与焦点顺序、高对比度主题、100%～200% 缩放、多显示器 DPI 切换仍未验收；A15 仅完成源码层面的语义补齐。

## 动态控件 AutomationId 复审（2026-10-09）

静态代码审查发现字幕列表的编辑按钮在每个虚拟化条目中都复用 `EditSubtitle`，而运行时创建的校对对话框和切换设备对话框控件没有稳定 AutomationId。虽然这些控件有可见文本或 Header，但自动化无法可靠区分每条字幕，也无法用稳定标识定位对话框操作。

- 字幕编辑按钮 ID 改为 `EditSubtitle_{subtitle UUID}`；字幕 ID 固定，因此同一存档内唯一且重载后稳定。
- 切换设备下拉框分别标为 `SwitchOutputDevice`、`SwitchInputDevice`。
- 校对对话框文本框标为 `CorrectionSource`、`CorrectionTranslation`，操作按钮标为 `RequestCorrection`、`RetranslateSubtitle`、`UndoCorrection`、`ApplyCorrectionSuggestion`。
- Windows Release x64 后台构建通过，0 错误、10 条既有 NAudio 弃用警告；CoreChecks 共 51 项通过，包含存档、音频策略和生命周期回归。`git diff --check` 通过。

按维护者要求没有启动 Echo 或 UI smoke。键盘遍历、Narrator、Accessibility Insights、真实高对比模式及多 DPI 仍需 UI 实机验收。
