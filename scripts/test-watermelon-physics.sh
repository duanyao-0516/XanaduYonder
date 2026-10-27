#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$PROJECT_ROOT/artifacts/logs"
python3 - "$UNITY_EDITOR" -batchmode -nographics -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.WatermelonPhysicsChecks.Run \
    -logFile "$PROJECT_ROOT/artifacts/logs/test-watermelon-physics.log" <<'PY'
import subprocess, sys
try:
    result = subprocess.run(sys.argv[1:], timeout=600)
    sys.exit(result.returncode)
except subprocess.TimeoutExpired:
    sys.exit('Unity physics checks timed out; inspect artifacts/logs/test-watermelon-physics.log')
PY
