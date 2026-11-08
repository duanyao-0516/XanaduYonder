#!/usr/bin/env bash
set -euo pipefail

install_after_build=false
device_serial=""
if [[ ${1:-} == --help || ${1:-} == -h ]]; then
    printf 'Usage: %s [--install [DEVICE_SERIAL]]\nBuild the isolated, locally signed Debug app without Unity Development mode.\n' "$0"
    exit 0
fi
if [[ $# -gt 0 ]]; then
    if [[ $1 != --install || $# -gt 2 || ${2:-} == -* ]]; then
        printf 'Usage: %s [--install [DEVICE_SERIAL]]\n' "$0" >&2
        exit 2
    fi
    install_after_build=true
    device_serial="${2:-}"
fi

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-android.log"
source "$PROJECT_ROOT/scripts/project-version.sh"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-debug.apk"
method="XanaduYonder.EditorTools.ProjectBuild.BuildAndroidDeviceDebug"

if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\nSet UNITY_EDITOR to the Editor executable.\n' "$UNITY_EDITOR" >&2
    exit 1
fi
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
python3 "$PROJECT_ROOT/scripts/bomb-it/build-content.py"
build_start="$(mktemp "$PROJECT_ROOT/artifacts/builds/.debug-build-start.XXXXXX")"
trap 'rm -f "$build_start"' EXIT
unset ANDROID_LINT_JARS XANADU_KEYSTORE_PATH XANADU_KEY_ALIAS XANADU_KEYSTORE_PASSWORD XANADU_KEY_PASSWORD

printf 'Building Android with %s\nLog: %s\n' "$UNITY_EDITOR" "$LOG_FILE"
# Keep Unity's temporary ADB server out of the later device installation.
if (
    source "$PROJECT_ROOT/scripts/startup-unity-environment.sh" || exit $?
    "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
        -buildTarget Android -executeMethod "$method" -logFile -
) 2>&1 | tee "$LOG_FILE"; then
    if [[ ! -s "$APK_FILE" || -z "$(find "$APK_FILE" -newer "$build_start" -print)" ]]; then
        printf 'Unity did not produce a fresh debug APK: %s\n' "$APK_FILE" >&2
        exit 1
    fi
else
    statuses=("${PIPESTATUS[@]}")
    if [[ ${statuses[0]} != 0 ]]; then
        printf 'Unity build failed (exit %s). Log: %s\n' "${statuses[0]}" "$LOG_FILE" >&2
        exit "${statuses[0]}"
    fi
    printf 'Could not write Unity build log: %s\n' "$LOG_FILE" >&2
    exit "${statuses[1]}"
fi
python3 "$PROJECT_ROOT/scripts/verify-android-runtime.py" "$APK_FILE" --expected-package com.duanyao.xanaduyonder.debug
printf 'APK: %s\n' "$APK_FILE"

if [[ $install_after_build == true ]]; then
    "$PROJECT_ROOT/scripts/install-android.sh" "$device_serial" "$APK_FILE" com.duanyao.xanaduyonder.debug
fi
