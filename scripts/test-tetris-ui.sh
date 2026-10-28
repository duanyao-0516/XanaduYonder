#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$PROJECT_ROOT/scripts/tetris-unity-environment.sh"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/tetris-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/tetris-ui/report.txt"
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.TetrisUiChecks.Run -logFile "$LOG_FILE"; then
    if [[ -s "$REPORT_FILE" ]] && rg -q '^TETRIS_UI_CHECKS_PASS:' "$REPORT_FILE"; then
        cat "$REPORT_FILE"
    else
        tail -n 100 "$LOG_FILE" >&2
        exit 1
    fi
else
    code=$?
    tail -n 100 "$LOG_FILE" >&2
    exit "$code"
fi
