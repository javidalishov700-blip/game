#!/usr/bin/env bash
# Downloads the Xcode project produced by the GitHub Actions workflow "iOS Xcode project"
# and unpacks it, so a Mac runner can compile and sign it without Unity being installed.
#
#   GITHUB_TOKEN   fine-grained PAT with Contents: read on the repository
#   GITHUB_REPO    owner/repo, e.g. javidalishov700-blip/game
#   RELEASE_TAG    optional; defaults to ios-xcode-latest
set -eu

DESTINATION="${1:-ios-xcode}"
TAG="${RELEASE_TAG:-ios-xcode-latest}"
API="https://api.github.com/repos/${GITHUB_REPO}"

if [ -z "${GITHUB_TOKEN:-}" ]; then
  echo "GITHUB_TOKEN is not set. Add it to the Codemagic environment group."
  exit 1
fi

echo "Looking up release $TAG in $GITHUB_REPO"

RELEASE="$(curl -fsS \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github+json" \
  "$API/releases/tags/$TAG")"

ASSET_ID="$(printf '%s' "$RELEASE" | python3 -c '
import json, sys

release = json.load(sys.stdin)

for asset in release.get("assets", []):
    if asset.get("name", "").endswith(".zip"):
        print(asset["id"])
        break
')"

if [ -z "$ASSET_ID" ]; then
  echo "No .zip asset on release $TAG. Run the \"iOS Xcode project\" workflow first."
  exit 1
fi

echo "Downloading asset $ASSET_ID"

curl -fsSL \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Accept: application/octet-stream" \
  -o ios-xcode.zip \
  "$API/releases/assets/$ASSET_ID"

rm -rf "$DESTINATION"
mkdir -p "$DESTINATION"
unzip -q ios-xcode.zip -d "$DESTINATION"

# unity-builder wraps the project in a folder named after the build target.
if [ -d "$DESTINATION/iOS" ]; then
  mv "$DESTINATION/iOS"/* "$DESTINATION"/
  rmdir "$DESTINATION/iOS"
fi

if [ ! -d "$DESTINATION/Unity-iPhone.xcodeproj" ]; then
  echo "Unity-iPhone.xcodeproj not found under $DESTINATION:"
  ls -la "$DESTINATION"
  exit 1
fi

echo "Xcode project ready at $DESTINATION"
