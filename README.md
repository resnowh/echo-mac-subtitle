# Echo

**聆听世界，实时看见每一句话。**

原生 macOS 实时语音转写与双语字幕工具。捕获电脑播放的声音或麦克风输入，边听边显示原文与译文；需要时，还可以将字幕悬浮在其他应用之上。

[![macOS CI](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml)
![macOS](https://img.shields.io/badge/macOS-15%2B-555555?logo=apple)
![SwiftUI](https://img.shields.io/badge/Swift-SwiftUI-F05138?logo=swift&logoColor=white)
![Development preview](https://img.shields.io/badge/status-development%20preview-blue)
![BYOK](https://img.shields.io/badge/API-BYOK-6A737D)

**简体中文** · [English](README.en.md) · [文档](docs/README.md) · [问题反馈](https://github.com/resnowh/echo-mac-subtitle/issues)

> [!IMPORTANT]
> **目前是开发预览项目，尚无可供普通用户直接下载的官方安装包。** Apple Developer Program 尚未开通，Developer ID 签名与 Apple 公证仍未进行。请不要将 CI 构建产物当作正式发行版。参见 [发布状态](docs/release.md)。

## Echo 能做什么？

- **实时双语字幕**：Soniox 流式识别与翻译，显示原文、译文与时间戳，可选匿名 Speaker 编号。
- **透明桌面字幕**：视频字幕式双语悬浮窗口，支持拖动、点击穿透与样式设置；真实多显示器和全屏覆盖仍待 macOS 实机验收。
- **灵活的音频来源**：电脑音频、麦克风或两者混合，录音中可切换输入模式。
- **按需切换语言与分段策略**：支持源语言、翻译语言与分段参数设置；识别/翻译设置在下一 Soniox 会话生效。
- **记录与复用**：本地存档、接续多段录音、自动导出单段 SRT、整档字幕导出；默认不保存原始音频。
- **保留人工控制**：手动纠正字幕、撤销修改、保留历史；可选 DeepSeek 校对建议、重新翻译及 AI 总结。

适用于线上课程、跨语言会议、没有现成字幕的视频以及日常外语听力。

<!-- Screenshot placeholder: add a real, sanitized app screenshot or short demo GIF when available.
     Do not use mockups or unrelated images as proof of working software. -->

## 使用 Echo

### 系统与服务要求

| 项目 | 要求 |
| --- | --- |
| 系统 | macOS 15.0+，Apple Silicon 或 Intel |
| 构建工具 | 支持对应 macOS SDK 的 Xcode（目前需从源码构建） |
| 实时识别 / 翻译 | 网络连接，以及用户自行申请的 [Soniox API Key](https://console.soniox.com/) |
| AI 校对 / 总结 | 可选 [DeepSeek API Key](https://platform.deepseek.com/) |

Echo 采用 **BYOK（Bring Your Own Key）** 模式：应用本身没有账号、内置免费识别额度或订阅系统，Soniox / DeepSeek 的 API 调用费用可能由对应服务商直接收取。

### 从源码构建

目前没有经过 Developer ID 签名和公证的公开 DMG。若你熟悉 Xcode，可以：

```bash
git clone https://github.com/resnowh/echo-mac-subtitle.git
cd echo-mac-subtitle
open macOS/EchoMac.xcodeproj
```

在 Xcode 选择 `Echo` Scheme 并运行。第一次使用时，在 **设置 → AI 服务** 中填写 Soniox Key（DeepSeek 可选），选择电脑音频、麦克风或混合输入，并按 macOS 提示授予相应权限。

若只想验证代码能否编译、**不启动 Echo**：

```bash
xcodebuild -project macOS/EchoMac.xcodeproj \
  -scheme Echo -configuration Debug -sdk macosx \
  -derivedDataPath "${TMPDIR:-/tmp}/echo-doc-build" \
  CODE_SIGNING_ALLOWED=NO build
```

> [!WARNING]
> `build_mac.command` 会在构建完成后自动打开应用。如果 Echo 正在录音，请勿运行该脚本，也不要退出、重新打开或覆盖当前运行的程序。

详见 [常见问题](docs/FAQ.md) 和 [发布流程](docs/release.md)。

## 隐私和数据

Echo **默认不会保存原始音频**，但使用实时识别时会将音频发送到 Soniox 云端；按需使用 AI 校对、重新翻译和总结时，相关文本会发往 DeepSeek。请在会议或课堂使用时遵守适用的告知与录音规定。

| 内容 | 当前处理方式 |
| --- | --- |
| 原始音频 | 默认不落盘；实时发送给 Soniox |
| AI 文本 | 仅在开启相关功能或主动请求时发送至 DeepSeek |
| Archive JSON | `~/Library/Application Support/Echo/Archives/` |
| SRT | 每段录音的字幕默认写入 `Downloads` |
| 服务 API Key | **目前仍保存在 UserDefaults，尚未迁移至 Keychain**；正式发布前必须加固 |

## 开发状态

| 平台或能力 | 状态 |
| --- | --- |
| **macOS** | 主开发平台，源码位于 `main`，有自动编译和隔离测试 |
| **桌面悬浮字幕** | 已有实现及逻辑测试；真实 GUI、全屏、多显示器仍待验证 |
| **官方 macOS 安装包** | 正在准备 DMG/签名/公证流程；未公开发布 |
| **Windows** | [独立预览分支](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview)，尚未正式分发 |
| **Android / iOS** | 远期规划，暂无实现 |

下一阶段的优先级是：**Keychain 凭据迁移 → 真实设备稳定性与权限验收 → 发行签名和公证 → 公开 Beta**。由于目前尚未加入 Apple Developer Program，签名、公证是待满足的外部前提，不代表已有可发布安装包。

## 文档与参与

[文档中心](docs/README.md) · [FAQ](docs/FAQ.md) · [架构](docs/architecture.md) · [数据模型](docs/data-model.md) · [测试](tests/README.md) · [变更记录](CHANGELOG.md) · [历史路线图](docs/roadmap/README.md) · [Releases](https://github.com/resnowh/echo-mac-subtitle/releases)

欢迎通过 [GitHub Issues](https://github.com/resnowh/echo-mac-subtitle/issues) 反馈问题。请提供复现步骤、macOS 版本及输入模式，**不要公开 API Key、私人录音、原始字幕或敏感日志**。

**许可证：** 本仓库目前公开可读，但尚无 `LICENSE` 文件。公开不等于授予复制、修改或再分发权限；许可证与未来收费方式仍在评估。
