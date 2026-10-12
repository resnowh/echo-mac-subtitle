# A24 Soniox endpoint controls and Mac segmentation baseline

Date checked: 2026-10-09. This is the source inventory entry for the segmentation implementation; values below are retained so later parity checks do not depend on live pages or current UI state.

## External source baseline

- Soniox WebSocket API: https://soniox.com/docs/api-reference/stt/websocket-api
- Soniox endpoint detection guide: https://soniox.com/docs/stt/rt/endpoint-detection
- Retrieved through official Soniox documentation search on 2026-10-09.
- Confirmed API fields: `enable_endpoint_detection`, `max_endpoint_delay_ms`, `endpoint_sensitivity`, and `endpoint_latency_adjustment_level`.
- Documented constraints: maximum endpoint delay 500–3000 ms; sensitivity −1.0 to 1.0; latency adjustment level 0–3. Sensitivity and latency adjustment are documented for Soniox v5.
- Documented defaults differ from Echo's product defaults: Soniox currently documents 2000 ms and sensitivity 0.0; Echo Mac chooses 3000 ms and −0.3.

## Mac product baseline

- Source files at baseline `origin/main` commit `ae0359dc90da0ccb5e526a275da1747954a49a4f`:
  - `macOS/Models/TranscriptModels.swift`
  - `macOS/Services/SonioxRequestBuilder.swift`
  - `macOS/ViewModels/SpeechViewModel.swift`
  - `macOS/EchoMacApp.swift`
- Mac defaults: max endpoint delay 3000 ms, sensitivity −0.3, latency adjustment 0; local silence fallback on at 4.5 seconds / 5 words; long-segment fallback on at 80 words / 90 seconds.
- Mac constraints: delay 500–3000 ms in 50 ms steps; sensitivity −1...1 in 0.1 steps; latency 0...3; silence 0.5...20 seconds / 1...100 words; long segment 10...1000 words / 5...600 seconds.
- Mac semantic endpoint is authoritative. Local silence requires both word count and quiet time; long fallback requires both word count and elapsed duration. Both local fallbacks wait for non-empty translation when translation is enabled.
- Mac reevaluates local fallback every 0.5 seconds and freezes a validated settings snapshot when a session begins.

## Windows implementation mapping

- `windows/Echo.Windows/Core/Preferences.cs`: persisted settings, defaults, range validation, per-session copy.
- `windows/Echo.Windows/Core/TranscriptSegmentationPolicy.cs`: endpoint/silence/long-segment policy.
- `windows/Echo.Windows/Core/SonioxRequestBuilder.cs`: request payload fields.
- `windows/Echo.Windows/ViewModels/MainPageViewModel.cs`: 500 ms reevaluation, translation readiness, per-session snapshot.
- `windows/Echo.Windows/Core/Transcript.cs`: explicit local finalization and cursor advancement.
- `windows/Echo.Windows/MainPage.xaml` and `.xaml.cs`: four settings sections and control ranges.

This is a requirements and implementation baseline, not evidence of a live Soniox call. Core checks use local deterministic inputs and do not send cloud requests.
