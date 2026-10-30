#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-kingdom-rush.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.8.0-kingdom-rush-preview.apk"

if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
for archive in KingdomRushRuntime.aar KingdomRushContent.aar; do
    if [[ ! -s "$PROJECT_ROOT/Assets/Plugins/Android/$archive" ]]; then
        printf 'Missing generated %s. Build the KR1 content and Android runtime first.\n' "$archive" >&2
        exit 1
    fi
done
python3 "$PROJECT_ROOT/scripts/kingdom-rush/verify-build-input.py" --root "$PROJECT_ROOT"

# Keep Unity's automatic ADB/Gradle lifecycle isolated from the service holding the phone.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/KingdomRushGradle"
KR1_BUILD_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$KR1_BUILD_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$KR1_BUILD_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0

mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
printf 'Building Kingdom Rush integration. Log: %s\n' "$LOG_FILE"
# An older file must never be mistaken for the result of this invocation.
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.kr1-build-start.XXXXXX")"
trap 'rm -f "$build_marker"' EXIT
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildKingdomRushPreview \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || ! "$APK_FILE" -nt "$build_marker" ]]; then
        printf 'Unity did not create a fresh Kingdom Rush APK.\n' >&2
        exit 1
    fi
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
