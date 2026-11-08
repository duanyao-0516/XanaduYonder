#!/usr/bin/env python3
"""Check that a player package contains exactly the accepted offline Bomb It payload."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def verify(artifact):
    expected_root = ROOT / 'artifacts/bomb-it/content'
    manifest = json.loads((expected_root / 'manifest.json').read_text())
    entries = manifest['files']
    expected = {e['path']: e for e in entries}
    if len(entries) != len(expected) or not {'game.swf', 'index.html'} <= expected.keys():
        raise ValueError('Invalid offline content inventory')
    if artifact.is_dir():
        folder = artifact / 'Data/Raw/web-games/bomb-it'
        names = {p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()}
        read = lambda n: (folder / n).read_bytes()
        archive = None
    else:
        archive = zipfile.ZipFile(artifact)
        all_names = archive.namelist()
        if len(all_names) != len(set(all_names)):
            raise ValueError('Duplicate package entries')
        if artifact.suffix.lower() == '.apk':
            prefix = 'assets/bomb-it/'
        else:
            roots = [n[:-len('manifest.json')] for n in all_names if n.endswith('.app/Data/Raw/web-games/bomb-it/manifest.json')]
            if len(roots) != 1:
                raise ValueError('Expected exactly one Bomb It content root')
            prefix = roots[0]
        names = {n[len(prefix):] for n in all_names if n.startswith(prefix) and not n.endswith('/')}
        read = lambda n: archive.read(prefix + n)
    try:
        if names != set(expected) | {'manifest.json'}:
            raise ValueError('Missing or extra offline game files: ' + str(names ^ (set(expected) | {'manifest.json'})))
        if json.loads(read('manifest.json')) != manifest:
            raise ValueError('Packaged manifest differs from the accepted build')
        total = 0
        for name, entry in expected.items():
            data = read(name)
            if len(data) != entry['bytes'] or hashlib.sha256(data).hexdigest() != entry['sha256']:
                raise ValueError('Packaged file changed: ' + name)
            total += len(data)
        print(json.dumps({'offlineBombItFiles': len(expected), 'bytes': total, 'exactContentMatch': True}))
    finally:
        if archive is not None:
            archive.close()


if __name__ == '__main__':
    verify(Path(sys.argv[1]).resolve())
