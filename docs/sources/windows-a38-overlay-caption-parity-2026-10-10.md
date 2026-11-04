# Windows A38 悬浮字幕行显示对齐

核验日期：2026-10-10。目标为按 Mac 生产显示语义修正 Windows 浮层，同时把未经真实桌面运行验证的部分留作待验。

## 数据底稿与来源清单

| 来源 | 版本 | SHA-256 | 核验范围 |
|---|---|---|---|
| Mac `macOS/Views/DesktopSubtitleOverlay.swift` | `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f` | `F7743B67EDE0BCEF8CDE0424A76C66BB2E7C769725A1DB4E0309B8D01389D0B9` | 原文/译文显示条件、每个 Text 的两行限制及尾部截断 |
| Mac `macOS/Models/DesktopSubtitleOverlayModels.swift` | 同上 | `C80B73388750946F11164F6F26870BFCFD2B669910EB2AD80D8011D57B42358F` | 显示偏好、禁止两路语言同时关闭的校验 |
| Windows `windows/Echo.Windows/Core/DesktopSubtitleOverlay.cs` | 本轮起始 HEAD `6f06e579269a49fd555d1dd51beeaa8909d06367` | `57F5A3F296F56EC7EAE7FD5D0FDBAECBA3D9BAEEFD223C485EEA7238AA166D71` | 新增 Mac 同款显示规则的纯逻辑 helper |
| Windows `windows/Echo.Windows/DesktopSubtitleOverlayWindow.xaml.cs` | 同上 | `55D3F9773733767A201FEA2D14B0EA734C2BE95BB324192FC928174CBF4613D08` | 浮层窗口使用 helper 决定原文/译文可见性 |
| Windows `windows/Echo.Windows/DesktopSubtitleOverlayWindow.xaml` | 同上 | `577C173D6FF4AE1BA90FEA7415506EBA4D993D2E76A93728C1C154CD8F3DA96F` | 原文、译文及阴影 TextBlock 设置 `MaxLines=2` 和尾部省略 |
| Windows `windows/Echo.CoreChecks/Program.cs` | 同上 | `F0CE533B547AAD76DE45E6A452DFC0B0684B3CDF5937BA00616F748F5E910A01` | 展示组合和实际 XAML 标记的可重复检查 |

源文件均保留在仓库检出中；本轮没有启动 Echo、采集音频、保存字幕内容或修改 `macOS/`。

## Mac 行为与原差异

Mac `subtitleLines` 的原文条件为：用户启用了原文，**或**当前会话关闭翻译，**或**用户关闭译文。这样不会因为没有译文可显示而隐藏唯一可用的原文。Mac 还为原文和译文分别设置最多两行，超出部分从尾部截断；文字阴影复用相同内容和布局。

Windows 原先只检查 `ShowOriginal`，所以“隐藏原文 + 显示译文”的设置遇到翻译关闭时会让原文和译文都不可见。四个 Windows TextBlock 也没有两行上限，可能在固定高度的透明窗口中溢出或被窗口边界裁切。

## 修正与验证

- 增加 `DesktopSubtitleOverlayPresentation`，从当前状态和显示偏好计算原文及译文可见性；窗口调整态也保留翻译关闭时必须显示原文的规则。
- `OriginalText`、`OriginalShadow`、`TranslationText`、`TranslationShadow` 均设置 `MaxLines="2"` 和 `TextTrimming="CharacterEllipsis"`，阴影与实际文字使用同一截断边界。
- `Echo.CoreChecks` 检查翻译关闭时原文强制可见、翻译开启且仅选择译文时只显示译文、偏好不允许两路同时关闭；测试会读取随测试构建复制的生产 XAML，核对四个 TextBlock 都有行数和尾部截断设置。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：99 项通过。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：成功，0 警告、0 错误。

这些检查证明合成显示规则和 XAML 属性与预期一致，不证明不同字体和 DPI 下的最终排版，也不证明真正透明像素、点击穿透、失焦、多显示器、全屏或主窗口最小化时仍可见。上述行为仍需安全隔离条件下的 GUI 验收。
