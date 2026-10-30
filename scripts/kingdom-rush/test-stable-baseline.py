#!/usr/bin/env python3
"""The menu release preserves the accepted game and rejects unrelated changes."""
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'artifacts/kingdom-rush-1/game.love'


class StableBaseline(unittest.TestCase):
    def test_exact_playable_archive(self):
        from content_stable_baseline import validate_baseline_archive
        result = validate_baseline_archive(GAME.parent / 'playable-baseline/game.love')
        self.assertTrue(result['passed'])
        self.assertEqual(result['file_count'], 2424)
        self.assertEqual(result['content_sha256'], '212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641')

    def test_changed_archive_rejected(self):
        from content_stable_baseline import validate_baseline_archive
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'game.love'
            path.write_bytes(b'not the playable baseline')
            with self.assertRaisesRegex(ValueError, 'stable baseline'):
                validate_baseline_archive(path)

    def test_menu_has_only_the_approved_delta(self):
        from content_stable_baseline import validate_archive, validate_baseline_archive
        result = validate_archive(GAME)
        self.assertEqual(result['unchanged_baseline_files'], 2423)
        self.assertEqual(result['file_count'], 2425)
        self.assertEqual(result['modified_files'], ['all-desktop/screen_slots.lua'])
        self.assertEqual(len(result['added_files']), 1)
        with self.assertRaises(ValueError):
            validate_baseline_archive(GAME)
        with self.assertRaises(ValueError):
            validate_archive(GAME.parent / 'playable-baseline/game.love')

    def test_unrelated_resource_change_and_missing_menu_rejected(self):
        from content_stable_baseline import validate_archive
        with zipfile.ZipFile(GAME) as archive:
            files = {name: archive.read(name) for name in archive.namelist()}
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / 'changed.love'
            for altered, removed in [('firstgen.lua', None), (None, 'all-desktop/xanadu_start_menu.lua')]:
                with self.subTest(altered=altered, removed=removed):
                    with zipfile.ZipFile(target, 'w') as archive:
                        for name, data in files.items():
                            if name != removed:
                                archive.writestr(name, data + b'\n-- changed' if name == altered else data)
                    with self.assertRaises(ValueError):
                        validate_archive(target)

    def test_old_or_failed_certificate_rejected(self):
        from content_stable_baseline import validate_archive, validate_certificate
        report = json.loads((GAME.parent / 'static-validation.json').read_text())
        report['stable_baseline'] = validate_archive(GAME)
        validate_certificate(report, GAME)
        for change in ({'passed': False}, {'stable_baseline': {}}, {'lua_files': 0}, {'content_sha256': 'invalid'}, {'errors': ['missing resource']}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                validate_certificate(dict(report, **change), GAME)


if __name__ == '__main__':
    unittest.main()
