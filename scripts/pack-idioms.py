#!/usr/bin/env python3
"""Deterministically export verified offline dictionary data into lean Unity resources."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def encode(value):
    raw = json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
    return gzip.compress(raw, compresslevel=9, mtime=0)

def verified_read(folder, checksums, relative):
    data = (folder / relative).read_bytes()
    if hashlib.sha256(data).hexdigest() != checksums.get(relative):
        raise ValueError('Cleaned source checksum mismatch: ' + relative)
    return data

def project_detail(row):
    sense = row['sense']
    explanation = sense['explanation']
    sources = {p['source'] for refs in sense['provenance'].values()
               for item in refs for p in (item if isinstance(item, list) else [item])}
    citation = lambda x: {'Text': x['text'], 'Reference': x['reference']}
    return {'Id': row['id'], 'SenseId': sense['id'], 'Word': row['word'],
            'Pinyin': sense['pinyin']['display'], 'Meaning': explanation['meaning'],
            'Stories': explanation['stories'], 'Origins': list(map(citation, explanation['origins'])),
            'Examples': list(map(citation, sense['examples'])),
            'SourceLabel': ' · '.join(n for k,n in [('jingmo','静墨词库'),('xinhua','新华成语词库')] if k in sources)}

def build_rows(entries, common):
    missing = common - {e['word'] for e in entries}
    if missing:
        raise ValueError('Recommended words absent from validated index: ' + ', '.join(sorted(missing)))
    return [[e['id'],e['word'],e['pinyin']['display'],e['firstKey'],e['lastKey'],
             e['senseId'],Path(e['detailFile']).stem,e['word'] in common] for e in entries]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, default=ROOT/'artifacts/idiom-cleaned')
    parser.add_argument('--output', type=Path, default=ROOT/'Assets/Resources/Idioms')
    args = parser.parse_args()
    checksums = json.loads((args.input/'checksums.json').read_text())
    index = json.loads(verified_read(args.input, checksums, 'game-index.json'))
    common = {line.strip() for line in (ROOT/'Data/Idioms/common-idioms.txt').read_text().splitlines()
              if line.strip() and not line.startswith('#')}
    rows = build_rows(index['entries'], common)
    resources = {}
    by_id = {e['id']: e for e in index['entries']}
    details_seen = set()
    for path in sorted({e['detailFile'] for e in index['entries']}):
        details = json.loads(verified_read(args.input, checksums, path))['entries']
        projected = []
        for entry in details:
            expected = by_id[entry['id']]
            if (entry['id'] in details_seen or expected['senseId'] != entry['sense']['id']
                    or expected['word'] != entry['word'] or expected['detailFile'] != path
                    or expected['pinyin']['display'] != entry['sense']['pinyin']['display']):
                raise ValueError('Invalid detail reference: ' + entry['id'])
            projected.append(project_detail(entry))
            details_seen.add(entry['id'])
        resources['details/'+Path(path).stem+'.bytes'] = encode(projected)
    if details_seen != set(by_id): raise ValueError('Missing dictionary details')
    version = hashlib.sha256(encode(rows) + ''.join(checksums[p] for p in sorted(checksums)).encode()).hexdigest()[:24]
    resources['catalog.bytes'] = encode({'version':version,'entries':rows})
    for path in sorted((args.input/'licenses').iterdir()):
        resources['licenses/'+path.name+'.txt'] = verified_read(args.input,checksums,'licenses/'+path.name)
    manifest = {'schemaVersion':1,'dictionaryVersion':version,'entryCount':len(rows),
                'recommendedCount':len(common),'sourceIndexSha256':checksums['game-index.json'],
                'resources':{k:hashlib.sha256(v).hexdigest() for k,v in resources.items()}}
    resources['manifest.json'] = (json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode()
    for path, data in resources.items():
        target = args.output/path
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(data)
    # Refuse stale packed data; the caller can explicitly remove an old export after review.
    existing = {str(p.relative_to(args.output)) for p in args.output.rglob('*') if p.is_file() and p.suffix!='.meta'}
    if existing != set(resources): raise ValueError('Stale resources present: '+str(existing-set(resources)))
    print(json.dumps({'entries':len(rows),'recommended':len(common),'version':version,
                      'bytes':sum(map(len,resources.values())),'files':len(resources)},ensure_ascii=False))

if __name__ == '__main__': main()
