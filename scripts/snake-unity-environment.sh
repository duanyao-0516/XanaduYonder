#!/usr/bin/env bash
# Source after PROJECT_ROOT is set; isolate this task from other Unity sessions.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/SnakeGradle"
SNAKE_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$SNAKE_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$SNAKE_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
