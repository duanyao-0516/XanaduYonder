#!/usr/bin/env bash
# Source after PROJECT_ROOT is set. Unity can stop Gradle/ADB at batch exit;
# use worktree-local caches and a private ADB listener instead of global state.
: "${PROJECT_ROOT:?Set PROJECT_ROOT to the Gomoku worktree before sourcing this file.}"
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/GomokuGradle"
GOMOKU_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$GOMOKU_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$GOMOKU_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
