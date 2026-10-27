#!/usr/bin/env python3
"""Reproduce the checked-in offline bank, or select from bounded fixed-version source prefixes."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
COMMIT = 'd8c8ebaee0c08c412cfba96af1923dfa61c83317'
BASE = f'https://raw.githubusercontent.com/grantm/sudoku-exchange-puzzle-bank/{COMMIT}/'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--refresh', action='store_true', help='Download fixed-version prefixes and select a fresh deterministic bank')
parser.add_argument('--easy', type=int, default=2000)
parser.add_argument('--medium', type=int, default=3000)
parser.add_argument('--hard', type=int, default=12000)
parser.add_argument('--diabolical', type=int, default=1000)
args = parser.parse_args()
selected = ROOT / 'ThirdParty/Sudoku/selected.tsv'
manifest = None
if not args.refresh and selected.exists():
    sources = [str(selected)]
else:
    cache = ROOT / 'Tests/SudokuContent/obj/import-cache'
    cache.mkdir(parents=True, exist_ok=True)
    sources, manifest = [], []
    manifest_path = ROOT / 'ThirdParty/Sudoku/source-prefixes.json'
    previous = json.loads(manifest_path.read_text())['sources'] if manifest_path.exists() else []
    for name in ('easy', 'medium', 'hard', 'diabolical'):
        count = getattr(args, name)
        if count <= 0:
            continue
        path = cache / f'{name}.txt'
        expected = next((item for item in previous if item['file'] == name + '.txt' and item['prefix_lines'] == count), None)
        cached = path.read_bytes() if path.exists() else b''
        if len(cached.splitlines()) != count or (expected and hashlib.sha256(cached).hexdigest() != expected['prefix_sha256']):
            request = urllib.request.Request(BASE + name + '.txt', headers={'User-Agent': 'XanaduYonder-offline-sudoku-import/1'})
            with urllib.request.urlopen(request, timeout=60) as response, path.open('wb') as output:
                for _ in range(count):
                    line = response.readline()
                    if not line:
                        raise RuntimeError(f'{name} ended before {count} lines')
                    output.write(line)
        data = path.read_bytes()
        if expected and hashlib.sha256(data).hexdigest() != expected['prefix_sha256']:
            raise RuntimeError(f'{name}: downloaded bytes differ from the pinned prefix checksum')
        sources.append(str(path))
        manifest.append({'file': name + '.txt', 'url': BASE + name + '.txt', 'prefix_lines': count, 'prefix_bytes': len(data), 'prefix_sha256': hashlib.sha256(data).hexdigest()})
        print(f'{name}: {count} source lines, {len(data)} bytes', flush=True)
    with urllib.request.urlopen(BASE + 'LICENSE.txt', timeout=30) as response:
        (ROOT / 'ThirdParty/Sudoku/LICENSE.txt').write_bytes(response.read())
subprocess.run([str(ROOT / 'scripts/test-sudoku-content.sh'), '--import', *sources], cwd=ROOT, check=True)
subprocess.run([str(ROOT / 'scripts/test-sudoku-content.sh'), '--bank'], cwd=ROOT, check=True)
if manifest is not None:
    (ROOT / 'ThirdParty/Sudoku/source-prefixes.json').write_text(json.dumps({'commit': COMMIT, 'sources': manifest}, indent=2) + '\n')

# Validate provenance against the emitted bank; when prefix caches exist, check exact original lines too.
bank_rows = {parts[0]: parts for parts in (line.split('|') for line in (ROOT / 'Assets/Resources/Sudoku/puzzles.txt').read_text().splitlines()[1:])}
source_cache = {}
provenance_ids = set()
checked_source_lines = 0
for line in selected.read_text().splitlines()[1:]:
    identifier, source, line_number, digest, rating, givens = line.split('\t')
    if identifier in provenance_ids or identifier not in bank_rows or bank_rows[identifier][3] != givens:
        raise RuntimeError('Duplicate or mismatched provenance: ' + identifier)
    provenance_ids.add(identifier)
    cached_path = ROOT / 'Tests/SudokuContent/obj/import-cache' / (source + '.txt')
    if cached_path.exists():
        if source not in source_cache:
            source_cache[source] = cached_path.read_text().splitlines()
        if int(line_number) <= len(source_cache[source]):
            if source_cache[source][int(line_number) - 1].split() != [digest, givens, rating]:
                raise RuntimeError('Original source line differs: ' + identifier)
            checked_source_lines += 1
if len(provenance_ids) != 2000:
    raise RuntimeError('Provenance must cover exactly 2,000 puzzles')
print(f'PASS all 2,000 provenance records match the bank; checked {checked_source_lines} available pinned source lines')
