# Automated checks (macOS)

These are **isolated logic and transport tests**. They do not launch Echo, use real audio devices, contact Soniox/DeepSeek, or require API credentials.

## Run

On macOS with Xcode selected:

```bash
python3 tests/run_stream_checks.py
```

If Command Line Tools and Xcode are different installations, set `DEVELOPER_DIR` for this command. The script compiles production Swift sources in a temporary directory and runs a loopback WebSocket server on an ephemeral localhost port. See also the [macOS CI workflow](../.github/workflows/ci.yml).

## Coverage

- **Audio and transport**: 30 minutes of synthetic dual-input timeline/PCM continuity, mixer resets, bounded prebuffer, ordered audio/end-of-stream delivery, 20 loopback WebSocket reconnects, cancellation and overflow failure.
- **Transcript and archive**: field locks, manual correction/undo, stale-revision rejection, old/new Archive JSON, cross-segment UUID matching, corrected SRT, recognition segmentation and endpoint request settings.
- **AI interfaces**: structured DeepSeek response parsing and rejection of invalid responses; **no live model calls** or model-quality evaluation.
- **Desktop overlay**: provisional/final updates, original/translation entry pairing, late translation, expiration/clear behavior, display settings and persistence. See [overlay acceptance](../docs/testing/desktop-subtitle-overlay.md).

## Not covered

Passing these tests or compiling Debug/Release **does not verify** physical microphone or ScreenCaptureKit capture, real network latency, Soniox accuracy, macOS window/Space/fullscreen behavior, sleep/wake recovery on hardware, or long-session UX. Those require separate real-device acceptance tests.

Do not run `build_mac.command` or restart/replace Echo merely to test while an active recording is in progress; that script automatically opens the built app.

## Isolated floating subtitle checks

`python3 tests/run_overlay_checks.py` compiles the production SwiftUI overlay
with a feed-only model and a disposable UserDefaults suite. It checks resetting
placement while disabled, exiting adjustment mode, and contiguous latest-line
selection. Pass an output directory to render large bilingual and narrow
adjustment previews into PNGs using hidden offscreen windows. It never launches
Echo, activates a visible window, opens audio devices, or connects to Soniox.
See `docs/desktop-subtitle-review.md` for coverage and remaining system checks.
