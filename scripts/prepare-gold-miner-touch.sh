#!/usr/bin/env bash
# Compile only. Device selection, adb push, and app_process execution are separate explicit actions.
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ANDROID_PLAYER="${UNITY_ANDROID_PLAYER:-/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer}"
TOUCH_JDK="$ANDROID_PLAYER/OpenJDK"
TOUCH_SDK="$ANDROID_PLAYER/SDK"
ANDROID_JAR="$TOUCH_SDK/platforms/android-36/android.jar"
OUTPUT_DIR="$PROJECT_ROOT/artifacts/gold-miner-development/device-tools"
OUTPUT_JAR="$OUTPUT_DIR/gold-miner-touch.jar"

for executable in "$TOUCH_JDK/bin/javac" "$TOUCH_JDK/bin/java" "$TOUCH_JDK/bin/jar"; do
    [[ -x "$executable" ]] || { printf 'Required JDK executable missing: %s\n' "$executable" >&2; exit 1; }
done
[[ -f "$ANDROID_JAR" ]] || { printf 'Android 36 SDK jar missing: %s\n' "$ANDROID_JAR" >&2; exit 1; }
D8=""
for candidate in "$TOUCH_SDK"/build-tools/36*/d8; do
    if [[ -x "$candidate" ]]; then D8="$candidate"; fi
done
[[ -n "$D8" ]] || { printf 'Android build-tools 36 D8 not found: %s\n' "$TOUCH_SDK" >&2; exit 1; }

mkdir -p "$OUTPUT_DIR"
BUILD_DIR="$(mktemp -d "$OUTPUT_DIR/.touch-build.XXXXXX")"
trap 'rm -rf "$BUILD_DIR"' EXIT
mkdir -p "$BUILD_DIR/classes" "$BUILD_DIR/dex"

"$TOUCH_JDK/bin/javac" --release 8 -Xlint:all -Werror -classpath "$ANDROID_JAR" \
    -d "$BUILD_DIR/classes" "$PROJECT_ROOT/Tests/GoldMinerDevice/NativeTouch.java"
"$TOUCH_JDK/bin/jar" cf "$BUILD_DIR/classes.jar" -C "$BUILD_DIR/classes" .
JAVA_HOME="$TOUCH_JDK" PATH="$TOUCH_JDK/bin:$PATH" "$D8" \
    --lib "$ANDROID_JAR" --min-api 23 --output "$BUILD_DIR/dex" "$BUILD_DIR/classes.jar"
[[ -s "$BUILD_DIR/dex/classes.dex" ]] || { printf 'D8 produced no classes.dex\n' >&2; exit 1; }
"$TOUCH_JDK/bin/jar" cf "$BUILD_DIR/gold-miner-touch.jar" -C "$BUILD_DIR/dex" classes.dex
mv "$BUILD_DIR/gold-miner-touch.jar" "$OUTPUT_JAR"

printf 'Compiled shell driver: %s\n' "$OUTPUT_JAR"
printf 'Built with Android 36, %s\n' "$D8"
printf '%s\n' 'No device was contacted. After choosing an explicit adb serial:'
printf '  adb -s SERIAL push "%s" /data/local/tmp/gold-miner-touch.jar\n' "$OUTPUT_JAR"
printf '%s\n' '  adb -s SERIAL shell CLASSPATH=/data/local/tmp/gold-miner-touch.jar app_process / NativeTouch --help'
