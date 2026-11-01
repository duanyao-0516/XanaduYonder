#!/usr/bin/env python3
"""Verify the built APK's host component, permission boundary and exact offline payload."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
ACTIVITY = 'com.xanaduyonder.zhaoyunadou.ZhaoyunAdouActivity'
PREFIX = 'assets/zhaoyun-adou/'


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): result.update(block)
    return result.hexdigest()


def verify_archive(apk, report):
    if report.get('passed') is not True: raise ValueError('Content report is not passing')
    rows = report['files']
    if len({row['path'] for row in rows}) != len(rows): raise ValueError('Duplicate content manifest paths')
    expected_digest = hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    if expected_digest != report.get('contentDigest'): raise ValueError('Content aggregate digest mismatch')
    with zipfile.ZipFile(apk) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)): raise ValueError('Duplicate APK ZIP entries')
        if archive.testzip() is not None: raise ValueError('APK CRC check failed')
        actual = {name[len(PREFIX):] for name in names if name.startswith(PREFIX) and not name.endswith('/')}
        expected = {row['path'] for row in rows}
        if actual != expected: raise ValueError('APK offline file set differs: ' + str(sorted(actual ^ expected)))
        for row in rows:
            name = PREFIX + row['path']
            if archive.getinfo(name).file_size != row['bytes'] or hashlib.sha256(archive.read(name)).hexdigest() != row['sha256']:
                raise ValueError('APK contains stale or corrupt content: ' + row['path'])
        index = json.loads(archive.read('assets/zhaoyun-adou-host/asset-index.json'))
        if index.get('files') != rows or index.get('contentDigest') != expected_digest:
            raise ValueError('APK asset allowlist does not match verified content')
        dex = b'\n'.join(archive.read(name) for name in names if re.fullmatch(r'classes\d*\.dex', name))
        for name in ['ZhaoyunAdouActivity', 'ZhaoyunAdouBridge']:
            if ('Lcom/xanaduyonder/zhaoyunadou/' + name + ';').encode() not in dex:
                raise ValueError('APK host class missing: ' + name)
    return {'contentFileCount':len(rows), 'contentBytes':sum(row['bytes'] for row in rows),
            'contentDigest':expected_digest, 'zipCrcAndContentHashesVerified':True,
            'assetAllowlistVerified':True, 'hostDexClassesPresent':True}


def activity_block(manifest):
    lines = manifest.splitlines()
    for i, line in enumerate(lines):
        if not re.search(r'\bE: activity\b', line): continue
        indentation = len(line) - len(line.lstrip())
        end = i + 1
        while end < len(lines):
            candidate = lines[end]
            if candidate.strip() and len(candidate) - len(candidate.lstrip()) <= indentation: break
            end += 1
        block = '\n'.join(lines[i:end])
        if ACTIVITY in block: return block
    raise ValueError('ZhaoyunAdou internal Activity missing from AndroidManifest')


def require_attribute(block, name, value):
    line = next((line for line in block.splitlines() if 'A: android:' + name + '(' in line), '')
    if not re.search(value, line): raise ValueError('Activity attribute mismatch ' + name + ': ' + line.strip())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk', type=Path)
    parser.add_argument('--aapt', type=Path, required=True)
    parser.add_argument('--package', default='com.duanyao.xanaduyonder.zhaoyunadoupreview')
    parser.add_argument('--content-report', type=Path, default=ROOT / 'artifacts/zhaoyun-adou/content-build.json')
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/zhaoyun-adou/apk-verification.json')
    args = parser.parse_args()
    def aapt(*words): return subprocess.check_output([str(args.aapt), 'dump', *words, str(args.apk)], text=True)
    badging = aapt('badging')
    permissions_text = aapt('permissions')
    manifest = subprocess.check_output([str(args.aapt), 'dump', 'xmltree', str(args.apk), 'AndroidManifest.xml'], text=True)
    if "package: name='" + args.package + "'" not in badging: raise ValueError('Unexpected APK package')
    if 'application-debuggable' in badging: raise ValueError('Delivery preview must not be a Development/debug APK')
    if "launchable-activity: name='com.xanaduyonder.kingdomrush.KingdomRushLauncherActivity'" not in badging:
        raise ValueError('Existing hall launcher/task-stack contract changed')
    permissions = set(re.findall(r"uses-permission[^:]*: name='([^']+)'", permissions_text))
    unexpected = permissions - {'android.permission.VIBRATE'}
    if unexpected: raise ValueError('Unexpected APK permissions: ' + ', '.join(sorted(unexpected)))
    block = activity_block(manifest)
    require_attribute(block, 'exported', r'=\(type 0x12\)0x0\b')
    require_attribute(block, 'screenOrientation', r'=\(type 0x10\)0x1\b')
    require_attribute(block, 'hardwareAccelerated', r'=\(type 0x12\)0xffffffff\b')
    require_attribute(block, 'enableOnBackInvokedCallback', r'=\(type 0x12\)0xffffffff\b')
    report = json.loads(args.content_report.read_text())
    result = verify_archive(args.apk, report)
    result.update({'passed':True, 'apk':str(args.apk.resolve()), 'apkBytes':args.apk.stat().st_size,
                   'apkSha256':digest(args.apk), 'package':args.package, 'permissions':sorted(permissions),
                   'internetPermission':False, 'newPermissions':False, 'debuggable':False,
                   'internalPortraitHardwareAcceleratedActivity':True, 'predictiveBackEnabled':True,
                   'contentBuildSha256':digest(args.content_report), 'gameplayOrDeviceRuntimeTested':False})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    try: main()
    except (ValueError, KeyError, OSError, json.JSONDecodeError, zipfile.BadZipFile) as error: raise SystemExit(str(error))
