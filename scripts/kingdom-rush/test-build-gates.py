#!/usr/bin/env python3
"""Reject stale certificates and AARs around the exact playable baseline."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
from content_stable_baseline import validate_archive

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SHA = lambda value: hashlib.sha256(value).hexdigest()


def load(name):
    spec = importlib.util.spec_from_file_location(name, HERE / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class BuildGates(unittest.TestCase):
    def test_changed_aar_or_report_rejected(self):
        game = ROOT / 'artifacts/kingdom-rush-1/game.love'
        original_validation = json.loads((game.parent / 'static-validation.json').read_text())
        original_validation['stable_baseline'] = validate_archive(game)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            plugins = root / 'Assets/Plugins/Android'
            plugins.mkdir(parents=True)
            reports = root / 'artifacts/kingdom-rush-1'
            reports.mkdir(parents=True)
            runtime = plugins / 'KingdomRushRuntime.aar'
            runtime.write_bytes(b'runtime')
            content = plugins / 'KingdomRushContent.aar'
            with zipfile.ZipFile(content, 'w', compression=zipfile.ZIP_STORED) as archive:
                archive.write(game, 'assets/game.love')
            validation = reports / 'static-validation.json'
            validation.write_text(json.dumps(original_validation))
            certificate = dict(gamePath=str(game), gameSha256=SHA(game.read_bytes()),
                               contentValidationReport=str(validation), contentValidationSha256=SHA(validation.read_bytes()),
                               runtimeAarSha256=SHA(runtime.read_bytes()), contentAarSha256=SHA(content.read_bytes()))
            certificate_path = reports / 'runtime-build.json'
            certificate_path.write_text(json.dumps(certificate))
            gate = load('verify-build-input')
            self.assertEqual(gate.validate(root), original_validation['content_sha256'])
            runtime.write_bytes(b'changed runtime')
            with self.assertRaisesRegex(AssertionError, 'AAR changed'):
                gate.validate(root)
            runtime.write_bytes(b'runtime')
            # Even a newly hashed report must retain the approved baseline proof.
            for changes in ({'passed': False}, {'stable_baseline': {}}, {'lua_files': 0}):
                with self.subTest(changes=changes):
                    validation.write_text(json.dumps(dict(original_validation, **changes)))
                    certificate['contentValidationSha256'] = SHA(validation.read_bytes())
                    certificate_path.write_text(json.dumps(certificate))
                    with self.assertRaises((ValueError, AssertionError)):
                        gate.validate(root)
            validation.write_text(json.dumps(original_validation))
            certificate['contentValidationSha256'] = SHA(validation.read_bytes())
            with zipfile.ZipFile(content, 'w') as archive:
                archive.writestr('assets/game.love', b'experimental content')
            certificate['contentAarSha256'] = SHA(content.read_bytes())
            certificate_path.write_text(json.dumps(certificate))
            with self.assertRaisesRegex(AssertionError, 'Packaged game differs'):
                gate.validate(root)


if __name__ == '__main__':
    unittest.main()
