[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]+\.[A-Za-z0-9][A-Za-z0-9.-]+$')][string]$PackageIdentifier,
    [Parameter(Mandatory)][string]$Publisher,
    [Parameter(Mandatory)][ValidatePattern('^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$RepositoryUrl,
    [Parameter(Mandatory)][string]$PackageFamilyName,
    [Parameter(Mandatory)][string]$X64Package,
    [Parameter(Mandatory)][string]$Arm64Package,
    [Parameter(Mandatory)][uri]$X64Url,
    [Parameter(Mandatory)][uri]$Arm64Url,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\winget')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Quote-Yaml([string]$value) { ConvertTo-Json $value -Compress }
function Read-Package([string]$path, [string]$architecture, [uri]$url) {
    $path = (Resolve-Path -LiteralPath $path).Path
    if ($url.Scheme -ne 'https') { throw 'Installer URLs must use HTTPS.' }
    if ((Get-AuthenticodeSignature -LiteralPath $path).Status -ne 'Valid') { throw 'WinGet requires a trusted, signed MSIX. Development/unsigned packages cannot be submitted.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $signature = $archive.GetEntry('AppxSignature.p7x')
        if (-not $signature) { throw 'Missing MSIX signature.' }
        $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $identity = $manifest.Package.Identity
        if ($identity.ProcessorArchitecture -ne $architecture) { throw "Expected $architecture package." }
        if ($identity.Name -match 'Development' -or $identity.Publisher -match 'Development') { throw 'Development identity is not publishable.' }
        $stream = $signature.Open()
        try { $signatureHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
        [pscustomobject]@{ Identity=$identity; Hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; SignatureHash=$signatureHash; Url=$url.AbsoluteUri }
    } finally { $archive.Dispose() }
}
$x64 = Read-Package $X64Package 'x64' $X64Url
$arm64 = Read-Package $Arm64Package 'arm64' $Arm64Url
if ($x64.Identity.Name -ne $arm64.Identity.Name -or $x64.Identity.Publisher -ne $arm64.Identity.Publisher -or $x64.Identity.Version -ne $arm64.Identity.Version) { throw 'Package identity/version must match between architectures.' }
if (-not $PackageFamilyName.StartsWith($x64.Identity.Name + '_', [StringComparison]::Ordinal)) { throw 'Package family name does not match package identity.' }
$version = $x64.Identity.Version
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json
PackageIdentifier: $PackageIdentifier
PackageVersion: $version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.12.0
"@ | Set-Content (Join-Path $OutputDirectory "$PackageIdentifier.yaml") -Encoding utf8
@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json
PackageIdentifier: $PackageIdentifier
PackageVersion: $version
PackageLocale: en-US
Publisher: $(Quote-Yaml $Publisher)
PackageName: Contact Palette
License: MIT
LicenseUrl: $RepositoryUrl/blob/main/LICENSE
PackageUrl: $RepositoryUrl
PublisherSupportUrl: $RepositoryUrl/issues
PrivacyUrl: $RepositoryUrl/blob/main/PRIVACY.md
ShortDescription: Search local Thunderbird contacts from PowerToys Command Palette.
Tags:
- windows-commandpalette-extension
- thunderbird
- contacts
ManifestType: defaultLocale
ManifestVersion: 1.12.0
"@ | Set-Content (Join-Path $OutputDirectory "$PackageIdentifier.locale.en-US.yaml") -Encoding utf8
@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json
PackageIdentifier: $PackageIdentifier
PackageVersion: $version
MinimumOSVersion: 10.0.22000.0
InstallerType: msix
PackageFamilyName: $(Quote-Yaml $PackageFamilyName)
UpgradeBehavior: install
Installers:
- Architecture: x64
  InstallerUrl: $(Quote-Yaml $x64.Url)
  InstallerSha256: $($x64.Hash)
  SignatureSha256: $($x64.SignatureHash)
- Architecture: arm64
  InstallerUrl: $(Quote-Yaml $arm64.Url)
  InstallerSha256: $($arm64.Hash)
  SignatureSha256: $($arm64.SignatureHash)
ManifestType: installer
ManifestVersion: 1.12.0
"@ | Set-Content (Join-Path $OutputDirectory "$PackageIdentifier.installer.yaml") -Encoding utf8
Write-Host "Created local manifests in $OutputDirectory. Validate with winget validate --manifest <directory>. No submission performed."
