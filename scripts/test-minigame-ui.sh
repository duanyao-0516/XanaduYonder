#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/minigame-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/minigame-ui/report.txt"
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
# Camera.Render supplies real UGUI graphic depths and screenshots; keep graphics enabled.
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.MiniGameUiChecks.Run -logFile "$LOG_FILE"; then
    check_status=0
else
    check_status=$?
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2
    exit "$check_status"
fi
if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^MINIGAME_UI_CHECKS_PASS:' "$REPORT_FILE"; then
    tail -n 100 "$LOG_FILE" >&2
    exit 1
fi
cat "$REPORT_FILE"
