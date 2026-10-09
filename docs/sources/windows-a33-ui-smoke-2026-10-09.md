# A33 Windows startup and UI Automation smoke

核验日期：2026-10-09  
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`  
Windows 分支：`feature/windows-mac-parity`，A33 修改前提交 `ef2fb36a0a462b1b41e6c2f4380e211531f884a3`。

## 启动故障与修复

使用 `winui-dev-workflow` 的 `BuildAndRun.ps1` 构建/启动 Windows Debug x64。首次构建 0 警告、0 错误；启动崩溃由 `winapp --debug-output` WinUI triage 和 Windows Error Reporting 定位为：

```text
System.NullReferenceException
Echo_Windows.MainPage..ctor in MainPage.xaml.cs:81
Echo_Windows.MainWindow..ctor in MainWindow.xaml.cs:28
Echo_Windows.App.OnLaunched in App.xaml.cs:71
```

`App.OnLaunched` 在 `new MainWindow()` 返回后才给静态 `App.Window` 赋值；而 `MainWindow` 构造期间同步导航创建 `MainPage`，因此 `MainPage` 构造函数里访问 `App.Window.Closed` 时为 null。修复将浮层关闭订阅放入 `MainWindow.Closed`，并由 MainPage 提供关闭浮层的方法。修复后 Debug x64 再构建成功，0 警告、0 错误；程序启动和 UIA 调用后正常关闭。

## UI 观察与修复

主界面语言 ComboBox 初始把 `LanguageChoice { Code = en, Title = English }` 这样的调试表示当作选项文字。为源语言和翻译目标选择框设置 `DisplayMemberPath="Title"`。UIA smoke 改为验证当前选择是非空标签且不是对象调试表示。

## 可复现 UIA 检查

通过 `windows/ui-smoke.ps1` 对 Debug 实例执行 12 项检查，结果摘要在 [`ui-smoke-results.json`](windows-a33-ui-smoke-2026-10-09/ui-smoke-results.json)：

1. 录音主界面可用，停止按钮在未录音时隐藏，默认音源为电脑音频。
2. 源语言和目标语言各显示用户可读标签。
3. 可打开设置和 AI 服务分类；Soniox Key 控件存在，页面没有模型输入控件。
4. 可以返回主界面，导出菜单和字幕列表可用。

12/12 通过。没有点击录音、编辑/保存设置、调用 Soniox/DeepSeek 或使用真实音频设备。UIA 只证明元素和基本导航可用，不证明截图视觉与 Mac 一致、透明浮层实际点击穿透、键盘、Narrator 或 DPI 行为。

留存的机器可读结果 `ui-smoke-results.json` 为 923 bytes，SHA-256：`08A0EA020BB809D269EB99C27052851E40668361C36E292E1791AE48004443A9`。

## 本机数据与留存边界

应用启动时从 MSIX 包隔离目录载入了此前存在的本机 Archive 内容。为避免提交本机字幕，测试只检查控件状态；生成的原始截图与 UIA 全树因包含该页面文字已删除，不在仓库留底。仓库仅保存上述不含文字稿的 12 项通过结果；启动异常以本文件中的 WER/triage 摘要记录，原始进程转储不纳入仓库。未打开归档文件或复制其内容，未保存设置或音频。

此结果作为本次 Windows GUI 控件与启动回归的证据；真实 Mac/Windows 截图对照及浮层、键盘、读屏、多 DPI/多显示器验收继续保持未验证。
