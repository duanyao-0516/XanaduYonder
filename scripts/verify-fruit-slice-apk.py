#!/usr/bin/env python3
"""Verify the isolated offline preview's actual package, permissions and content bytes."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parents[1]
apk = Path(sys.argv[1]).resolve()
build_tools = Path('/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0')
badging = subprocess.check_output([str(build_tools / 'aapt'), 'dump', 'badging', str(apk)], text=True)
assert "package: name='com.duanyao.xanaduyonder.fruitslicetest'" in badging, 'Unexpected package identity'
assert 'application-debuggable' not in badging, 'Development/debuggable app is not an offline preview'
assert "native-code: 'arm64-v8a'" in badging, 'Expected ARM64 preview'
for permission in ['android.permission.INTERNET', 'android.permission.ACCESS_NETWORK_STATE']:
    assert permission not in badging, 'Network permission present: ' + permission
subprocess.run([str(build_tools / 'apksigner'), 'verify', str(apk)], check=True)
content = root / 'Assets/StreamingAssets/fruit-slice'
checked = 0
with zipfile.ZipFile(apk) as archive:
    dex_files = [archive.read(name) for name in archive.namelist()
                 if name.startswith('classes') and name.endswith('.dex')]
    for cls in ['FruitSliceActivity', 'FruitSliceBridge', 'FruitSliceBackPressState']:
        descriptor = ('Lcom/xanaduyonder/fruitslice/' + cls + ';').encode()
        assert any(descriptor in dex for dex in dex_files), 'Missing Android class: ' + cls
    for path in sorted(content.rglob('*')):
        if not path.is_file() or path.suffix == '.meta': continue
        name = 'assets/fruit-slice/' + path.relative_to(content).as_posix()
        assert hashlib.sha256(archive.read(name)).digest() == hashlib.sha256(path.read_bytes()).digest(), name
        checked += 1
    assert checked > 300, 'Incomplete game asset bundle'
result = {'apk': str(apk), 'size_bytes': apk.stat().st_size, 'sha256': hashlib.sha256(apk.read_bytes()).hexdigest(),
          'package': 'com.duanyao.xanaduyonder.fruitslicetest', 'checked_assets': checked,
          'network_permissions': [], 'debuggable': False, 'signature_verified': True}
report = root / 'artifacts/validation/fruit-slice/apk.json'
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
