#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
source "$PROJECT_ROOT/scripts/startup-unity-environment.sh"
# This standalone collection does not use workstation-injected custom lint JARs.
# Keep standard Android lint enabled and avoid importing unrelated build hooks.
unset ANDROID_LINT_JARS
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-level-devil.log"
source "$PROJECT_ROOT/scripts/project-version.sh"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-level-devil-preview.apk"
python3 "$PROJECT_ROOT/scripts/kingdom-rush/verify-build-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/level-devil/build-content.py"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.level-devil-build-start.XXXXXX")"
trap 'rm -f "$build_marker"' EXIT
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildLevelDevilPreview \
    -logFile "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || ! "$APK_FILE" -nt "$build_marker" ]]; then
        printf 'Unity did not create a fresh Level Devil APK.\n' >&2
        exit 1
    fi
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
