#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/age-of-war-ui.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/age-of-war-ui/report.txt"
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
AOW_ADB_PORT="$(python3 - <<'PY_PORT'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY_PORT
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$AOW_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$AOW_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
if "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android -executeMethod XanaduYonder.EditorTools.AgeOfWarUiChecks.Run -logFile "$LOG_FILE"; then
    test_status=0
else
    test_status=$?
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2
    exit "$test_status"
fi
if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^AGE_OF_WAR_UI_CHECKS_PASS:' "$REPORT_FILE"; then
    tail -n 100 "$LOG_FILE" >&2
    exit 1
fi
cat "$REPORT_FILE"
