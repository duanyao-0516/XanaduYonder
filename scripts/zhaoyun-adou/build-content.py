#!/usr/bin/env python3
"""Build a deterministic, offline-only package from hash-pinned public client inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
VENDOR = ROOT / 'ThirdParty/ZhaoyunAdouContent'
BUNDLE_SHA = '249945e2ac02d62f259336b049751de805d913c252169111cbda2c684eaebf0e'
ANCHOR = '})();function _0x2d19'
EXCLUDE = {'index.html', 'gameIndex.html', 'dialog/GMDialog.lh', 'dialog/GetStaminaDialog.lh',
           'dialog/AuthorizeDialog.lh', 'dialog/PrivacyPolicyDialog.lh', 'dialog/SidebarDialog.lh',
           'dialog/AddDeskDialog.lh', 'dialog/ShareLpDialog.lh', 'data/agreement.txt',
           'data/agreement-1.txt', 'data/agreement-2.txt', 'data/agreement-3.txt'}

def sha(data):
    return hashlib.sha256(data).hexdigest()

def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=ROOT/'artifacts/4399-262277/source')
    parser.add_argument('--output', type=Path, default=ROOT/'artifacts/zhaoyun-adou/content')
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    # Only a dedicated build directory may be replaced. Resolve symlinks before
    # checking, and reject overlaps before reading or deleting any source input.
    artifacts = (ROOT/'artifacts').resolve()
    if (output == artifacts or artifacts not in output.parents or
            output == source or output in source.parents or source in output.parents):
        parser.error('--output must be a dedicated directory below artifacts, disjoint from --source')
    if output.exists() and not output.is_dir():
        parser.error('--output must be a directory')
    lock = json.loads((VENDOR/'upstream-lock.json').read_text())
    assert len(lock['files']) == 308, 'Unexpected upstream manifest size'
    for item in lock['files']:
        relative = Path(item['path'])
        assert not relative.is_absolute() and '..' not in relative.parts, relative
        data = (source/relative).read_bytes()
        assert len(data) == item['bytes'] and sha(data) == item['sha256'], 'Upstream changed: '+str(relative)
    original = (source/'js/bundle.js').read_bytes()
    assert sha(original) == BUNDLE_SHA, 'Upstream bundle hash mismatch'
    bundle = original.decode('utf-8')
    assert bundle.count(ANCHOR) == 1, 'Expected exactly one game-closure insertion anchor'
    injection = (VENDOR/'bundle-export.js').read_text()
    generated = bundle.replace(ANCHOR, '\n'+injection+'\n'+ANCHOR, 1)
    assert generated.replace('\n'+injection+'\n', '', 1) == bundle, 'Bundle mutation escaped the narrow export'
    output.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix='.zhaoyun-content-', dir=output.parent))
    try:
        for item in lock['files']:
            relative = item['path']
            if relative in EXCLUDE:
                continue
            target = staging/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source/relative, target)
        (staging/'js/bundle.js').write_text(generated, encoding='utf-8')
        config = json.loads((staging/'fileconfig.json').read_text())
        assert config['files'].pop('shader') == ['basicShader.shader','buttonShapeGlow.shader','sweepLight.shader']
        config['config'] = [x for x in config['config'] if x.get('t') != 2]
        write_json(staging/'fileconfig.json', config)
        scene = staging/'scene/GameOverScene.ls'
        old = scene.read_text()
        missing = 'res://5bf43f39-5425-4fb0-bffd-b4d58ddcac67'
        assert old.count(missing) == 1, 'Expected one missing victory-bag reference'
        scene.write_text(old.replace(missing, 'resources/img/mainUI/bag1.png'), encoding='utf-8')
        for name in ['index.html','offline-adapter.js','host-bridge.js','offline.css']:
            shutil.copyfile(VENDOR/name, staging/name)
        for name in ['js/bundle.js','offline-adapter.js','host-bridge.js']:
            subprocess.run(['node','--check',str(staging/name)],check=True,capture_output=True)
        files=[]
        for file in sorted(staging.rglob('*')):
            if file.is_file():
                data=file.read_bytes();files.append({'path':file.relative_to(staging).as_posix(),'bytes':len(data),'sha256':sha(data)})
        digest=sha(json.dumps(files,sort_keys=True,separators=(',',':')).encode())
        report={'passed':True,'schemaVersion':1,'sourceBundleSha256':BUNDLE_SHA,'inputFiles':len(lock['files']),
                'files':files,'contentDigest':digest,'excluded':sorted(EXCLUDE),'sourceFiles':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p.read_bytes())} for p in sorted([x for x in VENDOR.rglob('*') if x.is_file()]+[Path(__file__).resolve()])],'transforms':[
                    'one game-IIFE export insertion; all other original bundle bytes retained',
                    'local entry and business offline adapter; no platform SDK',
                    'remove unused shader registrations','replace victory bag with bundled bag1 frame']}
        if output.exists(): shutil.rmtree(output)
        staging.rename(output)
        write_json(output.parent/'content-build.json',report)
        print(json.dumps({'passed':True,'files':len(files),'contentDigest':digest,'output':str(output)}))
    finally:
        if staging.exists():shutil.rmtree(staging)

if __name__ == '__main__':
    main()
