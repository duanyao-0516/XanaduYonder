#!/usr/bin/env python3
"""Exercise the actual runtime content gate with tampered and unregistered files."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

HERE = Path(__file__).resolve().parent

class RuntimePackageTests(unittest.TestCase):
    def test_content_integrity_gate(self):
        script = HERE / 'build-runtime.py'
        self.assertTrue(script.is_file(), 'Runtime content gate is missing')
        spec = importlib.util.spec_from_file_location('zhaoyun_runtime_build', script)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory(prefix='zhaoyun-content-gate-') as tmp:
            content = Path(tmp) / 'content'
            content.mkdir()
            entry = content / 'index.html'
            entry.write_bytes(b'<html>local</html>')
            rows = [{'path':'index.html', 'bytes':entry.stat().st_size, 'sha256':hashlib.sha256(entry.read_bytes()).hexdigest()}]
            report = Path(tmp) / 'content-build.json'
            data = {'passed':True, 'files':rows, 'contentDigest':hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(',', ':')).encode()).hexdigest()}
            report.write_text(json.dumps(data))
            accepted, _ = module.validate_content(content, report)
            self.assertEqual(accepted['files'], rows)
            entry.write_bytes(b'<html>other</html>')
            with self.assertRaises(ValueError): module.validate_content(content, report)
            entry.write_bytes(b'<html>local</html>')
            extra = content / 'unregistered.js'; extra.write_text('alert(1)')
            with self.assertRaises(ValueError): module.validate_content(content, report)
            extra.unlink()
            data['passed'] = False; report.write_text(json.dumps(data))
            with self.assertRaises(ValueError): module.validate_content(content, report)
            data['passed'] = True
            data['files'] = rows + rows; report.write_text(json.dumps(data))
            with self.assertRaises(ValueError): module.validate_content(content, report)
            data['files'] = [{'path':'../index.html','bytes':0,'sha256':'a'*64}]; report.write_text(json.dumps(data))
            with self.assertRaises(ValueError): module.validate_content(content, report)
            data['files'] = rows; report.write_text(json.dumps(data))
            entry.unlink(); entry.symlink_to(report)
            with self.assertRaises(ValueError): module.validate_content(content, report)

if __name__ == '__main__': unittest.main()
