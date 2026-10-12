# A73 — Explicit Narrator names for main workflow controls

## Finding and change

Static review of `MainPage.xaml` found four meaningful controls without an explicit accessible name: start recording and stop recording relied on nested icon/text children, the local archive picker list had no name, and the polite live status region had no label. Added explicit names while retaining the existing AutomationIds and live-region setting. The status name is bound to the current status text (`录音状态：…`) so it gives context without hiding the changing value.

The Mac source has no equivalent requirement to add custom shortcut behavior; this change improves the native Windows accessibility tree without altering visible layout or recording behavior.

## Validation

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj --configuration Release`: exit 0, 160 checks passed. A new fixture assertion verifies the four names, that the status name follows `ViewModel.Status`, and the region's `Polite` setting. Full output: `corechecks.log`.
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj --configuration Release --arch x64`: exit 0, 0 warnings and 0 errors. Full output: `release-build.log`.
- `git diff --check`: exit 0. Output: `git-diff-check.log`.
- No GUI, UI Automation, Narrator/NVDA, Accessibility Insights, audio device, or cloud service was used. The running Echo process was not touched.

## Verification boundary

The XAML names and live-setting metadata are present and covered by CoreChecks. Actual Tab traversal, focus visibility, Narrator/NVDA announcements, high-contrast readability, and Accessibility Insights scans remain unverified until an isolated GUI session is available.

Source SHA-256 values are in `source-hashes.txt`; exact branch and PR state are in `git-baseline.txt`.
