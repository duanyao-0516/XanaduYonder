"""Shared deterministic packaging and dependency checks for the offline WebView games."""
from __future__ import annotations
import hashlib
import json
import re
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / 'ThirdParty/ClassicGoldMinerContent/web'
RUNTIME = ROOT / 'ThirdParty/ClassicGoldMinerRuntime'
AAR = ROOT / 'Assets/Plugins/Android/ClassicGoldMiner.aar'
REPORT_DIR = ROOT / 'artifacts/classic-gold-miner'
BUILD_FILES = ('build-runtime.py', 'verify-content.py', 'runtime_tools.py')
ASSET_SUFFIXES = {'.png', '.jpg', '.jpeg', '.gif', '.webp', '.svg', '.ogg', '.mp3', '.wav', '.m4a', '.json', '.fnt', '.ttf', '.woff', '.woff2'}
# These are inert library banners/XML namespace identifiers, never dependency allowlists.
ATTRIBUTION_URLS = {
    'http://phaser.io', 'https://phaser.io', 'http://www.phaser.io', 'https://www.phaser.io',
    'http://pixijs.com', 'https://pixijs.com', 'http://www.pixijs.com', 'https://www.pixijs.com',
    'https://github.com/schteppe/p2.js.git', 'https://github.com/schteppe/p2.js/issues',
    'http://www.w3.org/1999/xhtml', 'http://www.w3.org/2000/svg', 'http://www.w3.org/1999/xlink',
}
LIBRARIES = {'solo/phaser.js', 'duo/js/vendor-04f93d05.js'}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def encode_json(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def files_under(root: Path):
    return sorted(p for p in root.rglob('*') if p.is_file() and p.name != '.DS_Store' and not p.name.endswith('.meta'))


def input_hashes():
    sources = files_under(RUNTIME) + [Path(__file__).resolve().parent / name for name in BUILD_FILES]
    return {p.relative_to(ROOT).as_posix(): digest(p) for p in sorted(sources)}


def content_hashes():
    return {p.relative_to(CONTENT).as_posix(): digest(p) for p in files_under(CONTENT)}


def archive(destination: Path, entries: dict):
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_suffix(destination.suffix + '.tmp')
    with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as out:
        for name, data in sorted(entries.items()):
            info = zipfile.ZipInfo(name, (2026, 11, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            out.writestr(info, data.read_bytes() if isinstance(data, Path) else data, compresslevel=9)
    temporary.replace(destination)


class DocumentDependencies(HTMLParser):
    def __init__(self):
        super().__init__()
        self.references = []
        self.forbidden = []
        self.inline_scripts = []
        self.styles = []
        self.current_inline = None

    def handle_starttag(self, tag, attributes):
        attributes = dict(attributes)
        if tag == 'script' and not attributes.get('src'): self.current_inline = 'script'
        elif tag == 'style': self.current_inline = 'style'
        if any(name.startswith('on') for name in attributes): self.forbidden.append('inline event handler')
        if tag == 'meta' and attributes.get('http-equiv', '').lower() == 'refresh': self.forbidden.append('meta refresh')
        if tag in ('iframe', 'frame', 'object', 'embed', 'base', 'form'):
            self.forbidden.append(tag)
        for name in ('src', 'href', 'poster', 'action', 'data'):
            if attributes.get(name): self.references.append(attributes[name])
        if attributes.get('srcset'):
            self.references.extend(part.strip().split()[0] for part in attributes['srcset'].split(','))
        if attributes.get('style'):
            self.references.extend(css_references(attributes['style']))


    def handle_endtag(self, tag):
        if tag == self.current_inline: self.current_inline = None

    def handle_data(self, data):
        if self.current_inline == 'script' and data.strip(): self.inline_scripts.append(data)
        elif self.current_inline == 'style': self.styles.append(data)


def css_references(source):
    return re.findall(r'url\(\s*[\'\"]?([^\s)\'\"]+)', source, re.I)


def script_strings(source):
    """Collect quoted literals, skipping comments; unlike comment regex, keeps URL slashes intact.

    This is a bounded static dependency audit, not a JavaScript parser or runtime certificate.
    Regex literals can look like comments; we additionally check HTML/CSS and module imports.
    """
    index = 0
    while index < len(source):
        if source.startswith('//', index):
            end = source.find('\n', index + 2)
            index = len(source) if end < 0 else end + 1
        elif source.startswith('/*', index):
            end = source.find('*/', index + 2)
            index = len(source) if end < 0 else end + 2
        elif source[index] in ('\"', "'", '`'):
            quote, start = source[index], index
            index += 1
            value = []
            while index < len(source):
                char = source[index]
                if char == '\\' and index + 1 < len(source):
                    # Preserve ordinary path escapes; dependency URLs should use plain literals.
                    value.append(source[index:index + 2])
                    index += 2
                elif char == quote:
                    index += 1
                    break
                else:
                    value.append(char)
                    index += 1
            yield ''.join(value), start
        else:
            index += 1


def verify_content():
    errors, dependency_count, attribution_count = [], 0, 0
    required = ('index.html', 'shared/host.js', 'shared/host.css', 'solo/index.html', 'duo/index.html')
    for name in required:
        if not (CONTENT / name).is_file(): errors.append('Missing required entry: ' + name)
    assets = files_under(CONTENT)
    for path in CONTENT.rglob('*'):
        if path.is_symlink(): errors.append('Symlink is not a tracked offline asset: ' + path.relative_to(CONTENT).as_posix())
    for path in assets:
        name = path.relative_to(CONTENT).as_posix()
        if path.is_symlink() or not re.fullmatch(r'[A-Za-z0-9_./-]+', name):
            errors.append('Unsafe bundled asset name: ' + name)
        if path.suffix.lower() in {'.so', '.dex', '.jar', '.aar', '.exe', '.dll'}:
            errors.append('Unexpected executable/runtime in web assets: ' + name)

    def reference(owner, value, base=None, allow_data=False):
        nonlocal dependency_count
        if not value or value.startswith('#'): return
        if allow_data and value.startswith('data:image/'): return
        parts = urlsplit(value)
        if parts.scheme or parts.netloc or value.startswith('//'):
            errors.append(f'Non-asset dependency in {owner.relative_to(CONTENT)}: {value[:160]}')
            return
        if '%' in parts.path or '\\' in parts.path:
            errors.append(f'Encoded/escaped asset path in {owner.relative_to(CONTENT)}: {value[:160]}')
            return
        target = ((base or owner.parent) / unquote(parts.path)).resolve()
        if not target.is_relative_to(CONTENT.resolve()):
            errors.append(f'Dependency escapes bundled root in {owner.relative_to(CONTENT)}: {value}')
        elif not target.is_file():
            errors.append(f'Missing dependency in {owner.relative_to(CONTENT)}: {value}')
        else:
            dependency_count += 1

    for path in assets:
        relative = path.relative_to(CONTENT).as_posix()
        suffix = path.suffix.lower()
        if suffix not in {'.html', '.js', '.mjs', '.css', '.json', '.fnt'}: continue
        source = path.read_text(encoding='utf-8-sig')
        if suffix == '.html':
            parser = DocumentDependencies()
            parser.feed(source)
            if parser.inline_scripts: errors.append(f'Inline script is incompatible with native CSP in {relative}')
            for style in parser.styles:
                for value in re.findall(r'@import\s+[\'\"]([^\'\"]+)', style): reference(path, value)
            errors.extend(f'Forbidden document element in {relative}: {tag}' for tag in parser.forbidden)
            for value in parser.references: reference(path, value, allow_data=True)
            for value in css_references(source): reference(path, value, allow_data=True)
        elif suffix == '.css':
            for value in css_references(source): reference(path, value, allow_data=True)
            for value in re.findall(r'@import\s+[\'\"]([^\'\"]+)', source): reference(path, value)
        elif suffix in {'.js', '.mjs'}:
            for value in re.findall(r'(?:\bfrom\s+|\bimport\s+|\bimport\s*\(\s*)[\'\"]([^\'\"]+)', source):
                reference(path, value)
            for value, offset in script_strings(source):
                if re.match(r'^(?:https?:|wss?:|ftp:|file:|content:|javascript:|intent:|//)', value, re.I):
                    # Phaser uses file:// only as a loader protocol comparison; it is never a
                    # concrete asset address. Other exemptions are inert banners/metadata/namespaces.
                    if relative in LIBRARIES and (value.rstrip('/') in ATTRIBUTION_URLS or value == 'file://'):
                        attribution_count += 1
                    else:
                        reference(path, value)
                elif relative not in LIBRARIES and '/' in value and re.fullmatch(r'[A-Za-z0-9_./-]+', value) and Path(value).suffix.lower() in ASSET_SUFFIXES:
                    # Game loaders resolve paths against the page, while module imports use their source file.
                    page_root = CONTENT / relative.split('/')[0] if relative.startswith(('solo/', 'duo/')) else path.parent
                    reference(path, value, page_root)
        elif suffix == '.fnt':
            try:
                font = ET.fromstring(source)
                for page in font.findall('.//page'):
                    if page.get('file'): reference(path, page.get('file'))
            except ET.ParseError as error:
                errors.append(f'Invalid bitmap font {relative}: {error}')
        elif suffix == '.json':
            try:
                value = json.loads(source)
                if isinstance(value, dict) and isinstance(value.get('meta'), dict) and value['meta'].get('image'):
                    reference(path, value['meta']['image'])
            except ValueError as error:
                errors.append(f'Invalid JSON asset {relative}: {error}')
    manifest = ET.parse(RUNTIME / 'AndroidManifest.xml').getroot()
    namespace = '{http://schemas.android.com/apk/res/android}'
    if manifest.findall('uses-permission'):
        errors.append('Classic runtime must not declare permissions')
    for activity in manifest.findall('application/activity'):
        if activity.get(namespace + 'exported') != 'false': errors.append('Classic activity must not be exported')
        if activity.findall('intent-filter'): errors.append('Classic activity must not register a launcher/deep-link')
    report = {
        'schema': 1, 'passed': not errors, 'assetCount': len(assets),
        'verifiedStaticDependencyCount': dependency_count, 'inertLibraryAttributionCount': attribution_count,
        'assets': content_hashes(), 'runtimeInputs': input_hashes(), 'errors': sorted(set(errors)),
        'scope': 'Static entry/dependency/permission audit; dynamic loader paths and gameplay require runtime validation.',
    }
    return report


def verify_aar(path: Path, report):
    errors = []
    if not path.is_file(): return ['Missing AAR; run scripts/classic-gold-miner/build-runtime.py']
    try:
        with zipfile.ZipFile(path) as aar:
            provenance = json.loads(aar.read('META-INF/classic-gold-miner-provenance.json'))
            if provenance.get('assets') != report['assets']: errors.append('AAR web assets differ from current source; rebuild runtime')
            if provenance.get('runtimeInputs') != report['runtimeInputs']: errors.append('AAR native/build inputs differ from current source; rebuild runtime')
            expected = {'assets/classic-gold-miner/' + name for name in report['assets']}
            actual = {name for name in aar.namelist() if name.startswith('assets/')}
            if expected != actual: errors.append('AAR asset membership differs from source')
            for name, digest_value in report['assets'].items():
                archive_name = 'assets/classic-gold-miner/' + name
                if archive_name in actual and hashlib.sha256(aar.read(archive_name)).hexdigest() != digest_value:
                    errors.append('AAR asset hash differs: ' + name)
            if any(name.startswith(('jni/', 'libs/')) or name.endswith(('.so', '.dex')) for name in aar.namelist()):
                errors.append('AAR must contain no native engine, embedded browser or extra runtime libraries')
            if aar.read('AndroidManifest.xml') != (RUNTIME / 'AndroidManifest.xml').read_bytes(): errors.append('AAR manifest differs from source')
            if hashlib.sha256(aar.read('classes.jar')).hexdigest() != provenance.get('classesJarSha256'): errors.append('AAR Java bytecode hash differs from provenance')
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        errors.append('Invalid AAR: ' + str(error))
    return errors
