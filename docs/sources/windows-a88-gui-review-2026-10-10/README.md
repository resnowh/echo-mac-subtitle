# A88 GUI regression and summary-space review (2026-10-10)

## Scope and source baseline

This review checks the Windows recording screen against the Mac layout contract after A87. Mac remains the design reference: 820×650 DIP ideal window, 680×520 DIP minimum, 20 DIP content margin and 18 DIP row spacing when the available width supports the wide toolbar. A long generated AI summary must not push the transcript list out of the recording view.

The run used the current PR #6 source tree, plus isolated copies of the manifest, `MainPage.xaml`, `MainPage.xaml.cs`, and CoreChecks in `%LOCALAPPDATA%\Temp\Echo-A88-20261010\checkout`. The temporary manifest had a unique package identity (`C013C0F5-C05B-4615-9187-E88959C5A60A`) and the test mutex was isolated. Only deterministic synthetic settings and two synthetic archives were placed in that package's private cache. Both API keys were blank. The official Echo package was not launched or modified.

## Findings and change

The first isolated screenshots showed that an expanded summary could consume the flexible area and reduce `TranscriptList` to zero height. A bounded summary layout now reserves transcript space and caps summary content at the Mac 220 DIP limit. The transcript row keeps a 120 DIP minimum in compact-height windows and 140 DIP in taller windows. The summary body yields first when space is constrained.

The first final-size visual check also showed that WinUI's width trigger did not place the connection status beside the recording controls. A `RecordingPanel.SizeChanged`/`Loaded` layout update now uses the stable `XamlRoot` window width: at 700 DIP and above, it restores the 20/18 DIP Mac spacing and places connection status in the toolbar's right column; below that, it uses 16/12 DIP spacing and a second status row. This keeps margin changes from moving the width across the breakpoint.

## Verification

- CoreChecks: 191 checks passed. The synthetic WebSocket checks used no cloud service and saved no audio.
- Windows Release x64 build: 0 warnings, 0 errors.
- Isolated Debug package build: 0 warnings, 0 errors; test package identity was separate from the installed product identity.
- At 144 DPI, the initial window measured 820×650 DIP. UI Automation reported `TranscriptList` visible at 140 DIP and the status label on the same row as the input/record controls.
- At 144 DPI, resizing to the Mac minimum produced 680×520 DIP. UI Automation reported `TranscriptList` visible at 120 DIP; the final screenshot shows complete synthetic subtitle text and compact status placement.
- No recording was started, no audio device was opened, no API request was made, and no real archive, key, or recording was read.
- `macOS/` was not changed.

## Screenshot index

- `01-populated-dark-820x650.png`, `02-minimum-window-dark.png`: initial pre-fix state; transcript area collapsed behind the expanded summary.
- `03`–`08`: intermediate isolated iterations retained as diagnostic evidence.
- `09-final-wide-dark-820x650.png`: final wide layout with visible transcript, summary and inline status row.
- `10-final-minimum-dark.png`: final minimum layout with a full synthetic subtitle row and compact toolbar.
- `minimum-window-results-final.json`: final DPI/window/UIA geometry measurements.

The GUI sizing helper `windows/ui-main-window-size.ps1` refused the package-private LocalAppData path because its guard requires a `%TEMP%` data root. The final resize and measurements were therefore run directly against the isolated test HWND using Win32 `GetDpiForWindow`/`SetWindowPos` and `winapp ui`; no product data path was involved.

SHA-256 digests for the fixtures, screenshots, logs, measurements, and changed source/docs are recorded in `sha256.txt`.

## Data inventory

Source fixtures are the three `synthetic-*.json` files in this directory. They contain generated English/Chinese lecture subtitles, a generated summary and empty API keys. The package-private test cache was unregistered after the screenshots; screenshot and fixture evidence remains in this source directory.

## Remaining visual acceptance

This is isolated Windows evidence, not the user's final visual acceptance. Mac screenshots were unavailable in this run. Light theme, high-contrast theme, keyboard focus, Narrator, long-list scrolling, recording/permission states, and additional DPI/window widths still need review. The screenshot set is ready for the user to inspect.
