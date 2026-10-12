# Echo 文档中心 / Documentation

[项目首页](../README.md) · [English](../README.en.md) · [报告问题](https://github.com/resnowh/echo-mac-subtitle/issues)

这里汇总用户指南、实现说明、测试与发布记录。**功能以当前代码和 README 为准；规划文件不代表已经交付。**

## 使用 Echo

| 文档 | 适合谁 | 内容 |
| --- | --- | --- |
| [快速开始](../README.md#使用-echo) | 第一次接触 Echo | 系统要求、API Key、从源码运行 |
| [常见问题](FAQ.md) | 使用者 | 授权、费用、字幕、离线和数据处理 |
| [悬浮字幕](features/desktop-subtitle-overlay.md) | 需要边看边翻译的用户 | 显示、位置、点击穿透及未验证边界 |
| [发布与安装](release.md) | 想下载正式版本的用户及维护者 | DMG、签名、公证、验收及发布条件 |
| [变更记录](../CHANGELOG.md) | 关注进展的用户 | 已实现的功能与修复 |

## 技术实现

| 文档 | 内容 |
| --- | --- |
| [架构说明](architecture.md) | 音频采集、PCM 混音、Soniox、字幕、Archive |
| [数据模型](data-model.md) | 字幕、时间轴、Speaker、纠正及持久化 |
| [悬浮字幕测试](testing/desktop-subtitle-overlay.md) | 隔离测试与真实 Mac GUI 待验收事项 |
| [自动化测试](../tests/README.md) | 测试执行、覆盖与限制 |
| [macOS CI](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml) | 构建与隔离验证结果 |

## 路线图与平台

- [历史规划总览](roadmap/README.md)：保留设计决策、未来方案及历史上下文。
- [Mac 优先实施计划](roadmap/07-Mac优先的桌面实施方案.md)：桌面体验目标。
- [分发与上线计划](roadmap/06-正式分发与上线计划.md)：签名、安装和升级的详细门槛。

| 平台 | 当前状态 |
| --- | --- |
| macOS | `main` 开发预览；无经过签名、公证的公开安装包 |
| Windows | [`feature/windows-preview`](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview) 原生预览；尚未正式发行 |
| Android / iOS | 远期规划，未实现 |

**发行前提：** 项目维护者目前尚未加入付费 Apple Developer Program，因此 Developer ID 签名、公证尚未进行；发行工作流准备好并不代表已有正式安装包。

> [!NOTE]
> `roadmap/` 是历史设计记录，可能包含已改变的技术选型或时间估算。遇到冲突，以当前源码、[项目 README](../README.md)、[变更记录](../CHANGELOG.md) 和 [发布文档](release.md) 的状态为准。

## 安全反馈

欢迎提交 [GitHub Issues](https://github.com/resnowh/echo-mac-subtitle/issues)。不要上传 API Key、私人录音、真实字幕文本或包含敏感内容的完整日志。
