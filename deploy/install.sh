#!/usr/bin/env bash
#
# VideoForensics Installer Bootstrap (Debian/Ubuntu)
#
# This script fetches the latest VideoForensics release for the chosen channel at runtime
# and installs it. No version pinning — always gets the current build.
#
# Usage:
#   # Stable channel (default)
#   curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash
#
#   # Testing channel (rolling prerelease, built from the `dev` branch)
#   curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash -- --channel testing

set -euo pipefail

# Default channel is stable
CHANNEL="stable"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --channel)
            CHANNEL="$2"
            shift 2
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --channel CHANNEL    Release channel: 'stable' (default) or 'testing'"
            echo "  --help               Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Validate channel
if [[ "$CHANNEL" != "stable" && "$CHANNEL" != "testing" ]]; then
    echo "!!! ERROR: Invalid channel '$CHANNEL'. Must be 'stable' or 'testing'."
    exit 1
fi

# Determine the API endpoint based on channel
if [[ "$CHANNEL" == "testing" ]]; then
    API_URL="https://api.github.com/repos/rsperry79/VideoForensics/releases/tags/testing"
    echo ">>> Fetching latest Testing release from GitHub"
else
    API_URL="https://api.github.com/repos/rsperry79/VideoForensics/releases/latest"
    echo ">>> Fetching latest Stable release from GitHub"
fi

# Fetch release info (with User-Agent header, required by GitHub)
RELEASE_JSON=$(curl -fsSL -H "User-Agent: VideoForensics-Installer" "$API_URL")

if [[ -z "$RELEASE_JSON" ]]; then
    echo "!!! ERROR: Failed to fetch release information from GitHub"
    exit 1
fi

# Extract version from tag (remove leading 'v' if present)
VERSION_TAG=$(echo "$RELEASE_JSON" | grep -o '"tag_name":"[^"]*"' | head -1 | cut -d'"' -f4)
if [[ -z "$VERSION_TAG" ]]; then
    echo "!!! ERROR: Could not parse version from release"
    exit 1
fi

VERSION="${VERSION_TAG#v}"
echo ">>> Found release: $VERSION_TAG ($VERSION)"

# Find the .deb asset (use jq if available, otherwise grep/sed)
DEB_URL=""
DEB_NAME=""

if command -v jq &> /dev/null; then
    # Use jq for reliable JSON parsing
    DEB_INFO=$(echo "$RELEASE_JSON" | jq '.assets[] | select(.name | test("videoforensics_.*_amd64\\.deb")) | {name: .name, url: .browser_download_url}' | head -1)
    if [[ -n "$DEB_INFO" ]]; then
        DEB_NAME=$(echo "$DEB_INFO" | jq -r '.name')
        DEB_URL=$(echo "$DEB_INFO" | jq -r '.url')
    fi
else
    # Fallback: grep and sed for JSON parsing (simple, robust)
    DEB_NAME=$(echo "$RELEASE_JSON" | grep -o '"name":"videoforensics_[^"]*_amd64\.deb"' | head -1 | cut -d'"' -f4)
    if [[ -n "$DEB_NAME" ]]; then
        # Find the corresponding browser_download_url for this asset
        DEB_URL=$(echo "$RELEASE_JSON" | grep -A 5 "\"name\":\"$DEB_NAME\"" | grep '"browser_download_url"' | head -1 | cut -d'"' -f4)
    fi
fi

if [[ -z "$DEB_URL" || -z "$DEB_NAME" ]]; then
    echo "!!! ERROR: videoforensics_*_amd64.deb not found in release assets"
    exit 1
fi

echo ">>> Found asset: $DEB_NAME"

# Download to /tmp
DEB_PATH="/tmp/$DEB_NAME"
echo ">>> Downloading to $DEB_PATH"

curl -fsSL -o "$DEB_PATH" "$DEB_URL"

if [[ ! -f "$DEB_PATH" ]]; then
    echo "!!! ERROR: Failed to download $DEB_NAME"
    exit 1
fi

DEB_SIZE_MB=$(du -m "$DEB_PATH" | cut -f1)
echo ">>> Download complete ($DEB_SIZE_MB MB)"

# Check if running as root or if sudo is available
if [[ $EUID -eq 0 ]]; then
    # Already root
    echo ">>> Installing package (as root)"
    dpkg -i "$DEB_PATH"
    INSTALL_RESULT=$?
elif command -v sudo &> /dev/null; then
    # sudo is available
    echo ">>> Installing package (with sudo)"
    sudo dpkg -i "$DEB_PATH"
    INSTALL_RESULT=$?
else
    # Neither root nor sudo available
    echo ""
    echo "!!! NOTE: Not running as root and 'sudo' is not available."
    echo "    Please run the following command manually to complete installation:"
    echo ""
    echo "    sudo dpkg -i $DEB_PATH"
    echo ""
    exit 0
fi

if [[ $INSTALL_RESULT -eq 0 ]]; then
    echo ">>> Installation complete"
else
    echo "!!! ERROR: dpkg installation failed with exit code $INSTALL_RESULT"
    exit 1
fi
