<#
.SYNOPSIS
Stages the Debug build of the VideoForensics WebApp for the installed service. Visual Studio runs it after each Debug build.

.DESCRIPTION
Publishes the build that was just made, without rebuilding, then stages the publish folder with stage-update.ps1. The
running service applies the staged package and restarts. Nothing is staged when the service is not installed.

.PARAMETER ProjectPath
The WebApp project file.

.PARAMETER Configuration
Build configuration. Debug is the only one Visual Studio stages.

.PARAMETER ServiceName
The Windows service that runs the WebApp.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$ServiceName = 'VideoForensics'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "Service $ServiceName is not installed. Nothing staged."
    exit 0
}

$publishDir = Join-Path (Split-Path -Parent $ProjectPath) "bin\$Configuration\net10.0\publish"
$stageScript = Join-Path $PSScriptRoot 'stage-update.ps1'

# Framework-dependent, from the build that was just made. A plain build does not copy wwwroot, so the publish is needed.
Write-Host "Publishing the $Configuration build to $publishDir without rebuilding"
dotnet publish $ProjectPath -c $Configuration --no-build -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish --no-build failed with exit code $LASTEXITCODE." }

Write-Host "Staging the $Configuration build for service $ServiceName"
& $stageScript -PublishDir $publishDir -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "stage-update.ps1 failed with exit code $LASTEXITCODE. If access was denied, run deploy\windows\enable-dev-updates.ps1 once as administrator."
}
