#!/usr/bin/env python3
"""Audit curated common vocabulary against the exact bundled playable dictionary.

Coverage is static: only the immediately preceding input is marked used, with no
personal familiar words or notebook. It is an upper bound, not round success rate.
"""
import argparse
from collections import Counter, defaultdict
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('pack', ROOT / 'scripts/pack-idioms.py')
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)


def coverage(rows, words, mode):
    first = (lambda e: e[1][0]) if mode == 'character' else (lambda e: e[3])
    last = (lambda e: e[1][-1]) if mode == 'character' else (lambda e: e[4])
    common = [e for e in rows if e[1] in words]
    full_counts = Counter(first(e) for e in rows)
    common_counts = Counter(first(e) for e in common)
    by_first = defaultdict(list)
    for entry in common:
        by_first[first(entry)].append(entry)

    def has_followup(candidate, previous=None):
        remaining = common_counts[last(candidate)] - int(first(candidate) == last(candidate))
        if previous is not None and previous[1] in words and first(previous) == last(candidate):
            remaining -= 1
        return remaining > 0

    full_reply = common_reply = qualified_reply = 0
    for previous in rows:
        candidates = [e for e in by_first[last(previous)] if e[0] != previous[0]]
        full_reply += int(full_counts[last(previous)] - int(first(previous) == last(previous)) > 0)
        common_reply += int(bool(candidates))
        qualified_reply += int(any(has_followup(candidate, previous) for candidate in candidates))
    return {
        'commonStartKeys': len(common_counts),
        'commonWithOrdinaryFollowup': sum(has_followup(e) for e in common),
        'inputsWithFullDictionaryReply': full_reply,
        'inputsWithAnyCommonReply': common_reply,
        'inputsWithQualifiedReply': qualified_reply,
        'inputsWithFullReplyButNoQualifiedReply': full_reply - qualified_reply,
    }


def analyze(rows, text):
    words = pack.parse_common(text)
    missing = words - {e[1] for e in rows}
    if missing:
        raise ValueError('Common words not playable: ' + ', '.join(sorted(missing)))
    return {
        'count': len(words),
        'listSha256': hashlib.sha256(text.encode('utf-8')).hexdigest(),
        'character': coverage(rows, words, 'character'),
        'sound': coverage(rows, words, 'sound'),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog', type=Path, default=ROOT / 'Assets/Resources/Idioms/catalog.bytes')
    parser.add_argument('--common', type=Path, default=ROOT / 'Data/Idioms/common-idioms.txt')
    parser.add_argument('--baseline-revision', help='Read this Git revision common list without switching checkout')
    args = parser.parse_args()
    catalog = json.loads(gzip.decompress(args.catalog.read_bytes()))
    result = {'schemaVersion': 1, 'dictionaryVersion': catalog['version'],
              'entryCount': len(catalog['entries']),
              'assumption': 'No personal words or notebook; only the immediately preceding input is used. Static upper bound, not game success rate.',
              'current': analyze(catalog['entries'], args.common.read_text(encoding='utf-8'))}
    if args.baseline_revision:
        text = subprocess.check_output(['git', 'show', args.baseline_revision + ':Data/Idioms/common-idioms.txt'], cwd=ROOT).decode('utf-8')
        result['baselineRevision'] = args.baseline_revision
        result['baseline'] = analyze(catalog['entries'], text)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
