# Isolated stream checks

Run `python3 tests/run_stream_checks.py` on macOS with Xcode selected.
If local Command Line Tools and Xcode disagree, set `DEVELOPER_DIR` to the
installed Xcode developer directory for this command only.

The script compiles production PCM and transport sources in a temporary
directory. It checks 30 minutes of **synthetic** dual-input sample/timeline
continuity, reset, prebuffer bounds, 20 loopback WebSocket reconnects,
ordered audio/end-of-stream delivery, cancellation and overflow failure.
The server binds only to an ephemeral localhost port. No credentials,
real recording, Soniox service or running Echo instance are used.

This does not validate physical microphone/ScreenCaptureKit behavior,
real network latency, UI scrolling or sleep/wake. Those require manual
verification when the user's active recording can safely be stopped.
