# 悬浮字幕场景检查（2026-10-12）

## 设计参考

- [Apple 实时字幕](https://support.apple.com/en-bh/guide/mac-help/mchldd11f4fd/mac)：独立窗口、自由移动与缩放。Echo 在调整模式显示边界和操作提示，窗口内、设置页和主窗口菜单均可完成调整。
- [Windows 实时字幕](https://support.microsoft.com/en-us/accessibility/windows/use-live-captions-to-better-understand-audio)：位置与样式控制。Echo 保留原有字号、宽度、透明度和点击穿透设置。
- Echo 的实时跟读策略：原文与译文各显示连续的最新两行；完整文本仍保留在主窗口。观看状态保持透明，调整状态显示深色背景与边框。

## 本次修复与证据

| 场景 | 行为与验证 |
| --- | --- |
| 实时增长的中英文长句 | 按实际字体和宽度计算最新两行，避免第一行与句尾拼接；独立文本用例和离屏渲染验证 |
| 最大字号、双语、窄窗口 | 窗口高度随字体行高变化，取消 180 点高度上限；两种尺寸离屏渲染人工检查 |
| 重复定稿、重复更新 | 不延长原定保留期，不生成无意义 revision；回归用例先失败后通过 |
| 迟到译文、手动修订 | 有意义的新内容重新获得完整阅读时间；现有回归检查覆盖 |
| 切换到下一句、禁用翻译 | 不串用上一句译文；现有回归检查覆盖 |
| 清空会话、节流尾部更新 | 取消待发布内容，避免旧字幕复现；现有回归检查覆盖 |
| 清空当前句文本 | 移除当前悬浮内容，不保留旧字；回归检查覆盖 |
| 跨屏拖动、负坐标屏幕 | 按最大相交面积选屏，保存时避免刷新窗口导致拖动回跳；合成屏幕几何用例覆盖选屏 |
| 显示器断开、小显示区域 | 不索引空屏幕数组；窗口几何限制在可用区域；合成小屏用例覆盖 |
| 调整完成、位置锁定 | 窗口内与设置页提供完成按钮，菜单也可完成；退出调整并锁定，重新约束窗口范围 |
| 已关闭字幕时重置位置 | 设置直接写入独立存储，不依赖窗口存在；独立 UserDefaults 用例覆盖 |
| 重启时保存为解锁 | 开启窗口时恢复可调整状态；代码路径检查，待真实交互验收 |
| 全屏视频、Spaces、多屏热插拔、休眠唤醒 | 保留全屏辅助和跨空间窗口属性；实际系统交互尚未验收 |
| 麦克风、系统音频、真实 Soniox 会话 | 不改音频与识别配置；本次不访问设备和远端服务 |

## 验证方式

```sh
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer python3 tests/run_stream_checks.py
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer python3 tests/run_overlay_checks.py
# 可选：离屏截图。仅创建不显示的独立测试窗口，不启动 Echo。
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer python3 tests/run_overlay_checks.py /tmp/echo-overlay-previews
```

Debug 和 Release 使用独立 DerivedData 构建，不覆盖安装应用。新增 UI 检查已加入 CI 配置；远端 CI 运行结果以关联 PR 的检查记录为准。

实际多屏拖动、全屏、点击穿透、键鼠操作与录音会话测试：未自动执行：为避免中断用户当前正在运行的 Echo。离屏截图与构建不能替代这些系统交互测试。
