#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$PROJECT_ROOT/artifacts/logs"
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/AgeOfWarGradle"
AOW_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$AOW_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$AOW_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidAgeOfWarValidation \
    -logFile "$PROJECT_ROOT/artifacts/logs/build-age-of-war-validation.log"
