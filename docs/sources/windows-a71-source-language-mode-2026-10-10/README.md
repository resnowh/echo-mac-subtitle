# A71 — Recognition mode settings parity

## Mac source of truth

Read-only baseline: `EchoMacApp.swift` and `TranscriptModels.swift` at `origin/main` commit `ae0359dc90da0ccb5e526a275da1747954a49a4f`. Mac exposes two explicit modes, automatic recognition and preferred language. The preferred-language and strict-language controls appear only in preferred mode. Switching to automatic retains the preferred language for later use.

## Windows change

- Added a two-item WinUI `SelectorBar` for automatic recognition and preferred language.
- Grouped the preferred-language picker, strict-language toggle, and strict-mode helper text so the group hides in automatic mode.
- Added `PreferredSourceLanguage` to preferences. Existing settings migrate from `SourceLanguage`; existing automatic settings without a previous value default to English.
- Kept the main-window language menu compatible: choosing a specified language updates the preferred language, while choosing automatic leaves that preference intact.
- Request construction still omits language hints and strict mode when `SourceLanguage` is empty.

## Validation

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore -- --core-only`: exit 0; 159 checks passed, including migration, persisted preference, XAML structure, and save/event wiring.
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`: exit 0; 0 warnings and 0 errors.
- `git diff --check`: exit 0.
- No Echo instance or audio device was started or touched. No cloud API was called.
- No GUI smoke was run; visual layout, keyboard navigation, Narrator, high contrast, and actual settings save interaction remain unverified.

Raw outputs are `corechecks.log`, `release-build.log`, `git-diff-check.log`, and `validation-status.txt`. Mac/Windows source and document SHA-256 values are in `source-hashes.txt`.

## Git baseline

Local and remote Windows parity head before this iteration: `13ce6094a4c8c7e790065c5cb5aaeb44dff0a435`. `origin/main`: `ae0359dc90da0ccb5e526a275da1747954a49a4f`. `origin/feature/windows-preview`: `d500bbb21bc9bfa4d811c614576f38c0476efc50`. PR #5 was OPEN and CLEAN at the same head. Full refs and PR URL are in `git-baseline.txt`.
