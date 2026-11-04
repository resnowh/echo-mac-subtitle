param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][guid]$TestPackageIdentity,
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$PixelReportPath,
    [Parameter(Mandatory)][string]$ResultsPath,
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
foreach ($path in @($DataRoot, $PixelReportPath)) {
    $resolved = [IO.Path]::GetFullPath($path)
    if (-not $resolved.StartsWith($tempPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'DataRoot and PixelReportPath must be temporary isolated paths.'
    }
}

if (-not ('EchoA50WindowApi' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EchoA50WindowApi {
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW", SetLastError=true)] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
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
function Has-Style([long]$Style, [long]$Flag) { return ($Style -band $Flag) -ne 0 }

try {
    $windows = @(Invoke-Ui @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json)
    $mainWindow = $windows | Where-Object { $_.processId -eq $AppPid -and $_.title -eq 'Echo' } | Select-Object -First 1
    $overlayWindow = $windows | Where-Object {
        $_.processId -eq $AppPid -and $_.className -eq 'Echo.Windows.NativeSubtitleOverlay.1'
    } | Select-Object -First 1
    if (-not $mainWindow) { throw 'Could not find the isolated Echo main window.' }
    if (-not $overlayWindow) { throw 'The native subtitle overlay HWND did not appear.' }

    $overlayHwnd = [IntPtr]::new([long]$overlayWindow.hwnd)
    $mainHwnd = [IntPtr]::new([long]$mainWindow.hwnd)
    $style = [EchoA50WindowApi]::GetWindowLongPtr($overlayHwnd, -20).ToInt64()
    Add-Result 'Overlay is layered, a tool window, and does not activate' (
        (Has-Style $style 0x00080000) -and (Has-Style $style 0x00000080) -and (Has-Style $style 0x08000000)
    ) ("extendedStyle=0x{0:X}" -f $style)

    $aboveMain = $false
    $cursor = $mainHwnd
    for ($index = 0; $index -lt 100 -and $cursor -ne [IntPtr]::Zero; $index++) {
        $cursor = [EchoA50WindowApi]::GetWindow($cursor, 3)
        if ($cursor -eq $overlayHwnd) { $aboveMain = $true; break }
    }
    Add-Result 'Overlay remains above the main window in z-order' $aboveMain 'Compared by adjacent top-level window order.'
    Add-Result 'Overlay is visible' ([EchoA50WindowApi]::IsWindowVisible($overlayHwnd)) 'Native HWND is visible.'
    Add-Result 'Click-through state matches test expectation' ((Has-Style $style 0x00000020) -eq $ExpectedClickThrough) `
        ("expected={0}; actual={1}" -f $ExpectedClickThrough, (Has-Style $style 0x00000020))

    $accessibleName = [string]$overlayWindow.title
    Add-Result 'Synthetic source and translation appear in the accessible window name' (
        $accessibleName.Contains('Echo overlay preview') -and $accessibleName.Contains('双语悬浮字幕预览')
    ) ($accessibleName.Replace("`n", ' | '))

    if (-not (Test-Path -LiteralPath $PixelReportPath)) { throw 'Direct2D pixel report was not produced.' }
    $pixels = Get-Content -LiteralPath $PixelReportPath -Raw | ConvertFrom-Json
    $pixelCountMatches = [long]$pixels.pixelCount -eq ([long]$pixels.width * [long]$pixels.height)
    Add-Result 'Render target has both fully transparent and visible text pixels' (
        $pixelCountMatches -and [long]$pixels.transparentPixelCount -gt 0 -and [long]$pixels.visiblePixelCount -gt 0
    ) ("size={0}x{1}; transparent={2}; visible={3}" -f $pixels.width, $pixels.height, $pixels.transparentPixelCount, $pixels.visiblePixelCount)
    Add-Result 'Direct2D output satisfies premultiplied BGRA alpha' (
        $pixels.alphaMode -eq 'premultiplied-bgra8' -and [long]$pixels.nonPremultipliedPixelCount -eq 0
    ) ("alphaMode={0}; invalidPixels={1}" -f $pixels.alphaMode, $pixels.nonPremultipliedPixelCount)

    Invoke-Ui @('invoke', 'SubtitleOverlayMenu', '-w', "$($mainWindow.hwnd)") | Out-Null
    Invoke-Ui @('invoke', 'AdjustSubtitleOverlay', '-w', "$($mainWindow.hwnd)") | Out-Null
    Start-Sleep -Milliseconds 200
    $adjustedWindows = @(Invoke-Ui @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json)
    $adjustedOverlay = $adjustedWindows | Where-Object {
        $_.processId -eq $AppPid -and $_.className -eq 'Echo.Windows.NativeSubtitleOverlay.1'
    } | Select-Object -First 1
    if (-not $adjustedOverlay) { throw 'Native overlay disappeared in adjustment mode.' }
    $adjustedHwnd = [IntPtr]::new([long]$adjustedOverlay.hwnd)
    $adjustedStyle = [EchoA50WindowApi]::GetWindowLongPtr($adjustedHwnd, -20).ToInt64()
    Add-Result 'Adjustment mode receives pointer input by removing click-through' (-not (Has-Style $adjustedStyle 0x00000020)) `
        ("extendedStyle=0x{0:X}" -f $adjustedStyle)
    $adjustedPixels = Get-Content -LiteralPath $PixelReportPath -Raw | ConvertFrom-Json
    Add-Result 'Adjustment hit-test surface is present across the overlay bounds' (
        [long]$adjustedPixels.visiblePixelCount -eq [long]$adjustedPixels.pixelCount
    ) ("visible={0}/{1}" -f $adjustedPixels.visiblePixelCount, $adjustedPixels.pixelCount)

    Invoke-Ui @('invoke', 'SubtitleOverlayMenu', '-w', "$($mainWindow.hwnd)") | Out-Null
    Invoke-Ui @('invoke', 'AdjustSubtitleOverlay', '-w', "$($mainWindow.hwnd)") | Out-Null
    Start-Sleep -Milliseconds 200
    $restoredWindows = @(Invoke-Ui @('list-windows', '-a', "$AppPid", '--json') | ConvertFrom-Json)
    $restoredOverlay = $restoredWindows | Where-Object {
        $_.processId -eq $AppPid -and $_.className -eq 'Echo.Windows.NativeSubtitleOverlay.1'
    } | Select-Object -First 1
    $restoredStyle = [EchoA50WindowApi]::GetWindowLongPtr([IntPtr]::new([long]$restoredOverlay.hwnd), -20).ToInt64()
    Add-Result 'Leaving adjustment mode restores configured click-through' ((Has-Style $restoredStyle 0x00000020) -eq $ExpectedClickThrough) `
        ("expected={0}; actual={1}" -f $ExpectedClickThrough, (Has-Style $restoredStyle 0x00000020))
}
catch {
    Add-Result 'Native transparent subtitle overlay UI smoke' $false "$($_.Exception.Message)"
}
finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultsPath -Encoding utf8
}

$results | Format-Table name, status, detail -AutoSize
if (@($results | Where-Object status -eq 'FAIL').Count -gt 0) { exit 1 }
