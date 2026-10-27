[CmdletBinding()]
param(
    [string]$DotnetPath = 'dotnet',
    [string]$PackageSource
)

. (Join-Path $PSScriptRoot 'Common.ps1')
$script:Dotnet = Resolve-DotNet $DotnetPath
$version = Get-ProductVersion
$releaseRoot = Join-Path $RepositoryRoot "artifacts\release\v$version"
$staging = Join-Path $RepositoryRoot 'artifacts\publish\staging-win-x64'
$restore = @(Get-RestoreArguments $PackageSource)
Invoke-DotNet -Arguments (@('restore', $ApplicationProject, '-r', 'win-x64',
    '-p:PublishSingleFile=true', '-p:SelfContained=true') + $restore)
Invoke-DotNet -Arguments (@('restore', $TestProject) + $restore)
Invoke-DotNet -Arguments @('build', $TestProject, '-c', 'Release', '--no-restore',
    '-p:UseSharedCompilation=false', '--nologo')
Invoke-DotNet -Arguments @('publish', $ApplicationProject, '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'true', '--no-restore', '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugSymbols=false', '-p:DebugType=None', '-p:UseSharedCompilation=false',
    '--nologo', '-o', $staging)

# Validate the staged executable before packaging or touching a formal copy.
$assembly = Get-TestAssembly 'Release'
Invoke-DotNet -Arguments @($assembly, '--published', (Join-Path $staging 'TopMostTool.exe'))
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$portable = Join-Path $releaseRoot 'portable'
New-Item -ItemType Directory -Path $portable -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $staging 'TopMostTool.exe') -Destination (Join-Path $releaseRoot 'TopMostTool.exe') -Force
Copy-Item -LiteralPath (Join-Path $staging 'TopMostTool.exe') -Destination (Join-Path $portable 'TopMostTool.exe') -Force
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'LICENSE') -Destination (Join-Path $portable 'LICENSE.txt') -Force
$readme = @"
TopMostTool $version - Windows 11 x64

Run TopMostTool.exe. No installation or separate .NET runtime is required.
Ctrl + Space: toggle the foreground window's always-on-top state.
Ctrl + Shift + Space: remove always-on-top from the foreground window.
Ctrl + Shift + S: capture a region and pin it as an image.
Right-click the tray icon for settings, window management and Exit.
Save images before closing them or exiting. Unsaved images are not restored.
Exit an older instance through its tray menu before replacing the executable.

Source and documentation: https://github.com/shimushichen/TopMostTool
License: MIT (see LICENSE.txt).
"@
Set-Content -LiteralPath (Join-Path $portable 'README.txt') -Value $readme -Encoding UTF8
$zipName = "TopMostTool-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $releaseRoot $zipName) -Force
$checksums = @('TopMostTool.exe', $zipName) | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $releaseRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant(), $_
}
Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Value $checksums -Encoding ASCII
$notes = Get-Content -LiteralPath (Join-Path $RepositoryRoot "docs\releases\v$version.md") -Raw -Encoding UTF8
$notes += "`n`n## SHA-256`n`n" + '```text' + "`n" + ($checksums -join "`n") + "`n" + '```' + "`n"
Set-Content -LiteralPath (Join-Path $releaseRoot 'RELEASE_NOTES.md') -Value $notes -Encoding UTF8
Write-Host "Validated release assets: $releaseRoot"
