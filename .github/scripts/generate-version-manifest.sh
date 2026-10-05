#!/bin/bash
#
# Generate JSON version manifest for update-check clients.
#
# Usage: ./generate-version-manifest.sh <version> [<download-url>]
# Example: ./generate-version-manifest.sh 1.0.5 "https://github.com/.../releases/download/v1.0.5/installer.exe"
#
# Output: version-manifest.json
#

set -e

VERSION="${1:-}"
DOWNLOAD_URL="${2:-}"

if [ -z "$VERSION" ]; then
  echo "ERROR: Version not provided"
  echo "Usage: $0 <version> [<download-url>]"
  exit 1
fi

# Strip 'v' prefix if present
VERSION="${VERSION#v}"

MANIFEST_FILE="version-manifest.json"

# Determine channel from version string
CHANNEL="stable"
if [[ "$VERSION" =~ (-alpha|-beta) ]]; then
  CHANNEL="beta"
fi

# Get current date in ISO8601 format
RELEASE_DATE=$(date -u +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null || echo "$(date +%Y-%m-%d)")

# Construct default download URL if not provided
if [ -z "$DOWNLOAD_URL" ]; then
  GITHUB_REPO="${GITHUB_REPOSITORY:-}"
  GITHUB_SERVER="${GITHUB_SERVER_URL:-https://github.com}"
  if [ -n "$GITHUB_REPO" ]; then
    VERSION_TAG="v${VERSION}"
    DOWNLOAD_URL="$GITHUB_SERVER/$GITHUB_REPO/releases/download/$VERSION_TAG/VideoForensics-installer.exe"
  else
    DOWNLOAD_URL=""
  fi
fi

# Construct changelog URL
CHANGELOG_URL=""
if [ -n "${GITHUB_REPO}" ]; then
  GITHUB_SERVER="${GITHUB_SERVER_URL:-https://github.com}"
  VERSION_TAG="v${VERSION}"
  CHANGELOG_URL="$GITHUB_SERVER/$GITHUB_REPO/releases/tag/$VERSION_TAG"
fi

# Construct checksum URL
CHECKSUM_URL=""
if [ -n "${GITHUB_REPO}" ]; then
  GITHUB_SERVER="${GITHUB_SERVER_URL:-https://github.com}"
  VERSION_TAG="v${VERSION}"
  CHECKSUM_URL="$GITHUB_SERVER/$GITHUB_REPO/releases/download/$VERSION_TAG/checksums.sha256"
fi

# Create JSON manifest using printf for portability (avoids dependency on jq)
cat > "$MANIFEST_FILE" <<EOF
{
  "version": "$VERSION",
  "releaseDate": "$RELEASE_DATE",
  "channel": "$CHANNEL",
  "downloadUrl": "$DOWNLOAD_URL",
  "changelogUrl": "$CHANGELOG_URL",
  "checksumUrl": "$CHECKSUM_URL"
}
EOF

echo "Version manifest generated: $MANIFEST_FILE"
cat "$MANIFEST_FILE"

# Try to validate JSON if jq is available
if command -v jq &> /dev/null; then
  if jq empty "$MANIFEST_FILE" 2>/dev/null; then
    echo "JSON validation: OK"
  else
    echo "WARNING: JSON validation failed"
    exit 1
  fi
fi
