#!/usr/bin/env bash
set -euo pipefail
umask 077

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
UNITY_CONTENTS="$(dirname "$(dirname "$UNITY_EDITOR")")"
ANDROID_PLAYER="$UNITY_CONTENTS/PlaybackEngines/AndroidPlayer"
if [[ ! -d "$ANDROID_PLAYER" ]]; then
    ANDROID_PLAYER="$(dirname "$(dirname "$UNITY_CONTENTS")")/PlaybackEngines/AndroidPlayer"
fi
APKSIGNER="${APKSIGNER:-$ANDROID_PLAYER/SDK/build-tools/36.0.0/apksigner}"
AAPT="${AAPT:-$ANDROID_PLAYER/SDK/build-tools/36.0.0/aapt}"
if [[ -d "$ANDROID_PLAYER/OpenJDK" ]]; then export JAVA_HOME="$ANDROID_PLAYER/OpenJDK"; fi

for variable in XANADU_KEYSTORE_PATH XANADU_KEY_ALIAS XANADU_KEYSTORE_PASSWORD XANADU_KEY_PASSWORD; do
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
INTERMEDIATE_APK="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.1.0-release-intermediate.apk"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.1.0-android-arm64.apk"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
signed_apk="$(mktemp "$PROJECT_ROOT/artifacts/builds/.release-signed.XXXXXX")"
trap 'rm -f "$signed_apk" "$INTERMEDIATE_APK"' EXIT
rm -f "$INTERMEDIATE_APK"

printf 'Building release APK. Unity log: %s\n' "$LOG_FILE"
# Prevent credentials from entering the Unity process, generated Gradle files or its logs.
if ! env -u XANADU_KEYSTORE_PATH -u XANADU_KEY_ALIAS \
    -u XANADU_KEYSTORE_PASSWORD -u XANADU_KEY_PASSWORD \
    "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidRelease \
    -logFile "$LOG_FILE"; then
    printf 'Release build failed; inspect the local Unity log. No new release APK was published.\n' >&2
    exit 1
fi
if [[ ! -s "$INTERMEDIATE_APK" ]]; then
    printf 'Unity did not create a fresh release intermediate.\n' >&2
    exit 1
fi
badging="$("$AAPT" dump badging "$INTERMEDIATE_APK")"
if [[ "$badging" == *application-debuggable* ]]; then
    printf 'Refusing to sign a debuggable APK as a release.\n' >&2
    exit 1
fi

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
mv -f "$signed_apk" "$APK_FILE"
printf 'Release APK: %s\n' "$APK_FILE"
shasum -a 256 "$APK_FILE"
