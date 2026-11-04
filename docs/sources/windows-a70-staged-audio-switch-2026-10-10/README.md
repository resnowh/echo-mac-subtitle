# A70 — Recording-time audio source preflight

## Change

Windows now opens and starts candidate WASAPI routes before stopping the active routes. Once candidate startup succeeds, `SpeechSession` stops the old routes, sends their bounded buffered tail on the existing Soniox WebSocket, and atomically commits the new routes. At commit, buffered candidate samples from the overlapping preflight interval are cleared to avoid replaying audio already covered by the old source tail. A candidate startup error aborts the candidate and leaves active capture running. A candidate that fails asynchronously before commit is rejected; after commit, normal capture failure handling applies.

## Validation

- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore -- --core-only`: exit 0; 156 checks passed.
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release -p:Platform=x64 --no-restore`: exit 0; 0 warnings and 0 errors.
- `git diff --check`: exit 0.
- The synthetic WebSocket test rejects candidate startup and verifies that the old source remains running, `StopInputs` was not called, the same WebSocket stays open, and PCM continues.
- The synthetic default-device transition verifies the prepare/commit success path and continued PCM on the same connection.

Raw outputs: `corechecks.log`, `release-build.log`, `git-diff-check.log`, and `validation-status.txt`. SHA-256 values for the source and documentation files are in `source-hashes.txt`.

## Baseline and limits

Mac comparison source: `macOS/ViewModels/SpeechViewModel.swift`, read-only. No Mac files were changed. Local HEAD before this iteration was `76af76562ac4b9c95d16b66ca824ab50d873c41f`; fetched `origin/feature/windows-mac-parity` was its ancestor `79b44672a878d6f6c7a335e9010b15915e7a51a8`; `origin/main` was `ae0359dc90da0ccb5e526a275da1747954a49a4f`. PR #5 reported head `76af76562ac4b9c95d16b66ca824ab50d873c41f` before this iteration. Full baseline is in `git-baseline.txt`.

The tests use synthetic capture and local WebSocket fixtures. They do not prove acoustic continuity on physical devices, permission behavior, hot-unplug recovery, or actual Soniox service behavior. No microphone or loopback stream was opened, no audio was saved, and no cloud service was called.
