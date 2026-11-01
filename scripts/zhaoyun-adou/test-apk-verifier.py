#!/usr/bin/env python3
"""Prove the APK gate rejects missing/changed/unregistered offline content."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

class ApkVerifierTests(unittest.TestCase):
    def test_archive_payload_gate(self):
        script = Path(__file__).with_name('verify-apk.py')
        self.assertTrue(script.is_file(), 'APK verifier is missing')
        spec = importlib.util.spec_from_file_location('zhaoyun_apk_verifier', script)
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        payload = b'<html>offline</html>'
        rows = [{'path':'index.html','bytes':len(payload),'sha256':hashlib.sha256(payload).hexdigest()}]
        digest = hashlib.sha256(json.dumps(rows,sort_keys=True,separators=(',',':')).encode()).hexdigest()
        report = {'passed':True,'files':rows,'contentDigest':digest}
        with tempfile.TemporaryDirectory(prefix='zhaoyun-apk-test-') as tmp:
            apk = Path(tmp) / 'example.apk'
            def write(content=payload, extra=False, dex=True):
                with zipfile.ZipFile(apk,'w') as archive:
                    archive.writestr('assets/zhaoyun-adou/index.html',content)
                    archive.writestr('assets/zhaoyun-adou-host/asset-index.json',json.dumps({'files':rows,'contentDigest':digest}))
                    if dex: archive.writestr('classes.dex',b'Lcom/xanaduyonder/zhaoyunadou/ZhaoyunAdouActivity;\0Lcom/xanaduyonder/zhaoyunadou/ZhaoyunAdouBridge;\0')
                    if extra: archive.writestr('assets/zhaoyun-adou/extra.js',b'network()')
            write(); result = module.verify_archive(apk,report)
            self.assertEqual(result['contentFileCount'],1)
            write(content=b'<html>changed</html>')
            with self.assertRaises(ValueError): module.verify_archive(apk,report)
            write(extra=True)
            with self.assertRaises(ValueError): module.verify_archive(apk,report)
            write(dex=False)
            with self.assertRaises(ValueError): module.verify_archive(apk,report)

if __name__ == '__main__': unittest.main()
