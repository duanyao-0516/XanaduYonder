#!/usr/bin/env python3
"""The rollback build accepts only the exact previously playable game archive."""
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'artifacts/kingdom-rush-1/game.love'


class StableBaseline(unittest.TestCase):
    def test_exact_playable_archive(self):
        from content_stable_baseline import validate_archive
        result = validate_archive(GAME)
        self.assertTrue(result['passed'])
        self.assertEqual(result['file_count'], 2424)
        self.assertEqual(result['content_sha256'], '212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641')

    def test_changed_archive_rejected(self):
        from content_stable_baseline import validate_archive
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'game.love'
            path.write_bytes(b'not the playable baseline')
            with self.assertRaisesRegex(ValueError, 'stable baseline'):
                validate_archive(path)

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
