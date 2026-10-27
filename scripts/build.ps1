[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$DotnetPath = 'dotnet',
    [string]$PackageSource
)

. (Join-Path $PSScriptRoot 'Common.ps1')
$script:Dotnet = Resolve-DotNet $DotnetPath
$restore = @(Get-RestoreArguments $PackageSource)
# Restore both projects before building; a clean checkout has no obj folders.
Invoke-DotNet -Arguments (@('restore', $ApplicationProject) + $restore)
Invoke-DotNet -Arguments (@('restore', $TestProject) + $restore)
Invoke-DotNet -Arguments @('build', (Join-Path $RepositoryRoot 'TopMostTool.sln'), '-c', $Configuration,
    '--no-restore', '-p:UseSharedCompilation=false', '--nologo')
