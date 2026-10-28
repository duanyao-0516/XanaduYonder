#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/gold-miner-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/gold-miner-development/ui/report.txt"

if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
printf 'Running Gold Miner Play Mode UI checks in the isolated checkout.\nLog: %s\n' "$LOG_FILE"
# Run enters Play Mode and owns exit; graphics are required for actual UI meshes.
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.GoldMinerUiChecks.Run -logFile "$LOG_FILE"; then
    if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^GOLD_MINER_UI_CHECKS_PASS:' "$REPORT_FILE"; then
        printf 'Unity exited without a complete Gold Miner UI report.\n' >&2
        tail -n 100 "$LOG_FILE" >&2 || true
        exit 1
    fi
    cat "$REPORT_FILE"
else
    check_status=$?
    printf 'Gold Miner UI checks failed (exit %s).\n' "$check_status" >&2
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit "$check_status"
fi
