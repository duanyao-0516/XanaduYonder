#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
source "$PROJECT_ROOT/scripts/startup-unity-environment.sh"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-aircraft-fire.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.9.4.apk"
python3 "$PROJECT_ROOT/scripts/kingdom-rush/verify-build-input.py" --root "$PROJECT_ROOT"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.aircraft-build-start.XXXXXX")"
trap 'rm -f "$build_marker"' EXIT
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAircraftFirePreview \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || ! "$APK_FILE" -nt "$build_marker" ]]; then
        printf 'Unity did not create a fresh Aircraft Fire APK.\n' >&2
        exit 1
    fi
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
