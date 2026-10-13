# 文档索引

[项目首页](../README.md) · [English README](../README.en.md) · [Issues](https://github.com/resnowh/echo-mac-subtitle/issues)

本文用于导航；实现细节、数据契约和发布要求分别在对应文档维护，不在索引重复。

## 使用与功能

| 文档 | 内容 |
| --- | --- |
| [常见问题](FAQ.md) | 系统要求、权限、API 费用、存储位置 |
| [悬浮字幕](features/desktop-subtitle-overlay.md) | 使用方式、同步策略、已知限制 |
| [变更记录](../CHANGELOG.md) | 已提交功能与修复 |

## 开发与测试

| 文档 | 内容 |
| --- | --- |
| [架构](architecture.md) | macOS 音频、识别、字幕及模块边界 |
| [数据模型](data-model.md) | SubtitleEntry、Archive、时间线与向后兼容 |
| [自动化测试](../tests/README.md) | 隔离检查、运行方式及验证边界 |
| [悬浮字幕验收](testing/desktop-subtitle-overlay.md) | GUI、Space、多显示器的待测项 |

## 发布与规划

| 文档 | 内容 |
| --- | --- |
| [macOS 发布流程](release.md) | DMG、签名、公证和干净设备验收 |
| [历史规划](roadmap/README.md) | 早期技术决策和待实施方案，**不是当前实现清单** |

**当前范围：** macOS `main` 为主开发版本；Windows 原生预览位于独立 [`feature/windows-preview` 分支](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview)；移动端尚未实现。没有已发布的公证版 macOS 安装包。

**文档维护约定：** 用户可见功能看 [README](../README.md)，实现与不变量看架构/数据模型，测试结论看 [CI](https://github.com/resnowh/echo-mac-subtitle/actions) 与测试文档，发布进度看 [release.md](release.md)。历史规划与现状冲突时，以源码、CI 和当前技术文档为准。

反馈问题请使用 [Issues](https://github.com/resnowh/echo-mac-subtitle/issues)，不要提交 API Key、私人录音、原始字幕或含敏感数据的日志。
