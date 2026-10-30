#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$PROJECT_ROOT/scripts/startup-unity-environment.sh"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/dino-page-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/logs/dino-page-checks.txt"
if [[ ! -x "$UNITY_EDITOR" ]]; then
  printf 'Unity Editor not found: %s\nSet UNITY_EDITOR to the Editor executable.\n' "$UNITY_EDITOR" >&2
  exit 1
fi
if [[ -f "$PROJECT_ROOT/Temp/UnityLockfile" ]]; then
  printf 'This checkout already has a Unity lock. Finish its build/editor before running Dino page checks.\n' >&2
  exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"
mkdir -p "$PROJECT_ROOT/artifacts/locks"
LOCK_DIR="$PROJECT_ROOT/artifacts/locks/dino-build"
if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  printf 'A Dino build or page validation already owns: %s\n' "$LOCK_DIR" >&2
  exit 1
fi
trap 'rmdir "$LOCK_DIR"' EXIT
rm -f "$REPORT_FILE"
printf 'Running actual Dino page / UGUI state checks.\nLog: %s\n' "$LOG_FILE"
# The executeMethod enters play mode and owns the exit code. Do not use -quit or run beside a build.
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
  -executeMethod XanaduYonder.EditorTools.DinoPageValidation.Run -logFile "$LOG_FILE"; then
  if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^DINO_PAGE_CHECKS_PASS:' "$REPORT_FILE"; then
    printf 'Unity exited without a passing Dino page check report.\n' >&2
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
