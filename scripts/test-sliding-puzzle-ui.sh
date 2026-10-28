#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/sliding-puzzle-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/sliding-puzzle-ui/report.txt"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
source "$PROJECT_ROOT/scripts/sliding-puzzle-unity-environment.sh"
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE" "$LOG_FILE"
# Camera.Render supplies production UGUI depth/raycast and screenshots. Keep
# graphics enabled and let the asynchronous Play Mode runner exit the Editor.
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.SlidingPuzzleUiChecks.Run -logFile "$LOG_FILE"; then
    :
else
    check_status=$?
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit "$check_status"
fi
if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^SLIDING_PUZZLE_UI_CHECKS_PASS:' "$REPORT_FILE"; then
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit 1
fi
cat "$REPORT_FILE"
