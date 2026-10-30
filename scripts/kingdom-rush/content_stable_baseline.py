"""Preserve the accepted game byte-for-byte except the reviewed title-menu overlay."""
import hashlib
import importlib.util
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CONTENT_SHA256 = '212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641'
CONTENT_BYTES = 70585835
MANIFEST_SHA256 = 'e7e35ed7d0e817aa9c1c44aa4981d2b6b463c246807686ed45fc57cc0ac05af5'
MANIFEST = ROOT / 'ThirdParty/KingdomRushContent/profile/stable-content-manifest.json'


def validate_baseline_archive(path):
    path = Path(path)
    if path.stat().st_size != CONTENT_BYTES or hashlib.sha256(path.read_bytes()).hexdigest() != CONTENT_SHA256:
        raise ValueError('Game does not match the playable stable baseline; do not package experimental pruning output.')
    manifest_bytes = MANIFEST.read_bytes()
    if hashlib.sha256(manifest_bytes).hexdigest() != MANIFEST_SHA256:
        raise ValueError('The stable baseline source/resource manifest changed.')
    rows = json.loads(manifest_bytes)
    expected = {row['path']: row for row in rows}
    if len(rows) != len(expected) or len(expected) != 2424:
        raise ValueError('Invalid stable baseline manifest.')
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(expected) or set(names) != set(expected) or archive.testzip() is not None:
            raise ValueError('Stable baseline archive file set or CRC differs.')
        for name, row in expected.items():
            data = archive.read(name)
            if len(data) != row['bytes'] or hashlib.sha256(data).hexdigest() != row['sha256']:
                raise ValueError('Stable baseline source/resource changed: ' + name)
    return dict(passed=True, baseline='playable-20261030', content_sha256=CONTENT_SHA256,
                content_bytes=CONTENT_BYTES, file_count=len(expected), manifest_sha256=MANIFEST_SHA256)


def menu_module():
    spec = importlib.util.spec_from_file_location('content_menu_patch', Path(__file__).with_name('content-menu-patch.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_archive(path):
    """Require the approved title menu and reject every unrelated resource change."""
    path = Path(path)
    menu = menu_module()
    rows = menu.baseline_rows()
    additions = menu.expected_menu_files()
    expected = set(rows) | set(additions)
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(expected) or set(names) != expected or archive.testzip() is not None:
            raise ValueError('Stable baseline plus menu archive file set or CRC differs.')
        for name, row in rows.items():
            data = archive.read(name)
            if name == menu.SCREEN_PATH:
                menu.validate_patched_screen(data)
            elif len(data) != row['bytes'] or hashlib.sha256(data).hexdigest() != row['sha256']:
                raise ValueError('Unapproved stable gameplay/resource change: ' + name)
        for name, data in additions.items():
            if archive.read(name) != data:
                raise ValueError('Stale or altered start-menu addition: ' + name)
        changed = {menu.SCREEN_PATH: archive.read(menu.SCREEN_PATH), **additions}
        menu_files = {name: dict(bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
                      for name, data in sorted(changed.items())}
    return dict(passed=True, baseline='playable-20261030', baseline_content_sha256=CONTENT_SHA256,
                baseline_content_bytes=CONTENT_BYTES, manifest_sha256=MANIFEST_SHA256,
                unchanged_baseline_files=len(rows) - 1, modified_files=[menu.SCREEN_PATH],
                added_files=sorted(additions), menu_files=menu_files, file_count=len(expected),
                content_sha256=hashlib.sha256(path.read_bytes()).hexdigest(), content_bytes=path.stat().st_size)


def validate_certificate(report, game):
    actual = validate_archive(game)
    if (report.get('passed') is not True or report.get('errors') != []
            or report.get('content_sha256') != actual['content_sha256']
            or report.get('content_bytes') != actual['content_bytes']
            or report.get('stable_baseline') != actual
            or report.get('zip_crc_ok') is not True or report.get('lua_files') != 1295):
        raise ValueError('Missing, stale or failed stable baseline/menu validation certificate; run content-validate.py on the approved game.')
    return actual
