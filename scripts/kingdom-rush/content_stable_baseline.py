"""Pin the rollback to the complete game shipped in the 192.14 MB APK."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CONTENT_SHA256 = '212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641'
CONTENT_BYTES = 70585835
MANIFEST_SHA256 = 'e7e35ed7d0e817aa9c1c44aa4981d2b6b463c246807686ed45fc57cc0ac05af5'
MANIFEST = ROOT / 'ThirdParty/KingdomRushContent/profile/stable-content-manifest.json'


def validate_archive(path):
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


def validate_certificate(report, game):
    actual = validate_archive(game)
    if (report.get('passed') is not True or report.get('errors') != []
            or report.get('content_sha256') != CONTENT_SHA256
            or report.get('content_bytes') != CONTENT_BYTES
            or report.get('stable_baseline') != actual
            or report.get('zip_crc_ok') is not True or report.get('lua_files') != 1294):
        raise ValueError('Missing, stale or failed stable baseline validation certificate; run content-validate.py on the exact restored game.')
    return actual
