#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Hot-swaps the installed VideoForensics Windows Service binaries for local dev iteration,
    without a full installer round-trip (no Inno Setup compile, no UAC prompt beyond this
    script's own elevation, no reinstall of the service registration).

.DESCRIPTION
    Stops the VideoForensics service, copies a fresh publish output over the installed binaries
    in %ProgramFiles%\VideoForensics, and restarts the service. Optionally runs `dotnet publish`
    first with -Build.

    DATA SAFETY: this script only ever touches %ProgramFiles%\VideoForensics (binaries). It never
    references, copies into, or deletes anything under %ProgramData%\VideoForensics (the database,
    media, logs, and encryption keys) - do not add such a reference here. See
    deploy/windows/VideoForensics.iss's own top-of-file comment for why that boundary matters.

    Uses a plain newer-file copy (robocopy /E /XO), not /MIR - it will never delete a file already
    present at the destination that isn't in the fresh publish output, so it's safe to run
    repeatedly without accidentally wiping out unrelated installed content.

.PARAMETER PublishDir
    Path to an already-published VideoForensics.WebApp output. Defaults to the standard local
    build output path (bin\Release\net10.0\win-x64\publish) relative to the repo root.

.PARAMETER Build
    Run `dotnet publish` for VideoForensics.WebApp before swapping, so this is a single
    edit-then-run command for local iteration.

.PARAMETER InstallPath
    Where the service is installed. Defaults to %ProgramFiles%\VideoForensics.

.PARAMETER SkipHealthCheck
    Skip the post-restart HTTP health check against http://localhost:5162.

.EXAMPLE
    .\hotswap-service.ps1 -Build
    Publishes the current source and swaps it into the running service.

.EXAMPLE
    .\hotswap-service.ps1 -PublishDir C:\Build\VideoForensics
    Swaps in an already-published output without rebuilding.
#>
[CmdletBinding()]
param(
    [string]$PublishDir,

    [switch]$Build,

    [string]$InstallPath = "$env:ProgramFiles\VideoForensics",

    [switch]$SkipHealthCheck
)

$ErrorActionPreference = "Stop"
$ServiceName = "VideoForensics"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

function Write-Step {
    param([string]$Message)
    Write-Host ">>> $Message" -ForegroundColor Cyan
}

function Write-Failure {
    param([string]$Message)
    Write-Host "!!! $Message" -ForegroundColor Red
}

if (-not $PublishDir) {
    $PublishDir = Join-Path $RepoRoot "src\client\web\VideoForensics.WebApp\bin\Release\net10.0\win-x64\publish"
}

try {
    if ($Build) {
        Write-Step "Publishing VideoForensics.WebApp (win-x64, self-contained)..."
        dotnet publish (Join-Path $RepoRoot "src\client\web\VideoForensics.WebApp") `
            -c Release -r win-x64 --self-contained true -o $PublishDir
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }
    }

    if (-not (Test-Path $PublishDir)) {
        throw "Publish directory not found: $PublishDir (pass -Build to publish first, or -PublishDir to point at an existing one)"
    }

    if (-not (Test-Path $InstallPath)) {
        throw "Install path not found: $InstallPath - is VideoForensics actually installed? Run the installer first for the initial install; this script only hot-swaps an existing one."
    }

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        throw "Service '$ServiceName' not found. This script hot-swaps an already-installed service; run the Inno Setup installer first."
    }

    $wasRunning = $service.Status -eq "Running"
    if ($wasRunning) {
        Write-Step "Stopping $ServiceName service..."
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus("Stopped", (New-TimeSpan -Seconds 30))
    }

    # Wait for the process to actually exit (not just the SCM's view of it) so the exe file isn't
    # locked when we try to overwrite it - Stop-Service can return before the process handle is
    # fully released.
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Process -Name "VideoForensics.WebApp" -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }

    Write-Step "Copying $PublishDir -> $InstallPath (newer files only, nothing else touched or deleted)..."
    $robocopyArgs = @($PublishDir, $InstallPath, "/E", "/XO", "/R:3", "/W:2", "/NFL", "/NDL", "/NP")
    & robocopy @robocopyArgs | Out-Null
    # Robocopy exit codes 0-7 are success/informational; 8+ is a real failure.
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }

    Write-Step "Starting $ServiceName service..."
    Start-Service -Name $ServiceName
    $service.WaitForStatus("Running", (New-TimeSpan -Seconds 30))

    if (-not $SkipHealthCheck) {
        Write-Step "Checking http://localhost:5162 ..."
        $healthy = $false
        $deadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $deadline) {
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:5162" -UseBasicParsing -TimeoutSec 3
                if ($response.StatusCode -eq 200) {
                    $healthy = $true
                    break
                }
            } catch {
                Start-Sleep -Milliseconds 500
            }
        }

        if ($healthy) {
            Write-Step "Hot-swap complete - service is running and responding."
        } else {
            Write-Failure "Service is running but didn't respond on http://localhost:5162 within 20s - check Event Viewer (Application log, Source: VideoForensics)."
            exit 1
        }
    } else {
        Write-Step "Hot-swap complete (health check skipped)."
    }
}
catch {
    Write-Failure $_.Exception.Message
    exit 1
}
