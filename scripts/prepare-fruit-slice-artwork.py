#!/usr/bin/env python3
"""Verify the tracked large artwork before packaging; never restore the old thumbnail."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
for entry in json.loads((root / 'ThirdParty/FruitSliceHost/artwork.json').read_text()):
    target = root / entry['target']
    if not target.is_file() or not target.with_suffix(target.suffix + '.meta').is_file():
        raise SystemExit('Missing tracked Fruit Slice artwork or Unity metadata: ' + str(target))
    data = target.read_bytes()
    if len(data) != entry['bytes'] or hashlib.sha256(data).hexdigest() != entry['sha256']:
        raise SystemExit('Fruit Slice artwork differs from its recorded source: ' + str(target))
print('Tracked Fruit Slice lobby artwork hashes verified')
