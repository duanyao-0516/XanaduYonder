#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$PROJECT_ROOT/scripts/link-match-unity-environment.sh"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/link-match-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/link-match-ui/report.txt"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
# Camera.Render needs graphics; Run exits only after asynchronous Play Mode checks finish.
# A timeout prevents compiler errors or a failed Play Mode transition from hanging unattended.
if python3 - "$UNITY_EDITOR" "$PROJECT_ROOT" "$LOG_FILE" <<'PY'
import os
import signal
import subprocess
import sys

editor, project, log = sys.argv[1:]
process = subprocess.Popen([editor, '-batchmode', '-projectPath', project, '-buildTarget', 'Android',
                            '-executeMethod', 'XanaduYonder.EditorTools.LinkMatchUiChecks.Run',
                            '-logFile', log], start_new_session=True)
try:
    sys.exit(process.wait(timeout=900))
except subprocess.TimeoutExpired:
    print('Link Match UI checks exceeded 900 seconds.', file=sys.stderr)
    os.killpg(process.pid, signal.SIGTERM)
    try:
        process.wait(timeout=15)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()
    sys.exit(124)
PY
then
    check_status=0
else
    check_status=$?
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit "$check_status"
fi
if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^LINK_MATCH_UI_CHECKS_PASS:' "$REPORT_FILE"; then
    if [[ -s "$REPORT_FILE" ]]; then cat "$REPORT_FILE" >&2; fi
    tail -n 100 "$LOG_FILE" >&2 || true
    exit 1
fi
cat "$REPORT_FILE"
