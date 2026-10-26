#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"
python3 -m unittest discover -s Tests/IdiomResources
python3 - <<'PY'
import gzip,hashlib,json
from pathlib import Path
root=Path('Assets/Resources/Idioms')
manifest=json.loads((root/'manifest.json').read_text())
for name,digest in manifest['resources'].items():
    assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest,name
catalog=json.loads(gzip.decompress((root/'catalog.bytes').read_bytes()))
assert catalog['version']==manifest['dictionaryVersion']
entries={e[0]:e for e in catalog['entries']}
assert len(entries)==len(catalog['entries'])==manifest['entryCount']
assert len({e[1] for e in entries.values()})==len(entries)
seen=set()
for path in sorted((root/'details').glob('*.bytes')):
    rows=json.loads(gzip.decompress(path.read_bytes()))
    assert len(rows)<=256
    for row in rows:
        e=entries[row['Id']]
        assert row['Id'] not in seen
        assert [row['Word'],row['Pinyin'],row['SenseId'],path.stem]==[e[1],e[2],e[5],e[6]]
        assert row['Meaning'] and row['SourceLabel']
        seen.add(row['Id'])
assert seen==set(entries)
print('PASS: packed checksums, unique catalog, and all',len(seen),'exact definition references')
PY
