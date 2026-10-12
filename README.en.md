# Echo

**Hear it. Read it. Understand it — in real time.**

A native macOS app for **live transcription and bilingual subtitles**. Capture your Mac's playback audio, microphone, or both. Read the original speech and its translation as it happens, with an optional transparent desktop subtitle overlay.

[![macOS CI](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml)
![macOS](https://img.shields.io/badge/macOS-15%2B-555555?logo=apple)
![SwiftUI](https://img.shields.io/badge/Swift-SwiftUI-F05138?logo=swift&logoColor=white)
![Development preview](https://img.shields.io/badge/status-development%20preview-blue)
![BYOK](https://img.shields.io/badge/API-BYOK-6A737D)

[简体中文](README.md) · **English** · [Docs](docs/README.md) · [Issues](https://github.com/resnowh/echo-mac-subtitle/issues)

> [!IMPORTANT]
> **Development preview — no official public installer yet.** The developer has not enrolled in the paid Apple Developer Program, so Developer ID signing and Apple notarization have not been completed. CI build success is not a verified distribution. See [release status](docs/release.md).

## What Echo does

- **Live bilingual subtitles** — streaming Soniox transcription and translation, timestamps, and optional anonymous speaker labels.
- **Transparent desktop overlay** — video-style original/translated subtitles, drag-to-position, click-through and appearance controls. Real Mac fullscreen and multi-monitor validation is still pending.
- **Flexible audio input** — microphone, Mac system playback, or a local mix of both; switch modes while recording.
- **Configurable recognition** — source/target languages and segmentation settings; recognition and translation setting changes take effect in the next Soniox session.
- **Local archives and exports** — resume multi-session archives, save individual SRT files and export a complete archive. Raw audio is **not stored by default**.
- **Human-controlled editing and AI** — manually correct/undo subtitles and preserve history; optionally request DeepSeek correction suggestions, retranslation, and summaries.

Designed for lectures, meetings, videos without captions, and multilingual listening.

<!-- Add a genuine, privacy-sanitized app screenshot or demo GIF here when available.
     Do not imply an unverified mockup is a working app capture. -->

## Get started

### Requirements

| | |
| --- | --- |
| Platform | macOS 15.0+, Apple Silicon or Intel |
| Build | Xcode with a compatible macOS SDK (source build currently required) |
| Speech services | Internet access and your own [Soniox API key](https://console.soniox.com/) |
| Optional AI | [DeepSeek API key](https://platform.deepseek.com/) for correction and summaries |

Echo follows **BYOK (bring your own key)**. No Echo account, subscription, or bundled free API credits are provided. Soniox and DeepSeek may charge for usage.

### Build from source

There is no signed/notarized public DMG at present. For developers:

```bash
git clone https://github.com/resnowh/echo-mac-subtitle.git
cd echo-mac-subtitle
open macOS/EchoMac.xcodeproj
```

Select the `Echo` scheme in Xcode and run. In **Settings → AI 服务**, enter your Soniox API key (DeepSeek is optional). Choose a microphone, system audio or combined input, then grant permissions as macOS requests them.

To **compile only**, without opening Echo:

```bash
xcodebuild -project macOS/EchoMac.xcodeproj \
  -scheme Echo -configuration Debug -sdk macosx \
  -derivedDataPath "${TMPDIR:-/tmp}/echo-doc-build" \
  CODE_SIGNING_ALLOWED=NO build
```

> [!WARNING]
> `build_mac.command` automatically opens the built app. Do not run it, restart Echo, or replace the running app during an active recording.

See [FAQ](docs/FAQ.md) and [release guide](docs/release.md).

## Privacy and data

Echo does **not save raw audio by default**, but sends live audio to Soniox for transcription/translation. When AI correction, retranslation or summarization is enabled or requested, relevant **text** is sent to DeepSeek. Follow applicable consent and recording laws.

| Data | Handling |
| --- | --- |
| Live audio | Sent to Soniox; not saved to disk by default |
| AI text | Sent to DeepSeek only for enabled or expressly requested functions |
| Archive JSON | `~/Library/Application Support/Echo/Archives/` |
| Session SRT | User's `Downloads` folder |
| API credentials | **Currently stored in UserDefaults, not yet Keychain.** Security hardening is required before public distribution |

## Status and roadmap

| Area | Status |
| --- | --- |
| **macOS** | Primary development on `main`; CI and isolated tests |
| **Subtitle overlay** | Implemented with logic tests; real GUI/fullscreen/multi-display validation pending |
| **Public macOS installer** | DMG, signing and notarization prepared as workflows; **not released** |
| **Windows** | [Native preview branch](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview); no public installer |
| **Mobile** | Planned concept only |

Next priorities: **Keychain migration → hardware/permission acceptance → signing/notarization → public beta**. The Apple Developer Program enrollment is still a prerequisite for the signed public build.

## Documentation and feedback

[Documentation](docs/README.md) · [FAQ](docs/FAQ.md) · [Architecture](docs/architecture.md) · [Data model](docs/data-model.md) · [Tests](tests/README.md) · [Changelog](CHANGELOG.md) · [Historical roadmap](docs/roadmap/README.md) · [Releases](https://github.com/resnowh/echo-mac-subtitle/releases)

[Issues](https://github.com/resnowh/echo-mac-subtitle/issues) are welcome. Include reproducible steps, macOS version, and audio input mode. **Do not post API keys, private recordings, transcripts, or sensitive logs.**

**Licensing:** The repository is public but currently has no `LICENSE` file. Public visibility does not grant rights to reproduce, modify or redistribute. Licensing and monetization are undecided.
