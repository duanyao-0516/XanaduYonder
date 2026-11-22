#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/build/common.sh"
source "$PROJECT_ROOT/scripts/project-version.sh"
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/BakeryGradle"
export ANDROID_ADB_SERVER_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as port:
    port.bind(('127.0.0.1', 0))
    print(port.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$ANDROID_ADB_SERVER_PORT"
export ADB_MDNS_AUTO_CONNECT=0
LOG_FILE="$PROJECT_ROOT/artifacts/logs/bakery-android-v11-build.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-bakery-basket-prototype-v11.apk"
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
python3 "$PROJECT_ROOT/scripts/verify-cut-the-rope-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/zhaoyun-adou/verify-build-input.py" --root "$PROJECT_ROOT"
python3 "$PROJECT_ROOT/scripts/level-devil/build-content.py"
python3 "$PROJECT_ROOT/scripts/bomb-it/build-content.py"
marker="$(mktemp "$(dirname "$APK_FILE")/.bakery-build-start.XXXXXX")"
trap 'rm -f "$marker"' EXIT
unset ANDROID_LINT_JARS XANADU_KEYSTORE_PATH XANADU_KEY_ALIAS XANADU_KEYSTORE_PASSWORD XANADU_KEY_PASSWORD
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" -buildTarget Android \
    -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidBakeryBasketPrototype -logFile "$LOG_FILE" || {
    code=$?
    tail -n 100 "$LOG_FILE" >&2
    exit "$code"
}
[[ -s "$APK_FILE" && "$APK_FILE" -nt "$marker" ]]
printf 'APK: %s\n' "$APK_FILE"
