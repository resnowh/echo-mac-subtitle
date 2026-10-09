# Echo 文档中心 / Documentation

[返回项目首页](../README.md) · [English README](../README.en.md)

## 用户文档

- [常见问题 FAQ](FAQ.md)：系统要求、API Key、权限、收费、安装与数据安全。
- [变更记录](../CHANGELOG.md)：已实现的代码变更，非未来承诺。
- [发布与安装状态](release.md)：为什么目前没有官方 DMG；未来签名、公证与验收要求。

## 技术与开发

- [当前架构](architecture.md)：macOS 采集、PCM、Soniox、字幕与持久化。
- [数据模型](data-model.md)：字幕、Speaker、纠正历史、Archive 与 SRT 时间线。
- [自动化测试](../tests/README.md)：隔离测试范围和不能替代的真实设备验收。
- [CI 构建](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml)：macOS Debug/Release 与无设备依赖测试。

## 规划与平台状态

- [路线图入口](roadmap/README.md)：**历史设计方案**及当前状态说明；它不是功能完成清单。
- [Mac 优先的桌面实施方案](roadmap/07-Mac优先的桌面实施方案.md)：较新的桌面设计方向。
- [macOS 分发规划](roadmap/06-正式分发与上线计划.md)：发布验收细目。

### 平台现状

| 平台 | 代码位置 | 状态 |
| --- | --- | --- |
| macOS | `main` | 开发预览；未发布公证安装包 |
| Windows | `feature/windows-preview` | 独立原生预览；未正式发布 |
| Android / iOS | 规划文档 | 尚未实现 |

> 旧规划文档保留原始决策背景，可能引用历史提交、设计提议或未来目标。判断**当前实现**请优先查看 `main` 源码、Windows 分支、[README](../README.md) 和 [CHANGELOG](../CHANGELOG.md)。不要将旧文件中“尚未实现 Windows”等历史陈述当成现状。

## 安全反馈

欢迎提交 [GitHub Issues](https://github.com/resnowh/echo-mac-subtitle/issues)；请避免上传 API Key、录音原文、个人信息、完整敏感字幕或含密钥的日志。
