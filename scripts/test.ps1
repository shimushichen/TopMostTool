[CmdletBinding()]
param(
    [ValidateSet('Surface', 'Rendering', 'Menus', 'Badges', 'UI', 'About', 'Full')]
    [string]$Suite = 'Surface',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$DotnetPath = 'dotnet',
    [string]$PackageSource,
    [switch]$NoBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration -DotnetPath $DotnetPath -PackageSource $PackageSource
}
$script:Dotnet = Resolve-DotNet $DotnetPath
$assembly = Get-TestAssembly $Configuration
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build the test project first or omit -NoBuild.' }
$modes = @{
    Surface = '--badge-surface'; Rendering = '--rendering'; Menus = '--menus-shortcuts'
    Badges = '--badges'; UI = '--ui-preview'; About = '--about-interaction'
}
$arguments = @($assembly)
if ($Suite -ne 'Full') { $arguments += $modes[$Suite] }
Invoke-DotNet -Arguments $arguments
