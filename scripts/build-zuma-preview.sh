#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$PROJECT_ROOT/artifacts/logs"
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_ROOT" \
  -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.BuildZumaPreview \
  -logFile "$PROJECT_ROOT/artifacts/logs/build-zuma-preview.log"
test -s "$PROJECT_ROOT/artifacts/builds/XanaduYonder-0.3.0-zuma-preview.apk"
