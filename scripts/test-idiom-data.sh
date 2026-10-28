#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python3 -B -m unittest discover -s Tests/IdiomData -p 'test_*.py' -v
