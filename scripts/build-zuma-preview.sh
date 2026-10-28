#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
UNITY_CONTENTS="$(dirname "$(dirname "$UNITY_EDITOR")")"
ANDROID_PLAYER="$UNITY_CONTENTS/PlaybackEngines/AndroidPlayer"
if [[ ! -d "$ANDROID_PLAYER" ]]; then
    ANDROID_PLAYER="$(dirname "$(dirname "$UNITY_CONTENTS")")/PlaybackEngines/AndroidPlayer"
fi
AAPT="${AAPT:-$ANDROID_PLAYER/SDK/build-tools/36.0.0/aapt}"
APKSIGNER="${APKSIGNER:-$ANDROID_PLAYER/SDK/build-tools/36.0.0/apksigner}"
if [[ -d "$ANDROID_PLAYER/OpenJDK" ]]; then export JAVA_HOME="$ANDROID_PLAYER/OpenJDK"; fi
for executable in "$UNITY_EDITOR" "$AAPT" "$APKSIGNER"; do
    if [[ ! -x "$executable" ]]; then
        printf 'Required build tool is not executable: %s\n' "$executable" >&2
        exit 2
    fi
done
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.3.0-zuma-preview.apk"
mkdir -p "$PROJECT_ROOT/artifacts/logs"
rm -f "$APK_FILE"
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
  -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildZumaPreview \
  -logFile "$PROJECT_ROOT/artifacts/logs/build-zuma-preview.log"
python3 "$PROJECT_ROOT/scripts/verify-zuma-preview.py" "$APK_FILE" \
  --aapt "$AAPT" --apksigner "$APKSIGNER" \
  2>&1 | tee "$PROJECT_ROOT/artifacts/logs/verify-zuma-preview.log"
