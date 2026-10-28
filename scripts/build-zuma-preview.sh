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
# Follow the same source of truth as BuildApk so a version bump cannot validate an old APK.
PROJECT_VERSION="$(python3 - "$PROJECT_ROOT/Assets/Editor/ProjectBuild.cs" <<'PY_VERSION'
from pathlib import Path
import re
import sys
matches = re.findall(r'public\s+const\s+string\s+Version\s*=\s*"([^"\r\n]+)"\s*;', Path(sys.argv[1]).read_text())
if len(matches) != 1 or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?', matches[0]):
    raise SystemExit("Cannot resolve a single valid ProjectBuild.Version.")
print(matches[0])
PY_VERSION
)"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-$PROJECT_VERSION-zuma-preview.apk"
mkdir -p "$PROJECT_ROOT/artifacts/logs"
# Unity stops Gradle/ADB when a batch exits. Keep its registries local to this
# checkout so building Zuma cannot shut down another task's device session.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/ZumaGradle"
ZUMA_ADB_PORT="$(python3 - <<'PY_ADB_PORT'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY_ADB_PORT
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$ZUMA_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$ZUMA_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
rm -f "$APK_FILE"
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
  -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildZumaPreview \
  -logFile "$PROJECT_ROOT/artifacts/logs/build-zuma-preview.log"
python3 "$PROJECT_ROOT/scripts/verify-zuma-preview.py" "$APK_FILE" \
  --aapt "$AAPT" --apksigner "$APKSIGNER" \
  2>&1 | tee "$PROJECT_ROOT/artifacts/logs/verify-zuma-preview.log"
