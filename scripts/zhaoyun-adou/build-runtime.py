#!/usr/bin/env python3
"""Compile the permission-free Java host and package the exact verified offline content."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ThirdParty/ZhaoyunAdouRuntime'
ORIGIN = 'https://zhaoyun-adou.xanadu.invalid/'


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b''): result.update(data)
    return result.hexdigest()


def validate_content(content, report_path):
    content = Path(content)
    report_path = Path(report_path)
    report = json.loads(report_path.read_text())
    if report.get('passed') is not True: raise ValueError('Content validation did not pass')
    rows = report.get('files')
    if not isinstance(rows, list) or not rows: raise ValueError('Content file manifest is missing')
    manifest = {}
    for row in rows:
        if not isinstance(row, dict): raise ValueError('Invalid content row')
        name = row.get('path', '')
        if (not isinstance(name, str) or not name or '\\' in name or '%' in name or '?' in name or '#' in name
                or any(ord(c) < 32 or ord(c) == 127 for c in name)
                or PurePosixPath(name).is_absolute() or any(part in ('', '.', '..') for part in name.split('/'))):
            raise ValueError('Unsafe content path: ' + repr(name))
        if name in manifest: raise ValueError('Duplicate content path: ' + name)
        if not isinstance(row.get('bytes'), int) or isinstance(row['bytes'], bool) or row['bytes'] < 0:
            raise ValueError('Invalid content size: ' + name)
        if not re.fullmatch(r'[0-9a-f]{64}', str(row.get('sha256', ''))): raise ValueError('Invalid content digest: ' + name)
        file = content / name
        cursor = content
        if cursor.is_symlink(): raise ValueError('Symlinks are not package inputs: ' + name)
        for part in name.split('/'):
            cursor = cursor / part
            if cursor.is_symlink(): raise ValueError('Symlinks are not package inputs: ' + name)
        if not file.is_file() or file.stat().st_size != row['bytes'] or digest(file) != row['sha256']:
            raise ValueError('Missing or modified content: ' + name)
        manifest[name] = row
    actual = {str(path.relative_to(content)).replace(os.sep, '/') for path in content.rglob('*') if path.is_file() or path.is_symlink()}
    if actual != set(manifest): raise ValueError('Content contains unregistered or missing files: ' + str(sorted(actual ^ set(manifest))))
    if 'index.html' not in manifest: raise ValueError('Offline entry index.html is missing')
    if rows != sorted(rows, key=lambda row: row['path']): raise ValueError('Content manifest must be sorted by path')
    expected = hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    if report.get('contentDigest') != expected: raise ValueError('Content aggregate digest does not match its file manifest')
    return report, digest(report_path)


def archive(path, entries):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    try:
        with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as result:
            for name, source in sorted(entries.items()):
                info = zipfile.ZipInfo(name, (2026, 11, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                if isinstance(source, Path):
                    with source.open('rb') as stream, result.open(info, 'w', force_zip64=True) as target:
                        shutil.copyfileobj(stream, target)
                else: result.writestr(info, source)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--android-player', type=Path, default=Path(os.environ.get('UNITY_ANDROID_PLAYER', '/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer')))
    parser.add_argument('--content', type=Path, default=ROOT / 'artifacts/zhaoyun-adou/content')
    parser.add_argument('--content-report', type=Path, default=ROOT / 'artifacts/zhaoyun-adou/content-build.json')
    parser.add_argument('--output', type=Path, default=ROOT / 'Assets/Plugins/Android/ZhaoyunAdouRuntime.aar')
    parser.add_argument('--report', type=Path, default=ROOT / 'artifacts/zhaoyun-adou/runtime-build.json')
    args = parser.parse_args()
    content, content_report_digest = validate_content(args.content, args.content_report)
    java = args.android_player / 'OpenJDK/bin'
    android_jar = args.android_player / 'SDK/platforms/android-35/android.jar'
    for path in [java / 'javac', android_jar]:
        if not path.is_file(): raise ValueError('Required Unity Android dependency is missing: ' + str(path))
    manifest = SOURCE / 'AndroidManifest.xml'
    source_manifest = manifest.read_text()
    if '<uses-permission' in source_manifest: raise ValueError('The game host must not request permissions')
    java_files = sorted((SOURCE / 'src').rglob('*.java'))
    if not java_files: raise ValueError('Java host sources are missing')
    inputs = sorted([manifest, Path(__file__).resolve(), *java_files])
    input_bytes = {path: path.read_bytes() for path in inputs}
    source_rows = [{'path': str(path.relative_to(ROOT)), 'sha256': hashlib.sha256(input_bytes[path]).hexdigest()} for path in inputs]
    asset_index = json.dumps({'files': content['files'], 'contentDigest': content['contentDigest']}, sort_keys=True, separators=(',', ':')).encode()
    with tempfile.TemporaryDirectory(prefix='zhaoyun-runtime-build-') as temporary:
        stage = Path(temporary)
        classes = stage / 'classes'; classes.mkdir()
        snapshots = []
        for path in java_files:
            snapshot = stage / 'java' / path.relative_to(SOURCE / 'src')
            snapshot.parent.mkdir(parents=True, exist_ok=True)
            snapshot.write_bytes(input_bytes[path]); snapshots.append(snapshot)
        subprocess.run([str(java / 'javac'), '-encoding', 'UTF-8', '-source', '8', '-target', '8', '-Xlint:-options',
                        '-classpath', str(android_jar), '-d', str(classes), *map(str, snapshots)], check=True)
        classes_jar = stage / 'classes.jar'
        archive(classes_jar, {str(path.relative_to(classes)): path for path in classes.rglob('*.class')})
        entries = {'AndroidManifest.xml': input_bytes[manifest], 'classes.jar': classes_jar,
                   'proguard.txt': b'-keep class com.xanaduyonder.zhaoyunadou.** { *; }\n',
                   'assets/zhaoyun-adou-host/asset-index.json': asset_index}
        for row in content['files']:
            payload = (args.content / row['path']).read_bytes()
            if len(payload) != row['bytes'] or hashlib.sha256(payload).hexdigest() != row['sha256']:
                raise ValueError('Content changed during compilation: ' + row['path'])
            entries['assets/zhaoyun-adou/' + row['path']] = payload
        for path in inputs:
            if path.read_bytes() != input_bytes[path]: raise ValueError('Host source changed during compilation: ' + str(path))
        if digest(args.content_report) != content_report_digest: raise ValueError('Content report changed during compilation; rebuild against the new report')
        archive(args.output, entries)
        classes_digest = digest(classes_jar)
    result = {'passed': True, 'aarPath': str(args.output.resolve()), 'aarSha256': digest(args.output),
              'contentDigest': content['contentDigest'], 'contentBuildSha256': content_report_digest,
              'sourceFiles': source_rows,
              'classesJarSha256': classes_digest, 'assetIndexSha256': hashlib.sha256(asset_index).hexdigest(),
              'contentFileCount': len(content['files']), 'contentBytes': sum(row['bytes'] for row in content['files']),
              'javaSourceCount': len(java_files), 'origin': ORIGIN, 'entry': 'index.html',
              'androidJarSha256': digest(android_jar),
              'validation': 'Content tree and SHA-256 checked; Java compiled against Android 35; deterministic AAR generated. No device or gameplay execution.'}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.report.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    temporary.replace(args.report)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    try: main()
    except (ValueError, OSError, json.JSONDecodeError) as error: raise SystemExit(str(error))
