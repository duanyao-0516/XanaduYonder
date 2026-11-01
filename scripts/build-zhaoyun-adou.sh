#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
source "$PROJECT_ROOT/scripts/startup-unity-environment.sh"
ANDROID_AAPT="${ANDROID_AAPT:-/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-zhaoyun-adou.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.9.4-zhaoyun-adou-preview.apk"
python3 "$PROJECT_ROOT/scripts/kingdom-rush/verify-build-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/zhaoyun-adou/verify-build-input.py" --root "$PROJECT_ROOT"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.zhaoyun-build-start.XXXXXX")"
trap 'rm -f "$build_marker"' EXIT
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildZhaoyunAdouPreview \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || ! "$APK_FILE" -nt "$build_marker" ]]; then
        printf 'Unity did not create a fresh Zhaoyun Adou APK.\n' >&2
        exit 1
    fi
    python3 "$PROJECT_ROOT/scripts/zhaoyun-adou/verify-apk.py" "$APK_FILE" --aapt "$ANDROID_AAPT"
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
