# A26 Soniox economics context parity baseline

核验日期：2026-10-09  
Mac source baseline: `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`  
范围：Soniox real-time request context sent by Mac `SpeechViewModel.openSonioxSocket`; Windows request construction and deterministic CoreChecks. No Mac source was changed, no Echo app was launched, and no cloud API or user audio was used.

## Source inventory

| Data item | Source | Recorded facts | Limit |
|---|---|---|---|
| Mac request context | `macOS/ViewModels/SpeechViewModel.swift`, `openSonioxSocket` | `general` has economics and finance domain plus topic; fixed background text distinguishes micro/macro economics and derivative/duty; 39 recognition terms; 26 English-to-Chinese translation pairs | Static source comparison only; does not establish live model quality |
| User correction terms | Same Mac method | Split by newline, trim whitespace/newlines, discard empty values, truncate each to 80 Swift `Character` values, append first 100 to base terms | Windows uses .NET text-element segmentation and has synthetic combining-mark and ZWJ emoji coverage; Unicode segmentation versions can still evolve independently |
| Protocol shape | [Soniox Context](https://soniox.com/docs/stt/concepts/context), [Real-time transcription](https://soniox.com/docs/stt/rt/real-time-transcription), [Speech-to-text translation](https://soniox.com/docs/translation/stt-translation) | Context supports `general`, `text`, `terms`, and `translation_terms`; translation term entries use `source` and `target` | Documentation confirms structure, not application-specific recognition quality |
| Windows implementation | `windows/Echo.Windows/Core/SonioxRequestBuilder.cs` | Adds the fixed Mac context to each request, then appends normalized user correction terms | Request is covered locally; no cloud request performed |
| Synthetic checks | `windows/Echo.CoreChecks/Program.cs` | Asserts context keys/counts, duty/derivative guidance, user-term trimming/truncation/cap, and JSON serialization | Synthetic values only |

## Parity details

- The base vocabulary contains 39 entries; translation vocabulary contains 26 source/target pairs.
- User terms are appended to the base recognition terms, not translation terms, matching Mac behavior.
- Empty lines are ignored. Each retained user term is trimmed, limited to 80 Unicode text elements (matching Swift `Character` behavior for tested combining marks and ZWJ emoji), and no more than 100 user terms are added.
- Translation-disabled sessions retain the context, as Mac does, while omitting only the `translation` request block.
- Windows keeps its configurable Soniox model field. Mac currently hardcodes `stt-rt-v5`; this difference remains a platform configuration difference and is not changed in this round.

## Validation record

- Local CoreChecks assert base context, vocabulary lengths, custom term normalization/cap, combining-mark and ZWJ emoji truncation, and valid JSON serialization.
- The official protocol references above were checked on 2026-10-09.
- No live API call, real audio capture, application launch, or macOS edit occurred.
