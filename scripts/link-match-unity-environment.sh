#!/usr/bin/env bash
# Source after PROJECT_ROOT is set. Keep Unity's Gradle and ADB shutdown scoped
# to this validation worktree, without changing another task's registries.
export GRADLE_USER_HOME="$PROJECT_ROOT/Library/LinkMatchGradle"
LINK_MATCH_ADB_PORT="$(python3 - <<'PY'
import socket
with socket.socket() as listener:
    listener.bind(('127.0.0.1', 0))
    print(listener.getsockname()[1])
PY
)"
export ADB_SERVER_SOCKET="tcp:127.0.0.1:$LINK_MATCH_ADB_PORT"
export ANDROID_ADB_SERVER_PORT="$LINK_MATCH_ADB_PORT"
export ADB_MDNS_AUTO_CONNECT=0
