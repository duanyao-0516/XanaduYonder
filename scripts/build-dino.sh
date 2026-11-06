#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-dino.log"
source "$PROJECT_ROOT/scripts/project-version.sh"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-${PROJECT_VERSION}-dino-validation.apk"
mkdir -p "$PROJECT_ROOT/artifacts/locks" "$(dirname "$LOG_FILE")" "$(dirname "$APK_FILE")"
LOCK_DIR="$PROJECT_ROOT/artifacts/locks/dino-build"
if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  printf 'A Dino build lock already exists: %s\n' "$LOCK_DIR" >&2
  exit 1
fi
trap 'rmdir "$LOCK_DIR"' EXIT
build_marker="$(mktemp "$PROJECT_ROOT/artifacts/builds/.dino-build-start.XXXXXX")"
trap 'rm -f "$build_marker"; rmdir "$LOCK_DIR"' EXIT
printf 'Isolated project: %s\nApplication ID: com.duanyao.xanaduyonder.dinotest\nLog: %s\n' "$PROJECT_ROOT" "$LOG_FILE"
# Unity stops its ADB server on exit; give this build a private server socket.
ADB_SERVER_SOCKET=tcp:localhost:5039 "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" -buildTarget Android \
  -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidDinoValidation -logFile "$LOG_FILE"
[[ -s "$APK_FILE" && "$APK_FILE" -nt "$build_marker" ]] || { printf 'Missing fresh APK: %s\n' "$APK_FILE" >&2; exit 1; }
printf 'APK: %s\n' "$APK_FILE"
