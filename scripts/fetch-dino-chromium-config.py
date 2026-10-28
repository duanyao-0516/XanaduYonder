#!/usr/bin/env python3
"""Fetch pinned public Chromium Dino reference files; never edit runtime settings.

The small literal parser accepts only the numeric/string/object subset present in
these configuration declarations. It does not execute downloaded JavaScript.
"""
import argparse
import ast
import base64
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import urllib.request

DEFAULT_REVISION = '3df8d372a3f9d5c38a89d0bc496cc44c85af1feb'
ORIGIN = 'https://chromium.googlesource.com/chromium/src'
GAME = 'components/neterror/resources/dino_game/'
SOURCE_PATHS = {name: GAME + name for name in (
    'offline.ts', 'trex.ts', 'obstacle.ts', 'offline_sprite_definitions.ts',
    'game_config.ts', 'constants.ts', 'night_mode.ts', 'horizon.ts',
    'horizon_line.ts', 'utils.ts', 'game_state_provider.ts',
    'image_sprite_provider.ts', 'dimensions.ts', 'sprite_position.ts',
    'distance_meter.ts')}
SOURCE_PATHS.update({'neterror.css': 'components/neterror/resources/neterror.css', 'LICENSE': 'LICENSE'})
TOKEN = re.compile(r'\s+|//[^\n]*|/\*[\s\S]*?\*/|(?:\d+(?:\.\d*)?|\.\d+)|\'(?:\\.|[^\'\\])*\'|"(?:\\.|[^"\\])*"|[A-Za-z_$][\w$]*|[{}\[\]():,./+-]')


class LiteralParser:
    def __init__(self, text):
        self.tokens = []
        pos = 0
        while pos < len(text):
            match = TOKEN.match(text, pos)
            if not match:
                raise ValueError('Unsupported source syntax near: ' + repr(text[pos:pos + 70]))
            token = match.group()
            if not token.isspace() and not token.startswith(('//', '/*')):
                self.tokens.append(token)
            pos = match.end()
        self.i = 0

    def take(self, expected=None):
        value = self.tokens[self.i]
        self.i += 1
        if expected is not None and value != expected:
            raise ValueError('Expected ' + expected + ', found ' + value)
        return value

    def peek(self):
        return self.tokens[self.i] if self.i < len(self.tokens) else None

    def value(self):
        token = self.take()
        if token == '{':
            result = {}
            while self.peek() != '}':
                key = self.take()
                if key == '[':  # [Status.RUNNING] in the animation lookup.
                    key = self.take()
                    while self.peek() != ']':
                        key += self.take()
                    self.take(']')
                elif key.startswith(('"', "'")):
                    key = ast.literal_eval(key)
                self.take(':')
                result[key] = self.value()
                if self.peek() != '}':
                    self.take(',')
            self.take('}')
            return result
        if token == '[':
            result = []
            while self.peek() != ']':
                result.append(self.value())
                if self.peek() != ']':
                    self.take(',')
            self.take(']')
            return result
        if token == 'new':
            self.take('CollisionBox'); self.take('(')
            values = [self.value()]
            for _ in range(3):
                self.take(','); values.append(self.value())
            self.take(')')
            return dict(zip(('x', 'y', 'width', 'height'), values))
        if token in ('true', 'false'):
            return token == 'true'
        if token.startswith(('"', "'")):
            return ast.literal_eval(token)
        sign = -1 if token == '-' else 1
        if token in ('-', '+'):
            token = self.take()
        if token == 'FPS':
            result = 60
        elif re.fullmatch(r'(?:\d+(?:\.\d*)?|\.\d+)', token):
            result = float(token) if '.' in token else int(token)
        else:
            raise ValueError('Unsupported literal token: ' + token)
        result *= sign
        if self.peek() == '/':
            self.take('/')
            result /= self.value()
        return result


def declaration(text, name):
    match = re.search(r'\b(?:const|let)\s+' + re.escape(name) + r'\b[^=]*=\s*', text)
    if not match:
        raise ValueError('Missing declaration: ' + name)
    start = match.end()
    # Config literals contain no semicolon; this intentionally fails on syntax changes.
    end = text.index(';', start)
    parser = LiteralParser(text[start:end])
    result = parser.value()
    if parser.peek() is not None:
        raise ValueError('Unconsumed tokens in ' + name)
    return result, {'startLine': text.count('\n', 0, match.start()) + 1,
                    'endLine': text.count('\n', 0, end) + 1}


def extract(files, revision):
    anchors = {}
    def read(file, name):
        value, span = declaration(files[file].decode(), name)
        anchors[name] = dict(file='source/' + file, **span,
                             url=f'{ORIGIN}/+/{revision}/{SOURCE_PATHS[file]}#n{span["startLine"]}')
        return value
    def read_enum(file, name):
        text = files[file].decode()
        match = re.search(r'\benum\s+' + re.escape(name) + r'\s*\{', text)
        if not match:
            raise ValueError('Missing enum: ' + name)
        start = text.index('{', match.start())
        end = text.index('}', start) + 1
        body = re.sub(r'\b([A-Z_]+)\s*=', r'\1:', text[start:end])
        value = LiteralParser(body).value()
        anchors[file + ':' + name] = {'file':'source/' + file,
            'startLine':text.count('\n', 0, match.start()) + 1,
            'endLine':text.count('\n', 0, end) + 1,
            'url':f'{ORIGIN}/+/{revision}/{SOURCE_PATHS[file]}#n{text.count(chr(10), 0, match.start()) + 1}'}
        return value
    fps = read('constants.ts', 'FPS')
    assert fps == 60, 'Update unit conversion before accepting a changed reference FPS'
    result = {
        'schemaVersion': 1,
        'purpose': 'Reference snapshot only. Not consumed by the Unity runtime.',
        'sourceRevision': revision,
        'coordinateSystem': 'Chromium canvas logical pixels, origin top-left; y grows downward. HDPI source image resolution does not double gameplay units.',
        'constants': {'fps': fps, 'dimensions': read('constants.ts', 'DEFAULT_DIMENSIONS')},
        'runner': {
            'base': read('offline.ts', 'defaultBaseConfig'),
            'normal': read('offline.ts', 'normalModeConfig'),
            'slow': read('offline.ts', 'slowModeConfig'),
        },
        'trex': {
            'base': read('trex.ts', 'defaultTrexConfig'),
            'normalJump': read('trex.ts', 'normalJumpConfig'),
            'slowJump': read('trex.ts', 'slowJumpConfig'),
            'collisionBoxes': read('trex.ts', 'collisionBoxes'),
            'animation': read('trex.ts', 'animFrames'),
        },
        'sprites': read('offline_sprite_definitions.ts', 'spriteDefinitionByType'),
        'obstacleDefaults': {'maxGapCoefficient':read('obstacle.ts','maxGapCoefficient'),
                             'maxObstacleLength':read('obstacle.ts','maxObstacleLength')},
        'distanceMeter': read_enum('distance_meter.ts','Config'),
        'night': {'config':read_enum('night_mode.ts','Config'), 'moonPhases':read('night_mode.ts','PHASES')},
        'sourceDeclarations': anchors,
    }
    # Formula expressions are descriptive strings, not instructions for execution.
    result['updateContracts'] = {
        'speedUnits': 'logical pixels per 60 Hz reference frame',
        'accelerationUnits': 'speed units per non-collision Runner.update callback; not multiplied by deltaTime',
        'speedIncrement': 'if currentSpeed < maxSpeed: currentSpeed += acceleration (can overshoot max by less than one increment)',
        'mobileInitialSpeed': 'if width < 600: min(speed, hasSlowdown ? speed : speed * width / 600 * mobileSpeedCoefficient); otherwise speed',
        'distanceIncrement': 'currentSpeed * deltaTime / (1000 / FPS)',
        'obstacleXIncrement': '-floor((currentSpeed + optionalSignedSpeedOffset) * FPS / 1000 * deltaTime)',
        'jumpInitialVelocity': 'initialJumpVelocity - currentSpeed / 10',
        'groundYPos': '150 - height(47) - bottomPad(10) = 93',
        'minJumpYThreshold': 'groundYPos - config.minJumpHeight',
        'maxJumpYThreshold': 'config.maxJumpHeight is an absolute y threshold for endJump, NOT a net jump height or hard clamp',
        'jumpPositionThenGravity': 'framesElapsed=deltaTime/(1000/60); y += round(jumpVelocity*framesElapsed); jumpVelocity += gravity*framesElapsed',
        'jumpRelease': 'endJump only clamps upward velocity to dropVelocity after reachedMinHeight',
        'airDuck': 'setSpeedDrop sets jumpVelocity=1; subsequent y increment multiplies velocity by speedDropCoefficient=3; gravity remains configured',
        'obstacleMinGap': 'round(groupWidth * speed + type.minGap * effectiveGapCoefficient)',
        'effectiveGapCoefficient': 'hasSlowdown ? horizonConstructorGapCoefficient * 2 : horizonConstructorGapCoefficient; toggleSpeed changes Runner config but does not update the stored Horizon coefficient',
        'normalToSlowGapCoefficient': 'Normal Horizon construction stores 0.6; first slow toggle therefore passes 0.6 * 2 = 1.2, not slowModeConfig.gapCoefficient(0.3) * 2',
        'slowObstacleDefinitionMutation': 'Each adjustObstacleSpeed call while hasSlowdown halves multipleSpeed and minSpeed, multiplies minGap by 1.5, and replaces variable yPos by its first entry; in-place mutation is not reversed by the non-slow branch',
        'obstacleMaxGap': 'round(minGap * maxGapCoefficient)',
        'obstacleGapSelection': 'uniform inclusive integer between minGap and maxGap; doubled if audio cues enabled',
        'groupSelection': 'uniform integer 1..3, forced to 1 if multipleSpeed > currentSpeed',
        'obstacleEligibility': 'currentSpeed >= type.minSpeed; maxObstacleDuplication limits repeated types',
        'collision': 'strict AABB overlap (< and >), detailed boxes relative to outer bounds x+1,y+1,width-2,height-2; no swept AABB in original',
        'score': 'round(ceil(distanceRan) * 0.025); this score triggers invertDistance milestones',
        'night': 'CSS invert(1) maps opaque white border to black; mode invertDistance controls score milestones and invertFadeDuration controls night duration',
    }
    result['equivalentAt60UpdateCallbacksPerSecond'] = {}
    for mode in ('normal', 'slow'):
        config = result['runner'][mode]
        result['equivalentAt60UpdateCallbacksPerSecond'][mode] = {
            'wideInitialLogicalPixelsPerSecond': config['speed'] * fps,
            'nominalMaxLogicalPixelsPerSecond': config['maxSpeed'] * fps,
            'accelerationLogicalPixelsPerSecondSquared': config['acceleration'] * fps * fps,
            'wideNominalRampSeconds': (config['maxSpeed'] - config['speed']) / (config['acceleration'] * fps),
            'gravityLogicalPixelsPerSecondSquared': result['trex'][mode+'Jump']['gravity'] * fps * fps,
            'initialJumpVelocityAtModeStartLogicalPixelsPerSecond':
                (result['trex'][mode+'Jump']['initialJumpVelocity'] - config['speed'] / 10) * fps,
        }
    return result


def fetch(url):
    with urllib.request.urlopen(url, timeout=45) as response:
        return response.read()


def resolve(ref):
    if not re.fullmatch(r'[A-Za-z0-9._/-]+', ref):
        raise ValueError('Invalid Git ref')
    url = f'{ORIGIN}/+/{ref}?format=JSON'
    data = json.loads(fetch(url).decode().removeprefix(")]}'"))
    assert re.fullmatch('[0-9a-f]{40}', data['commit'])
    return data


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def compare_ref(ref, files, reference_revision):
    commit = resolve(ref)
    comparisons = []
    for name in ('offline.ts', 'trex.ts', 'obstacle.ts', 'offline_sprite_definitions.ts'):
        url = f'{ORIGIN}/+/{commit["commit"]}/{SOURCE_PATHS[name]}'
        data = base64.b64decode(fetch(url+'?format=TEXT'), validate=True)
        comparisons.append({'file':name, 'sourceUrl':url,
            'tagSha256':hashlib.sha256(data).hexdigest(),
            'referenceSha256':hashlib.sha256(files[name]).hexdigest(),
            'identicalBytes':data == files[name]})
    return {'schemaVersion':1,
        'purpose':'Compare an explicitly selected Chrome version tag with the pinned snapshot; not evidence of the OPPO Chrome version.',
        'tag':ref, 'referenceRevision':reference_revision, 'status':'resolved',
        'revision':commit['commit'], 'commitTime':commit['committer']['time'],
        'files':comparisons, 'allFourFilesIdentical':all(x['identicalBytes'] for x in comparisons)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ref', default=DEFAULT_REVISION, help='Fixed commit by default; use main to explicitly refresh.')
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'docs/chromium-dino-reference')
    parser.add_argument('--verify', action='store_true', help='Offline: verify all bytes and re-extracted JSON against the manifest.')
    parser.add_argument('--compare-ref', help='Optionally compare four core files with an official version tag; writes a small hash-only comparison JSON.')
    args = parser.parse_args()
    out = args.output
    if args.verify:
        manifest = json.loads((out/'manifest.json').read_text())
        files = {}
        for entry in manifest['files']:
            data = (out/entry['localPath']).read_bytes()
            assert hashlib.sha256(data).hexdigest() == entry['sha256'], entry['localPath']
            assert len(data) == entry['bytes'], entry['localPath']
            files[Path(entry['localPath']).name] = data
        parameters = extract(files, manifest['revision'])
        assert parameters == json.loads((out/'parameters.json').read_text()), 'Extracted parameters changed'
        for path in out.glob('*-comparison.json'):
            comparison = json.loads(path.read_text())
            assert comparison['referenceRevision'] == manifest['revision'], str(path)
            if comparison['status'] == 'resolved':
                for entry in comparison['files']:
                    assert entry['referenceSha256'] == hashlib.sha256(files[entry['file']]).hexdigest()
                    assert entry['identicalBytes'] == (entry['tagSha256'] == entry['referenceSha256'])
                assert comparison['allFourFilesIdentical'] == all(x['identicalBytes'] for x in comparison['files'])
        print(f'VERIFIED {len(files)} source files and all extracted parameters at {manifest["revision"]}')
        return
    commit = resolve(args.ref)
    revision = commit['commit']
    def get(item):
        name, path = item
        url = f'{ORIGIN}/+/{revision}/{path}?format=TEXT'
        data = base64.b64decode(fetch(url), validate=True)
        assert data and b'Copyright' in data[:2500], 'Unexpected source response for ' + name
        return name, data
    with ThreadPoolExecutor(max_workers=5) as pool:
        files = dict(pool.map(get, SOURCE_PATHS.items()))
    parameters = extract(files, revision)  # Fail before writing if upstream syntax changed.
    (out/'source').mkdir(parents=True, exist_ok=True)
    entries = []
    for name, data in files.items():
        (out/'source'/name).write_bytes(data)
        entries.append({'localPath':'source/'+name, 'upstreamPath':SOURCE_PATHS[name],
                        'sourceUrl':f'{ORIGIN}/+/{revision}/{SOURCE_PATHS[name]}',
                        'bytes':len(data), 'sha256':hashlib.sha256(data).hexdigest()})
    manifest = {'schemaVersion':1, 'origin':ORIGIN, 'requestedRef':args.ref,
                'revision':revision, 'commitTime':commit['committer']['time'],
                'fetchedAtUtc':datetime.now(timezone.utc).isoformat(),
                'decoding':'Gitiles ?format=TEXT base64 decoded to original file bytes',
                'files':entries}
    write_json(out/'manifest.json',manifest)
    write_json(out/'parameters.json',parameters)
    if args.compare_ref:
        comparison = compare_ref(args.compare_ref, files, revision)
        filename = 'chrome-' + args.compare_ref.split('/')[-1] + '-comparison.json'
        write_json(out/filename, comparison)
    print(f'SAVED {len(files)} source files plus manifest and extracted parameters at {revision}')


if __name__ == '__main__':
    main()
