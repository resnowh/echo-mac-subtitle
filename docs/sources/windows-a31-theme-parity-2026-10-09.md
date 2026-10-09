# A31 默认主题与循环顺序对齐底稿

核验日期：2026-10-09
Mac 基线：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`

## 来源清单

| 行为 | Mac 源码 | Windows 修正 | 验证范围 |
|---|---|---|---|
| 新装/缺少主题配置时默认深色 | `macOS/EchoMacApp.swift` 中主窗口和设置均以 `AppThemeMode.dark.rawValue` 初始化；无效值回退 `.dark` | `Preferences.Theme` 默认值改为 `Dark`；System.Text.Json 读取缺失字段时保留属性默认值 | CoreChecks 检查新对象和 `{}` legacy 配置；未启动 GUI |
| 主题菜单与循环顺序及生效时机 | `macOS/Models/TranscriptModels.swift` 的 `AppThemeMode.allCases` 顺序为 light、dark、system；`cycleThemeMode` 取下一枚举值；设置绑定变更立即生效 | WinUI 选项和主题按钮映射为 Light、Dark、Default（Windows 的系统主题）并同序循环；选择变更立即保存并应用 | `ThemePreference` 自动检查索引与完整循环；未做像素/系统主题实机对照 |
| 已显式保存的用户偏好 | Mac 将选择存为 `themeMode` | Windows 保留现存 `Theme` 值；只有值缺失时才应用深色默认 | 代码路径静态核验；没有读取或改写用户机器设置文件 |

## 验证记录

- CoreChecks 检查新配置、缺失字段反序列化、三态顺序及 Dark→System→Light→Dark 循环。
- Release x64 构建通过；GUI 实测及高对比度/每显示器主题行为仍未验。
- 未启动 Echo，未改 Mac 源码或用户设置。
