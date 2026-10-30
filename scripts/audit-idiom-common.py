#!/usr/bin/env python3
"""Audit curated common vocabulary against the exact bundled playable dictionary.

Coverage is static: only the immediately preceding input is marked used, with no
personal familiar words or notebook. It is an upper bound, not round success rate.
"""
import argparse
from collections import defaultdict
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
    pairs = {e[0]: [(e[1][0], e[1][-1])] if mode == 'character' else e[8] for e in rows}
    common_ids = {e[0] for e in rows if e[1] in words}
    full_by_first = defaultdict(set)
    common_by_first = defaultdict(set)
    for entry_id, readings in pairs.items():
        for first, _ in readings:
            full_by_first[first].add(entry_id)
            if entry_id in common_ids:
                common_by_first[first].add(entry_id)

    def followers(index, keys, used):
        return set().union(*(index.get(key, set()) for key in keys)) - used

    def has_followup(candidate_id, previous_id=None, incoming=None):
        # A matching first key may only release the last key from that same pair.
        tails = {last for first, last in pairs[candidate_id] if incoming is None or first in incoming}
        return bool(followers(common_by_first, tails, {candidate_id, previous_id}))

    full_reply = common_reply = other_reply = with_followup = eligible_inputs = 0
    for previous_id, readings in pairs.items():
        tails = {last for _, last in readings}
        eligible_inputs += int(bool(readings))
        candidates = followers(common_by_first, tails, {previous_id})
        legal = followers(full_by_first, tails, {previous_id})
        full_reply += int(bool(legal))
        common_reply += int(bool(candidates))
        other_reply += int(bool(legal - candidates))
        with_followup += int(any(has_followup(candidate_id, previous_id, tails) for candidate_id in candidates))
    return {
        'inputsEligibleForMode': eligible_inputs,
        'commonStartKeys': len(common_by_first),
        'commonWithOrdinaryFollowup': sum(has_followup(entry_id) for entry_id in common_ids),
        'inputsWithFullDictionaryReply': full_reply,
        'inputsWithAnyCommonReply': common_reply,
        'inputsWithOtherLegalReply': other_reply,
        'inputsWithCommonReplyAndOrdinaryFollowup': with_followup,
        'inputsRequiringFullDictionaryFallback': full_reply - common_reply,
        'inputsWithSelectableReply': full_reply,
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
    result = {'schemaVersion': 2, 'dictionaryVersion': catalog['version'],
              'entryCount': len(catalog['entries']),
              'assumption': 'No personal words or notebook; only the immediately preceding input is used. Sound input starts with all its parsed reading pairs; later turns may narrow those pairs. Ordinary followup affects weight only; other legal words remain selectable even with a nonempty common pool. Static upper bound, not selection probability or game success rate.',
              'current': analyze(catalog['entries'], args.common.read_text(encoding='utf-8'))}
    if args.baseline_revision:
        text = subprocess.check_output(['git', 'show', args.baseline_revision + ':Data/Idioms/common-idioms.txt'], cwd=ROOT).decode('utf-8')
        result['baselineRevision'] = args.baseline_revision
        result['baseline'] = analyze(catalog['entries'], text)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
