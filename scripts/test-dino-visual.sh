#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/dino-visual-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/dino-ui/report.txt"
if [[ ! -x "$UNITY_EDITOR" ]]; then
  printf 'Unity Editor not found: %s\nSet UNITY_EDITOR to the Editor executable.\n' "$UNITY_EDITOR" >&2
  exit 1
fi
if [[ -f "$PROJECT_ROOT/Temp/UnityLockfile" ]]; then
  printf 'This checkout already has a Unity lock. Finish its build/editor before visual checks.\n' >&2
  exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")" "$PROJECT_ROOT/artifacts/locks"
LOCK_DIR="$PROJECT_ROOT/artifacts/locks/dino-build"
if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  printf 'A Dino build or validation already owns: %s\n' "$LOCK_DIR" >&2
  exit 1
fi
trap 'rmdir "$LOCK_DIR"' EXIT
rm -f "$REPORT_FILE"
printf 'Rendering production Dino UGUI at portrait and landscape sizes.\nLog: %s\n' "$LOG_FILE"
# Keep graphics enabled for real Camera.Render and UGUI mask/stencil checks.
if ADB_SERVER_SOCKET=tcp:localhost:5039 "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
  -executeMethod XanaduYonder.EditorTools.DinoVisualChecks.Run -logFile "$LOG_FILE"; then
  if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^DINO_VISUAL_CHECKS_PASS:' "$REPORT_FILE"; then
    printf 'Unity exited without a passing visual check report.\n' >&2
    tail -n 100 "$LOG_FILE" >&2 || true
    exit 1
  fi
  cat "$REPORT_FILE"
else
  check_status=$?
  if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
  tail -n 100 "$LOG_FILE" >&2 || true
  exit "$check_status"
fi
