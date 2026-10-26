#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
UNITY_CONTENTS="$(dirname "$(dirname "$UNITY_EDITOR")")"
ANDROID_PLAYER="$UNITY_CONTENTS/PlaybackEngines/AndroidPlayer"
if [[ ! -d "$ANDROID_PLAYER" ]]; then
    ANDROID_PLAYER="$(dirname "$(dirname "$UNITY_CONTENTS")")/PlaybackEngines/AndroidPlayer"
fi
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
CODE="${CODE:-/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code}"
errors=0

require_path() {
    if [[ -e "$2" ]]; then
        printf 'OK   %-24s %s\n' "$1" "$2"
    else
        printf 'MISS %-24s %s\n' "$1" "$2" >&2
        errors=$((errors + 1))
    fi
}

printf 'Host: %s / %s\n' "$(uname -s)" "$(uname -m)"
printf 'Project: %s\n' "$PROJECT_ROOT"
require_path 'Unity Editor' "$UNITY_EDITOR"
if [[ -f "$UNITY_CONTENTS/Info.plist" ]]; then
    actual_version=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$UNITY_CONTENTS/Info.plist")
    expected_version=$(awk '/^m_EditorVersion:/ {print $2}' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")
    printf 'Unity version: %s (project: %s)\n' "$actual_version" "$expected_version"
    if [[ "$actual_version" != "$expected_version" ]]; then
        printf 'Editor version differs from the pinned project version.\n' >&2
        errors=$((errors + 1))
    fi
fi
require_path 'Android Build Support' "$ANDROID_PLAYER/Variations/il2cpp"
require_path 'Android SDK' "$ANDROID_PLAYER/SDK/platforms"
require_path 'Android SDK build tools' "$ANDROID_PLAYER/SDK/build-tools"
require_path 'Android NDK' "$ANDROID_PLAYER/NDK/source.properties"
require_path 'OpenJDK' "$ANDROID_PLAYER/OpenJDK/bin/java"
if [[ -f "$ANDROID_PLAYER/NDK/source.properties" ]]; then
    awk '/Pkg.Revision/ {print "NDK " $0}' "$ANDROID_PLAYER/NDK/source.properties"
fi
if [[ -d "$ANDROID_PLAYER/SDK/platforms" ]]; then
    printf 'SDK platforms: '
    printf '%s\n' "$ANDROID_PLAYER"/SDK/platforms/android-* | sed 's|.*/||' | tr '\n' ' '
    printf '\n'
fi
if [[ -d "$ANDROID_PLAYER/SDK/build-tools" ]]; then
    printf 'SDK build tools: '
    printf '%s\n' "$ANDROID_PLAYER"/SDK/build-tools/* | sed 's|.*/||' | tr '\n' ' '
    printf '\n'
fi
if [[ -x "$ANDROID_PLAYER/OpenJDK/bin/java" ]]; then
    "$ANDROID_PLAYER/OpenJDK/bin/java" -version 2>&1
fi
require_path '.NET SDK' "$DOTNET"
if [[ -x "$DOTNET" ]]; then "$DOTNET" --version; fi
require_path 'VS Code' "$CODE"
if [[ -x "$CODE" ]]; then
    "$CODE" --version
    extensions=$("$CODE" --list-extensions --show-versions)
    unity_extension=$(printf '%s\n' "$extensions" | awk 'tolower($0) ~ /^visualstudiotoolsforunity.vstuc@/')
    if [[ -n "$unity_extension" ]]; then
        printf 'Unity extension: %s\n' "$unity_extension"
    else
        printf 'MISS VS Code Unity extension (visualstudiotoolsforunity.vstuc)\n' >&2
        errors=$((errors + 1))
    fi
fi

ADB="${ADB:-$ANDROID_PLAYER/SDK/platform-tools/adb}"
if [[ ! -x "$ADB" ]]; then ADB="$(command -v adb || true)"; fi
if [[ ! -x "$ADB" && -x "$HOME/Library/Android/sdk/platform-tools/adb" ]]; then
    ADB="$HOME/Library/Android/sdk/platform-tools/adb"
fi
if [[ -x "$ADB" ]]; then
    "$ADB" version
    "$ADB" devices -l
    while read -r serial state rest; do
        [[ "$state" == device ]] || continue
        model=$("$ADB" -s "$serial" shell getprop ro.product.model | tr -d '\r')
        abi=$("$ADB" -s "$serial" shell getprop ro.product.cpu.abilist | tr -d '\r')
        emulator=$("$ADB" -s "$serial" shell getprop ro.kernel.qemu | tr -d '\r')
        printf 'Device: %s | model=%s | ABI=%s | emulator=%s\n' "$serial" "$model" "$abi" "${emulator:-0}"
    done < <("$ADB" devices)
else
    printf 'MISS adb (set ADB to its executable path).\n' >&2
    errors=$((errors + 1))
fi
printf 'Environment check: %s missing or mismatched requirements. Unity licensing is verified by the subsequent batch run.\n' "$errors"
[[ "$errors" -eq 0 ]]
