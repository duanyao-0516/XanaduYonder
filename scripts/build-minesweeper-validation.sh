#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-minesweeper-validation.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.9.0-minesweeper-validation.apk"
source "$PROJECT_ROOT/scripts/minesweeper-unity-environment.sh"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"
printf 'Building isolated Minesweeper Android APK. Log: %s\n' "$LOG_FILE"
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidMinesweeperValidation \
    -logFile "$LOG_FILE"; then
    [[ -s "$APK_FILE" ]] || { printf 'Missing APK: %s\n' "$APK_FILE" >&2; exit 1; }
    printf 'APK: %s\n' "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
