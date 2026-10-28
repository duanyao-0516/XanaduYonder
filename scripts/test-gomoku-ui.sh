#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$PROJECT_ROOT/scripts/gomoku-unity-environment.sh"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/gomoku-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/gomoku-ui/report.txt"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
# Camera.Render supplies real UGUI depth/raycast and screenshots; retain graphics.
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.GomokuUiChecks.Run -logFile "$LOG_FILE"; then
    check_status=0
else
    check_status=$?
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit "$check_status"
fi
if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^GOMOKU_UI_CHECKS_PASS:' "$REPORT_FILE"; then
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit 1
fi
cat "$REPORT_FILE"
