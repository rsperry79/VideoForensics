#!/bin/sh
# Bump version in version.json based on current branch
# - dev/wip branch: bump patch version
# - main/tags: do nothing (releases don't auto-bump)
# Usage: GITHUB_TOKEN=token bash bump-version.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VERSION_FILE="$REPO_ROOT/version.json"

# Get current branch
BRANCH=$(git rev-parse --abbrev-ref HEAD)
echo "Current branch: $BRANCH"

# Exit early if main branch or if running on a tag
if [ "$BRANCH" = "main" ] || [ "$BRANCH" = "HEAD" ]; then
    echo "Skipping version bump for $BRANCH (release branches don't auto-bump)"
    exit 0
fi

# Check if version.json exists
if [ ! -f "$VERSION_FILE" ]; then
    echo "ERROR: version.json not found at $VERSION_FILE"
    exit 1
fi

# Parse current version using jq if available, otherwise use grep/sed
if command -v jq >/dev/null 2>&1; then
    CURRENT_VERSION=$(jq -r '.version' "$VERSION_FILE")
else
    # Fallback: extract version using grep and sed
    CURRENT_VERSION=$(grep -oP '"version":\s*"\K[^"]+' "$VERSION_FILE" | head -1)
fi

if [ -z "$CURRENT_VERSION" ]; then
    echo "ERROR: Could not parse version from $VERSION_FILE"
    exit 1
fi

echo "Current version: $CURRENT_VERSION"

# Parse semantic version - handle formats like "1.0-alpha", "1.0.5", "1.0.5-alpha", "1.0.5-alpha.2+build.15"
# Use more robust pattern matching
# Extract major: first sequence of digits
MAJOR=$(echo "$CURRENT_VERSION" | grep -o '^[0-9]*' || true)

# Extract minor: digits after first dot
MINOR=$(echo "$CURRENT_VERSION" | sed -n 's/^[0-9]*\.\([0-9]*\).*/\1/p' || true)

# Extract patch: digits after second dot (if present)
PATCH=$(echo "$CURRENT_VERSION" | sed -n 's/^[0-9]*\.[0-9]*\.\([0-9]*\).*/\1/p' || true)

# If PATCH is empty, it means version is like "1.0-alpha" (no patch number), so default to 0
if [ -z "$PATCH" ]; then
    PATCH="0"
fi

# Validate parsed values
if [ -z "$MAJOR" ] || [ -z "$MINOR" ]; then
    echo "ERROR: Could not parse semantic version from: $CURRENT_VERSION"
    exit 1
fi

# Determine bump type based on branch
NEW_VERSION=""
case "$BRANCH" in
    dev)
        # Integration/testing branch: bump minor
        NEW_MINOR=$((MINOR + 1))
        NEW_VERSION="${MAJOR}.${NEW_MINOR}.0"
        echo "Bumping minor version for dev branch: $CURRENT_VERSION → $NEW_VERSION"
        ;;
    wip)
        # Work-in-progress/dev build: bump patch
        NEW_PATCH=$((PATCH + 1))
        NEW_VERSION="${MAJOR}.${MINOR}.${NEW_PATCH}"
        echo "Bumping patch version for wip branch: $CURRENT_VERSION → $NEW_VERSION"
        ;;
    *)
        # Unknown branch: do nothing
        echo "Skipping version bump for unknown branch: $BRANCH"
        exit 0
        ;;
esac

# Update version.json
if command -v jq >/dev/null 2>&1; then
    jq ".version = \"$NEW_VERSION\"" "$VERSION_FILE" > "${VERSION_FILE}.tmp"
    mv "${VERSION_FILE}.tmp" "$VERSION_FILE"
else
    # Fallback: use sed to replace version string
    sed -i.bak "s/\"version\":\s*\"[^\"]*\"/\"version\": \"$NEW_VERSION\"/" "$VERSION_FILE"
    rm -f "${VERSION_FILE}.bak"
fi

echo "Updated version.json: $NEW_VERSION"

# Stage the change
git add "$VERSION_FILE"

# Configure git user if env vars are set (for CI environments)
if [ -n "$GIT_AUTHOR_NAME" ]; then
    git config user.name "$GIT_AUTHOR_NAME"
fi
if [ -n "$GIT_AUTHOR_EMAIL" ]; then
    git config user.email "$GIT_AUTHOR_EMAIL"
fi

# Commit with [skip ci] to prevent recursive CI triggers
git commit -m "chore: bump version to $NEW_VERSION [skip ci]"

echo "Committed version bump"

# Push to current branch
git push origin "$BRANCH"

echo "Pushed to $BRANCH"
echo "Version bump complete: $CURRENT_VERSION → $NEW_VERSION"
