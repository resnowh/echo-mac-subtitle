# A27 Archive ordering and multi-segment parity baseline

核验日期：2026-10-09  
Mac source baseline: `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`  
范围：Archive list ordering and multi-segment export/import behavior. No Mac source was changed, no Echo app was launched, and all test entries are synthetic.

## Source inventory

| Data item | Source | Recorded behavior | Limit |
|---|---|---|---|
| Mac archive load ordering | `macOS/Storage/TranscriptArchiveStore.swift`, `load()` | Decodes JSON archives and sorts by `updatedAt` descending | Invalid Mac archive files are skipped by Mac loader; Windows reports unreadable-file count and preserves originals |
| Mac recording continuation | `macOS/ViewModels/SpeechViewModel.swift`, `prepareArchiveForRecording()` and `saveArchiveProgress()` | Restores all segments into the transcript; new recording entries are stored in the current segment; archive list is re-sorted after segment save | Static source comparison only |
| Windows load/save ordering | `windows/Echo.Windows/ViewModels/MainPageViewModel.cs` | Sorts parsed archives by `updatedAt`; saving an archive moves it to its new sorted position | No live UI automation this round |
| Windows archive contract | `windows/Echo.Windows/Core/Transcript.cs` | Snapshot clones every segment and subtitle; SRT sorts segments by start time and uses absolute `recordedAt` when available | Actual two-segment Mac production fixture is generated and checked by the A32 CI pipeline; current commit's GitHub run is pending |
| Synthetic fixture/check | `windows/Echo.CoreChecks/Program.cs` | Swift Codable-shaped JSON contains two segments 100 seconds apart, fixed UUIDs, timestamps, speaker/language metadata; Windows round-trips and compares exact SRT output | This is a hand-authored protocol fixture, not a Mac runtime encoder output |

## Difference fixed

Before this round, `MainPageViewModel.LoadArchivesAsync` appended files in filesystem enumeration order. The Mac store sorts newest `updatedAt` first. Windows now uses the same descending ordering at load, and an archive moves when a save changes its `updatedAt` value.

## Verification

- CoreChecks verify deterministic newest-first ordering.
- A two-segment Swift-shaped fixture verifies IDs, `recordedAt`, speaker/language metadata, 100-second wall-clock separation, segment ordering, and exact SRT text after a Windows JSON round trip.
- A32 adds a repeatable cross-runner pipeline for a two-segment Mac production fixture: Mac `TranscriptArchiveStore`/`SRTExporter` generate it, Windows CoreChecks imports and re-encodes it, then Mac production decoding/SRT output are compared. The current PR's Actions run must pass before this is treated as verified. Real user archive validation remains open.
- No cloud requests, audio capture, application launch, or real archive access occurred.
