#!/usr/bin/env bash
# Prints the path of the Unity executable on a macOS build machine.
# Honours UNITY_HOME when it points at a real editor, otherwise probes the
# usual install locations. On failure it dumps what is actually installed.
set -u

candidates=()

if [ -n "${UNITY_HOME:-}" ]; then
  candidates+=("$UNITY_HOME/Contents/MacOS/Unity")
  candidates+=("$UNITY_HOME")
fi

for app in /Applications/Unity/Hub/Editor/*/Unity.app; do
  [ -d "$app" ] && candidates+=("$app/Contents/MacOS/Unity")
done

candidates+=("/Applications/Unity/Unity.app/Contents/MacOS/Unity")

while IFS= read -r app; do
  [ -n "$app" ] && candidates+=("$app/Contents/MacOS/Unity")
done <<< "$(find /Applications -maxdepth 5 -name 'Unity.app' -type d 2>/dev/null | grep -v 'Unity Hub.app')"

for candidate in "${candidates[@]}"; do
  if [ -x "$candidate" ]; then
    echo "$candidate"
    exit 0
  fi
done

{
  echo "Unity executable not found. UNITY_HOME='${UNITY_HOME:-}'"
  echo "--- /Applications ---"
  ls -1 /Applications 2>/dev/null
  echo "--- /Applications/Unity (2 levels) ---"
  find /Applications/Unity -maxdepth 2 2>/dev/null | head -40
  echo "--- Unity.app anywhere under /Applications ---"
  find /Applications -maxdepth 5 -name 'Unity.app' -type d 2>/dev/null
  echo "--- Unity Hub editors reported by the hub CLI ---"
  "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless editors --installed 2>/dev/null || echo "(hub CLI unavailable)"
} >&2

exit 1
