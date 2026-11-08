#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$PROJECT_ROOT/scripts/snake-unity-environment.sh"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/snake-ui-checks.log"
REPORT_FILE="$PROJECT_ROOT/artifacts/validation/snake-ui/report.txt"
[[ -x "$UNITY_EDITOR" ]] || { printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2; exit 1; }
mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$REPORT_FILE"
# Production UGUI/raycast and Camera.Render require graphics: do not add -nographics.
if python3 - "$UNITY_EDITOR" -batchmode -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.SnakeUiChecks.Run -logFile "$LOG_FILE" <<'PY'
import subprocess, sys
try:
    sys.exit(subprocess.run(sys.argv[1:], timeout=900).returncode)
except subprocess.TimeoutExpired:
    sys.exit('Snake UI checks timed out after 900 seconds; inspect the Unity log.')
PY
then
    check_status=0
else
    check_status=$?
    [[ ! -s "$REPORT_FILE" ]] || cat "$REPORT_FILE" >&2
    tail -n 100 "$LOG_FILE" >&2 || true
    exit "$check_status"
fi
if [[ ! -s "$REPORT_FILE" ]] || ! rg -q '^SNAKE_UI_CHECKS_PASS:' "$REPORT_FILE"; then
    [[ ! -s "$REPORT_FILE" ]] || cat "$REPORT_FILE" >&2
    tail -n 100 "$LOG_FILE" >&2 || true
    exit 1
fi
cat "$REPORT_FILE"
