#!/usr/bin/env python3
"""Apply an allowlisted start-menu overlay to the exact accepted playable stage."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MENU = ROOT / 'ThirdParty/KingdomRushContent/menu'
MANIFEST = ROOT / 'ThirdParty/KingdomRushContent/profile/stable-content-manifest.json'
MANIFEST_SHA256 = 'e7e35ed7d0e817aa9c1c44aa4981d2b6b463c246807686ed45fc57cc0ac05af5'
SCREEN_PATH = 'all-desktop/screen_slots.lua'
MODULE_PATH = 'all-desktop/xanadu_start_menu.lua'
FONT_PATH = 'all-desktop/assets/fonts/HarmonyOS_Sans_SC.ttf'
ADDED_FILES = (MODULE_PATH,)
HOOK = b'-- XANADU_START_MENU: isolated help and existing settings entry.\nrequire("xanadu_start_menu").attach(screen)\n\n'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def baseline_rows():
    data = MANIFEST.read_bytes()
    if digest(data) != MANIFEST_SHA256:
        raise ValueError('The accepted playable manifest changed')
    return {row['path']: row for row in json.loads(data)}


def expected_menu_files():
    return {MODULE_PATH: (MENU / 'xanadu_start_menu.lua').read_bytes()}


menu_sources = expected_menu_files


def patch_text(original):
    """Return bytes with one footer hook; accept only the original pinned screen."""
    row = baseline_rows()[SCREEN_PATH]
    if len(original) != row['bytes'] or digest(original) != row['sha256']:
        raise ValueError('The supplied screen is not the accepted playable start screen')
    if not original.rstrip().endswith(b'\nreturn screen'):
        raise ValueError('The stable start-screen export changed')
    footer = original.rfind(b'return screen')
    return original[:footer] + HOOK + original[footer:]


def validate_patched_screen(data):
    """Prove the hook location and restore/check the exact original screen SHA."""
    if data.count(HOOK) != 1:
        raise ValueError('The start-screen hook is missing or duplicated')
    original = data.replace(HOOK, b'', 1)
    if patch_text(original) != data:
        raise ValueError('The start-screen hook is outside its approved footer location')
    return True


def validate_files(stage, menu=False):
    stage = Path(stage)
    rows = baseline_rows()
    additions = expected_menu_files() if menu else {}
    names = {p.relative_to(stage).as_posix() for p in stage.rglob('*') if p.is_file()}
    if names != set(rows) | set(additions):
        raise ValueError('Content file set differs from the accepted baseline and approved menu files')
    for name, row in rows.items():
        data = (stage / name).read_bytes()
        if menu and name == SCREEN_PATH:
            validate_patched_screen(data)
            data = data.replace(HOOK, b'', 1)
        if len(data) != row['bytes'] or digest(data) != row['sha256']:
            raise ValueError('Unapproved stable gameplay/resource change: ' + name)
    for name, data in additions.items():
        if (stage / name).read_bytes() != data:
            raise ValueError('Stale or altered start-menu addition: ' + name)
    return rows, additions


def validate_menu_stage(stage):
    stage = Path(stage)
    rows, additions = validate_files(stage, menu=True)
    changed = {SCREEN_PATH: (stage / SCREEN_PATH).read_bytes(), **additions}
    return {'passed': True, 'baseline': 'playable-20261030', 'unchanged_baseline_files': len(rows) - 1,
            'modified_files': [SCREEN_PATH], 'added_files': sorted(additions),
            'menu_files': {name: {'bytes': len(data), 'sha256': digest(data)}
                           for name, data in sorted(changed.items())}}


def apply_menu_patch(stage):
    stage = Path(stage)
    source = (stage / SCREEN_PATH).read_bytes()
    if HOOK in source:
        return validate_menu_stage(stage)
    validate_files(stage)
    additions = expected_menu_files()  # Resolve all inputs before changing a single file.
    source = patch_text(source)
    for name, data in {SCREEN_PATH: source, **additions}.items():
        target = stage / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return validate_menu_stage(stage)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    report = validate_menu_stage(args.staging) if args.check else apply_menu_patch(args.staging)
    output = json.dumps(report, ensure_ascii=False, indent=2)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(output + '\n')
    print(output)


if __name__ == '__main__':
    main()
