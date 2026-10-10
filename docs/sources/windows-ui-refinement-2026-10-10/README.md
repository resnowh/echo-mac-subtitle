# Windows 主界面 UI 复核 A86（2026-10-10）

## 基线

- Mac `main`：`ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows PR #5：`b52f68efafd7fd1b0d1671ded4fc3f37c9a701e7`，仍开放，目标 `main`。
- Windows UI 分支 PR #6：`4f0a77de01fa13c4b4e96887ae69d7c9f7350199`，仍开放，目标 `feature/windows-mac-parity`。
- 拉取并核对远端 `main`、`feature/windows-mac-parity`、`feature/windows-ui-mac-parity`、`feature/windows-preview`：没有相对上述基线的新提交。PR 状态原始 JSON 和当时检查列表见本目录 `pr*-baseline.json`、`pr6-checks-before-a86.json`；Git 引用见 `git-baseline.txt`。

## 视觉差异清单

本轮复核了顶栏、语言菜单、字幕列表、Speaker/时间戳、纠正操作、滚动提示、录音工具栏、存档工具栏、波形、AI 总结和主题资源。已有参数差异表及运行态边界见 [`docs/windows/ui-parity.md`](../../windows/ui-parity.md) 和 [`docs/windows/mac-parity-matrix.md`](../../windows/mac-parity-matrix.md)。

发现一项遗漏：Mac `SynchronizedTranscriptView.metadata(for:)` 会按照当前显示配置决定是否显示 Speaker，并忽略空 speaker；Windows 模板一直展示 speaker TextBlock 和中点分隔符。A86 新增 UI 呈现策略：录音中使用启动时冻结的 speaker 设置，闲置时使用已保存设置；speaker 字段为空时隐藏名称和中点。ListView 行加载、`DataContextChanged` 回收复用和 speaker 字段变化都刷新元信息显示。未改 Soniox 请求、识别处理、归档、SRT 或 Mac 文件。

## 参考图核验

任务附件中的两张图片在打开后均显示邮件撰写/回复窗口，不是 Echo Mac 或 Windows UI。附件完整路径和 SHA-256 记录于 `source-sha256.txt`；由于截图含无关邮件内容，没有将图片复制进项目。本轮无法基于这些图做 Mac/Windows 像素对照，也没有启动 Echo。用户保留最终视觉评估。

## 改动文件

- `windows/Echo.Windows/Core/TranscriptPresentationPolicy.cs`
- `windows/Echo.Windows/ViewModels/MainPageViewModel.cs`
- `windows/Echo.Windows/MainPage.xaml`
- `windows/Echo.Windows/MainPage.xaml.cs`
- `windows/Echo.CoreChecks/Program.cs`
- `docs/windows/ui-parity.md`
- `docs/windows/mac-parity-matrix.md`
- `docs/windows/testing.md`
- `docs/roadmap/数据清单.md`
- 本目录的来源说明、PR/Git 基线、哈希及原始日志

## 验证

- 隔离 worktree CoreChecks：最终源码 188 项通过；结束摘要为 `Completed 188 checks. No cloud calls; no audio was saved.`，见 `a86-corechecks-final5.log`。
- 隔离 worktree Windows Release x64：最终 UI/XAML 源码构建成功，0 警告、0 错误，见 `a86-build-final4.log`。
- `git diff --check`：通过。
- 没有启动或操作 Echo，没有进行 UIA 或截图测试。故本轮没有实证浅/深主题、125/150/200% DPI、键盘/Narrator、弹出菜单、滚动和实际像素布局。
- 早期隔离尝试的完整失败输出一并保留：外置 MSBuild 中间目录触发重复生成源；模板内嵌套静态绑定生成错误；第一轮静态断言也未匹配后来扩展的通知。当前最终构建和全部 CoreChecks 已通过。日志按文件名区分，未删除或改写原始输出。

## 原始材料

- `a86-corechecks.log`、`a86-build.log`：最终通过日志。
- `a86-corechecks-final5.log`、`a86-build-final4.log`：最新元信息范围断言通过的最终权威日志。CoreChecks 检查时间戳/语言仍保留，且数据上下文复用会刷新行显示。
- `a86-isolated-output-path-*-failure.log`：首次隔离输出目录失败。
- `a86-template-binding-build-failure.log`：DataTemplate 绑定方案失败。
- `a86-initial-worktree-corechecks.log`、`a86-pre-final-assertion-corechecks.log`：中间检查失败。
- `source-sha256.txt`：Mac 参考源、Windows UI/测试源和附件的 SHA-256。
- `pr5-baseline.json`、`pr6-baseline.json`、`pr6-checks-before-a86.txt`：变更前远端 PR 状态。
- `git-baseline.txt`：刷新后的基线引用。

本轮结论是源码级条件和 XAML 构建已验证；Mac/Windows 截图视觉验收仍未完成，不将静态检查描述为像素级匹配。

## 提交与 GitHub CI

- Windows UI 修改提交：`e2ad87f3a125391ce252883290e1c47700d8e822`，已推送至 [PR #6](https://github.com/resnowh/echo-mac-subtitle/pull/6)，目标仍为 `feature/windows-mac-parity`。
- Windows CI：run [`38028073622`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38028073622) 全部 6 个 job 通过，含 CoreChecks、Release x64 和 Mac Archive/SRT 回读。
- macOS CI：run [`38028073642`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38028073642) 通过。
- macOS unsigned distribution preflight：run [`38028073669`](https://github.com/resnowh/echo-mac-subtitle/actions/runs/38028073669) 通过。
- 上述 run 的原始 JSON 和 PR check 摘要见 `windows-ci-e2ad87f.json`、`macos-ci-e2ad87f.json`、`distribution-preflight-e2ad87f.json`、`pr-checks-e2ad87f.json`。
