#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
UNITY_CONTENTS="$(dirname "$(dirname "$UNITY_EDITOR")")"
ANDROID_PLAYER="$UNITY_CONTENTS/PlaybackEngines/AndroidPlayer"
if [[ ! -d "$ANDROID_PLAYER" ]]; then
    ANDROID_PLAYER="$(dirname "$(dirname "$UNITY_CONTENTS")")/PlaybackEngines/AndroidPlayer"
fi
ADB="${ADB:-$ANDROID_PLAYER/SDK/platform-tools/adb}"
if [[ ! -x "$ADB" ]]; then ADB="$(command -v adb || true)"; fi
if [[ ! -x "$ADB" && -x "$HOME/Library/Android/sdk/platform-tools/adb" ]]; then
    ADB="$HOME/Library/Android/sdk/platform-tools/adb"
fi
if [[ $# -gt 2 ]]; then
    printf 'Too many arguments. Usage: %s [PHYSICAL_DEVICE_SERIAL] [APK_PATH]\n' "$0" >&2
    exit 2
fi
if [[ "${1:-}" == -h || "${1:-}" == --help ]]; then
    printf 'Usage: %s [PHYSICAL_DEVICE_SERIAL] [APK_PATH]\nWithout a serial, exactly one connected, authorized physical device is required.\n' "$0"
    exit 0
fi
if [[ ! -x "$ADB" ]]; then
    printf 'adb not found. Set ADB to its executable path.\n' >&2
    exit 1
fi
APK_FILE="${2:-$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.6.0.apk}"
if [[ ! -s "$APK_FILE" ]]; then
    printf 'APK missing or empty: %s\nRun scripts/build-android.sh first.\n' "$APK_FILE" >&2
    exit 1
fi
serial="${1:-${ANDROID_SERIAL:-}}"
if [[ -z "$serial" ]]; then
    physical_devices=()
    uncertain_devices=0
    while read -r candidate state rest; do
        [[ -n "$candidate" && "$candidate" != List && "$candidate" != emulator-* ]] || continue
        if [[ "$state" != device ]]; then
            uncertain_devices=$((uncertain_devices + 1))
            continue
        fi
        is_emulator=$("$ADB" -s "$candidate" shell getprop ro.kernel.qemu | tr -d '\r')
        if [[ "$is_emulator" != 1 ]]; then physical_devices+=("$candidate"); fi
    done < <("$ADB" devices)
    if [[ ${#physical_devices[@]} -ne 1 || "$uncertain_devices" -ne 0 ]]; then
        printf 'Pass an explicit physical device serial; found %s ready and %s unavailable candidates.\n' \
            "${#physical_devices[@]}" "$uncertain_devices" >&2
        "$ADB" devices -l >&2
        exit 1
    fi
    serial="${physical_devices[0]}"
fi
if [[ "$serial" == emulator-* ]]; then
    printf 'This ARM64 device build must be installed on a physical Android device.\n' >&2
    exit 1
fi
if [[ "$("$ADB" -s "$serial" get-state 2>/dev/null || true)" != device ]]; then
    printf 'Device is not connected and authorized: %s\n' "$serial" >&2
    exit 1
fi
is_emulator=$("$ADB" -s "$serial" shell getprop ro.kernel.qemu | tr -d '\r')
if [[ "$is_emulator" == 1 ]]; then
    printf 'Refusing emulator target: %s\n' "$serial" >&2
    exit 1
fi
abis=$("$ADB" -s "$serial" shell getprop ro.product.cpu.abilist | tr -d '\r')
if [[ ",$abis," != *,arm64-v8a,* ]]; then
    printf 'Device %s does not advertise arm64-v8a: %s\n' "$serial" "$abis" >&2
    exit 1
fi
printf 'Installing %s on physical device %s\n' "$APK_FILE" "$serial"
"$ADB" -s "$serial" install -r "$APK_FILE"
"$ADB" -s "$serial" shell am start -n com.duanyao.xanaduyonder/com.unity3d.player.UnityPlayerActivity
