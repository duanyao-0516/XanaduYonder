#!/usr/bin/env bash
# Every Unity invocation uses this worktree's own Gradle registry and ADB port.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/TetrisGradle"
TETRIS_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$TETRIS_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$TETRIS_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
