#!/usr/bin/env bash
set -euo pipefail

# Compatibility entry for the filtered iOS export.
if [[ $# -gt 1 || ( $# -eq 1 && $1 != --help && $1 != -h ) ]]; then
    printf 'Usage: %s [--help]\n' "$0" >&2
    exit 2
fi
exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)/build.sh" ios "$@"
