#!/usr/bin/env python3
"""Check the help text against the untouched playable game's existing font."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--staging', type=Path, required=True)
parser.add_argument('--pythonlib', type=Path, required=True)
parser.add_argument('--report', type=Path)
args = parser.parse_args()
sys.path.insert(0, str(args.pythonlib))
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[2]
source = (root / 'ThirdParty/KingdomRushContent/menu/xanadu_start_menu.lua').read_bytes()
characters = {ord(c) for c in source.decode() if c.isprintable()}
font_path = 'all-desktop/assets/fonts/HarmonyOS_Sans_SC.ttf'
font_bytes = (args.staging / font_path).read_bytes()
rows = json.loads((root / 'ThirdParty/KingdomRushContent/profile/stable-content-manifest.json').read_bytes())
expected = next(row for row in rows if row['path'] == font_path)
font_hash = hashlib.sha256(font_bytes).hexdigest()
assert font_hash == expected['sha256'], 'The original playable font must remain byte-identical'
with TTFont(args.staging / font_path) as font:
    missing = sorted(characters - set(font.getBestCmap()))
report = {'passed': not missing, 'menu_sha256': hashlib.sha256(source).hexdigest(),
          'font': font_path, 'font_sha256': font_hash, 'characters_checked': len(characters),
          'missing_characters': ''.join(chr(c) for c in missing),
          'font_changes': 'None. Reuses the byte-identical accepted playable font.'}
output = json.dumps(report, ensure_ascii=False, indent=2)
if args.report:
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(output + '\n')
print(output)
assert not missing, 'The existing font cannot render every help character'
