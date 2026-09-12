#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs the VideoForensics web application as a Windows Service.

.DESCRIPTION
    Publishes or accepts a pre-published VideoForensics.WebApp, copies it to the
    installation directory, creates the Windows Event Log source, installs the service,
    configures failure recovery, and starts the service.

    This script is re-runnable and safe for upgrades: if the service already exists,
    it is stopped and removed before reinstalling.

.PARAMETER PublishedPath
    Path to the published VideoForensics.WebApp output (containing VideoForensics.WebApp.exe).
    Defaults to: $PSScriptRoot\..\src\client\web\VideoForensics.WebApp\bin\Release\net10.0\publish

.PARAMETER InstallPath
    Destination directory for the installed service files.
    Defaults to: $env:ProgramFiles\VideoForensics

.EXAMPLE
    .\install-service.ps1
    # Uses default paths

.EXAMPLE
    .\install-service.ps1 -PublishedPath "C:\MyBuild\publish" -InstallPath "D:\Services\VideoForensics"
    # Uses custom paths

.NOTES
    Requires administrator privileges.
    The Windows Event Log source "VideoForensics" will be created if it doesn't already exist.
    Failure recovery is configured to restart the service on crash.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$PublishedPath = "$PSScriptRoot\..\src\client\web\VideoForensics.WebApp\bin\Release\net10.0\publish",

    [Parameter(Mandatory = $false)]
    [string]$InstallPath = "$env:ProgramFiles\VideoForensics"
)

$ErrorActionPreference = "Stop"

Write-Host "VideoForensics Service Installation" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Validate published path exists
Write-Host "Validating published path: $PublishedPath"
if (-not (Test-Path -Path $PublishedPath -PathType Container)) {
    Write-Host "ERROR: Published path not found: $PublishedPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run: dotnet publish src/client/web/VideoForensics.WebApp -c Release --self-contained false"
    Write-Host "Or specify a custom -PublishedPath parameter."
    exit 1
}

if (-not (Test-Path -Path "$PublishedPath\VideoForensics.WebApp.exe")) {
    Write-Host "ERROR: VideoForensics.WebApp.exe not found in $PublishedPath" -ForegroundColor Red
    exit 1
}

Write-Host "Published path OK" -ForegroundColor Green
Write-Host ""

# Check if service already exists and remove it if so
Write-Host "Checking for existing service..."
$existingService = Get-Service -Name "VideoForensics" -ErrorAction SilentlyContinue

if ($null -ne $existingService) {
    Write-Host "  Existing service found, stopping and removing..."
    Stop-Service -Name "VideoForensics" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    # Try Remove-Service first (PowerShell 6+), fall back to sc.exe on older versions
    $psVersion = $PSVersionTable.PSVersion.Major
    if ($psVersion -ge 6) {
        Remove-Service -Name "VideoForensics" -Force -ErrorAction SilentlyContinue
        Write-Host "  Service removed with Remove-Service" -ForegroundColor Green
    }
    else {
        & sc.exe delete "VideoForensics" | Out-Null
        Start-Sleep -Seconds 1
        Write-Host "  Service removed with sc.exe" -ForegroundColor Green
    }
}
else {
    Write-Host "  No existing service to remove" -ForegroundColor Green
}
Write-Host ""

# Create install directory if needed
Write-Host "Creating/preparing install directory: $InstallPath"
if (-not (Test-Path -Path $InstallPath -PathType Container)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Write-Host "  Created new directory" -ForegroundColor Green
}
else {
    Write-Host "  Directory exists, will overwrite files" -ForegroundColor Green
}

# Copy published files to install path
Write-Host "Copying published files..."
Copy-Item -Path "$PublishedPath\*" -Destination $InstallPath -Recurse -Force | Out-Null
Write-Host "  Files copied successfully" -ForegroundColor Green
Write-Host ""

# Create Windows Event Log source if needed
Write-Host "Configuring Windows Event Log..."
$eventSourceName = "VideoForensics"
$eventLogName = "Application"

if ([System.Diagnostics.EventLog]::SourceExists($eventSourceName)) {
    Write-Host "  Event Log source '$eventSourceName' already exists" -ForegroundColor Green
}
else {
    Write-Host "  Creating Event Log source '$eventSourceName'..."
    New-EventLog -LogName $eventLogName -Source $eventSourceName -ErrorAction Stop | Out-Null
    Write-Host "  Event Log source created" -ForegroundColor Green
}
Write-Host ""

# Install the service
Write-Host "Installing Windows Service..."
$exePath = "$InstallPath\VideoForensics.WebApp.exe"
New-Service -Name "VideoForensics" `
    -BinaryPathName $exePath `
    -DisplayName "VideoForensics" `
    -Description "VideoForensics web application and API server" `
    -StartupType Automatic `
    -ErrorAction Stop | Out-Null

Write-Host "  Service installed successfully" -ForegroundColor Green
Write-Host ""

# Configure failure recovery (requires sc.exe, New-Service has no recovery options parameter)
Write-Host "Configuring service failure recovery..."
& sc.exe failure "VideoForensics" reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
Write-Host "  Failure recovery configured: restart after 60 seconds (up to 24-hour reset window)" -ForegroundColor Green
Write-Host ""

# Start the service
Write-Host "Starting service..."
Start-Service -Name "VideoForensics" -ErrorAction Stop
Start-Sleep -Seconds 2

$service = Get-Service -Name "VideoForensics"
$status = $service.Status

Write-Host ""
Write-Host "Service started successfully!" -ForegroundColor Green
Write-Host "Status: $status" -ForegroundColor Green
Write-Host ""

Write-Host "Installation Complete" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host "Service Name:  VideoForensics"
Write-Host "Install Path:  $InstallPath"
Write-Host "Startup Type:  Automatic"
Write-Host "Current Status: $status"
Write-Host ""
Write-Host "Event Log entries will appear in: Windows Event Viewer > Windows Logs > Application (Source: VideoForensics)"
Write-Host ""
