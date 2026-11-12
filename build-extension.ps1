[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string[]]$Platforms = @('x64', 'ARM64'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts'),
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$Version = '0.1.6.0',
    [switch]$Development,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Only build local, unsigned packages; never sign, install or publish.
if (-not $Development -and (-not $IdentityName -or -not $Publisher -or -not $PublisherDisplayName)) {
    throw 'Supply the three exact Partner Center identity values, or use -Development for a local test build.'
}
if (-not $Development -and ($IdentityName -match 'Development|PLACEHOLDER' -or $Publisher -match 'Development|PLACEHOLDER')) {
    throw 'A development identity cannot be used for a Store candidate.'
}
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
if ($env:DOTNET_ROOT -and (Test-Path (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) {
    $dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
}
$makeappx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if (-not $makeappx) {
    $sdkTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($sdkTool) { $makeappx = Get-Command $sdkTool.FullName }
}
if (-not $makeappx) { throw 'makeappx.exe is required. Install it with: scoop install windows-sdk-buildtools' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$buildRoot = Join-Path $PSScriptRoot ('work\build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildRoot, $OutputDirectory -Force | Out-Null
$project = Join-Path $PSScriptRoot 'src\contact-palette\contact-palette.csproj'
Push-Location $PSScriptRoot
try {
    & $dotnet run --project 'tests/contact-palette-tests/contact-palette-tests.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Contact tests failed.' }
    $packageDir = Join-Path $buildRoot 'packages'
    New-Item -ItemType Directory -Path $packageDir | Out-Null
    foreach ($platform in $Platforms) {
        $architecture = $platform.ToLowerInvariant()
        $stage = Join-Path $buildRoot $architecture
        & $dotnet publish $project -c Release "-p:Platform=$platform" -p:EnableMsixTooling=false --self-contained true -o $stage
        if ($LASTEXITCODE -ne 0) { throw "$platform publish failed." }
        Copy-Item (Join-Path $PSScriptRoot 'LICENSE') $stage
        Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $stage
        [xml]$manifest = Get-Content (Join-Path $PSScriptRoot 'src\contact-palette\Package.appxmanifest') -Raw
        $manifest.Package.Identity.SetAttribute('Version', $Version)
        $manifest.Package.Identity.SetAttribute('ProcessorArchitecture', $architecture)
        if (-not $Development) {
            $manifest.Package.Identity.SetAttribute('Name', $IdentityName)
            $manifest.Package.Identity.SetAttribute('Publisher', $Publisher)
            $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
        }
        $app = $manifest.Package.Applications.Application
        $app.SetAttribute('Executable', 'contact-palette.exe')
        $app.SetAttribute('EntryPoint', 'Windows.FullTrustApplication')
        $manifest.Package.Resources.Resource.SetAttribute('Language', 'en-US')
        $manifest.Save((Join-Path $stage 'AppxManifest.xml'))
        New-Item -ItemType Directory -Path (Join-Path $stage 'Public') -Force | Out-Null
        $package = Join-Path $packageDir "ContactPalette-$Version-$architecture.msix"
        $packLog = Join-Path $buildRoot "$architecture-pack.log"
        & $makeappx.Source pack /d $stage /p $package /h SHA256 /o *> $packLog
        if ($LASTEXITCODE -ne 0) { Get-Content $packLog -Tail 30; throw "$platform MSIX validation/packaging failed." }
        Write-Host "PASS: $platform MSIX validation and packaging"
    }
    $suffix = if ($Development) { '-development-unsigned' } else { '-store-unsigned' }
    $bundle = Join-Path $OutputDirectory "ContactPalette-$Version$suffix.msixbundle"
    $bundleLog = Join-Path $buildRoot 'bundle.log'
    & $makeappx.Source bundle /d $packageDir /p $bundle /bv $Version /o *> $bundleLog
    if ($LASTEXITCODE -ne 0) { Get-Content $bundleLog -Tail 30; throw 'MSIX bundle creation failed.' }
    Get-ChildItem $packageDir -Filter '*.msix' | Copy-Item -Destination $OutputDirectory -Force
    Get-ChildItem $OutputDirectory -File | Where-Object Extension -in '.msix','.msixbundle' | Get-FileHash -Algorithm SHA256 |
        Select-Object @{Name='File';Expression={Split-Path $_.Path -Leaf}},Hash |
        ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'SHA256SUMS.json') -Encoding utf8
    Write-Host "Unsigned local build completed: $bundle"
    [pscustomobject]@{ BuildRoot=$buildRoot; Bundle=$bundle; Platforms=$Platforms }
} finally { Pop-Location }
