#!/bin/zsh -f
# Deploy beside the verified renew-ios.py and config.json, outside the checkout.
# launchd calls --check. --force is reserved for an explicit manual rehearsal.
set -euo pipefail

task_root=${0:A:h}
mode=${1:---check}
if (( $# > 1 )) || [[ "$mode" != --check && "$mode" != --status && "$mode" != --force ]]; then
    print -u2 'Usage: run-ios-renewal.sh [--check|--status|--force]'
    exit 64
fi

expected_sha256=e2e4f6b7d2d6b6675172023e33de26297df9b0244731843bb9a878926b20b732
actual_sha256=$(/usr/bin/shasum -a 256 "$task_root/renew-ios.py")
if [[ ${actual_sha256%% *} != "$expected_sha256" ]]; then
    print '{"status":"failed","reason":"executor_hash_mismatch"}'
    exit 1
fi

export PATH=/usr/bin:/bin:/usr/sbin:/sbin
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
umask 077
# Prevent idle sleep only while a check/build/install is in progress. This does
# not wake a sleeping Mac or override lid closure or an explicit sleep request.
exec /usr/bin/caffeinate -i /usr/bin/python3 "$task_root/renew-ios.py" \
    --config "$task_root/config.json" "$mode"
