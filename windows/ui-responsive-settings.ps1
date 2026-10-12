param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][guid]$TestPackageIdentity,
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$ResultsPath,
    [Parameter(Mandatory)][string]$ScreenshotPath
)
$ErrorActionPreference = 'Stop'

# This test changes the target window size. Require a uniquely identified test
# package and a temporary data directory so it cannot operate on a user's Echo.
$packageName = $TestPackageIdentity.ToString().ToUpperInvariant()
$package = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if (-not $package) { throw "Test package $packageName is not registered." }
$process = Get-Process -Id $AppPid -ErrorAction Stop
$processPath = [IO.Path]::GetFullPath($process.Path)
$packagePath = [IO.Path]::GetFullPath($package.InstallLocation).TrimEnd('\') + '\'
if (-not $processPath.StartsWith($packagePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The PID does not belong to the specified isolated test package.'
}
$tempPath = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$resolvedDataRoot = [IO.Path]::GetFullPath($DataRoot)
if ((-not $resolvedDataRoot.StartsWith($tempPath, [StringComparison]::OrdinalIgnoreCase)) -or ($resolvedDataRoot -eq [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'EchoWindows')))) {
    throw 'DataRoot must be a temporary isolated directory, not the user Echo data directory.'
}

if (-not ('EchoA46WindowApi' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public struct EchoA46Rect { public int Left; public int Top; public int Right; public int Bottom; }
public static class EchoA46WindowApi {
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out EchoA46Rect rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
}
'@
}

$results = [System.Collections.Generic.List[object]]::new()
$resultsFolder = Split-Path -Parent $ResultsPath
New-Item -ItemType Directory -Path $resultsFolder -Force | Out-Null
function Add-Result([string]$Name, [bool]$Passed, [string]$Detail = '') {
    $results.Add([pscustomobject]@{ name = $Name; status = $(if ($Passed) { 'PASS' } else { 'FAIL' }); detail = $Detail })
}
function Invoke-Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n")
}
function Get-UiElements {
    $json = Invoke-Ui @('inspect', '-a', "$AppPid", '--interactive', '--json') | ConvertFrom-Json
    return @($json.windows | Where-Object { $_.hwnd -eq $script:mainHwnd } | ForEach-Object { $_.elements })
}
function Get-Element([string]$AutomationId) {
    return Get-UiElements | Where-Object { $_.automationId -eq $AutomationId } | Select-Object -First 1
}

$windows = Invoke-Ui @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json
$mainWindow = $windows | Where-Object { $_.processId -eq $AppPid -and $_.title -ne 'PopupHost' } | Select-Object -First 1
if (-not $mainWindow) { throw 'Could not find the test app main window.' }
$script:mainHwnd = [IntPtr]::new([long]$mainWindow.hwnd)
$dpi = [EchoA46WindowApi]::GetDpiForWindow($script:mainHwnd)
if ($dpi -eq 0) { throw 'GetDpiForWindow returned zero.' }
$scale = $dpi / 96.0
$targetWidth = [int][Math]::Round(430 * $scale)
$targetHeight = [int][Math]::Round(360 * $scale)
$previousDpiContext = [EchoA46WindowApi]::SetThreadDpiAwarenessContext([IntPtr](-4))
if ($previousDpiContext -eq [IntPtr]::Zero) { throw 'Could not enable per-monitor-v2 DPI awareness for window measurements.' }
try {
    $oldRect = [EchoA46Rect]::new()
    if (-not [EchoA46WindowApi]::GetWindowRect($script:mainHwnd, [ref]$oldRect)) { throw 'GetWindowRect failed.' }
    if (-not [EchoA46WindowApi]::SetWindowPos($script:mainHwnd, [IntPtr]::Zero, $oldRect.Left, $oldRect.Top, $targetWidth, $targetHeight, 0x0004)) {
        throw 'SetWindowPos failed.'
    }
    Start-Sleep -Milliseconds 400
    $smallRect = [EchoA46Rect]::new()
    [void][EchoA46WindowApi]::GetWindowRect($script:mainHwnd, [ref]$smallRect)
}
finally {
    [void][EchoA46WindowApi]::SetThreadDpiAwarenessContext($previousDpiContext)
}
$actualWidthDip = ($smallRect.Right - $smallRect.Left) / $scale
$actualHeightDip = ($smallRect.Bottom - $smallRect.Top) / $scale
Add-Result 'Test window reduced to a 430x360 DIP viewport' ($actualWidthDip -le 435 -and $actualHeightDip -le 365) ("DPI=$dpi; window={0:0}x{1:0} DIP" -f $actualWidthDip, $actualHeightDip)

try {
    $saveButton = Get-Element 'SaveSettings'
    if (-not $saveButton) { Invoke-Ui @('invoke', 'SettingsTab', '-a', "$AppPid") | Out-Null }
    Invoke-Ui @('wait-for', 'SaveSettings', '-a', "$AppPid", '-t', '5000') | Out-Null
    Invoke-Ui @('wait-for', 'SettingsGeneral', '-a', "$AppPid", '-t', '5000') | Out-Null
    $saveButton = Get-Element 'SaveSettings'
    $settingsSections = Get-Element 'SettingsGeneral'
    $saveVisible = $saveButton -and -not $saveButton.isOffscreen -and $saveButton.width -gt 0 -and $saveButton.height -gt 0
    $sectionsVisible = $settingsSections -and -not $settingsSections.isOffscreen -and $settingsSections.width -gt 0
    Add-Result 'Settings header and Complete button remain visible in the reduced viewport' ($saveVisible -and $sectionsVisible) ("SaveSettings.offscreen=$($saveButton.isOffscreen); SettingsSections.offscreen=$($settingsSections.isOffscreen)")

    Invoke-Ui @('invoke', 'SettingsSegmentation', '-a', "$AppPid") | Out-Null
    Invoke-Ui @('scroll', 'SettingsScrollViewer', '-a', "$AppPid", '--to', 'bottom') | Out-Null
    Start-Sleep -Milliseconds 250
    Invoke-Ui @('wait-for', 'ResetSegmentation', '-a', "$AppPid", '-t', '5000') | Out-Null
    $resetButton = Get-Element 'ResetSegmentation'
    $saveButton = Get-Element 'SaveSettings'
    $scrollWorks = $resetButton -and -not $resetButton.isOffscreen -and $resetButton.width -gt 0
    $saveRemainsVisible = $saveButton -and -not $saveButton.isOffscreen -and $saveButton.width -gt 0
    Add-Result 'Long segmentation settings scroll into view while Complete remains reachable' ($scrollWorks -and $saveRemainsVisible) ("ResetSegmentation.offscreen=$($resetButton.isOffscreen); SaveSettings.offscreen=$($saveButton.isOffscreen)")

    $outputFolder = Split-Path -Parent $ScreenshotPath
    New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
    # Move the pointer to the selected category so slider value tooltips do
    # not obscure the screenshot or appear as an unrelated popup window.
    Invoke-Ui @('click', 'SettingsSegmentation', '-a', "$AppPid") | Out-Null
    Invoke-Ui @('screenshot', '-w', "$($mainWindow.hwnd)", '-o', $ScreenshotPath) | Out-Null
    Add-Result 'Constrained settings screenshot captured' (Test-Path -LiteralPath $ScreenshotPath) $ScreenshotPath
}
catch {
    Add-Result 'Responsive settings UIA flow' $false "$_"
}
finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
}

$results | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
