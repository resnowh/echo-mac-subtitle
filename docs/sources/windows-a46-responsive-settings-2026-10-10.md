# A46：设置面板适配受限工作区

核验日期：2026-10-10  
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`  
Windows 修改基线：`b033569c5383edabf6ed11c98d8bed05a9f1bf87`

## 来源与差异

- Mac `SettingsView` 在 `macOS/EchoMacApp.swift` 使用 `.frame(width: 520, height: 560)`，并把四类设置内容放在 `ScrollView(.vertical)` 中。该视图源码 SHA-256：`CB2ED238D2EE30643BE2F4621E18599D7102ADBECFDF898D0EC52204E7802D87`。
- Windows `MainWindow.xaml.cs` 读取当前显示器工作区并按物理像素限制主窗尺寸；SHA-256：`C6F8888505A98F0599C5241893A11626CCBC287BC322C5893C6A4F069A2A8E9A`。
- 修改前 Windows `MainPage.xaml` 设置面板为固定 `Width="520" Height="560"`；SHA-256：`0C65EFEF79285A0C2A43E43DA8521C0029345CA6397ADDA04763CF39D10F3546`。

## 实施与验证

Windows 保留 `MaxWidth="520" MaxHeight="560"` 作为正常目标上限，取消固定宽高并设置水平、垂直 Stretch。现有内部 `Grid` 的星号内容行和 `ScrollViewer` 负责在剩余高度内访问长设置页。常规窗口应保持接近 Mac 的设置尺寸；工作区受限时可收缩。

- 修改后 `MainPage.xaml` SHA-256：`03D87081606BA50D4DC4D7096AA3DCAFEC4F73E48E4F3FC2E2A7309E19E6F998`。
- `Echo.CoreChecks.csproj` 加入 MainPage XAML fixture；修改前 SHA-256 `70517E1691B830F8DD4164508A6BF7FC118B22BCF3DECF1479B463055ECA483A`，修改后 `BB80C8DF5D80CF00ABBCAD0C154F0EAE4BD9817CC6F54738798AA4C5D5230AD2`。
- `Echo.CoreChecks/Program.cs` 检查无固定宽高、最大尺寸仍为 520×560、容器为 Stretch 且仍含 ScrollViewer；修改前 SHA-256 `9C4B864C89DC24F681F1CCCEAF6D13B6D4802B2D23BE3327DE0324B7ACBB3EC6`，修改后 `F4D50445BA36F3ADB14A033A806B319BEE389DF06EBD2EE2A9980FD0420F0E60`。
- 本机 CoreChecks：121 项通过。
- 本机 Release x64 构建：0 警告、0 错误。
- 未启动 Echo；高 DPI、短屏、键盘与 Narrator 的实际 GUI 验收仍待完成。自动契约检查和 XAML 构建不能替代 GUI 实测。

数据为当前公开仓库源码和本机合成检查结果；没有用户数据、音频、凭据或云端服务调用。未修改 `macOS/`。
