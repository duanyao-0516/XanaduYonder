#!/usr/bin/env python3
"""Restore the unchanged, hash-pinned large logo; the compact line icon is tracked."""
import hashlib
import json
import re
from pathlib import Path
import urllib.request
import uuid

root = Path(__file__).resolve().parents[1]
template = (root / 'Assets/Resources/Lobby/Icons/aircraft-fire.png.meta').read_text()
for entry in json.loads((root / 'ThirdParty/FruitSliceHost/artwork.json').read_text()):
    target = root / entry['target']
    source = target if target.exists() else root / ('artifacts/fruit-slice-235576/evidence/lobby-' + entry['name'] + '.jpg')
    if not source.exists():
        request = urllib.request.Request(entry['source_url'], headers={'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(request, timeout=30) as response:
            data = response.read()
        assert hashlib.sha256(data).hexdigest() == entry['sha256'], 'Source illustration changed'
        source.parent.mkdir(parents=True, exist_ok=True)
        source.write_bytes(data)
    data = source.read_bytes()
    assert len(data) == entry['bytes'] and hashlib.sha256(data).hexdigest() == entry['sha256'], source
    name = 'fruit-slice-logo'
    target.write_bytes(data)
    metadata = re.sub(r'^guid: .*$', 'guid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'xanaduyonder/' + name).hex,
                      template.replace('aircraft-fire', name), flags=re.MULTILINE)
    if not target.with_suffix('.jpg.meta').exists():
        target.with_suffix('.jpg.meta').write_text(metadata)
print('Fruit Slice lobby artwork hashes verified and restored')
