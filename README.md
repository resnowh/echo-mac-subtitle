# Echo

**macOS 原生实时转写与双语字幕工具**  
*Real-time transcription & bilingual subtitles for macOS.*

[![macOS CI](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml)
![macOS 15+](https://img.shields.io/badge/macOS-15%2B-555555?logo=apple)
![SwiftUI](https://img.shields.io/badge/UI-SwiftUI-F05138?logo=swift&logoColor=white)
![Stage](https://img.shields.io/badge/status-development%20preview-blue)
![API](https://img.shields.io/badge/API-BYOK-6A737D)

**简体中文** · [English](README.en.md) · [功能与文档](docs/README.md) · [问题反馈](https://github.com/resnowh/echo-mac-subtitle/issues)

---

Echo 让你在 Mac 上**实时阅读正在播放的声音或麦克风讲话内容**：转写原文、翻译成另一种语言，并保存带时间戳的字幕。适合课程、跨语言会议、视频与日常听力辅助。

> **当前状态：开发预览版，尚无公开的、经过 Apple 签名和公证的安装包。** macOS 源码位于 `main`；Windows 原生预览代码位于独立的 [`feature/windows-preview` 分支](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview)，尚未正式发布。请勿将 CI 构建成功等同于完成真实设备验收。

## 核心功能

| 能力 | 说明 |
| --- | --- |
| **实时双语字幕** | Soniox 流式识别与翻译；支持原文、译文、时间戳和匿名 Speaker 编号。 |
| **多种音频输入** | 麦克风、Mac 电脑音频、两者混合；录音期间可切换输入模式。 |
| **主界面快速切换语言** | 直接从双栏标题的下拉菜单切换源语言、翻译目标，也能关闭翻译。新 Soniox 会话使用新配置。 |
| **可调字幕分段** | Soniox 端点灵敏度与延迟、本地静音及长段落兜底参数可配置。 |
| **保存与导出** | 本地多段 Archive、接续录音、自动单段 SRT、全量字幕导出。默认**不保存原始音频**。 |
| **纠正与 AI 辅助** | 手动编辑、撤销、保留原始识别稿；可选择用 DeepSeek 生成需人工确认的校对建议及总结。 |
| **原生桌面体验** | SwiftUI、浅色／深色模式、窗口置顶、睡眠唤醒恢复逻辑。 |

**尚未交付**：独立桌面悬浮字幕窗口、正式签名和公证、自动更新、商店版本、移动客户端。部分功能已在计划或独立开发分支中，不代表 `main` 已实现。

## 快速开始

### 运行条件

- macOS **15.0 或更高版本**，Apple Silicon 或 Intel Mac。
- 支持该系统版本的 Xcode（从源码构建）。
- 网络连接和你自己的 [Soniox API Key](https://console.soniox.com/)。
- 如使用 AI 校对或总结，另需 [DeepSeek API Key](https://platform.deepseek.com/)。

> Echo 使用 **BYOK（Bring Your Own Key）** 模式。第三方服务可能按用量收费；Echo 不包含免费的 Soniox/DeepSeek 调用额度。

### 从源码构建

目前没有面向普通用户的 DMG 下载。开发者可克隆仓库并打开 `macOS/EchoMac.xcodeproj`，在 Xcode 中选择 `Echo` Scheme 构建或运行：

```bash
git clone https://github.com/resnowh/echo-mac-subtitle.git
cd echo-mac-subtitle
open macOS/EchoMac.xcodeproj
```

只想**编译验证而不启动应用**，可使用隔离构建路径：

```bash
xcodebuild -project macOS/EchoMac.xcodeproj \
  -scheme Echo -configuration Debug -sdk macosx \
  -derivedDataPath "${TMPDIR:-/tmp}/echo-doc-build" \
  CODE_SIGNING_ALLOWED=NO build
```

> `build_mac.command` **会在 Release 构建后自动打开 Echo.app**。如果你正用 Echo 录音，请不要运行该脚本，也不要覆盖、退出或替换正在使用的应用。

### 首次使用

1. 在 **设置 → 服务** 中填写 Soniox API Key；DeepSeek Key 可选。（当前主线设置为纵向分区；[标签页改版](https://github.com/resnowh/echo-mac-subtitle/pull/1) 仍在独立 PR 中。）
2. 选择 **麦克风 / 电脑音频 / 两者混合**。根据系统提示授权麦克风或“屏幕与系统音频录制”。
3. 在双栏字幕标题直接选择识别语言和翻译目标，然后点击 **开始录音**。
4. 停止后在本地查看存档；按需编辑字幕、生成 AI 总结或导出 SRT。

## 你的数据会去哪里？

| 数据 | 处理方式 |
| --- | --- |
| 实时音频 | 传输到 Soniox 进行识别／翻译；Echo 默认不把原始音频保存到磁盘。 |
| AI 校对、重新翻译、总结 | 仅在开启相应功能或主动调用时，将相关**文字**发送到 DeepSeek；不发送音频。 |
| Archive JSON | 本机 `~/Library/Application Support/Echo/Archives/`。 |
| 自动单段 SRT | 默认写入用户的 `Downloads` 文件夹。 |
| Soniox / DeepSeek Key | **当前版本保存在本机 UserDefaults，尚未迁移到 Keychain**；正式分发前需要安全加固。 |

请在使用会议、课堂录音功能时遵守适用的隐私及录音告知要求。

## 项目状态与路线

- **macOS（`main`）**：主要开发版本；[自动化构建与隔离检查](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml)。
- **Windows（`feature/windows-preview`）**：原生 WinUI 3/C# 预览实现，仍待完整真机／云端验收；**没有正式下载版**。
- **macOS 分发**：正在准备 Universal 构建、Developer ID 签名、公证和 DMG。**当前未具备正式签名、公证凭据，也没有发布公共 Release**。进度与门槛见 [发布文档](docs/release.md)。
- **移动端**：远期构想，尚未开始实现。

[变更记录](CHANGELOG.md) · [产品路线图](docs/roadmap/README.md) · [GitHub Releases](https://github.com/resnowh/echo-mac-subtitle/releases)

## 文档与开发

| 文档 | 用途 |
| --- | --- |
| [文档导航](docs/README.md) | 使用、技术、测试、发布文档总入口 |
| [常见问题](docs/FAQ.md) | 权限、API Key、费用、数据及当前限制 |
| [架构说明](docs/architecture.md) | 音频、Soniox、字幕、存档的数据流 |
| [数据模型](docs/data-model.md) | Archive、时间戳、Speaker、兼容性 |
| [测试说明](tests/README.md) | 隔离测试的范围与局限 |
| [发布准备](docs/release.md) | Mac 签名、公证和真实设备验收要求 |

欢迎通过 [Issues](https://github.com/resnowh/echo-mac-subtitle/issues) 报告问题或提出功能建议。反馈时请勿附上 API Key、真实录音、私人字幕或含个人信息的日志。

**许可证说明：** 仓库目前公开可读，但尚未附带 `LICENSE` 文件；公开源码不代表已经授权他人复制、修改或再分发。许可证与商业模式仍待确定。
