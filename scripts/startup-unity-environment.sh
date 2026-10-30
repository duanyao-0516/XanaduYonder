#!/usr/bin/env bash
# Keep this worktree's Unity checks/builds away from shared Gradle and ADB services.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/StartupGradle"
STARTUP_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$STARTUP_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$STARTUP_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
