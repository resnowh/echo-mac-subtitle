# A61 麦克风权限拒绝指引（2026-10-10）

## 目的与基线

对照 Windows 官方桌面应用麦克风授权说明，补齐拒绝访问时的用户操作指引。Mac 基线仍为 `origin/main`，提交 `ae0359dc90da0ccb5e526a275da1747954a49a4f`；本项只改 Windows 代码与 Windows 文档，没有修改 `macOS/`。

官方页面：[Turn on app permissions for your microphone in Windows](https://support.microsoft.com/en-us/windows/privacy/turn-on-app-permissions-for-your-microphone-in-windows)。页面原始 HTML 保存在本目录 `microphone-permissions.html`，SHA-256：`B75FBDC0EB9E21B443774BC9467781192D33A399788629DEAE870A259041B8F3`。该说明要求桌面应用启用 Windows 麦克风访问与“让桌面应用访问麦克风”。

## 实现

`AudioCaptureErrorPresentation` 识别 `UnauthorizedAccessException`、Win32 错误 5、COM `E_ACCESSDENIED`，并递归检查包装异常与 aggregate exception。话筒模式返回“设置 → 隐私和安全性 → 麦克风”的中文说明，提示启用两项桌面麦克风权限；非话筒音源返回音频设备访问提示；其他异常仍显示原消息。启动录音、识别会话故障和两种录音中设备/模式切换错误都使用该映射。若切换成功回滚，状态同时说明旧音源已恢复、录音继续。

## 文件与哈希

| 文件 | SHA-256 |
|---|---|
| `windows/Echo.Windows/Services/AudioCapture.cs` | `6D6A8AEE1E3F6BC9D0B7C29A43FA3C220389BB2438B48A52AD619C422270DE15` |
| `windows/Echo.Windows/ViewModels/MainPageViewModel.cs` | `42E063DCA0C8E1383B19325163E8D6F38D01D1614719B30A43A97C41C4F4A8B7` |
| `windows/Echo.CoreChecks/Program.cs` | `D44CE83977E02986D3EED806FE6A3F806F9FD235472B77E194AD16ACFA450382` |
| `test-results.txt` | `0EA228591F1747BE977ED2CDF735C26E6A39DF808A3EB01B66F2F8440908B2DA` |
| `build-results.txt` | `949D3D5DA4526B4CCE3D46A7648357435CCAF32A7BBDA961B98F7067F1391503` |

## 验证与边界

`dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release`：142 项通过。用合成 `UnauthorizedAccessException`、包装的 COM `E_ACCESSDENIED` 和一般 `IOException` 验证权限识别、话筒/非话筒提示与普通异常保留。`dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`：构建成功，0 警告、0 错误。

未启动 Echo 应用，没有打开麦克风、触发真实 Windows 隐私开关拒绝或访问云服务；因此系统拒绝后的实际异常形态、用户开启权限后的恢复流程和界面呈现仍需在 Windows 实机验收。CI 状态应以本提交推送后的 PR #5 checks 为准。
