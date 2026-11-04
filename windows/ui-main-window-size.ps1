param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][guid]$TestPackageIdentity,
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$ResultsPath,
    [Parameter(Mandatory)][string]$ScreenshotPath
)
$ErrorActionPreference = 'Stop'

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
if (-not $resolvedDataRoot.StartsWith($tempPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'DataRoot must be a temporary isolated directory.'
}

if (-not ('EchoA48WindowApi' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public struct EchoA48Rect { public int Left; public int Top; public int Right; public int Bottom; }
public static class EchoA48WindowApi {
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out EchoA48Rect rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
}
'@
}

$results = [System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path (Split-Path -Parent $ResultsPath) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $ScreenshotPath) -Force | Out-Null
function Add-Result([string]$Name, [bool]$Passed, [string]$Detail = '') {
    $results.Add([pscustomobject]@{ name = $Name; status = $(if ($Passed) { 'PASS' } else { 'FAIL' }); detail = $Detail })
}
function Invoke-Ui([string[]]$Arguments) {
    $output = & winapp ui @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    return ($output -join "`n")
}
function Read-WindowRect([IntPtr]$Hwnd) {
    $rect = [EchoA48Rect]::new()
    if (-not [EchoA48WindowApi]::GetWindowRect($Hwnd, [ref]$rect)) { throw 'GetWindowRect failed.' }
    return $rect
}
function Get-MainWindowElements([long]$Hwnd) {
    $tree = Invoke-Ui @('inspect', '-a', "$AppPid", '--interactive', '--json') | ConvertFrom-Json
    return @($tree.windows | Where-Object { [long]$_.hwnd -eq $Hwnd } | ForEach-Object { $_.elements })
}

try {
    $windows = Invoke-Ui @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json
    $mainWindow = $windows | Where-Object { $_.processId -eq $AppPid -and $_.title -eq 'Echo' } | Select-Object -First 1
    if (-not $mainWindow) { throw 'Could not find the test app main window.' }
    $hwnd = [IntPtr]::new([long]$mainWindow.hwnd)
    $dpi = [EchoA48WindowApi]::GetDpiForWindow($hwnd)
    if ($dpi -eq 0) { throw 'GetDpiForWindow returned zero.' }
    $scale = $dpi / 96.0
    $previousDpiContext = [EchoA48WindowApi]::SetThreadDpiAwarenessContext([IntPtr](-4))
    if ($previousDpiContext -eq [IntPtr]::Zero) { throw 'Could not enable per-monitor-v2 DPI awareness for window measurements.' }
    try {
        $initial = Read-WindowRect $hwnd
        $initialWidthDip = ($initial.Right - $initial.Left) / $scale
        $initialHeightDip = ($initial.Bottom - $initial.Top) / $scale
        Add-Result 'Main window opens at the Mac ideal 820x650 DIP size' ([Math]::Abs($initialWidthDip - 820) -le 5 -and [Math]::Abs($initialHeightDip - 650) -le 5) ("DPI=$dpi; window={0:0}x{1:0} DIP" -f $initialWidthDip, $initialHeightDip)
        $elements = Get-MainWindowElements $hwnd.ToInt64()
        $hasPermanentReturn = @($elements | Where-Object { $_.automationId -eq 'ReturnToLatest' }).Count -gt 0
        Add-Result 'No permanent Return to latest action appears with no new subtitles' (-not $hasPermanentReturn) "ReturnToLatest UIA element present=$hasPermanentReturn"

        $tooSmallWidth = [int][Math]::Round(600 * $scale)
        $tooSmallHeight = [int][Math]::Round(450 * $scale)
        if (-not [EchoA48WindowApi]::SetWindowPos($hwnd, [IntPtr]::Zero, $initial.Left, $initial.Top, $tooSmallWidth, $tooSmallHeight, 0x0004)) {
            throw 'SetWindowPos failed while testing the minimum tracking size.'
        }
        Start-Sleep -Milliseconds 400
        $minimum = Read-WindowRect $hwnd
        $minimumWidthDip = ($minimum.Right - $minimum.Left) / $scale
        $minimumHeightDip = ($minimum.Bottom - $minimum.Top) / $scale
        Add-Result 'Window resize respects the Mac 680x520 DIP minimum' ($minimumWidthDip -ge 675 -and $minimumHeightDip -ge 515) ("window={0:0}x{1:0} DIP after requesting 600x450 DIP" -f $minimumWidthDip, $minimumHeightDip)
    }
    finally {
        [void][EchoA48WindowApi]::SetThreadDpiAwarenessContext($previousDpiContext)
    }

    Invoke-Ui @('screenshot', '-w', "$($mainWindow.hwnd)", '-o', $ScreenshotPath) | Out-Null
    Add-Result 'Main window minimum-size screenshot captured' (Test-Path -LiteralPath $ScreenshotPath) $ScreenshotPath
}
catch {
    Add-Result 'Mac-aligned main window UIA flow' $false "$($_.Exception.Message)"
}
finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
}

$results | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
