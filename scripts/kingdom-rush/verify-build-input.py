#!/usr/bin/env python3
"""Reject stale or unvalidated Android content before launching Unity."""
import argparse,hashlib,json,zipfile
from pathlib import Path
from content_stable_baseline import validate_certificate

def validate(root):
    report=json.loads((root/'artifacts/kingdom-rush-1/runtime-build.json').read_text())
    if not {'gamePath','contentValidationReport','contentValidationSha256'} <= report.keys():
        raise SystemExit('This Android content has no current validation certificate. Validate game.love and rerun build-runtime.py.')
    validation_path=Path(report['contentValidationReport'])
    validation=json.loads(validation_path.read_text())
    sha=lambda data:hashlib.sha256(data).hexdigest()
    assert validation.get('passed') and sha(validation_path.read_bytes())==report['contentValidationSha256'],'Content validation report changed or failed'
    validate_certificate(validation, Path(report['gamePath']))
    assert sha(Path(report['gamePath']).read_bytes())==report['gameSha256'],'Current game.love changed after validation'
    for name,key in [('KingdomRushRuntime.aar','runtimeAarSha256'),('KingdomRushContent.aar','contentAarSha256')]:
        path=root/'Assets/Plugins/Android'/name
        assert sha(path.read_bytes())==report[key],'AAR changed after validation: '+name
    with zipfile.ZipFile(root/'Assets/Plugins/Android/KingdomRushContent.aar') as archive:
        content=sha(archive.read('assets/game.love'))
        assert content==report['gameSha256']==validation['content_sha256'],'Packaged game differs from validated content'
    return content

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    args=parser.parse_args();print('Validated Android game input: '+validate(args.root))
