#!/usr/bin/env bash
set +x
set -euo pipefail

# Compatibility entry: keep the legacy mode fixed and accept only legacy options.
if [[ ${1:-} == --help || ${1:-} == -h ]]; then
    printf 'Usage: %s [--install [DEVICE_SERIAL]]\nBuild and sign the release app; optionally replace the same-package, same-signature installation.\nCompatibility entry for scripts/build.sh android --configuration release.\n' "$0"
    exit 0
fi
if [[ $# -gt 0 && ( $1 != --install || $# -gt 2 || ${2:-} == -* ) ]]; then
    printf 'Usage: %s [--install [DEVICE_SERIAL]]\n' "$0" >&2
    exit 2
fi
if [[ $# -eq 2 && -z $2 ]]; then set -- --install; fi
exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)/build.sh" android --configuration release "$@"
