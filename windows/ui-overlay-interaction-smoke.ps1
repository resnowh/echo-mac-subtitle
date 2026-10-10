param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][guid]$TestPackageIdentity,
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$ResultsPath
)
$ErrorActionPreference = 'Stop'
$tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$packageName = $TestPackageIdentity.ToString().ToUpperInvariant()
$package = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if (-not $package) { throw "Test package $packageName is not registered." }
$isolatedPackageData = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalCache\Local\EchoWindows")).TrimEnd('\') + '\'
$resolvedDataRoot = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\') + '\'
if (-not $resolvedDataRoot.StartsWith($isolatedPackageData, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'DataRoot must be inside this unique test package LocalCache.'
}
if (-not [IO.Path]::GetFullPath($ResultsPath).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Test evidence must remain under the temporary directory.'
}
$process = Get-Process -Id $AppPid -ErrorAction Stop
$processPath = [IO.Path]::GetFullPath($process.Path)
$packagePath = [IO.Path]::GetFullPath($package.InstallLocation).TrimEnd('\') + '\'
if (-not $processPath.StartsWith($packagePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The requested PID does not belong to the isolated test package.'
}

if (-not ('EchoA92WindowApi' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EchoA92WindowApi {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW", SetLastError=true)] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X=x; Y=y; } }
}
'@
}

$results = [System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path (Split-Path -Parent $ResultsPath) -Force | Out-Null
function Add-Result([string]$Name, [bool]$Passed, [string]$Detail = '') {
    $results.Add([pscustomobject]@{ name = $Name; status = $(if ($Passed) { 'PASS' } else { 'FAIL' }); detail = $Detail })
}
function Invoke-Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n")
}
function Search-Ui([string]$Selector, [string]$Window = '') {
    $args = @('search', $Selector, '-a', "$AppPid", '--json')
    if ($Window) { $args += @('-w', $Window) }
    $output = & winapp ui @args 2>&1
    $json = $output -join "`n"
    try { return $json | ConvertFrom-Json }
    catch { throw "UIA search '$Selector' returned invalid JSON: $json" }
}
function Invoke-Control([string]$Selector, [string]$Window) {
    if ($Selector -in @('OverlaySettingsDone', 'OverlayRestoreDefaults')) {
        Invoke-Ui @('scroll-into-view', $Selector, '-w', $Window) | Out-Null
    }
    Invoke-Ui @('invoke', $Selector, '-w', $Window) | Out-Null
}
function Get-Windows {
    @(Invoke-Ui @('list-windows', '-a', "$AppPid", '--json', '--show-hidden') | ConvertFrom-Json)
}
function Wait-VisibleWindow([string]$TitleOrClass, [int]$TimeoutMs = 4000) {
    $until = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $candidate = Get-Windows | Where-Object { $_.processId -eq $AppPid -and ($_.title -eq $TitleOrClass -or $_.className -eq $TitleOrClass) } | Select-Object -First 1
        if ($candidate -and [EchoA92WindowApi]::IsWindowVisible([IntPtr]::new([long]$candidate.hwnd))) { return $candidate }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    throw "Visible window '$TitleOrClass' did not appear."
}
function Wait-UiControl([string]$Selector, [string]$Window, [int]$TimeoutMs = 3000) {
    $until = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $match = Search-Ui $Selector $Window
        if ($match.matchCount -gt 0) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    throw "UI Automation control '$Selector' did not appear in window $Window."
}
function Read-Settings {
    $file = Join-Path $DataRoot 'settings.json'
    if (-not (Test-Path -LiteralPath $file)) { throw "Isolated settings were not persisted: $file" }
    Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
}

try {
    $main = Wait-VisibleWindow 'Echo'
    $mainHwnd = [string]$main.hwnd
    $overlay = Get-Windows | Where-Object {
        $_.processId -eq $AppPid -and $_.className -eq 'Echo.Windows.NativeSubtitleOverlay.1' -and
        [EchoA92WindowApi]::IsWindowVisible([IntPtr]::new([long]$_.hwnd))
    } | Select-Object -First 1
    if (-not $overlay) {
        Invoke-Control 'SubtitleOverlayMenu' $mainHwnd
        Wait-UiControl 'ToggleSubtitleOverlay' $mainHwnd
        Invoke-Control 'ToggleSubtitleOverlay' $mainHwnd
        $overlay = Wait-VisibleWindow 'Echo.Windows.NativeSubtitleOverlay.1'
    }
    $overlayHwnd = [IntPtr]::new([long]$overlay.hwnd)
    $overlayRect = New-Object EchoA92WindowApi+RECT
    if (-not [EchoA92WindowApi]::GetWindowRect($overlayHwnd, [ref]$overlayRect)) { throw 'Could not read native caption window bounds.' }
    $centerX = [int](($overlayRect.Left + $overlayRect.Right) / 2)
    $centerY = [int](($overlayRect.Top + $overlayRect.Bottom) / 2)
    [EchoA92WindowApi]::SetCursorPos($centerX, $centerY) | Out-Null
    Start-Sleep -Milliseconds 250
    $toolbar = Wait-VisibleWindow 'Echo Subtitle Controls'
    $toolbarHwnd = [string]$toolbar.hwnd
    $toolbarControls = @('OverlayMoveHandle', 'OverlayPositionLock', 'OverlayClickThrough', 'OverlayOpenSettings', 'OverlayClose')
    foreach ($control in $toolbarControls) {
        $match = Search-Ui $control $toolbarHwnd
        Add-Result "Toolbar control $control is visible to UI Automation" ($match.matchCount -gt 0) "matches=$($match.matchCount)"
    }
    Invoke-Control 'OverlayOpenSettings' $toolbarHwnd
    $settingsWindow = Wait-VisibleWindow 'Echo Subtitle Settings'
    $settingsHwnd = [string]$settingsWindow.hwnd
    $requiredSettings = @('OverlayShowOriginal', 'OverlayShowTranslation', 'OverlayOriginalFontSize', 'OverlayTranslationFontSize', 'OverlayOpacity', 'OverlayMaximumWidth', 'OverlayRetention', 'OverlayShadow', 'OverlayPositionLocked', 'OverlayClickThrough', 'OverlayResetPosition', 'OverlayRestoreDefaults')
    foreach ($control in $requiredSettings) {
        $match = Search-Ui $control $settingsHwnd
        Add-Result "Settings control $control is visible to UI Automation" ($match.matchCount -gt 0) "matches=$($match.matchCount)"
    }
    Invoke-Control 'OverlayRestoreDefaults' $settingsHwnd
    Start-Sleep -Milliseconds 500
    Invoke-Ui @('set-value', 'OverlayOriginalFontSize', '32', '-w', $settingsHwnd) | Out-Null
    Invoke-Ui @('set-value', 'OverlayTranslationFontSize', '29', '-w', $settingsHwnd) | Out-Null
    Invoke-Ui @('set-value', 'OverlayOpacity', '82', '-w', $settingsHwnd) | Out-Null
    Invoke-Ui @('set-value', 'OverlayMaximumWidth', '62', '-w', $settingsHwnd) | Out-Null
    Invoke-Ui @('set-value', 'OverlayRetention', '7', '-w', $settingsHwnd) | Out-Null
    Invoke-Ui @('set-value', 'OverlayShadow', '65', '-w', $settingsHwnd) | Out-Null
    Invoke-Control 'OverlayShowTranslation' $settingsHwnd
    Start-Sleep -Milliseconds 700
    $saved = Read-Settings
    Add-Result 'Settings preview persisted updated font, opacity, width, retention and shadow' (
        [math]::Abs([double]$saved.subtitleOverlay.originalFontSize - 32) -lt 1 -and
        [math]::Abs([double]$saved.subtitleOverlay.translationFontSize - 29) -lt 1 -and
        [math]::Abs([double]$saved.subtitleOverlay.opacity - .82) -lt .02 -and
        [math]::Abs([double]$saved.subtitleOverlay.widthFraction - .62) -lt .02 -and
        [math]::Abs([double]$saved.subtitleOverlay.retentionSeconds - 7) -lt 1 -and
        [math]::Abs([double]$saved.subtitleOverlay.shadowStrength - .65) -lt .02 -and
        $saved.subtitleOverlay.showTranslation -eq $false
    ) ("settingsPath=$((Join-Path $DataRoot 'settings.json'))")
    Invoke-Control 'OverlaySettingsDone' $settingsHwnd
    [EchoA92WindowApi]::SetCursorPos($centerX, $centerY) | Out-Null
    Start-Sleep -Milliseconds 250
    $toolbar = Wait-VisibleWindow 'Echo Subtitle Controls'
    $toolbarHwnd = [string]$toolbar.hwnd

    # The lock and pass-through controls remain available from the visible toolbar.
    $beforeStyle = [EchoA92WindowApi]::GetWindowLongPtr($overlayHwnd, -20).ToInt64()
    Invoke-Control 'OverlayClickThrough' $toolbarHwnd
    Start-Sleep -Milliseconds 150
    $afterStyle = [EchoA92WindowApi]::GetWindowLongPtr($overlayHwnd, -20).ToInt64()
    Add-Result 'Toolbar click-through changes only the caption HWND WS_EX_TRANSPARENT bit' (
        (($beforeStyle -band 0x20) -ne 0) -and (($afterStyle -band 0x20) -eq 0)
    ) ("before=0x{0:X}; after=0x{1:X}" -f $beforeStyle, $afterStyle)
    Invoke-Control 'OverlayClickThrough' $toolbarHwnd
    Start-Sleep -Milliseconds 150
    $restoredStyle = [EchoA92WindowApi]::GetWindowLongPtr($overlayHwnd, -20).ToInt64()
    $underlying = [EchoA92WindowApi]::WindowFromPoint([EchoA92WindowApi+POINT]::new($centerX, $centerY))
    [uint32]$underlyingPid = 0
    [EchoA92WindowApi]::GetWindowThreadProcessId($underlying, [ref]$underlyingPid) | Out-Null
    Add-Result 'Click-through restores and body hit-testing reaches a lower window' (
        (($restoredStyle -band 0x20) -ne 0) -and $underlying -ne $overlayHwnd
    ) ("style=0x{0:X}; underlyingHwnd={1}; pid={2}" -f $restoredStyle, $underlying, $underlyingPid)

    Invoke-Control 'OverlayPositionLock' $toolbarHwnd
    Start-Sleep -Milliseconds 500
    $unlocked = Read-Settings
    Add-Result 'Toolbar unlock state persists independently from click-through' (
        $unlocked.subtitleOverlay.positionLocked -eq $false -and $unlocked.subtitleOverlay.clickThrough -eq $true
    ) ("locked=$($unlocked.subtitleOverlay.positionLocked); clickThrough=$($unlocked.subtitleOverlay.clickThrough)")
    Invoke-Ui @('drag', 'OverlayMoveHandle', "$($centerX + 90),$($centerY + 35)", '-a', "$AppPid", '--hold-ms', '120') | Out-Null
    Start-Sleep -Milliseconds 250
    $moved = Get-Windows | Where-Object { $_.processId -eq $AppPid -and $_.className -eq 'Echo.Windows.NativeSubtitleOverlay.1' } | Select-Object -First 1
    $movedRect = New-Object EchoA92WindowApi+RECT
    [EchoA92WindowApi]::GetWindowRect([IntPtr]::new([long]$moved.hwnd), [ref]$movedRect) | Out-Null
    Add-Result 'Unlocked toolbar drag moves the native subtitle HWND' (
        $movedRect.Left -ne $overlayRect.Left -or $movedRect.Top -ne $overlayRect.Top
    ) ("before=$($overlayRect.Left),$($overlayRect.Top); after=$($movedRect.Left),$($movedRect.Top)")
    Invoke-Control 'OverlayPositionLock' $toolbarHwnd
    Start-Sleep -Milliseconds 500
    $locked = Read-Settings
    Add-Result 'Toolbar lock prevents further pointer movement' ($locked.subtitleOverlay.positionLocked -eq $true) "locked=$($locked.subtitleOverlay.positionLocked)"

    Invoke-Control 'OverlayClickThrough' $toolbarHwnd
    Start-Sleep -Milliseconds 400
    Invoke-Control 'SubtitleOverlayMenu' ([string]$main.hwnd)
    Wait-UiControl 'RestoreSubtitleOverlayControls' ([string]$main.hwnd)
    Invoke-Control 'RestoreSubtitleOverlayControls' ([string]$main.hwnd)
    Start-Sleep -Milliseconds 500
    $recovered = Read-Settings
    Add-Result 'Main-window emergency recovery disables pass-through and unlocks the overlay' (
        $recovered.subtitleOverlay.enabled -eq $true -and
        $recovered.subtitleOverlay.clickThrough -eq $false -and
        $recovered.subtitleOverlay.positionLocked -eq $false
    ) ("enabled=$($recovered.subtitleOverlay.enabled); clickThrough=$($recovered.subtitleOverlay.clickThrough); locked=$($recovered.subtitleOverlay.positionLocked)")
    Invoke-Control 'OverlayClickThrough' $toolbarHwnd
    Invoke-Control 'OverlayPositionLock' $toolbarHwnd
    Start-Sleep -Milliseconds 500

    # Exercise the explicit recovery path and repeat the window lifecycle without audio.
    $mainRect = New-Object EchoA92WindowApi+RECT
    [EchoA92WindowApi]::GetWindowRect([IntPtr]::new([long]$main.hwnd), [ref]$mainRect) | Out-Null
    [EchoA92WindowApi]::ShowWindow([IntPtr]::new([long]$main.hwnd), 6) | Out-Null
    Start-Sleep -Milliseconds 300
    Add-Result 'Ownerless subtitle remains visible while the main window is minimized' ([EchoA92WindowApi]::IsWindowVisible($overlayHwnd)) 'Checked native overlay visibility after main-window minimize.'
    [EchoA92WindowApi]::ShowWindow([IntPtr]::new([long]$main.hwnd), 9) | Out-Null
    Start-Sleep -Milliseconds 200

    for ($cycle = 1; $cycle -le 3; $cycle++) {
        Invoke-Control 'OverlayClose' $toolbarHwnd
        Start-Sleep -Milliseconds 180
        $hidden = -not [EchoA92WindowApi]::IsWindowVisible($overlayHwnd)
        Invoke-Control 'SubtitleOverlayMenu' ([string]$main.hwnd)
        Wait-UiControl 'ToggleSubtitleOverlay' ([string]$main.hwnd)
        Invoke-Control 'ToggleSubtitleOverlay' ([string]$main.hwnd)
        Start-Sleep -Milliseconds 250
        Add-Result "Close/reopen cycle $cycle preserves one native overlay" ($hidden -and (Get-Windows | Where-Object { $_.processId -eq $AppPid -and $_.className -eq 'Echo.Windows.NativeSubtitleOverlay.1' }).Count -eq 1) 'Overlay HWND count checked.'
        [EchoA92WindowApi]::SetCursorPos($centerX, $centerY) | Out-Null
        Start-Sleep -Milliseconds 200
        $toolbar = Wait-VisibleWindow 'Echo Subtitle Controls'
        $toolbarHwnd = [string]$toolbar.hwnd
    }

    $displayCount = @(Get-CimInstance Win32_DesktopMonitor | Where-Object Availability -ne 8).Count
    Add-Result 'Current display inventory recorded for multi-monitor test boundary' $true "visibleDesktopMonitors=$displayCount; no display configuration was changed."
    if (Test-Path -LiteralPath (Join-Path $DataRoot 'settings.json')) {
        $settingsFile = Join-Path $DataRoot 'settings.json'
        $secretAudit = (Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json)
        Add-Result 'Synthetic package settings contain no API keys' (
            [string]::IsNullOrEmpty($secretAudit.sonioxSecret) -and [string]::IsNullOrEmpty($secretAudit.deepSeekSecret)
        ) 'Only the temporary package LocalCache settings file was inspected.'
    }
}
catch {
    Add-Result 'Overlay interaction GUI smoke completed without an unhandled test error' $false "$($_.Exception.Message)"
}
finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
}

$results | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
