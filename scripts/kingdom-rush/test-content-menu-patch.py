#!/usr/bin/env python3
"""Verify the menu patch cannot alter any playable baseline game data."""
import argparse
import hashlib
import importlib.util
from pathlib import Path
import shutil
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--staging', type=Path, required=True)
    args = parser.parse_args()
    script = Path(__file__).with_name('content-menu-patch.py')
    assert script.is_file(), 'The bounded start-menu patch has not been implemented'
    spec = importlib.util.spec_from_file_location('menu_patch', script)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)

    def snapshot(folder):
        return {p.relative_to(folder).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in folder.rglob('*') if p.is_file()}

    def rejected(call):
        try:
            call()
        except ValueError:
            return
        raise AssertionError('Expected rejection of content outside the approved menu patch')

    with tempfile.TemporaryDirectory(prefix='kr-menu-patch-') as temp:
        stage = Path(temp) / 'staging'
        shutil.copytree(args.staging, stage)
        supplied = snapshot(stage)
        screen = stage / module.SCREEN_PATH
        if module.HOOK in screen.read_bytes() or any((stage / path).exists() for path in module.ADDED_FILES):
            # A build output is a valid fixture only after its complete overlay
            # has passed the same strict checks as the packaged game. Normalize
            # this disposable copy, never the caller's production staging tree.
            module.validate_menu_stage(stage)
            screen.write_bytes(screen.read_bytes().replace(module.HOOK, b'', 1))
            for path in module.ADDED_FILES:
                (stage / path).unlink()
        module.validate_files(stage)
        before = snapshot(stage)
        report = module.apply_menu_patch(stage)
        after = snapshot(stage)
        assert report['passed'] is True
        assert set(after) - set(before) == set(module.ADDED_FILES)
        assert set(before) - set(after) == set()
        assert {p for p in before if before[p] != after[p]} == {module.SCREEN_PATH}
        assert module.apply_menu_patch(stage) == report, 'Patch must be idempotent'
        assert snapshot(stage) == after
        assert module.validate_menu_stage(stage) == report

        # A changed gameplay file is never accepted or restored silently.
        gameplay = stage / 'firstgen.lua'
        original = gameplay.read_bytes()
        gameplay.write_bytes(original + b'\n-- unexpected gameplay edit\n')
        rejected(lambda: module.validate_menu_stage(stage))
        rejected(lambda: module.apply_menu_patch(stage))
        assert gameplay.read_bytes().endswith(b'-- unexpected gameplay edit\n')
        gameplay.write_bytes(original)

        extra = stage / 'unexpected-resource.png'
        extra.write_bytes(b'not an approved addition')
        rejected(lambda: module.validate_menu_stage(stage))
        extra.unlink()

        menu = stage / module.MODULE_PATH
        menu_original = menu.read_bytes()
        menu.write_bytes(menu_original + b'\n-- stale menu\n')
        rejected(lambda: module.validate_menu_stage(stage))
        menu.write_bytes(menu_original)

        screen = stage / module.SCREEN_PATH
        screen_original = screen.read_bytes()
        screen.write_bytes(screen_original + module.HOOK)
        rejected(lambda: module.validate_menu_stage(stage))
        screen.write_bytes(screen_original)
        font = stage / module.FONT_PATH
        font.unlink()
        rejected(lambda: module.validate_menu_stage(stage))
    assert snapshot(args.staging) == supplied, 'The input staging tree must remain untouched'
    print('PASS: baseline or validated overlay input, two approved file differences, idempotence, gameplay mutation, extra file, stale module, duplicate hook, missing original font and untouched input')


if __name__ == '__main__':
    main()
