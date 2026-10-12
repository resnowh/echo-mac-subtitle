param(
    [Parameter(Mandatory = $true)] [string] $BuildOutputPath,
    [Parameter(Mandatory = $true)] [string] $OutputPath,
    [string] $TimestampServer = 'https://timestamp.acs.microsoft.com'
)

$ErrorActionPreference = 'Stop'

$encodedPfx = $env:ECHO_WINDOWS_SIGNING_PFX_BASE64
$pfxPassword = $env:ECHO_WINDOWS_SIGNING_PFX_PASSWORD
Remove-Item Env:\ECHO_WINDOWS_SIGNING_PFX_BASE64 -ErrorAction SilentlyContinue
Remove-Item Env:\ECHO_WINDOWS_SIGNING_PFX_PASSWORD -ErrorAction SilentlyContinue
if ([string]::IsNullOrWhiteSpace($encodedPfx) -or [string]::IsNullOrWhiteSpace($pfxPassword)) {
    throw 'Set the WINDOWS_SIGNING_PFX_BASE64 and WINDOWS_SIGNING_PFX_PASSWORD GitHub Actions secrets before dispatching this workflow.'
}

$buildRoot = (Resolve-Path -LiteralPath $BuildOutputPath).Path
$manifestPath = Join-Path $buildRoot 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Fresh build manifest not found: $manifestPath" }
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$publisher = [string]$manifest.Package.Identity.Publisher
if ([string]::IsNullOrWhiteSpace($publisher)) { throw 'Fresh build manifest has no Publisher identity.' }

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
$pfxPath = Join-Path ([System.IO.Path]::GetTempPath()) ('Echo-Windows-Signing-' + [guid]::NewGuid().ToString('N') + '.pfx')
$storePath = 'Cert:\CurrentUser\My'
$preexistingThumbprints = @{}
Get-ChildItem -LiteralPath $storePath | ForEach-Object { $preexistingThumbprints[$_.Thumbprint] = $true }
$importedThumbprints = [System.Collections.Generic.List[string]]::new()
$securePassword = ConvertTo-SecureString -String $pfxPassword -AsPlainText -Force
$pfxPassword = $null
$pfxBytes = $null

try {
    $pfxBytes = [Convert]::FromBase64String($encodedPfx.Trim())
    $encodedPfx = $null
    [System.IO.File]::WriteAllBytes($pfxPath, $pfxBytes)
    [Array]::Clear($pfxBytes, 0, $pfxBytes.Length)
    $pfxBytes = $null

    $imported = @(Import-PfxCertificate -FilePath $pfxPath -Password $securePassword -CertStoreLocation $storePath)
    foreach ($certificate in $imported) {
        if (-not $preexistingThumbprints.ContainsKey($certificate.Thumbprint)) {
            $importedThumbprints.Add($certificate.Thumbprint)
        }
    }

    $signer = $imported |
        Where-Object { $_.HasPrivateKey -and $_.Subject -eq $publisher } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
    if (-not $signer) {
        $subjects = ($imported | ForEach-Object Subject | Sort-Object -Unique) -join '; '
        throw "No private-key certificate matches manifest Publisher '$publisher'. Certificates in PFX: $subjects"
    }
    if ($signer.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or $signer.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) {
        throw 'The publisher certificate is not currently within its validity period.'
    }
    $hasCodeSigningUsage = @($signer.EnhancedKeyUsageList | Where-Object { $_.ObjectId.Value -eq '1.3.6.1.5.5.7.3.3' }).Count -gt 0
    if (-not $hasCodeSigningUsage) { throw 'The publisher certificate does not include the Code Signing extended key usage.' }

    $packageScript = Join-Path $PSScriptRoot 'package-preview.ps1'
    $result = & $packageScript `
        -BuildOutputPath $buildRoot `
        -OutputPath $OutputPath `
        -SignerThumbprint $signer.Thumbprint `
        -TimestampServer $TimestampServer `
        -RequireTrustedSignature
    if ($result.SignatureStatus -ne 'Valid' -or $result.SignerThumbprint -ne $signer.Thumbprint -or
        [string]::IsNullOrWhiteSpace($result.TimestampThumbprint)) {
        throw 'Release candidate verification did not return the expected trusted signer and RFC 3161 timestamp.'
    }
    $result
}
finally {
    if ($pfxBytes) { [Array]::Clear($pfxBytes, 0, $pfxBytes.Length) }
    foreach ($thumbprint in $importedThumbprints) {
        Remove-Item -LiteralPath (Join-Path $storePath $thumbprint) -ErrorAction SilentlyContinue
    }
    $fullPfxPath = [System.IO.Path]::GetFullPath($pfxPath)
    if ($fullPfxPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $fullPfxPath) -like 'Echo-Windows-Signing-*.pfx' -and
        (Test-Path -LiteralPath $fullPfxPath -PathType Leaf)) {
        Remove-Item -LiteralPath $fullPfxPath -Force
    }
    $securePassword.Dispose()
}
