param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][guid]$TestPackageIdentity,
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$ResultsPath,
    [Parameter(Mandatory)][string]$ScreenshotPath,
    [Parameter(Mandatory)][bool]$ExpectedClickThrough
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

if (-not ('EchoA49WindowApi' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EchoA49WindowApi {
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW", SetLastError=true)] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
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
function Get-ExtendedStyle([IntPtr]$Hwnd) {
    return [EchoA49WindowApi]::GetWindowLongPtr($Hwnd, -20).ToInt64()
}
function Has-Style([long]$Style, [long]$Flag) { return ($Style -band $Flag) -ne 0 }

try {
    $windows = @(Invoke-Ui @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json)
    $mainWindow = $windows | Where-Object { $_.processId -eq $AppPid -and $_.title -eq 'Echo' } | Select-Object -First 1
    $overlayWindow = $windows | Where-Object { $_.processId -eq $AppPid -and $_.title -eq 'Echo 悬浮字幕' } | Select-Object -First 1
    if (-not $mainWindow) { throw 'Could not find the isolated Echo main window.' }
    if (-not $overlayWindow) { throw 'The isolated subtitle overlay window did not appear.' }
    $overlayHwnd = [IntPtr]::new([long]$overlayWindow.hwnd)
    $mainHwnd = [IntPtr]::new([long]$mainWindow.hwnd)
    $style = Get-ExtendedStyle $overlayHwnd
    $toolWindowAndNoActivate = (Has-Style $style 0x00000080) -and (Has-Style $style 0x08000000)
    Add-Result 'Overlay is a tool-window and does not activate' $toolWindowAndNoActivate ("extendedStyle=0x{0:X}" -f $style)
    Add-Result 'Layered style is active for transparent composition' (Has-Style $style 0x00080000) ("extendedStyle=0x{0:X}" -f $style)
    $aboveMain = $false
    $cursor = $mainHwnd
    for ($index = 0; $index -lt 100 -and $cursor -ne [IntPtr]::Zero; $index++) {
        $cursor = [EchoA49WindowApi]::GetWindow($cursor, 3)
        if ($cursor -eq $overlayHwnd) { $aboveMain = $true; break }
    }
    Add-Result 'Overlay remains above the main window in z-order' $aboveMain 'Compared by adjacent top-level window order.'
    Add-Result 'Overlay visibility matches test expectation' ([EchoA49WindowApi]::IsWindowVisible($overlayHwnd)) 'Window is visible.'
    Add-Result 'Click-through state matches test expectation' ((Has-Style $style 0x00000020) -eq $ExpectedClickThrough) ("expected={0}; actual={1}" -f $ExpectedClickThrough, (Has-Style $style 0x00000020))

    $original = Invoke-Ui @('inspect', 'Echo overlay preview', '-w', "$($overlayWindow.hwnd)")
    $translation = Invoke-Ui @('inspect', '双语悬浮字幕预览', '-w', "$($overlayWindow.hwnd)")
    Add-Result 'Synthetic original text is visible to UI Automation' ($original.Contains('Echo overlay preview')) $original.Trim()
    Add-Result 'Synthetic translated text is visible to UI Automation' ($translation.Contains('双语悬浮字幕预览')) $translation.Trim()

    Invoke-Ui @('screenshot', '-w', "$($overlayWindow.hwnd)", '--capture-screen', '--json', '-o', $ScreenshotPath) | Out-Null
    Add-Result 'Overlay-only screenshot captured' (Test-Path -LiteralPath $ScreenshotPath) $ScreenshotPath
}
catch {
    Add-Result 'Transparent subtitle overlay UI smoke' $false "$($_.Exception.Message)"
}
finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
}

$results | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
