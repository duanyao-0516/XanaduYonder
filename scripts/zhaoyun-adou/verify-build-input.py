#!/usr/bin/env python3
"""Reject stale offline content, native sources, or packaged assets before Unity builds."""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import zipfile


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe_path(value):
    path = PurePosixPath(value)
    require(bool(value) and not path.is_absolute() and '..' not in path.parts and '\\' not in value,
            'Unsafe manifest path: ' + value)
    require(path.as_posix() == value, 'Noncanonical manifest path: ' + value)
    return value


def check_sources(root, report):
    sources = report.get('sourceFiles', [])
    require(bool(sources), 'Missing source fingerprints; rebuild the content/runtime')
    seen = set()
    for entry in sources:
        path = safe_path(entry['path'])
        require(path not in seen, 'Duplicate source: ' + path)
        seen.add(path)
        require(sha((root/path).read_bytes()) == entry['sha256'], 'Source changed; rebuild: ' + path)
    return seen


def validate(root):
    root = root.resolve()
    base = root/'artifacts/zhaoyun-adou'
    content_path, runtime_path = base/'content-build.json', base/'runtime-build.json'
    content, runtime = json.loads(content_path.read_text()), json.loads(runtime_path.read_text())
    require(content.get('passed') is True and runtime.get('passed') is True, 'Content/runtime build did not pass')
    for directory, script, report in [
        ('ThirdParty/ZhaoyunAdouContent', 'build-content.py', content),
        ('ThirdParty/ZhaoyunAdouRuntime', 'build-runtime.py', runtime),
    ]:
        sources = check_sources(root, report)
        require('scripts/zhaoyun-adou/' + script in sources, 'Build script is not fingerprinted')
        # Docs and tests do not affect packaged behavior; all runtime/content source inputs do.
        relevant = {p.relative_to(root).as_posix() for p in (root/directory).rglob('*')
                    if p.is_file() and p.suffix in {'.js', '.html', '.css', '.json', '.java', '.xml'}
                    and 'tests' not in p.relative_to(root/directory).parts}
        require(relevant <= sources, 'Unfingerprinted source files: ' + ', '.join(sorted(relevant-sources)))
    require(sha(content_path.read_bytes()) == runtime['contentBuildSha256'], 'Content report changed after AAR build')
    files = content['files']
    digest = sha(json.dumps(files, sort_keys=True, separators=(',', ':')).encode())
    require(digest == content['contentDigest'] == runtime['contentDigest'], 'Content digest mismatch')
    expected = {}
    for entry in files:
        path = safe_path(entry['path'])
        require(path not in expected, 'Duplicate content: ' + path)
        expected[path] = entry
        data = (base/'content'/path).read_bytes()
        require(len(data) == entry['bytes'] and sha(data) == entry['sha256'], 'Content changed: ' + path)
    actual = {p.relative_to(base/'content').as_posix() for p in (base/'content').rglob('*') if p.is_file()}
    require(actual == set(expected), 'Content inventory changed after build')
    require({'index.html', 'offline-adapter.js', 'host-bridge.js', 'js/bundle.js'} <= set(expected), 'Missing offline entry/adapter')
    aar = root/'Assets/Plugins/Android/ZhaoyunAdouRuntime.aar'
    require(sha(aar.read_bytes()) == runtime['aarSha256'], 'AAR changed after build')
    with zipfile.ZipFile(aar) as archive:
        names = archive.namelist()
        require(len(set(names)) == len(names) and archive.testzip() is None, 'Invalid AAR archive')
        prefix = 'assets/zhaoyun-adou/'
        assets = {name[len(prefix):] for name in names if name.startswith(prefix) and not name.endswith('/')}
        require(assets == set(expected), 'AAR asset inventory differs from validated content')
        for path, entry in expected.items():
            data = archive.read(prefix+path)
            require(len(data) == entry['bytes'] and sha(data) == entry['sha256'], 'AAR content mismatch: ' + path)
        manifest = archive.read('AndroidManifest.xml').decode()
        require('uses-permission' not in manifest, 'Offline runtime must not introduce permissions')
        with zipfile.ZipFile(io.BytesIO(archive.read('classes.jar'))) as classes:
            require('com/xanaduyonder/zhaoyunadou/ZhaoyunAdouBridge.class' in classes.namelist(), 'Native bridge missing')
    return digest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    try:
        print('Validated offline Zhaoyun Adou input: ' + validate(args.root))
    except (ValueError, KeyError, OSError, zipfile.BadZipFile) as error:
        parser.exit(1, str(error)+'\n')
