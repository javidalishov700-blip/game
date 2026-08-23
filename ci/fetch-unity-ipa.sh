#!/usr/bin/env bash
# Fetches the .ipa produced by Unity Build Automation.
#
# Preferred path: ask the Build Automation API for the latest successful build and use the
# signed download link it returns. The link shown in the dashboard is bound to the browser
# session and answers 401 to anything else, so it is only a fallback.
#
#   UNITY_CLOUD_API_KEY   Unity Cloud → Build Automation → Settings → API key
#   UNITY_ORG_ID          organisation id (from the dashboard URL)
#   UNITY_PROJECT_ID      project id (from the dashboard URL)
#   UNITY_BUILD_TARGET    build target id, e.g. ios-testflight
#   UNITY_BUILD_NUMBER    optional; defaults to the latest successful build
#   IPA_URL               optional direct link, used when no API key is configured
set -u

OUTPUT="${1:-app.ipa}"
API="https://build-api.cloud.unity3d.com/api/v1"
DOWNLOAD_URL=""

resolve_from_api() {
  local url

  if [ -n "${UNITY_BUILD_NUMBER:-}" ]; then
    url="$API/orgs/$UNITY_ORG_ID/projects/$UNITY_PROJECT_ID/buildtargets/$UNITY_BUILD_TARGET/builds/$UNITY_BUILD_NUMBER"
  else
    url="$API/orgs/$UNITY_ORG_ID/projects/$UNITY_PROJECT_ID/buildtargets/$UNITY_BUILD_TARGET/builds?per_page=25"
  fi

  echo "Querying Unity Build Automation: $url"

  local response
  response="$(curl -fsS -H "Authorization: Basic $UNITY_CLOUD_API_KEY" -H "Content-Type: application/json" "$url")" || return 1

  printf '%s' "$response" | python3 -c '
import json, sys

payload = json.load(sys.stdin)
builds = payload if isinstance(payload, list) else [payload]

def link(build):
    links = build.get("links") or {}
    primary = links.get("download_primary") or {}
    return primary.get("href")

for build in builds:
    if build.get("buildStatus") == "success" and link(build):
        sys.stdout.write(link(build))
        sys.exit(0)

sys.exit(1)
'
}

if [ -n "${UNITY_CLOUD_API_KEY:-}" ] && [ -n "${UNITY_ORG_ID:-}" ] && [ -n "${UNITY_PROJECT_ID:-}" ] && [ -n "${UNITY_BUILD_TARGET:-}" ]; then
  DOWNLOAD_URL="$(resolve_from_api)" || DOWNLOAD_URL=""
fi

if [ -z "$DOWNLOAD_URL" ]; then
  DOWNLOAD_URL="${IPA_URL:-}"
  [ -n "$DOWNLOAD_URL" ] && echo "Falling back to IPA_URL."
fi

if [ -z "$DOWNLOAD_URL" ]; then
  echo "No download link. Set UNITY_CLOUD_API_KEY (with UNITY_ORG_ID, UNITY_PROJECT_ID," >&2
  echo "UNITY_BUILD_TARGET) or IPA_URL." >&2
  exit 1
fi

echo "Downloading build artifact."
curl -fL --retry 3 "$DOWNLOAD_URL" -o payload.bin || {
  echo "Download failed. A dashboard link expires and is session-bound — use the API key path." >&2
  exit 1
}

ls -lh payload.bin

# Unity hands back either the .ipa itself or a zip containing it.
if unzip -l payload.bin | grep -q "Payload/"; then
  mv payload.bin "$OUTPUT"
else
  rm -rf extracted
  unzip -oq payload.bin -d extracted
  FOUND="$(find extracted -name '*.ipa' | head -1)"

  if [ -z "$FOUND" ]; then
    echo "No .ipa inside the downloaded archive:" >&2
    find extracted -maxdepth 3 | head -40 >&2
    exit 1
  fi

  mv "$FOUND" "$OUTPUT"
fi

ls -lh "$OUTPUT"
