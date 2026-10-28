#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-sheep-validation.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.7.0-sheep-validation.apk"

if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\nSet UNITY_EDITOR to the Editor executable.\n' "$UNITY_EDITOR" >&2
    exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")"
printf 'Building Android with %s\nLog: %s\n' "$UNITY_EDITOR" "$LOG_FILE"
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidSheepValidation \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" ]]; then
        printf 'Unity exited successfully but APK is missing or empty: %s\n' "$APK_FILE" >&2
        exit 1
    fi
    printf 'APK: %s\n' "$APK_FILE"
else
    build_status=$?
    printf 'Unity build failed (exit %s). Last log lines:\n' "$build_status" >&2
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
