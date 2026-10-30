#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="$PROJECT_ROOT/artifacts/logs/build-gold-miner-validation.log"
APK_FILE="$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.9.0-gold-miner-validation.apk"
mkdir -p "$(dirname "$LOG_FILE")"
if [[ ! -x "$UNITY_EDITOR" ]]; then
    printf 'Unity Editor not found: %s\n' "$UNITY_EDITOR" >&2
    exit 1
fi
if "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
    -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildAndroidGoldMinerValidation \
    -logFile "$LOG_FILE"; then
    [[ -s "$APK_FILE" ]] || { printf 'APK missing: %s\n' "$APK_FILE" >&2; exit 1; }
    printf 'Isolated APK (com.duanyao.xanaduyonder.goldminertest): %s\n' "$APK_FILE"
else
    status=$?
    tail -n 80 "$LOG_FILE" >&2 || true
    exit "$status"
fi
