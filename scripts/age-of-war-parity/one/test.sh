#!/bin/bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/../../.." && pwd)"
python3 "$repo/scripts/age-of-war-parity/one/native.py"
python3 "$repo/scripts/age-of-war-parity/one/compare.py" "$repo/Tests/AgeOfWarParity/One/fixtures/ruffle-controlled-first800.csv" /tmp/aow-parity-one/native.csv
python3 "$repo/scripts/age-of-war-parity/one/native.py" --projectiles
python3 "$repo/scripts/age-of-war-parity/one/compare-projectiles.py" "$repo/Tests/AgeOfWarParity/One/fixtures/projectiles/reference.csv" /tmp/aow-parity-one-projectiles/native.csv

python3 "$repo/scripts/age-of-war-parity/one/native-scenario.py" properties
python3 "$repo/scripts/age-of-war-parity/one/native-scenario.py" effects-special
python3 "$repo/scripts/age-of-war-parity/one/compare-effects-special.py" "$repo/Tests/AgeOfWarParity/One/fixtures/effects-special/reference.csv" /tmp/aow-parity-one-effects-special/native.csv
