#!/usr/bin/env bash
# Downloads the Xcode project produced by the GitHub Actions workflow "iOS Xcode project"
# and unpacks it, so a Mac runner can compile and sign it without Unity being installed.
#
#   GITHUB_REPO    owner/repo, e.g. javidalishov700-blip/game
#   RELEASE_TAG    optional; defaults to ios-xcode-latest
#   GITHUB_TOKEN   only needed if the repository is private: fine-grained PAT with
#                  Contents: read on it
set -eu

DESTINATION="${1:-ios-xcode}"
TAG="${RELEASE_TAG:-ios-xcode-latest}"
API="https://api.github.com/repos/${GITHUB_REPO}"

# Every build is published under its own tag (ios-xcode-<run id>, "-testads" for a
# TestFlight-only test-ads build) and marked as the repository's "latest" release, so
# "ios-xcode-latest" means: whatever GitHub currently calls the latest release.
#
# A public repository is resolved without api.github.com: Codemagic's shared build machines
# burn through GitHub's unauthenticated API allowance (60 requests an hour per address) and
# get HTTP 403, while github.com/<repo>/releases/latest is an ordinary redirect that has no
# such limit. A private repository has no public redirect (it is a 404), so with a
# GITHUB_TOKEN the newest release is asked of the API instead — an authenticated call has a
# far larger allowance.
#
# Either way, if the newest release cannot be worked out this stops. It used to carry on with
# the literal tag "ios-xcode-latest", an old rolling release, and happily built and uploaded
# a stale app (1.2.0 after 1.2.1 existed) that App Store Connect then rejected.
if [ "$TAG" = "ios-xcode-latest" ]; then
  RESOLVED=""

  if [ -n "${GITHUB_TOKEN:-}" ]; then
    RESOLVED="$(curl -fsS \
        -H "Authorization: Bearer $GITHUB_TOKEN" \
        -H "Accept: application/vnd.github+json" \
        "$API/releases/latest" \
      | python3 -c 'import json, sys; print(json.load(sys.stdin)["tag_name"])' || true)"
  fi

  if [ -z "$RESOLVED" ]; then
    LATEST_URL="$(curl -fsSL -o /dev/null -w '%{url_effective}' "https://github.com/${GITHUB_REPO}/releases/latest" || true)"
    RESOLVED="${LATEST_URL##*/releases/tag/}"
  fi

  case "$RESOLVED" in
    ios-xcode-*)
      TAG="$RESOLVED"
      ;;
    *)
      echo "ERROR: could not work out the newest Xcode project release."
      echo "  If the repository is private, GITHUB_TOKEN must be set in the Codemagic"
      echo "  environment group (fine-grained token, Contents: Read-only on $GITHUB_REPO)."
      echo "  If the token was set, it has expired or been revoked: create a new one."
      exit 1
      ;;
  esac

  echo "Newest Xcode project release: $TAG"

  case "$TAG" in
    *-testads)
      echo "NOTE: this build contains the hidden test-ads switch. TestFlight testing only —"
      echo "      do not submit it to App Review."
      ;;
  esac
fi

PUBLIC_URL="https://github.com/${GITHUB_REPO}/releases/download/${TAG}/ios-xcode.zip"

download_through_api() {
  if [ -z "${GITHUB_TOKEN:-}" ]; then
    echo "The release is not publicly downloadable and GITHUB_TOKEN is not set."
    echo "Add a fine-grained PAT (Contents: read) to the Codemagic environment group."
    exit 1
  fi

  echo "Looking up release $TAG in $GITHUB_REPO"

  # A token GitHub no longer accepts fails with a bare 401, which reads like a bug in this
  # script rather than what it is — an expired or revoked PAT.
  if ! RELEASE="$(curl -fsS \
      -H "Authorization: Bearer $GITHUB_TOKEN" \
      -H "Accept: application/vnd.github+json" \
      "$API/releases/tags/$TAG")"; then
    echo "GitHub refused the release lookup. If the error above is 401, GITHUB_TOKEN has"
    echo "expired or been revoked: create a new fine-grained PAT and replace it in Codemagic."
    exit 1
  fi

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
}

# Public repository: the release asset downloads directly, no credentials involved — and so
# nothing to expire. The API route is the fallback for a private repository or an archive
# uploaded by hand under some other name.
echo "Downloading $PUBLIC_URL"

if ! curl -fsSL -o ios-xcode.zip "$PUBLIC_URL"; then
  echo "Direct download failed; trying the GitHub API."
  download_through_api
fi

rm -rf "$DESTINATION" .xcode-unpack
mkdir -p .xcode-unpack
unzip -q ios-xcode.zip -d .xcode-unpack

# The zip may come from CI (wrapped in a target-named folder) or straight from a local
# Unity build on Windows (wrapped in whatever the user zipped). Find the project instead
# of assuming a layout.
PROJECT="$(find .xcode-unpack -maxdepth 4 -name 'Unity-iPhone.xcodeproj' -print -quit)"

if [ -z "$PROJECT" ]; then
  echo "Unity-iPhone.xcodeproj not found in the archive. Top level:"
  ls -la .xcode-unpack
  exit 1
fi

mv "$(dirname "$PROJECT")" "$DESTINATION"
rm -rf .xcode-unpack

# A zip made on Windows carries no executable bit, and Unity's Xcode project runs shell
# scripts from its build phases — without this the archive step dies on "Permission denied".
find "$DESTINATION" -name '*.sh' -exec chmod +x {} +

echo "Xcode project ready at $DESTINATION"

# Say which app this actually is, so a stale download is one glance in the log rather than a
# rejection from App Store Connect twenty minutes later.
if [ -f "$DESTINATION/Info.plist" ]; then
  VERSION_LINE="$(grep -A1 'CFBundleShortVersionString' "$DESTINATION/Info.plist" | grep '<string>' | sed 's/[^0-9.]//g' | head -n 1)"
  echo "App version in this project (CFBundleShortVersionString): ${VERSION_LINE:-unknown}"
fi
