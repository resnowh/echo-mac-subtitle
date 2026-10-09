# A72 — Native Win32/Direct2D overlay route confirmation

## User decision and current implementation

The user selected a native Win32/Direct2D transparent subtitle window to replace the black-backed WinUI 3 overlay. This route is already implemented on the current Windows parity branch: `NativeDesktopSubtitleOverlayWindow` creates an unowned layered HWND, draws a premultiplied BGRA surface with Win2D/Direct2D, and presents it with `UpdateLayeredWindow(ULW_ALPHA)`. The main app remains WinUI 3.

This iteration confirmed the selected implementation and reran its synthetic/source-contract coverage. It did not change application code or repeat a GUI test.

## Validation

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj --configuration Release`: exit 0, 159 checks passed. Overlay checks cover placement calculations at 100/150/200% DPI, negative monitor origins, display/work-area message wiring, hide/adjustment behavior, unowned topmost no-activate lifecycle, close cleanup, language visibility, and text truncation. Full output: `corechecks.log`.
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj --configuration Release --arch x64`: exit 0, 0 warnings and 0 errors. Full output: `release-build.log`.
- `gh pr checks 5`: all listed current PR checks passed. Snapshot: `pr-checks.log`.
- `git diff --check`: exit 0. Output: `git-diff-check.log`.
- The current `Echo.Windows.exe` was only listed as a process; no window/UIA interaction, close, restart, or deployment was performed.
- No audio was captured and no cloud API was called.

## Verification boundary

The selected implementation is present and the local build, synthetic checks, and PR CI pass. This does not prove actual rendering after a live monitor/DPI change, unplugging a display, full-screen compatibility, virtual desktop behavior, main-window minimization behavior, keyboard/Narrator accessibility, or side-by-side Mac visual parity. Those GUI checks require a separate isolated app process and desktop session; the active Echo process was left untouched.

## Git baseline

See `git-baseline.txt` for exact local and remote refs. Source SHA-256 values are in `source-hashes.txt`.
