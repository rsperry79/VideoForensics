#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Uninstalls the VideoForensics Windows Service.

.DESCRIPTION
    Stops the VideoForensics service if it is running and removes it from the
    system. The application data directory (%ProgramData%\VideoForensics\) is
    left untouched and must be removed manually if desired.

    To also remove the installed binary files from %ProgramFiles%\VideoForensics\,
    specify the -RemoveInstallPath switch.

.PARAMETER RemoveInstallPath
    If specified, also removes the installed application files from
    $env:ProgramFiles\VideoForensics\. Default: leave the files in place.

.EXAMPLE
    .\uninstall-service.ps1
    # Removes the service, leaves data and binaries in place

.EXAMPLE
    .\uninstall-service.ps1 -RemoveInstallPath
    # Removes the service AND the installed binaries, leaves data in place

.NOTES
    Requires administrator privileges.
    The data directory %ProgramData%\VideoForensics\ (database, logs, encryption keys)
    is never automatically deleted, regardless of the -RemoveInstallPath switch.
#>

param(
    [Parameter(Mandatory = $false)]
    [switch]$RemoveInstallPath = $false
)

$ErrorActionPreference = "Stop"

Write-Host "VideoForensics Service Uninstallation" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$installPath = "$env:ProgramFiles\VideoForensics"
$dataPath = "$env:ProgramData\VideoForensics"

# Check if service exists
Write-Host "Checking for VideoForensics service..."
$service = Get-Service -Name "VideoForensics" -ErrorAction SilentlyContinue

if ($null -eq $service) {
    Write-Host "  Service not found (already removed or not installed)" -ForegroundColor Yellow
}
else {
    Write-Host "  Service found"

    # Stop the service if running
    if ($service.Status -eq "Running") {
        Write-Host "  Stopping service..."
        Stop-Service -Name "VideoForensics" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "  Service stopped" -ForegroundColor Green
    }
    else {
        Write-Host "  Service is not running (status: $($service.Status))" -ForegroundColor Green
    }

    # Remove the service
    Write-Host "  Removing service..."
    $psVersion = $PSVersionTable.PSVersion.Major
    if ($psVersion -ge 6) {
        # PowerShell 6+ has Remove-Service
        Remove-Service -Name "VideoForensics" -Force -ErrorAction Stop
        Write-Host "  Service removed with Remove-Service" -ForegroundColor Green
    }
    else {
        # PowerShell 5.1 and earlier: use sc.exe
        & sc.exe delete "VideoForensics" | Out-Null
        Start-Sleep -Seconds 1
        Write-Host "  Service removed with sc.exe" -ForegroundColor Green
    }
}
Write-Host ""

# Remove install path if requested
if ($RemoveInstallPath) {
    Write-Host "Removing installed binaries..."
    if (Test-Path -Path $installPath -PathType Container) {
        Remove-Item -Path $installPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Installed files removed from $installPath" -ForegroundColor Green
    }
    else {
        Write-Host "  Install path not found (already removed)" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Important warning about data directory
Write-Host "Data Directory Notice" -ForegroundColor Yellow
Write-Host "=====================" -ForegroundColor Yellow
Write-Host ""
Write-Host "The data directory has been left in place for safety:" -ForegroundColor Yellow
Write-Host "  Location: $dataPath"
Write-Host ""
Write-Host "This directory contains:" -ForegroundColor Yellow
Write-Host "  - Database file (VideoForensics.db)"
Write-Host "  - Application settings"
Write-Host "  - Encryption keys (Data Protection API)"
Write-Host "  - Log files"
Write-Host ""
Write-Host "To completely remove VideoForensics, you must manually delete this directory if desired." -ForegroundColor Yellow
Write-Host "This is never done automatically to prevent accidental data loss." -ForegroundColor Yellow
Write-Host ""

if ($RemoveInstallPath) {
    Write-Host "Uninstallation complete." -ForegroundColor Green
    Write-Host "Service and binaries removed. Data directory left at: $dataPath" -ForegroundColor Green
}
else {
    Write-Host "Uninstallation complete." -ForegroundColor Green
    Write-Host "Service removed. Binaries left at: $installPath" -ForegroundColor Green
    Write-Host "Data directory left at: $dataPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Tip: Run with -RemoveInstallPath to also remove the installed binaries." -ForegroundColor Cyan
}
Write-Host ""
