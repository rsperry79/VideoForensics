#!/bin/bash
set -euo pipefail

# build-deb.sh — Build a .deb package for VideoForensics
#
# Usage:
#   ./build-deb.sh [published-path] [version]
#
# Arguments:
#   published-path  : Path to the self-contained publish output (optional)
#                     Default: ../src/client/web/VideoForensics.WebApp/bin/Release/net10.0/linux-x64/publish
#   version         : Version string for the package (optional)
#                     Default: "1.0.0" or read from $VERSION env var
#
# Environment Variables:
#   VERSION         : Override the version string (if not specified as argument)
#
# Output:
#   videoforensics_<VERSION>_amd64.deb in the current directory

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Default published path (relative to script, then resolve)
DEFAULT_PUBLISHED_PATH="$SCRIPT_DIR/../../src/client/web/VideoForensics.WebApp/bin/Release/net10.0/linux-x64/publish"

# Arguments
PUBLISHED_PATH="${1:-$DEFAULT_PUBLISHED_PATH}"
VERSION="${2:-${VERSION:-1.0.0}}"

# Resolve published path to absolute
PUBLISHED_PATH="$(cd "$PUBLISHED_PATH" 2>/dev/null && pwd)" || {
    echo "ERROR: Published path not found: $1" >&2
    echo "Usage: $0 [published-path] [version]" >&2
    exit 1
}

# Verify the published executable exists
if [ ! -f "$PUBLISHED_PATH/VideoForensics.WebApp" ]; then
    echo "ERROR: VideoForensics.WebApp executable not found in $PUBLISHED_PATH" >&2
    exit 1
fi

echo "Building VideoForensics Debian package"
echo "======================================="
echo "Published path: $PUBLISHED_PATH"
echo "Version: $VERSION"
echo ""

# Create temporary build directory
BUILD_DIR=$(mktemp -d)
trap "rm -rf '$BUILD_DIR'" EXIT

echo "Using temporary build directory: $BUILD_DIR"

# Create Debian package directory structure
#   DEBIAN/       — control files and scripts
#   usr/lib/videoforensics/  — application binaries
#   lib/systemd/system/  — systemd unit file

DEBIAN_DIR="$BUILD_DIR/DEBIAN"
APP_DIR="$BUILD_DIR/usr/lib/videoforensics"
SYSTEMD_DIR="$BUILD_DIR/lib/systemd/system"

mkdir -p "$DEBIAN_DIR"
mkdir -p "$APP_DIR"
mkdir -p "$SYSTEMD_DIR"

echo "  Created directory structure"

# Copy the control file, substituting version
echo "  Copying control metadata..."
sed "s/@VERSION@/$VERSION/g" "$SCRIPT_DIR/control" > "$DEBIAN_DIR/control"
chmod 0644 "$DEBIAN_DIR/control"

# Copy maintainer scripts with proper permissions (0755 for executables)
echo "  Copying maintainer scripts..."
cp "$SCRIPT_DIR/postinst" "$DEBIAN_DIR/postinst"
chmod 0755 "$DEBIAN_DIR/postinst"

cp "$SCRIPT_DIR/prerm" "$DEBIAN_DIR/prerm"
chmod 0755 "$DEBIAN_DIR/prerm"

cp "$SCRIPT_DIR/postrm" "$DEBIAN_DIR/postrm"
chmod 0755 "$DEBIAN_DIR/postrm"

# Copy systemd unit file
echo "  Copying systemd unit file..."
cp "$SCRIPT_DIR/videoforensics.service" "$SYSTEMD_DIR/videoforensics.service"
chmod 0644 "$SYSTEMD_DIR/videoforensics.service"

# Copy published binaries to install location
echo "  Copying application binaries..."
cp -r "$PUBLISHED_PATH"/* "$APP_DIR/"

# Make the main executable executable
chmod 0755 "$APP_DIR/VideoForensics.WebApp"

# Build the .deb file
# --root-owner-group: use root:root for all files (standard for .deb packages)
OUTPUT_FILE="$SCRIPT_DIR/videoforensics_${VERSION}_amd64.deb"

echo "  Building .deb package..."
dpkg-deb --build --root-owner-group "$BUILD_DIR" "$OUTPUT_FILE"

echo ""
echo "Build Complete"
echo "=============="
echo "Package created: $OUTPUT_FILE"
echo ""
echo "To install:"
echo "  sudo dpkg -i $OUTPUT_FILE"
echo "Or:"
echo "  sudo apt install $OUTPUT_FILE"
