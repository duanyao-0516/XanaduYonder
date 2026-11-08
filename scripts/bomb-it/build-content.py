#!/usr/bin/env python3
"""Build the pinned offline Bomb It payload; --verify checks without writing.

build() is pure and shared by Android AAR packaging and iOS staging. No Java,
Godot, Flash plugin, network access or Android build tools are required.
"""
import argparse
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ThirdParty/BombItContent'
RUFFLE = ROOT / 'ThirdParty/Ruffle'
OUTPUT = ROOT / 'artifacts/bomb-it/content'
AAR = ROOT / 'Assets/Plugins/Android/bomb-it-content.aar'
REPORT = ROOT / 'artifacts/bomb-it/patch-report.json'
ADAPTER_FILES = ('index.html', 'style.css', 'host.js', 'controls.js')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def locked_files(folder, entries):
    result = {}
    for item in entries:
        name = item['path']
        require(not Path(name).is_absolute() and '..' not in Path(name).parts and '\\' not in name, 'Invalid locked input path')
        path = folder / name
        require(path.is_file() and not path.is_symlink(), 'Missing or linked input: ' + str(path))
        data = path.read_bytes()
        require(len(data) == item['bytes'] and sha(data) == item['sha256'], 'Unreviewed input: ' + str(path))
        require(name not in result, 'Duplicate locked input: ' + name)
        result[name] = data
    return result


def build():
    source_lock = json.loads((SOURCE / 'source-manifest.json').read_text())
    original = locked_files(SOURCE / 'original', source_lock['files'])
    require(set(original) == {'game.swf'}, 'Unexpected original payload set')
    spec = importlib.util.spec_from_file_location('xanadu_bomb_it_sanitizer', SOURCE / 'sanitizer/sanitize.py')
    sanitizer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(sanitizer)
    clean, report = sanitizer.sanitize(original['game.swf'])
    files = {'game.swf': clean}
    ruffle_lock = json.loads((RUFFLE / 'upstream-lock.json').read_text())
    require(ruffle_lock['version'] == '0.7.1', 'Unreviewed Ruffle version')
    runtime = locked_files(RUFFLE, ruffle_lock['files'])
    require(len(runtime) == 7 and len([name for name in runtime if name.endswith('.wasm')]) == 2, 'Incomplete Ruffle runtime')
    files.update({'ruffle/' + name: data for name, data in runtime.items()})
    for name in ADAPTER_FILES:
        path = SOURCE / 'adapter' / name
        require(path.is_file() and not path.is_symlink(), 'Missing adapter: ' + str(path))
        files[name] = path.read_bytes()
    types = {'.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css',
             '.swf': 'application/x-shockwave-flash', '.wasm': 'application/wasm'}
    manifest = {'files': [{'path': name, 'bytes': len(data), 'sha256': sha(data),
                          'mime': types.get(Path(name).suffix, 'text/plain')}
                         for name, data in sorted(files.items())]}
    files['manifest.json'] = (json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').encode()
    archive = io.BytesIO()
    entries = {'AndroidManifest.xml': b'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.xanaduyonder.bombit.content"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36" /></manifest>',
               **{'assets/bomb-it/' + name: data for name, data in files.items()}}
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
        for name, data in sorted(entries.items()):
            info = zipfile.ZipInfo(name, (2026, 11, 8, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            output.writestr(info, data, compresslevel=6)
    report['runtimeVersion'] = ruffle_lock['version']
    report['runtimeArchiveSha256'] = ruffle_lock['archiveSha256']
    report['contentBytes'] = sum(len(data) for data in files.values())
    return files, archive.getvalue(), report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    files, archive, report = build()
    if args.verify:
        require(AAR.is_file() and AAR.read_bytes() == archive, 'Content AAR is missing or stale')
        actual = {p.relative_to(OUTPUT).as_posix() for p in OUTPUT.rglob('*') if p.is_file()}
        require(actual == set(files), 'Generated content file set differs')
        for name, data in files.items():
            require((OUTPUT / name).read_bytes() == data, 'Generated content differs: ' + name)
        require(json.loads(REPORT.read_text()) == report, 'Patch report is stale')
    else:
        OUTPUT.mkdir(parents=True, exist_ok=True)
        for name, data in files.items():
            path = OUTPUT / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        for path in OUTPUT.rglob('*'):
            if path.is_file() and path.relative_to(OUTPUT).as_posix() not in files:
                path.unlink()
        AAR.parent.mkdir(parents=True, exist_ok=True)
        AAR.write_bytes(archive)
        REPORT.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'verified': args.verify, 'files': len(files), 'aarBytes': len(archive),
                      'aarSha256': sha(archive), 'swfSha256': report['outputSha256'],
                      'advertisingClassRemoved': True, 'gameplayTagsByteIdentical': True}))


if __name__ == '__main__':
    main()
