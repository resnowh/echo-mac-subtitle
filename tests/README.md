# Isolated stream checks

`CorrectionChecks.swift` additionally tests field locks, late recognition,
undo/stale revisions, old/new Archive JSON, cross-segment UUID matching,
corrected SRT, structured DeepSeek responses, configurable transcript
segmentation, UserDefaults persistence, and Soniox endpoint request settings.
No DeepSeek calls are made; these checks do not establish real AI correction
quality.

`LifecycleRecoveryChecks.swift` runs the production sleep/wake state machine
through ten recovery cycles and checks that inactive wake events, duplicate
notifications, user stops, and stale recovery results cannot restart recording.
These checks exercise state transitions only; they do not put the Mac to sleep,
start Echo, or access audio devices.

The macOS CI run also emits a synthetic Archive through the production Mac
encoder and its SRT exporter. The Windows job imports that exact fixture,
re-encodes it, and compares its SRT with Mac output; a final macOS job reads the
Windows-written JSON with the production Mac decoder. The fixture contains no
user data, audio, or credentials.

Run `python3 tests/run_stream_checks.py` on macOS with Xcode selected.
If local Command Line Tools and Xcode disagree, set `DEVELOPER_DIR` to the
installed Xcode developer directory for this command only.

The script compiles production PCM, transport, archive model, and storage code
in a temporary directory. It checks 30 minutes of **synthetic** dual-input sample/timeline
continuity, reset, prebuffer bounds, 20 loopback WebSocket reconnects,
ordered audio/end-of-stream delivery, cancellation and overflow failure.
The server binds only to an ephemeral localhost port. No credentials,
real recording, Soniox service or running Echo instance are used.

This does not validate physical microphone/ScreenCaptureKit behavior,
real network latency, UI scrolling or sleep/wake. Those require manual
verification when the user's active recording can safely be stopped.
