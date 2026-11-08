#!/usr/bin/env bash
# Shared repository and pinned Editor discovery for the platform build backends.
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
UNITY_VERSION="$(awk '/^m_EditorVersion:/ {print $2}' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")"
if [[ ! "$UNITY_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+$ ]]; then
    printf 'Cannot read the pinned Unity Editor version.\n' >&2
    return 1
fi
export UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
UNITY_CONTENTS="$(dirname "$(dirname "$UNITY_EDITOR")")"
UNITY_VERSION_ROOT="$(dirname "$(dirname "$UNITY_CONTENTS")")"
if [[ -x "$UNITY_EDITOR" ]]; then
    # Resolve linked Editor bundles before locating modules beside Unity.app.
    UNITY_CONTENTS="$(cd "$(dirname "$UNITY_EDITOR")/.." && pwd -P)"
    UNITY_VERSION_ROOT="$(cd "$UNITY_CONTENTS/../.." && pwd -P)"
fi
