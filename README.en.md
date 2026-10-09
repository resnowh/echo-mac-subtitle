# Echo

**Native real-time transcription and bilingual subtitles for macOS.**

[![macOS CI](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml)
![macOS 15+](https://img.shields.io/badge/macOS-15%2B-555555?logo=apple)
![SwiftUI](https://img.shields.io/badge/UI-SwiftUI-F05138?logo=swift&logoColor=white)
![Stage](https://img.shields.io/badge/status-development%20preview-blue)
![API](https://img.shields.io/badge/API-BYOK-6A737D)

[简体中文](README.md) · **English** · [Documentation](docs/README.md) · [Report an issue](https://github.com/resnowh/echo-mac-subtitle/issues)

---

Echo captures **microphone input, macOS playback audio, or both**, sends the audio to Soniox for live transcription/translation, and displays bilingual text with timestamps. It is designed for lectures, meetings, videos, and language-learning workflows.

> **Development preview:** There is **no publicly distributed, Developer ID–signed and notarized macOS installer yet**. The macOS app is on `main`. The separate [`feature/windows-preview` branch](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview) contains a native Windows preview implementation, not a finished public release.

## Features

| Feature | What it does |
| --- | --- |
| **Live bilingual subtitles** | Streaming Soniox recognition and translation, timestamps, optional anonymous speaker labels. |
| **Three input modes** | Microphone, macOS system audio, or locally mixed microphone + system audio; input modes can be switched while recording. |
| **Quick language selection** | Select source and target language from the subtitle column headers; disable translation if needed. Changes apply to new Soniox sessions. |
| **Configurable segmentation** | Adjust Soniox endpoint sensitivity/delay and local silence/long-segment fallbacks. |
| **Local archives and SRT** | Save multiple recording sessions in one archive, resume an archive, export SRT. Raw audio is **not saved by default**. |
| **Editing and optional AI** | Edit/undo subtitles and preserve correction history; request DeepSeek suggestions and summaries when desired. |
| **Transparent subtitle overlay** | Optional bilingual video-style overlay with drag-to-position, click-through, and appearance settings; real macOS GUI/fullscreen acceptance is pending. |\n| **Native macOS interface** | SwiftUI, dark/light themes, always-on-top option, sleep/wake recovery logic. |

**Not shipped:** notarized installer, automatic updates, mobile apps, or a public Windows installer. The desktop subtitle overlay is implemented in source and covered by isolated checks, but **not yet verified on real macOS GUI/fullscreen setups**.

## Getting started

### Requirements

- macOS **15.0+** on Apple Silicon or Intel.
- A compatible Xcode version for building from source.
- An internet connection and **your own [Soniox API key](https://console.soniox.com/)**.
- An optional [DeepSeek API key](https://platform.deepseek.com/) for AI corrections and summaries.

Echo follows **BYOK (bring your own key)**. Service providers may bill you directly for API usage; no included recognition credits are provided.

### Build from source

There is currently no official DMG download. Clone the repository and open `macOS/EchoMac.xcodeproj` in Xcode, then build/run the `Echo` scheme:

```bash
git clone https://github.com/resnowh/echo-mac-subtitle.git
cd echo-mac-subtitle
open macOS/EchoMac.xcodeproj
```

To **compile without launching Echo**, use an isolated derived-data directory:

```bash
xcodebuild -project macOS/EchoMac.xcodeproj \
  -scheme Echo -configuration Debug -sdk macosx \
  -derivedDataPath "${TMPDIR:-/tmp}/echo-doc-build" \
  CODE_SIGNING_ALLOWED=NO build
```

> `build_mac.command` **opens Echo.app after building**. Do not run it or replace the currently running app while recording.

### First use

1. Open **Settings → AI 服务**, enter your Soniox API key; DeepSeek is optional. (Settings are organized into tabs.)
2. Choose the microphone, computer audio, or combined input mode. Grant microphone and/or screen & system audio capture permissions when prompted.
3. Pick recognition and translation languages in the subtitle column headers, then start recording.
4. Stop recording to inspect local archives, edit subtitles, optionally summarize, and export SRT.

## Privacy and local storage

| Data | Handling |
| --- | --- |
| Live audio | Sent to Soniox for recognition/translation; Echo does not save raw audio by default. |
| AI correction/translation/summary | The relevant **text**, not audio, is sent to DeepSeek when the feature is enabled or explicitly requested. |
| Archive JSON | `~/Library/Application Support/Echo/Archives/` |
| Auto-exported session SRT | Your `Downloads` folder |
| Soniox / DeepSeek keys | Currently stored in local **UserDefaults**, **not yet Keychain**. Hardening is pending before public distribution. |

Follow applicable consent, disclosure, and recording laws when transcribing classes or meetings.

## Project status

- **macOS (`main`)**: main development target, with [automated builds and isolated tests](https://github.com/resnowh/echo-mac-subtitle/actions/workflows/ci.yml).
- **Windows (`feature/windows-preview`)**: native WinUI 3/C# preview; full hardware/cloud acceptance testing and public packaging are still outstanding.
- **macOS distribution**: Universal build/DMG, Developer ID signing and notarization are being prepared; **no signed/notarized public Release exists yet**. See [release planning](docs/release.md).
- **Mobile**: long-term ideas, no shipping implementation.

## Documentation

[Docs index](docs/README.md) · [FAQ](docs/FAQ.md) · [Architecture](docs/architecture.md) · [Data model](docs/data-model.md) · [Tests](tests/README.md) · [Changelog](CHANGELOG.md) · [Roadmap](docs/roadmap/README.md) · [Releases](https://github.com/resnowh/echo-mac-subtitle/releases)

Feedback and bug reports are welcome via [GitHub Issues](https://github.com/resnowh/echo-mac-subtitle/issues). Never include API keys, private recordings, transcripts, or sensitive logs.

**License:** The repository is publicly readable but currently has **no `LICENSE` file**. Visibility does not grant a license to copy, modify, or redistribute; licensing and commercialization are undecided.
