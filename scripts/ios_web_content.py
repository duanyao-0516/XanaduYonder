"""Validate and stage offline HTML games independently of Android AARs."""
import hashlib
import importlib.util
import json
import mimetypes
from pathlib import Path, PurePosixPath
import subprocess
import sys

GAMES = ("zhaoyun-adou", "cut-the-rope", "level-devil", "bomb-it")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_relative(value):
    path = PurePosixPath(value)
    if (not value or path.is_absolute() or '..' in path.parts or '\\' in value or '%' in value
            or any(ord(c) < 32 or ord(c) == 127 for c in value) or path.as_posix() != value):
        raise ValueError('Unsafe web content path: ' + str(value))
    return value


def regular_file(root, relative):
    path = root / safe_relative(relative)
    if path.resolve() != path or not path.is_file():
        raise ValueError('Missing or linked web content: ' + str(path))
    return path


def validate_report(root, content, report, fingerprints):
    if not fingerprints:
        raise ValueError('Missing web content source fingerprints')
    for name, digest in fingerprints.items():
        if sha(regular_file(root, name).read_bytes()) != digest:
            raise ValueError('Stale web content source: ' + name)
    entries = report['files']
    expected = {}
    for entry in entries:
        name = safe_relative(entry['path'])
        if name in expected or name == 'manifest.json':
            raise ValueError('Duplicate/reserved content path: ' + name)
        data = regular_file(content, name).read_bytes()
        if len(data) != entry['bytes'] or sha(data) != entry['sha256']:
            raise ValueError('Stale web asset: ' + name)
        expected[name] = data
    actual = {p.relative_to(content).as_posix() for p in content.rglob('*') if p.is_file()}
    actual.discard('manifest.json')
    if actual != set(expected) or 'index.html' not in expected:
        raise ValueError('Web content inventory mismatch: ' + str(content))
    return expected


def cached_files(root, game):
    if game == 'zhaoyun-adou':
        base = root / 'artifacts/zhaoyun-adou'
        report = json.loads((base / 'content-build.json').read_text())
        if report.get('passed') is not True:
            raise ValueError('Zhaoyun content build failed')
        entries = report['sourceFiles']
        fingerprints = {x['path']: x['sha256'] for x in entries}
        required = {p.relative_to(root).as_posix() for p in (root / 'ThirdParty/ZhaoyunAdouContent').rglob('*') if p.is_file()}
        required.add('scripts/zhaoyun-adou/build-content.py')
        if len(entries) != len(fingerprints) or not required <= fingerprints.keys():
            raise ValueError('Zhaoyun source fingerprint inventory mismatch')
        return validate_report(root, base / 'content', report, fingerprints)
    base = root / 'artifacts/cut-the-rope/offline'
    report = json.loads((base / 'manifest.json').read_text())
    inputs = report['buildInputs']
    required = {'scripts/cut-the-rope/build-content.py', 'scripts/cut-the-rope/decode-hx.cjs',
                'ThirdParty/CutTheRopeContent/index.html', 'ThirdParty/CutTheRopeContent/offline-interface.js',
                'ThirdParty/CutTheRopeContent/start.js'}
    if set(inputs) != required or report['levelCount'] != 425:
        raise ValueError('Cut the Rope source fingerprint inventory mismatch')
    return validate_report(root, base, report, inputs)


def content_files(root, game):
    if game in ('level-devil', 'bomb-it'):
        # This deterministic builder verifies the pinned source and applies the same
        # adapters as Android. Its pure build function does not write an AAR.
        directory = root / 'scripts' / game
        sys.path.insert(0, str(directory))
        try:
            spec = importlib.util.spec_from_file_location('xanadu_' + game.replace('-', '_') + '_content', directory / 'build-content.py')
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            files, _, _ = module.build()
            del files['manifest.json']
            return files
        finally:
            sys.path.remove(str(directory))
    try:
        return cached_files(root, game)
    except (OSError, ValueError, KeyError):
        # Rebuild from pinned local source inputs only. No network or Android tools.
        subprocess.run([sys.executable, str(root / 'scripts' / game / 'build-content.py')], check=True)
        return cached_files(root, game)


def mime(name):
    if Path(name).name in ('LICENSE_APACHE', 'LICENSE_MIT'):
        return 'text/plain'
    overrides = {'.js': 'application/javascript', '.json': 'application/json', '.aac': 'audio/aac',
                 '.m4a': 'audio/mp4', '.wasm': 'application/wasm', '.swf': 'application/x-shockwave-flash',
                 '.css': 'text/css', '.html': 'text/html'}
    return overrides.get(Path(name).suffix) or mimetypes.guess_type(name)[0] or 'application/octet-stream'


def stage_web_games(root, candidate):
    records = []
    for game in GAMES:
        files = content_files(root, game)
        destination = candidate / 'Assets/StreamingAssets/web-games' / game
        if destination.exists():
            raise ValueError('Web payload must be staged once: ' + str(destination))
        entries = []
        for name, data in sorted(files.items()):
            safe_relative(name)
            target = destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            entries.append({'path': name, 'bytes': len(data), 'sha256': sha(data), 'mime': mime(name)})
        manifest = (json.dumps({'files': entries}, ensure_ascii=False, indent=2) + '\n').encode()
        (destination / 'manifest.json').write_bytes(manifest)
        records.append({'id': game, 'root': destination.relative_to(candidate).as_posix(),
                        'file_count': len(entries), 'bytes': sum(x['bytes'] for x in entries),
                        'manifest_sha256': sha(manifest)})
    return records
