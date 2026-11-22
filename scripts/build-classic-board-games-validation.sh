#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-classic-board-games-validation.log"
source "$PROJECT_ROOT/scripts/project-version.sh"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-classic-board-games-validation.apk"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi

UNITY_EDITOR="$UNITY_EDITOR" python3 "$PROJECT_ROOT/scripts/cut-the-rope/prepare-build-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/zhaoyun-adou/verify-build-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/level-devil/build-content.py"
python3 "$PROJECT_ROOT/scripts/bomb-it/build-content.py"

# Unity can stop Gradle and ADB on exit. Keep this build separate from the device session.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/ClassicBoardGamesGradle"
ANDROID_ADB_SERVER_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ANDROID_ADB_SERVER_PORT
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$ANDROID_ADB_SERVER_PORT"
export ADB_MDNS_AUTO_CONNECT=0

mkdir -p "$(dirname "$LOG_FILE")"
rm -f "$APK_FILE" "$LOG_FILE"
printf 'Building isolated board games Android APK. Log: %s\n' "$LOG_FILE"
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidClassicBoardGamesValidation \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" ]] || ! rg -Fq -- "Android APK: $APK_FILE (" "$LOG_FILE"; then
        printf 'Missing APK or successful build evidence: %s\n' "$APK_FILE" >&2
        exit 1
    fi
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
