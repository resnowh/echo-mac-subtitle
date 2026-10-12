# A47：修复受限窗口下设置内容无法滚动

核验日期：2026-10-10。Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。本轮承接 A46；只修改 Windows 设置布局、CoreChecks 与验证/证据文档，未修改 `macOS/`。

## 数据底稿与来源清单

| 来源 | 版本/状态 | SHA-256 | 用途 |
|---|---|---|---|
| Mac `macOS/EchoMacApp.swift` | Mac 基线 | `CB2ED238D2EE30643BE2F4621E18599D7102ADBECFDF898D0EC52204E7802D87` | 设置 sheet 使用有限高度的滚动内容区 |
| Windows `windows/Echo.Windows/MainPage.xaml` | A47 修改后 | `1A83F97E5BAA97AF0BC386552E51E376C4AC3FDBF39A801B1840E534D0D4B34C` | 设置分类为 Auto 行、ScrollViewer 为有限的星号行 |
| Windows `windows/Echo.CoreChecks/Program.cs` | A47 修改后 | `E15655E88861AEF09C506D5865FEDB30F4C9E549FAF7E5A3600D53DF2DA5DDCD` | 静态断言滚动器位于 `Auto,*` Grid 的第二行 |
| `windows/ui-responsive-settings.ps1` | A47 新增 | `B638CE88BE31D114C03BDFAFFC9F8B4505EC44265FE678FE1601F4BC9686F10A` | 隔离包身份、临时数据目录、DPI 感知窗口尺寸与 UIA 可见性检查 |
| UIA 结果 `windows-a46-responsive-settings-ui-2026-10-10/test-results.json` | 本轮原始结果 | `00A84F7DD15A921CA241BEE30109854FE388C87854AC87EEEAA2D8656833DF1E` | 4 项通过结果 |
| 窄窗口截图 `windows-a46-responsive-settings-ui-2026-10-10/constrained-settings.png` | 本轮原始截图，28,980 bytes | `83373B43CE41030E473EF02466CCD1F4439C95A8D60D7CDEE831595148DBB359` | 设置页滚到底部后的真实窗口画面 |

## 发现与修复

A46 已把设置 Border 改为可收缩容器，但 GUI 验收发现：设置分类栏和 `ScrollViewer` 被放在纵向 `StackPanel` 里。该父容器以无限可用高度测量子项，导致滚动器无法取得有限视口；窄窗口下，长设置页底部的“恢复默认值”虽在 UIA 树中，却不可见。

Windows 现在用两行 Grid 承载分类栏和内容：第一行为 `Auto`，第二行为 `*`；`ScrollViewer` 置于第二行并标注 `SettingsScrollViewer`。内容获得有限高度后，滚动器可以实际滚动。A46 的 520×560 DIP 最大尺寸和 Stretch 行为保留。

## 验证

- 在 144 DPI（150% 缩放）的 Windows 环境中，通过独立 MSIX Package Identity 和临时 DataRoot 启动隔离调试副本；不触碰现有 Echo 实例或其数据。
- UIA 将窗口压到 430×360 DIP，确认设置标题、分类栏和“完成”按钮可见；切换到“分段”并滚动至底部后，确认“恢复默认值”及“完成”同时可见。4 项检查全部 PASS；截图画面为 627×531 物理像素。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：121 项通过。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 -p:PublishReadyToRun=false --no-restore`：0 警告、0 错误。
- UI 测试副本已通过窗口关闭键正常退出，测试包已注销；原有 `B7582E49-F75A-4EFA-950C-C6754B9E496E` 包仍注册，且未运行。

本轮仅验证 144 DPI 下的受限窗口与设置滚动；125%/200% 缩放、键盘、Narrator、真实音频设备仍待验。没有调用云端服务、填写 API Key、录音或保存音频。A46 原始截图先暴露了滚动缺陷；本底稿记录修复后的截图与通过结果。
