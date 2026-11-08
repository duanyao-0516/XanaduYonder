#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-cut-the-rope.log"
VERSION="$(sed -n 's/^[[:space:]]*public const string Version = "\([^"]*\)";.*/\1/p' "$PROJECT_ROOT/Assets/Editor/ProjectBuild.cs")"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-$VERSION-cut-the-rope-test.apk"

if [[ ! -x "$UNITY_EDITOR" || -z "$VERSION" ]]; then
    printf 'Unity Editor or project version is unavailable: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
for archive in CutTheRopeRuntime.aar CutTheRopeContent.aar; do
    if [[ ! -s "$PROJECT_ROOT/Assets/Plugins/Android/$archive" ]]; then
        printf 'Missing generated %s. Prepare offline game content and Android runtimes first.\n' "$archive" >&2
        exit 1
    fi
done
python3 "$PROJECT_ROOT/scripts/verify-cut-the-rope-input.py" --root "$PROJECT_ROOT"

# This build must not stop a device service or share mutable Gradle state with another worktree.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/CutTheRopeGradle"
CUT_THE_ROPE_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$CUT_THE_ROPE_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$CUT_THE_ROPE_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0

mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.cut-the-rope-build-start.XXXXXX")"
trap 'rm -f "$build_marker"' EXIT
printf 'Building 割绳子测试. Log: %s\n' "$LOG_FILE"
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildCutTheRopeTest \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || ! "$APK_FILE" -nt "$build_marker" ]]; then
        printf 'Unity did not create a fresh Cut the Rope APK.\n' >&2
        exit 1
    fi
    python3 "$PROJECT_ROOT/scripts/verify-cut-the-rope-apk.py" "$APK_FILE"
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
