#!/usr/bin/env bash
# Source after PROJECT_ROOT is set. Unity may stop Gradle/ADB at batch exit;
# isolate this worktree's registries without touching another task's servers.
: "${PROJECT_ROOT:?Set PROJECT_ROOT before sourcing the Unity environment}"
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/SlidingPuzzleGradle"
SLIDING_PUZZLE_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$SLIDING_PUZZLE_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$SLIDING_PUZZLE_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
