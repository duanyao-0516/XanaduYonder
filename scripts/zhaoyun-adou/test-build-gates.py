#!/usr/bin/env python3
"""Exercise the real build gate against isolated corrupt/stale copies of a valid build."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('zhaoyun_gate', Path(__file__).with_name('verify-build-input.py'))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class BuildGateTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        gate.validate(ROOT)
        cls.scratch = tempfile.TemporaryDirectory(prefix='zhaoyun-build-gate-')
        cls.root = Path(cls.scratch.name)
        for name in ['ThirdParty/ZhaoyunAdouContent', 'ThirdParty/ZhaoyunAdouRuntime', 'artifacts/zhaoyun-adou/content']:
            shutil.copytree(ROOT/name, cls.root/name)
        sources = set()
        for name in ['content-build.json', 'runtime-build.json']:
            relative = Path('artifacts/zhaoyun-adou')/name
            shutil.copyfile(ROOT/relative, cls.root/relative)
            sources.update(item['path'] for item in json.loads((ROOT/relative).read_text())['sourceFiles'])
        sources.add('Assets/Plugins/Android/ZhaoyunAdouRuntime.aar')
        for name in sources:
            target = cls.root/name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT/name, target)

    @classmethod
    def tearDownClass(cls):
        cls.scratch.cleanup()

    def changed(self, relative, suffix):
        path = self.root/relative
        original = path.read_bytes()
        try:
            path.write_bytes(original + suffix)
            with self.assertRaises((ValueError, json.JSONDecodeError)):
                gate.validate(self.root)
        finally:
            path.write_bytes(original)

    def test_valid_inputs_pass(self):
        self.assertEqual(gate.validate(self.root), gate.validate(ROOT))

    def test_content_change_rejected(self):
        self.changed('artifacts/zhaoyun-adou/content/offline-adapter.js', b'\n// changed\n')

    def test_native_source_change_rejected(self):
        self.changed('ThirdParty/ZhaoyunAdouRuntime/src/com/xanaduyonder/zhaoyunadou/ZhaoyunAdouActivity.java', b'\n// changed\n')

    def test_adapter_source_change_rejected(self):
        self.changed('ThirdParty/ZhaoyunAdouContent/host-bridge.js', b'\n// changed\n')

    def test_packaged_archive_change_rejected(self):
        self.changed('Assets/Plugins/Android/ZhaoyunAdouRuntime.aar', b'changed')

    def test_changed_report_rejected(self):
        self.changed('artifacts/zhaoyun-adou/content-build.json', b'\n')

    def test_unlisted_content_rejected(self):
        extra = self.root/'artifacts/zhaoyun-adou/content/unlisted.html'
        try:
            extra.write_text('<script src="https://example.invalid/sdk.js"></script>')
            with self.assertRaises(ValueError): gate.validate(self.root)
        finally:
            extra.unlink()

    def test_unfingerprinted_native_source_rejected(self):
        extra = self.root/'ThirdParty/ZhaoyunAdouRuntime/src/Unlisted.java'
        try:
            extra.write_text('class Unlisted {}')
            with self.assertRaises(ValueError): gate.validate(self.root)
        finally:
            extra.unlink()


if __name__ == '__main__':
    unittest.main(verbosity=2)
