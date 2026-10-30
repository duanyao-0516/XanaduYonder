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
for e in entries.values():
    assert len(e)==9 and len(e[1])==4,e[:2]
    assert isinstance(e[8],list),e[:2]
    assert all(len(pair)==2 and all(isinstance(key,str) and key for key in pair) for pair in e[8]),e[:2]
    assert len({tuple(pair) for pair in e[8]})==len(e[8]),e[:2]
    assert [e[3],e[4]]==(e[8][0] if e[8] else ['','']),e[:2]
seen=set()
for path in sorted((root/'details').glob('*.bytes')):
    rows=json.loads(gzip.decompress(path.read_bytes()))
    assert len(rows)<=256
    for row in rows:
        e=entries[row['Id']]
        assert row['Id'] not in seen
        assert [row['Word'],row['Pinyin'],row['SenseId'],path.stem]==[e[1],e[2],e[5],e[6]]
        assert row['Meaning'] and row['SourceLabel']
        assert row['Stories']==row['Origins']==row['Examples']==[]
        sources=row['Sources']
        assert sources and len({s['SourceRecordId'] for s in sources})==len(sources)
        for source in sources:
            assert source['SourceLabel']=={'jingmo':'景墨词库','xinhua':'新华成语词库'}[source['SourceKey']]
            assert source['SourceRecordId'].startswith(source['SourceKey']+':')
            assert isinstance(source['Pinyin'],str) and isinstance(source['Meaning'],str)
            assert all(isinstance(s,str) for s in source['Stories'])
            assert all(isinstance(c['Text'],str) and isinstance(c['Reference'],str)
                       for c in source['Origins']+source['Examples'])
        assert row['Pinyin']==' / '.join(dict.fromkeys(s['Pinyin'] for s in sources if s['Pinyin']))
        assert row['Meaning']=='\n\n'.join('【%s】\n%s'%(s['SourceLabel'],s['Meaning']) for s in sources)
        assert row['SourceLabel']==' · '.join(dict.fromkeys(s['SourceLabel'] for s in sources))
        seen.add(row['Id'])
assert seen==set(entries)
print('PASS: packed checksums, 9-column catalog, all source variants, and',len(seen),'exact definition references')
PY
