#!/bin/bash
#
# Generate markdown release notes from git commits between tags.
#
# Usage: ./generate-release-notes.sh <version-tag>
# Example: ./generate-release-notes.sh v1.0.5
#
# Output: release-notes.md
#

set -e

VERSION_TAG="${1:-}"

if [ -z "$VERSION_TAG" ]; then
  echo "ERROR: Version tag not provided"
  echo "Usage: $0 <version-tag>"
  exit 1
fi

# Extract version number (strip 'v' prefix if present)
VERSION="${VERSION_TAG#v}"
NOTES_FILE="release-notes.md"

# Try to find previous tag
PREV_TAG=$(git describe --abbrev=0 --tags "$VERSION_TAG"^ 2>/dev/null || echo "")

if [ -z "$PREV_TAG" ]; then
  # No previous tag found; use all commits up to this tag
  COMMIT_RANGE="$VERSION_TAG"
else
  COMMIT_RANGE="$PREV_TAG..$VERSION_TAG"
fi

# Initialize notes file with header
{
  echo "# Version $VERSION"
  echo ""
} > "$NOTES_FILE"

# Check if CHANGELOG.md has an entry for this version and use it as override
if [ -f CHANGELOG.md ] && grep -q "^## \[$VERSION\]" CHANGELOG.md; then
  echo "Found CHANGELOG.md entry for $VERSION, prepending to release notes"
  awk "/^## \[$VERSION\]/{flag=1; next} /^## \[/{flag=0} flag" CHANGELOG.md >> "$NOTES_FILE"
  echo "" >> "$NOTES_FILE"
fi

# Get commits in conventional commit format
FEATURES=""
FIXES=""
PERF=""
DOCS=""

while IFS= read -r line; do
  # Skip empty lines
  [ -z "$line" ] && continue

  # Parse conventional commit prefix
  case "$line" in
    *" feat: "*)
      MSG="${line#* feat: }"
      FEATURES="$FEATURES"$'\n'"- ${MSG%% (*}"
      ;;
    *" fix: "*)
      MSG="${line#* fix: }"
      FIXES="$FIXES"$'\n'"- ${MSG%% (*}"
      ;;
    *" perf: "*)
      MSG="${line#* perf: }"
      PERF="$PERF"$'\n'"- ${MSG%% (*}"
      ;;
    *" docs: "*)
      MSG="${line#* docs: }"
      DOCS="$DOCS"$'\n'"- ${MSG%% (*}"
      ;;
  esac
done < <(git log --oneline "$COMMIT_RANGE" 2>/dev/null || true)

# Write sections to notes file (only if they have content)
if [ -n "$FEATURES" ]; then
  {
    echo "## Features"
    echo "$FEATURES" | sed '/^$/d'
    echo ""
  } >> "$NOTES_FILE"
fi

if [ -n "$FIXES" ]; then
  {
    echo "## Fixes"
    echo "$FIXES" | sed '/^$/d'
    echo ""
  } >> "$NOTES_FILE"
fi

if [ -n "$PERF" ]; then
  {
    echo "## Performance"
    echo "$PERF" | sed '/^$/d'
    echo ""
  } >> "$NOTES_FILE"
fi

if [ -n "$DOCS" ]; then
  {
    echo "## Documentation"
    echo "$DOCS" | sed '/^$/d'
    echo ""
  } >> "$NOTES_FILE"
fi

# If no commits were found and no CHANGELOG entry, add a note
if [ -z "$FEATURES" ] && [ -z "$FIXES" ] && [ -z "$PERF" ] && [ -z "$DOCS" ]; then
  if ! grep -q "^## \[$VERSION\]" CHANGELOG.md 2>/dev/null; then
    echo "No commits found for $COMMIT_RANGE; see commit history for details." >> "$NOTES_FILE"
  fi
fi

echo "Release notes generated: $NOTES_FILE"
cat "$NOTES_FILE"
