# A28 AI correction and retranslation request parity baseline

核验日期：2026-10-09  
Mac source baseline: `origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`  
范围：DeepSeek subtitle correction/retranslation request and response contract. No Mac source was changed, no Echo app was launched, and no cloud request or user transcript was used.

## Source inventory

| Data item | Source | Recorded behavior | Limit |
|---|---|---|---|
| Request body | `macOS/Services/DeepSeekService.swift`, `correctionRequest` | Uses `deepseek-v4-flash`, disables thinking, includes JSON-only system instruction and JSON payload with source, translation, up to 3,000 context characters and 4,000 term characters | Windows retains a configurable model field; Mac model is fixed |
| Request intent | Same method | Translation-only requests require source to remain byte-for-byte unchanged; proofreading uses neighboring context only for judgment, without merging context into current subtitle | Prompts guide a remote model but cannot guarantee output quality |
| Scheduler | `macOS/ViewModels/SpeechViewModel.swift`, `requestCorrection` / `runNextCorrection` | One request at a time; manual work is inserted before queued automatic work; pending queue maximum is 20; duplicate queued subtitle IDs are ignored | Windows now serializes requests, but its semaphore does not yet guarantee Mac manual-priority order |
| Response shape | `DeepSeekService.decodeCorrection` and `CorrectionSuggestion` | Requires `source`, `translation`, `reason`, and `uncertain`; rejects incomplete/nonconforming results before showing suggestions | Synthetic JSON checks only |
| Windows implementation | `windows/Echo.Windows/Services/SubtitleCorrectionService.cs`, `MainPageViewModel.cs`, `Core/Transcript.cs` | Uses matching intent-specific instructions, `thinking.type=disabled`, serialization around all requests, and required response fields | Model selector, queue priority, live UI, and actual service response remain different or unverified |
| Synthetic checks | `windows/Echo.CoreChecks/Program.cs` | Asserts bearer header, thinking configuration, separate intent instructions, exact input source in retranslation payload, and rejection when `uncertain` is absent | No network access is used |

## Protocol sources

- [DeepSeek Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode/) documents the OpenAI-compatible `thinking: { "type": "enabled/disabled" }` switch.
- [DeepSeek Chat Completions API](https://api-docs.deepseek.com/api/create-chat-completion/) documents `thinking` as a request object and JSON object response mode.
- Checked 2026-10-09. These references confirm the request field syntax, not model availability, response quality, or app-specific behavior.

## Verification record

- CoreChecks validate locally constructed request JSON; no API key is transmitted.
- Missing `uncertain` fails deserialization instead of silently becoming `false`.
- The full existing check suite and Release x64 build are run for the implementation commit.
- No cloud response, runtime user interaction, or Mac app behavior was exercised.
