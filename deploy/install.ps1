# VideoForensics Installer Bootstrap
#
# This script fetches the latest VideoForensics release for the chosen channel at runtime
# and launches the installer. No version pinning — always gets the current build.
#
# Usage:
#   # Interactive (default)
#   powershell -NoProfile -Command "iwr https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 -UseBasicParsing | iex"
#
#   # Silent (auto-install)
#   powershell -NoProfile -Command "&([scriptblock]::Create((iwr https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 -UseBasicParsing).Content)) -Channel Stable -Silent"
#
#   # Testing channel (rolling prerelease, built from the `dev` branch)
#   powershell -NoProfile -Command "&([scriptblock]::Create((iwr https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 -UseBasicParsing).Content)) -Channel Testing"

param(
    [ValidateSet("Stable", "Testing")]
    [string]$Channel = "Stable",

    [switch]$Silent
)

$ErrorActionPreference = "Stop"

function Write-Progress {
    param([string]$Message)
    Write-Host ">>> $Message" -ForegroundColor Cyan
}

function Write-Error {
    param([string]$Message)
    Write-Host "!!! ERROR: $Message" -ForegroundColor Red
}

try {
    # Determine the API endpoint based on channel
    if ($Channel -eq "Testing") {
        $ApiUrl = "https://api.github.com/repos/rsperry79/VideoForensics/releases/tags/testing"
        Write-Progress "Fetching latest Testing release from GitHub"
    }
    else {
        $ApiUrl = "https://api.github.com/repos/rsperry79/VideoForensics/releases/latest"
        Write-Progress "Fetching latest Stable release from GitHub"
    }

    # Call GitHub Releases API with User-Agent (required by GitHub)
    $headers = @{
        "User-Agent" = "VideoForensics-Installer"
    }

    $release = Invoke-RestMethod -Uri $ApiUrl -Headers $headers -UseBasicParsing

    # Extract version from tag
    $version = $release.tag_name
    if ($version.StartsWith("v")) {
        $version = $version.Substring(1)
    }

    Write-Progress "Found release: $($release.tag_name) ($version)"

    # Find the installer asset
    $installerAsset = $release.assets | Where-Object { $_.name -eq "VideoForensicsSetup.exe" }

    if (-not $installerAsset) {
        Write-Error "VideoForensicsSetup.exe not found in release assets"
        exit 1
    }

    # Download to temp path
    $tempPath = Join-Path $env:TEMP "VideoForensicsSetup.exe"
    Write-Progress "Downloading installer to $tempPath"

    Invoke-WebRequest -Uri $installerAsset.browser_download_url -OutFile $tempPath -UseBasicParsing

    Write-Progress "Download complete ($([math]::Round(((Get-Item $tempPath).Length / 1MB), 2)) MB)"

    # Launch the installer
    if ($Silent) {
        Write-Progress "Launching installer in silent mode (/VERYSILENT)"
        & $tempPath /VERYSILENT
    }
    else {
        Write-Progress "Launching installer (interactive)"
        & $tempPath
    }

    Write-Progress "Installer launched successfully"
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
