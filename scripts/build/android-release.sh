#!/usr/bin/env bash
set +x
set -euo pipefail
umask 077

install_after_build=false
device_serial=""
if [[ ${1:-} == --help || ${1:-} == -h ]]; then
    printf 'Usage: %s [--install [DEVICE_SERIAL]]\nBuild and sign the release app; optionally replace the same-package, same-signature installation.\n' "$0"
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
ANDROID_PLAYER="$UNITY_CONTENTS/PlaybackEngines/AndroidPlayer"
if [[ ! -d "$ANDROID_PLAYER" ]]; then
    ANDROID_PLAYER="$UNITY_VERSION_ROOT/PlaybackEngines/AndroidPlayer"
fi
APKSIGNER="${APKSIGNER:-$ANDROID_PLAYER/SDK/build-tools/36.0.0/apksigner}"
AAPT="${AAPT:-$ANDROID_PLAYER/SDK/build-tools/36.0.0/aapt}"
if [[ -d "$ANDROID_PLAYER/OpenJDK" ]]; then export JAVA_HOME="$ANDROID_PLAYER/OpenJDK"; fi

signing_keys=(XANADU_KEYSTORE_PATH XANADU_KEY_ALIAS XANADU_KEYSTORE_PASSWORD XANADU_KEY_PASSWORD)
if [[ -z ${XANADU_KEYSTORE_PATH+x} && -z ${XANADU_KEY_ALIAS+x} &&
      -z ${XANADU_KEYSTORE_PASSWORD+x} && -z ${XANADU_KEY_PASSWORD+x} ]]; then
    # Read literal JSON strings through a private pipe, including spaces and shell metacharacters.
    signing_file="$HOME/Library/Application Support/XanaduYonder/signing/release-signing.json"
    for variable in "${signing_keys[@]}"; do
        IFS= read -r -d '' "$variable" || exit 2
        export "$variable"
    done < <(python3 - "$signing_file" "${signing_keys[@]}" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
try:
    config = json.loads(path.read_text(encoding="utf-8"))
    values = [config[key] for key in sys.argv[2:]]
    if not all(isinstance(value, str) and value and "\0" not in value for value in values):
        raise ValueError
    payload = b"\0".join(value.encode("utf-8") for value in values) + b"\0"
except (OSError, KeyError, TypeError, ValueError):
    sys.exit(f"Missing or invalid signing configuration: {path}. Alternatively, set all four signing variables.")
sys.stdout.buffer.write(payload)
PY
)
fi
for variable in "${signing_keys[@]}"; do
    if [[ -z "${!variable:-}" ]]; then
        printf 'Required signing environment variable is missing: %s\n' "$variable" >&2
        exit 2
    fi
done
if [[ ! -f "$XANADU_KEYSTORE_PATH" || ! -r "$XANADU_KEYSTORE_PATH" ]]; then
    printf 'Release keystore is missing or unreadable.\n' >&2
    exit 2
fi
for executable in "$UNITY_EDITOR" "$APKSIGNER" "$AAPT"; do
    if [[ ! -x "$executable" ]]; then
        printf 'Required build tool is not executable: %s\n' "$executable" >&2
        exit 2
    fi
done

LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-android-release.log"
SIGN_LOG="$PROJECT_ROOT/artifacts/logs/sign-android-release.log"
source "$PROJECT_ROOT/scripts/project-version.sh"
INTERMEDIATE_APK="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-release-intermediate.apk"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-android-arm64.apk"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
signed_apk="$(mktemp "$PROJECT_ROOT/artifacts/builds/.release-signed.XXXXXX")"
trap 'rm -f "$signed_apk" "$INTERMEDIATE_APK"' EXIT
rm -f "$INTERMEDIATE_APK"

printf 'Building release APK. Unity log: %s\n' "$LOG_FILE"
# Prevent credentials from entering the Unity process, generated Gradle files or its logs.
# Keep Unity's temporary ADB server out of the later device installation.
if ! (
    source "$PROJECT_ROOT/scripts/startup-unity-environment.sh" || exit $?
    env -u ANDROID_LINT_JARS -u XANADU_KEYSTORE_PATH -u XANADU_KEY_ALIAS \
        -u XANADU_KEYSTORE_PASSWORD -u XANADU_KEY_PASSWORD \
        "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
        -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidRelease \
        -logFile "$LOG_FILE"
); then
    printf 'Release build failed; inspect the local Unity log. No new release APK was published.\n' >&2
    exit 1
fi
if [[ ! -s "$INTERMEDIATE_APK" ]]; then
    printf 'Unity did not create a fresh release intermediate.\n' >&2
    exit 1
fi
python3 "$PROJECT_ROOT/scripts/verify-android-runtime.py" "$INTERMEDIATE_APK" \
    --aapt "$AAPT" --expected-package com.duanyao.xanaduyonder

if ! "$APKSIGNER" sign --ks "$XANADU_KEYSTORE_PATH" --ks-key-alias "$XANADU_KEY_ALIAS" \
    --ks-pass env:XANADU_KEYSTORE_PASSWORD --key-pass env:XANADU_KEY_PASSWORD \
    --v4-signing-enabled false --out "$signed_apk" "$INTERMEDIATE_APK" > "$SIGN_LOG" 2>&1; then
    printf 'Release signing failed; inspect the private local signing log. No new release APK was published.\n' >&2
    exit 1
fi
if ! "$APKSIGNER" verify --verbose --print-certs "$signed_apk" >> "$SIGN_LOG" 2>&1; then
    printf 'Release signature verification failed. No new release APK was published.\n' >&2
    exit 1
fi
if [[ ! -s "$signed_apk" ]]; then
    printf 'The verified release APK is empty.\n' >&2
    exit 1
fi
python3 "$PROJECT_ROOT/scripts/verify-removed-games.py" --artifact "$signed_apk" \
    --packed-assets "$INTERMEDIATE_APK.packed-assets.txt"
mv -f "$signed_apk" "$APK_FILE"
mv -f "$INTERMEDIATE_APK.packed-assets.txt" "$APK_FILE.packed-assets.txt"
printf 'Release APK: %s\n' "$APK_FILE"
shasum -a 256 "$APK_FILE"

if [[ $install_after_build == true ]]; then
    unset "${signing_keys[@]}"
    "$PROJECT_ROOT/scripts/install-android.sh" "$device_serial" "$APK_FILE" com.duanyao.xanaduyonder
fi
