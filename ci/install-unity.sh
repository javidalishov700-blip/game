#!/usr/bin/env bash
# Installs the Unity editor through the Unity Hub CLI when the build machine
# only ships the Hub. Idempotent: exits immediately if an editor is already there.
set -u

VERSION="${UNITY_VERSION:-6000.3.22f1}"
MODULES="${UNITY_MODULES:-ios}"
HUB="/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"

if EXISTING="$(bash ci/find-unity.sh 2>/dev/null)"; then
  echo "Unity already installed: $EXISTING"
  exit 0
fi

if [ ! -x "$HUB" ]; then
  echo "Unity Hub not found at: $HUB"
  exit 1
fi

echo "Installing Unity $VERSION (modules: $MODULES) — this takes a while."

# The Hub CLI frequently exits non-zero even on success, so results are verified below.
"$HUB" -- --headless install-path --set /Applications/Unity/Hub/Editor || true

MODULE_ARGS=()
for module in $MODULES; do
  MODULE_ARGS+=(--module "$module")
done

"$HUB" -- --headless install --version "$VERSION" "${MODULE_ARGS[@]}" --childModules || true

if UNITY_BIN="$(bash ci/find-unity.sh 2>/dev/null)"; then
  echo "Installed: $UNITY_BIN"
  exit 0
fi

{
  echo "Unity $VERSION could not be installed."
  echo "--- editors --installed ---"
  "$HUB" -- --headless editors --installed || true
  echo "--- releases the Hub offers ---"
  "$HUB" -- --headless editors --releases 2>/dev/null | head -40 || true
  echo "--- install path ---"
  "$HUB" -- --headless install-path --get || true
} >&2

exit 1
