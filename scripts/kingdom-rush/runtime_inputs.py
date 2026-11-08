"""Fingerprint owned runtime build inputs independently of generated AARs."""
import hashlib
import json


def runtime_source_sha256(root):
    source = root / 'ThirdParty/KingdomRushRuntime'
    paths = [source / name for name in ('AndroidManifest.xml', 'provenance.json', 'jni-contract.json')]
    for directory in ('src', 'native', 'include'):
        paths.extend(path for path in (source / directory).rglob('*')
                     if path.is_file() and path.name != '.DS_Store' and not path.name.endswith('.meta'))
    paths.extend(root / 'scripts/kingdom-rush' / name
                 for name in ('build-runtime.py', 'verify-build-input.py', 'runtime_inputs.py'))
    inputs = {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
              for path in sorted(paths)}
    return hashlib.sha256(json.dumps(inputs, sort_keys=True, separators=(',', ':')).encode('utf-8')).hexdigest()
