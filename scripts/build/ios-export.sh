#!/usr/bin/env bash
set -euo pipefail

if [[ $# -eq 1 && ( $1 == --help || $1 == -h ) ]]; then
    cat <<'HELP'
Usage: scripts/build.sh ios
Export the Unity-native games to artifacts/ios/xcode using a filtered project copy.
This command does not compile, sign, or install an iOS application with Xcode.

Environment:
  UNITY_EDITOR          Unity executable (defaults to the project's pinned Hub version).
  XANADU_IOS_TEAM_ID     Optional Apple development team ID; no credentials are accepted.

The Unity iOS Build Support module must be installed for that Editor version.
Existing Xcode output is preserved as artifacts/ios/previous-xcode-<timestamp>.
The source Assets and ProjectSettings are never used as the build workspace.
HELP
    exit 0
fi
if [[ $# -ne 0 ]]; then
    printf 'Usage: %s [--help]\n' "$0" >&2
    exit 2
fi

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\nSet UNITY_EDITOR to the Editor executable.\n' "$UNITY_EDITOR" >&2
    exit 1
fi
IOS_SUPPORT="$UNITY_VERSION_ROOT/PlaybackEngines/iOSSupport"
if [[ ! -d "$IOS_SUPPORT" ]]; then
    IOS_SUPPORT="$UNITY_CONTENTS/PlaybackEngines/iOSSupport"
fi
if [[ ! -f "$IOS_SUPPORT/UnityEditor.iOS.Extensions.dll" ]]; then
    printf 'Unity iOS Build Support is missing for this Editor: %s\nInstall it through Unity Hub.\n' "$UNITY_VERSION_ROOT" >&2
    exit 1
fi

IOS_ROOT="$PROJECT_ROOT/artifacts/ios"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-ios.log"
export XANADU_IOS_XCODE_PATH="$IOS_ROOT/xcode"
# Reject linked artifact directories before creating logs, locks, or moving an old export.
python3 - "$PROJECT_ROOT" <<'PY'
from pathlib import Path
import sys
root = Path(sys.argv[1])
for relative in ('artifacts/ios', 'artifacts/ios/xcode', 'artifacts/logs', 'artifacts/logs/build-ios.log'):
    path = root / relative
    if path.resolve() != path:
        raise SystemExit(f'Refusing unsafe artifact path: {path}')
PY
mkdir -p "$IOS_ROOT" "$(dirname "$LOG_FILE")"
if ! mkdir "$IOS_ROOT/.build-lock"; then
    printf 'An iOS build lock already exists: %s\n' "$IOS_ROOT/.build-lock" >&2
    exit 1
fi
build_start=""
trap '[[ -z "$build_start" ]] || rm -f "$build_start"; rmdir "$IOS_ROOT/.build-lock"' EXIT
python3 "$PROJECT_ROOT/scripts/prepare-ios-project.py"
if [[ -e "$XANADU_IOS_XCODE_PATH" ]]; then
    previous="$IOS_ROOT/previous-xcode-$(date -u +%Y%m%dT%H%M%SZ)-$$"
    if [[ -e "$previous" ]]; then
        printf 'Previous export destination already exists: %s\n' "$previous" >&2
        exit 1
    fi
    mv "$XANADU_IOS_XCODE_PATH" "$previous"
    printf 'Preserved previous Xcode export: %s\n' "$previous"
fi
build_start="$(mktemp "$IOS_ROOT/.build-start.XXXXXX")"
printf 'Exporting iOS with %s\nLog: %s\n' "$UNITY_EDITOR" "$LOG_FILE"
if "$UNITY_EDITOR" -batchmode -nographics -quit -buildTarget iOS \
    -projectPath "$IOS_ROOT/project" \
    -executeMethod XanaduYonder.EditorTools.ProjectBuildIOS.Export -logFile - 2>&1 | tee "$LOG_FILE"; then
    python3 - "$XANADU_IOS_XCODE_PATH" "$build_start" <<'PY'
from pathlib import Path
import sys
output, started = map(Path, sys.argv[1:])
project = output / 'Unity-iPhone.xcodeproj/project.pbxproj'
# The previous export was moved away. Equal timestamps are valid on coarse filesystems.
if (not project.is_file() or project.stat().st_size == 0
        or project.stat().st_mtime_ns < started.stat().st_mtime_ns
        or not (output / 'Data').is_dir()):
    raise SystemExit(f'Unity did not produce a fresh Xcode project: {output}')
PY
else
    statuses=("${PIPESTATUS[@]}")
    if [[ ${statuses[0]} != 0 ]]; then
        printf 'Unity export failed (exit %s). Log: %s\n' "${statuses[0]}" "$LOG_FILE" >&2
        exit "${statuses[0]}"
    fi
    printf 'Could not write Unity export log: %s\n' "$LOG_FILE" >&2
    exit "${statuses[1]}"
fi
printf 'Xcode project: %s\n' "$XANADU_IOS_XCODE_PATH/Unity-iPhone.xcodeproj"
