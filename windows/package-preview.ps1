param(
    [Parameter(Mandatory = $true)] [string] $BuildOutputPath,
    [Parameter(Mandatory = $true)] [string] $OutputPath,
    [Parameter(Mandatory = $true)] [string] $SignerThumbprint
)

$ErrorActionPreference = 'Stop'

$buildRoot = (Resolve-Path -LiteralPath $BuildOutputPath).Path
$baseAppX = Join-Path $buildRoot 'AppX'
$manifestPath = Join-Path $buildRoot 'AppxManifest.xml'
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputFullPath)

if (-not (Test-Path -LiteralPath $baseAppX -PathType Container)) { throw "AppX metadata/assets not found: $baseAppX" }
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Fresh build manifest not found: $manifestPath" }
if (Test-Path -LiteralPath $outputFullPath) { throw "Refusing to overwrite an existing package: $outputFullPath" }

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$identity = $manifest.Package.Identity
if (-not $identity.Name -or -not $identity.Publisher -or -not $identity.Version) { throw 'Fresh build manifest is missing package identity fields.' }

$signer = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $SignerThumbprint } | Select-Object -First 1
if (-not $signer -or -not $signer.HasPrivateKey) { throw "Signing certificate with a private key was not found in CurrentUser\My: $SignerThumbprint" }
if ($signer.Subject -ne $identity.Publisher) { throw "Signer subject '$($signer.Subject)' does not match manifest publisher '$($identity.Publisher)'." }

$toolPackageRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
if (-not (Test-Path -LiteralPath $toolPackageRoot)) { throw "Windows SDK Build Tools NuGet package not found: $toolPackageRoot" }
$sdkTools = $null
foreach ($packageVersion in (Get-ChildItem -LiteralPath $toolPackageRoot -Directory | Sort-Object { [version]$_.Name } -Descending)) {
    $binRoot = Join-Path $packageVersion.FullName 'bin'
    if (-not (Test-Path -LiteralPath $binRoot)) { continue }
    foreach ($sdkVersion in (Get-ChildItem -LiteralPath $binRoot -Directory | Sort-Object { [version]$_.Name } -Descending)) {
        $candidate = Join-Path $sdkVersion.FullName 'x64'
        if ((Test-Path (Join-Path $candidate 'makeappx.exe')) -and (Test-Path (Join-Path $candidate 'signtool.exe'))) {
            $sdkTools = $candidate
            break
        }
    }
    if ($sdkTools) { break }
}
if (-not $sdkTools) { throw 'MakeAppx.exe and SignTool.exe were not found in the Windows SDK Build Tools packages.' }

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Echo-MsixStage-' + [guid]::NewGuid().ToString('N'))
$verifyRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Echo-MsixVerify-' + [guid]::NewGuid().ToString('N'))
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'

try {
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    Copy-Item -Path (Join-Path $baseAppX '*') -Destination $stageRoot -Recurse -Force

    # AppX may have been prepared by an earlier development registration.
    # Overlay fresh Release output so the package cannot silently use stale binaries.
    Get-ChildItem -LiteralPath $buildRoot -Force |
        Where-Object { $_.Name -ne 'AppX' -and $_.Name -ne 'Echo.Windows.pdb' -and $_.Name -ne 'Echo.Windows.build.appxrecipe' } |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stageRoot -Recurse -Force }

    [xml]$stagedManifest = Get-Content -LiteralPath (Join-Path $stageRoot 'AppxManifest.xml') -Raw
    if ($stagedManifest.Package.Identity.Name -ne $identity.Name -or
        $stagedManifest.Package.Identity.Publisher -ne $identity.Publisher -or
        $stagedManifest.Package.Identity.Version -ne $identity.Version) {
        throw 'Staged package identity does not match the fresh Release build manifest.'
    }
    foreach ($payloadName in @('Echo.Windows.exe', 'Echo.Windows.dll')) {
        $builtHash = (Get-FileHash -LiteralPath (Join-Path $buildRoot $payloadName) -Algorithm SHA256).Hash
        $stagedHash = (Get-FileHash -LiteralPath (Join-Path $stageRoot $payloadName) -Algorithm SHA256).Hash
        if ($builtHash -ne $stagedHash) { throw "Staged payload does not match fresh Release output: $payloadName" }
    }

    $packOutput = & (Join-Path $sdkTools 'makeappx.exe') pack /d $stageRoot /p $outputFullPath 2>&1
    if ($LASTEXITCODE -ne 0) { $packOutput | Select-Object -Last 20; throw "MakeAppx failed with exit code $LASTEXITCODE." }

    $signOutput = & (Join-Path $sdkTools 'signtool.exe') sign /fd SHA256 /sha1 $SignerThumbprint $outputFullPath 2>&1
    if ($LASTEXITCODE -ne 0) { $signOutput | Select-Object -Last 20; throw "SignTool failed with exit code $LASTEXITCODE." }

    $signature = Get-AuthenticodeSignature -FilePath $outputFullPath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $SignerThumbprint) {
        throw 'The resulting package does not report the requested signing certificate.'
    }

    $unpackOutput = & (Join-Path $sdkTools 'makeappx.exe') unpack /p $outputFullPath /d $verifyRoot 2>&1
    if ($LASTEXITCODE -ne 0) { $unpackOutput | Select-Object -Last 20; throw "MakeAppx could not unpack the signed package (exit code $LASTEXITCODE)." }
    [xml]$verifiedManifest = Get-Content -LiteralPath (Join-Path $verifyRoot 'AppxManifest.xml') -Raw
    if ($verifiedManifest.Package.Identity.Name -ne $identity.Name -or
        $verifiedManifest.Package.Identity.Publisher -ne $identity.Publisher -or
        $verifiedManifest.Package.Identity.Version -ne $identity.Version) {
        throw 'The signed package manifest does not match the fresh Release build.'
    }
    foreach ($payloadName in @('Echo.Windows.exe', 'Echo.Windows.dll')) {
        $builtHash = (Get-FileHash -LiteralPath (Join-Path $buildRoot $payloadName) -Algorithm SHA256).Hash
        $packedHash = (Get-FileHash -LiteralPath (Join-Path $verifyRoot $payloadName) -Algorithm SHA256).Hash
        if ($builtHash -ne $packedHash) { throw "Signed package payload does not match fresh Release output: $payloadName" }
    }

    $packageFile = Get-Item -LiteralPath $outputFullPath
    [pscustomobject]@{
        Path = $packageFile.FullName
        Bytes = $packageFile.Length
        SHA256 = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash
        Identity = $identity.Name
        Publisher = $identity.Publisher
        Version = $identity.Version
        Architecture = $identity.ProcessorArchitecture
        PayloadMatchesBuild = $true
        SignatureStatus = $signature.Status
        SignerThumbprint = $signature.SignerCertificate.Thumbprint
    }
}
finally {
    foreach ($temporaryPath in @($stageRoot, $verifyRoot)) {
        $temporaryFullPath = [System.IO.Path]::GetFullPath($temporaryPath)
        $leaf = Split-Path -Leaf $temporaryFullPath
        if ($temporaryFullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            ($leaf -like 'Echo-MsixStage-*' -or $leaf -like 'Echo-MsixVerify-*') -and
            (Test-Path -LiteralPath $temporaryFullPath -PathType Container)) {
            [System.IO.Directory]::Delete($temporaryFullPath, $true)
        }
    }
}
