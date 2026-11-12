[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Local development only. Enable Windows Developer Mode yourself before running.
$existing = Get-AppxPackage -Name ContactPalette.Development
[xml]$sourceManifest = Get-Content (Join-Path $PSScriptRoot 'src\contact-palette\Package.appxmanifest') -Raw
$buildVersion = [version]$sourceManifest.Package.Identity.Version
if ($existing -and [version]$existing.Version -ge $buildVersion) {
    # Windows rejects re-registering the same development version at a new path.
    $current = [version]$existing.Version
    $buildVersion = [version]::new($current.Major, $current.Minor, $current.Build + 1, 0)
}
$build = & (Join-Path $PSScriptRoot 'build-extension.ps1') -Development -Platforms x64 -Version $buildVersion.ToString() |
    Where-Object { $_ -is [pscustomobject] -and $_.PSObject.Properties.Name -contains 'BuildRoot' } |
    Select-Object -Last 1
if (-not $build) { throw 'No successful local build was produced.' }
$manifest = Join-Path $build.BuildRoot 'x64\AppxManifest.xml'
$existing = Get-AppxPackage -Name ContactPalette.Development
$palettePackage = Get-AppxPackage -Name Microsoft.CommandPalette
$paletteProcess = Get-Process -Name Microsoft.CmdPal.UI -ErrorAction SilentlyContinue
$restartPalette = $paletteProcess -and $palettePackage
if ($restartPalette) {
    $paletteExe = Join-Path $palettePackage.InstallLocation 'Microsoft.CmdPal.UI.exe'
    $paletteProcess | Where-Object { $_.Path -eq $paletteExe } | Stop-Process -Force
}
if ($existing) {
    $oldExe = Join-Path $existing.InstallLocation 'contact-palette.exe'
    Get-Process -Name contact-palette -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $oldExe } | Stop-Process -Force
}
try {
    Add-AppxPackage -Register -Path $manifest -ForceApplicationShutdown -ErrorAction Stop
} finally {
    if ($restartPalette) { Start-Process -FilePath $paletteExe -WindowStyle Hidden }
}
Write-Host 'Contact Palette is registered for this Windows user.'
Write-Host 'Open Command Palette and choose Search Thunderbird contacts. The host was restarted to clear old version commands.'
Write-Host "Keep this deployment folder while installed: $($build.BuildRoot)"
Write-Host 'Uninstall only this test package with: Get-AppxPackage -Name ContactPalette.Development | Remove-AppxPackage'
