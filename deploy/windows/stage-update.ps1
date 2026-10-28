<#
.SYNOPSIS
    Stages the current source as an update package for the installed VideoForensics service. No administrator rights needed.

.DESCRIPTION
    Publishes VideoForensics.WebApp (with -Build) or takes an existing publish folder, builds manifest.json, and places the
    package in %ProgramData%\VideoForensics\updates\<version>. The running service notices the package, restarts, and
    applies it (see StagedUpdateWatcher / StartupUpdateApplier). Replaces hotswap-service.ps1 for the dev loop.

    Requires enable-dev-updates.ps1 to have been run once as administrator. That turns updates on for the service and
    lets your account write to the updates folder. Without it, this script fails with an access error.

    The package is assembled in a dot-prefixed folder and renamed into place last, so the service never sees a half-written one.

    DATA SAFETY: only the updates folder is written. Nothing under %ProgramData%\VideoForensics outside updates\ is touched.

.PARAMETER PublishDir
    An already-published VideoForensics.WebApp output. Defaults to the standard local publish path.

.PARAMETER Build
    Run `dotnet publish` first.

.PARAMETER UpdatesRoot
    Where packages are staged. Defaults to %ProgramData%\VideoForensics\updates.
#>
[CmdletBinding()]
param(
    [string]$PublishDir,

    [switch]$Build,

    [string]$UpdatesRoot = (Join-Path $env:ProgramData 'VideoForensics\updates')
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
. (Join-Path $PSScriptRoot 'stage-update-helpers.ps1')

if (-not $PublishDir) {
    $PublishDir = Join-Path $RepoRoot 'src\client\web\VideoForensics.WebApp\bin\Release\net10.0\win-x64\publish'
}

try {
    if ($Build) {
        Write-Host '>>> Publishing VideoForensics.WebApp (win-x64, self-contained)...' -ForegroundColor Cyan
        dotnet publish (Join-Path $RepoRoot 'src\client\web\VideoForensics.WebApp\src\VideoForensics.Client.WebApp.csproj') `
            -c Release -r win-x64 --self-contained true -o $PublishDir
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }
    }

    $exe = Join-Path $PublishDir 'VideoForensics.Client.WebApp.exe'
    if (-not (Test-Path $exe)) {
        throw "No published WebApp at $PublishDir (pass -Build to publish first, or -PublishDir to point at one)"
    }

    $version = (Get-Item $exe).VersionInfo.ProductVersion
    if (-not $version -or -not (Test-UpdateVersionName $version)) {
        throw "Cannot use product version '$version' as a package folder name"
    }

    $staging = Join-Path $UpdatesRoot ".staging\$([guid]::NewGuid().ToString('N'))"
    Write-Host ">>> Staging $version ..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -Path (Join-Path $PublishDir '*') -Destination $staging -Recurse -Force

    # The manifest is written last: its presence is what marks the package complete.
    New-UpdateManifest -PackageDir $staging -Version $version | Set-Content -Path (Join-Path $staging 'manifest.json') -Encoding utf8

    $final = Join-Path $UpdatesRoot $version
    if (Test-Path $final) {
        Remove-Item -Path $final -Recurse -Force
    }
    Move-Item -Path $staging -Destination $final

    Write-Host ">>> Staged at $final. The service applies it and restarts within a few seconds." -ForegroundColor Green
}
catch {
    Write-Host "!!! $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
