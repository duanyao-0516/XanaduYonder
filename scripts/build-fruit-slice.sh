#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
source "$PROJECT_ROOT/scripts/startup-unity-environment.sh"
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/FruitSliceGradle"
VERSION="$(sed -n 's/.*public const string Version = "\([^"]*\)";.*/\1/p' "$PROJECT_ROOT/Assets/Editor/ProjectBuild.cs")"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-fruit-slice.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-$VERSION-fruit-slice-preview.apk"
python3 "$PROJECT_ROOT/scripts/kingdom-rush/verify-build-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/fruit-slice/build-content.py"
python3 "$PROJECT_ROOT/scripts/fruit-slice/verify-content.py"
python3 "$PROJECT_ROOT/scripts/prepare-fruit-slice-artwork.py"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.fruit-slice-build-start.XXXXXX")"
trap 'rm -f "$build_marker"' EXIT
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildFruitSlicePreview \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || ! "$APK_FILE" -nt "$build_marker" ]]; then
        printf 'Unity did not create a fresh Fruit Slice APK.\n' >&2
        exit 1
    fi
    python3 "$PROJECT_ROOT/scripts/verify-fruit-slice-apk.py" "$APK_FILE"
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
