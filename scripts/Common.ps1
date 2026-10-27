Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ApplicationProject = Join-Path $RepositoryRoot 'src\TopMostTool\TopMostTool.csproj'
$TestProject = Join-Path $RepositoryRoot 'tests\TopMostTool.Tests\TopMostTool.Tests.csproj'

function Resolve-DotNet {
    param([string]$DotnetPath = 'dotnet')
    $command = Get-Command $DotnetPath -ErrorAction Stop
    $sdk = & $command.Source --version
    if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^8\.') {
        throw 'A .NET 8 SDK is required. Set -DotnetPath to an installed SDK executable.'
    }
    return $command.Source
}

function Invoke-DotNet {
    param([string[]]$Arguments)
    & $script:Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE." }
}

function Get-RestoreArguments {
    param([string]$PackageSource)
    $arguments = @('-p:NuGetAudit=false', '--nologo')
    if ($PackageSource) { $arguments += @('--source', $PackageSource) }
    return $arguments
}

function Get-ProductVersion {
    $project = [xml](Get-Content -LiteralPath $ApplicationProject -Raw -Encoding UTF8)
    return [string]$project.Project.PropertyGroup.Version
}

function Get-TestAssembly {
    param([string]$Configuration)
    return Join-Path $RepositoryRoot "tests\TopMostTool.Tests\bin\$Configuration\net8.0-windows\TopMostTool.Tests.dll"
}
