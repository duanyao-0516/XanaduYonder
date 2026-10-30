#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-startup-validation.log"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi

BUILD_VERSION="$(python3 - "$PROJECT_ROOT/Assets/Editor/ProjectBuild.cs" <<'PY'
from pathlib import Path
import re
import sys
versions = re.findall(r'public\s+const\s+string\s+Version\s*=\s*"([0-9][0-9A-Za-z.+-]*)"\s*;', Path(sys.argv[1]).read_text())
if len(versions) != 1:
    raise SystemExit('Expected exactly one ProjectBuild.Version constant')
print(versions[0])
PY
)"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-$BUILD_VERSION-startup-validation.apk"

source "$PROJECT_ROOT/scripts/startup-unity-environment.sh"

mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
rm -f "$APK_FILE" "$LOG_FILE"
printf 'Building isolated startup Android APK. Log: %s\n' "$LOG_FILE"
if env -u XANADU_KEYSTORE_PATH -u XANADU_KEY_ALIAS \
    -u XANADU_KEYSTORE_PASSWORD -u XANADU_KEY_PASSWORD \
    "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidStartupValidation \
    -logFile "$LOG_FILE"; then
    [[ -s "$APK_FILE" ]] || { printf 'Missing APK from this build: %s\n' "$APK_FILE" >&2; exit 1; }
    if ! rg -Fq -- "Android APK: $APK_FILE (" "$LOG_FILE"; then
        printf 'Missing successful APK evidence in build log: %s\n' "$LOG_FILE" >&2
        tail -n 80 "$LOG_FILE" >&2 || true
        exit 1
    fi
    printf 'APK: %s\n' "$APK_FILE"
    shasum -a 256 "$APK_FILE"
else
    build_status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$build_status"
fi
