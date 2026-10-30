#!/usr/bin/env python3
"""Read-only finite-resource and conservative Lua load-site audit of game.love.

No Lua is executed. This is a lexical inventory plus source-pinned profile
contracts, not a whole-program proof. Unknown or changed dynamic expressions
remain INCOMPLETE (exit 2), even when the finite file checks pass.
"""
import argparse
import ast
import hashlib
import io
import json
import re
import zipfile
from collections import Counter, deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SEARCH = ['?.lua', '?/init.lua', 'kr3-desktop/?.lua', 'kr3/?.lua',
          'all-desktop/?.lua', 'all/?.lua', 'lib/?.lua', 'lib/?/init.lua']
CONSTANTS = {'KR_PATH_GAME': 'kr3', 'KR_PATH_GAME_TARGET': 'kr3-desktop',
             'KR_PATH_ALL': 'all', 'KR_PATH_ALL_TARGET': 'all-desktop'}
SEEDS = ['main.lua', 'conf.lua', 'main_globals.lua', 'firstgen.lua']
ENTRY_MODULES = ['director', 'screen_splash', 'screen_slots', 'screen_loading',
                 'screen_map', 'game', 'game_gui', 'screen_comics',
                 'screen_kr1_end', 'screen_kr1_fa', 'screen_credits',
                 'storage_io_generic', 'assets.strings.zh-Hans']
BUILTINS = {'bit', 'ffi', 'jit', 'jit.opt', 'jit.util', 'coroutine', 'math',
            'string', 'table', 'io', 'os', 'debug', 'package', 'utf8'}
NATIVE_MODULES = {'libjni_android': 'Android AAR module; verified by verify-apk.py'}
DIRECT_METHODS = {
    'love.graphics': {'newImage', 'newFont', 'setNewFont', 'newShader', 'newImageFont', 'newVideo'},
    'love.image': {'newImageData', 'newCompressedData'},
    'love.audio': {'newSource'}, 'love.mouse': {'newCursor'},
    'love.filesystem': {'load', 'read', 'newFile', 'newFileData', 'lines'},
    'io': {'open', 'lines', 'input', 'output'},
}
_LONG = re.compile(r'\[(=*)\[')
_TOKEN = re.compile(r'[A-Za-z_]\w*|\d+(?:\.\d+)?|\.\.|.')


def tokens(text):
    """Skip Lua comments; retain quoted and long strings as one token."""
    out = []
    p = 0
    line = 1
    while p < len(text):
        start = p
        ch = text[p]
        if ch.isspace() or ch == '\ufeff':
            line += ch == '\n'; p += 1; continue
        comment = text.startswith('--', p)
        q = p + 2 if comment else p
        long = _LONG.match(text, q)
        if long:
            close = ']' + long.group(1) + ']'
            end = text.find(close, q + len(long.group()))
            if end < 0: raise ValueError('Unterminated Lua long string/comment')
            p = end + len(close)
            if not comment: out.append(('str', text[q + len(long.group()):end], start, p, line))
            line += text[start:p].count('\n'); continue
        if comment:
            end = text.find('\n', p); p = len(text) if end < 0 else end; continue
        if ch in '\"\x27':
            p += 1
            while p < len(text):
                if text[p] == '\\': p += 2; continue
                if text[p] == ch: p += 1; break
                p += 1
            raw = text[start:p]
            try: value = ast.literal_eval(raw)
            except (SyntaxError, ValueError): value = None
            out.append(('str', value, start, p, line)); line += raw.count('\n'); continue
        match = _TOKEN.match(text, p)
        value = match.group(); p += len(value)
        kind = 'id' if re.match(r'^[A-Za-z_]', value) else 'sym'
        out.append((kind, value, start, p, line))
    return out


def expression(ts, text):
    return text[ts[0][2]:ts[-1][3]].strip() if ts else ''


def constant(ts):
    if len(ts) == 1:
        if ts[0][0] == 'str': return ts[0][1]
        return CONSTANTS.get(ts[0][1])
    parts = []
    for i, token in enumerate(ts):
        if i % 2:
            if token[1] != '..': return None
        else:
            value = constant([token])
            if value is None: return None
            parts.append(value)
    return ''.join(parts) if len(ts) % 2 else None


def calls(text):
    ts = tokens(text)
    aliases = dict(re.findall(r'local\s+(\w+)\s*=\s*(love\.\w+)\b', text))
    aliases.update(dict(re.findall(r'local\s+(\w+)\s*=\s*require\(["\x27](love\.\w+)["\x27]\)', text)))
    found = []
    for i, token in enumerate(ts):
        if token[0] != 'id' or (i and ts[i-1][1] in ('.', ':', 'function')): continue
        j = i + 1; pieces = [token[1]]
        while j + 1 < len(ts) and ts[j][1] in ('.', ':') and ts[j+1][0] == 'id':
            pieces.extend([ts[j][1], ts[j+1][1]]); j += 2
        callee = ''.join(pieces)
        if j >= len(ts): continue
        if callee == 'require' and ts[j][0] == 'str': args = [[ts[j]]]
        elif ts[j][1] == '(':
            depth = 0; start = j + 1; args = []; k = start
            while k < len(ts):
                v = ts[k][1]
                if ts[k][0] == 'sym' and v in ('(', '{', '['): depth += 1
                elif ts[k][0] == 'sym' and v in (')', '}', ']'):
                    if depth == 0:
                        args.append(ts[start:k]); break
                    depth -= 1
                elif ts[k][0] == 'sym' and v == ',' and depth == 0:
                    args.append(ts[start:k]); start = k + 1
                k += 1
        else: continue
        if callee in ('pcall', 'xpcall') and args:
            callee = expression(args[0], text); args = args[1:]
        prefix, _, method = callee.rpartition('.')
        prefix = aliases.get(prefix, prefix)
        normalized = prefix + '.' + method if prefix else callee
        if normalized not in ('require', '_require', 'loadfile', 'dofile') and method not in DIRECT_METHODS.get(prefix, set()): continue
        arg = args[0] if args else []
        found.append({'line': token[4], 'api': normalized,
                      'expression': expression(arg, text), 'literal': constant(arg)})
    # Thread workers are Lua source strings and have their own filesystem API state.
    for index, token in enumerate(ts):
        if token[0] == 'str' and isinstance(token[1], str) and "require" in token[1] and 'cin:demand()' in token[1]:
            for row in calls(token[1]):
                row['line'] += token[4] - 1; row['embedded_thread'] = True; found.append(row)
    return found


def classify_profile(report, z, check, resolve, read, template_review=None):
    """Classify reviewed call sites, retaining changed/unknown sites as unresolved.

    These are explicit profile contracts, not inferred whole-program reachability.
    All finite inputs and the evidence source lines are emitted in the report.
    """
    names = set(z.namelist())
    report['classified_dynamic_calls'] = []
    report['profile_contracts'] = []
    report['finite_domains'] = {}
    cache = {}

    def source(p):
        if p not in cache: cache[p] = read(p)
        return cache[p]

    def anchor(p, value):
        text = source(p)
        offset = text.find(value)
        return {'source': p, 'anchor': value, 'present': offset >= 0,
                'line': text[:offset].count('\n') + 1 if offset >= 0 else None,
                'sha256': hashlib.sha256(z.read(p)).hexdigest()}

    contracts = [
        ('main_globals.lua', 'KR_GAME = "kr3"'),
        ('main_globals.lua', 'KR_TARGET = "desktop"'),
        ('main_globals.lua', 'KR_PLATFORM = "win"'),
        ('firstgen.lua', 'settings.locale = P.locale'),
        ('firstgen.lua', 'settings.texture_size = "fullhd"'),
        ('firstgen.lua', 'game.store.level_idx >= 45 and game.store.level_idx <= 76'),
        ('firstgen.lua', 'game.store.level_mode >= 1 and game.store.level_mode <= 3'),
        ('firstgen.lua', 'lh.power_selection = {slots={"fireball_g1", "reinforcement_g1", "empty_spell"}}'),
        ('all/i18n.lua', 'i18n.supported_locales = {"zh-Hans"}'),
        ('features.lua', 'platform_services = {}'),
        ('version.lua', 'v = "RELEASE"'),
    ]
    for p, value in contracts:
        evidence = anchor(p, value); report['profile_contracts'].append(evidence)
        if not evidence['present']:
            report['errors'].append({'kind': 'profile-classification-contract-changed', **evidence})
    if any(not row['present'] for row in report['profile_contracts']): return
    reviewed_raw = (ROOT / 'ThirdParty/KingdomRushContent/direct-load-reviewed-sources.json').read_bytes()
    reviewed = json.loads(reviewed_raw)
    changed = [p for p, sha in reviewed['sources'].items()
               if p not in names or hashlib.sha256(z.read(p)).hexdigest() != sha]
    report['reviewed_source_binding'] = {'manifest': 'ThirdParty/KingdomRushContent/direct-load-reviewed-sources.json',
        'manifest_sha256': hashlib.sha256(reviewed_raw).hexdigest(), 'count': len(reviewed['sources']), 'changed': changed}
    if changed:
        report['errors'].append({'kind': 'manual-dynamic-review-stale', 'sources': changed,
            'action': 'Review the changed source/control flow and update the reviewed-source manifest only after review.'})
        return
    report['native_launch_contract'] = {'source': 'ThirdParty/KingdomRushRuntime/src/org/love2d/android/GameActivity.java',
        'scope': 'fused assets/game.love launch; no debug/custom/balance arguments', 'device_execution': False}
    java = (ROOT / report['native_launch_contract']['source']).read_bytes()
    report['native_launch_contract']['sha256'] = hashlib.sha256(java).hexdigest()
    if b'args = new String[0];' not in java or b'return args;' not in java:
        report['errors'].append({'kind': 'native-launch-contract-changed'})
        return
    external_roots = ('atlas/input/', 'atlas/output/', 'animations/immutable/', 'animations/alterable/',
                      'dds2pkm_lz4/', 'wave_editor/', 'custom/')
    unexpected = sorted(p for p in names if p.startswith(external_roots))
    if unexpected:
        report['errors'].append({'kind': 'unexpected-external-input-bundled', 'paths': unexpected})
        return
    report['external_input_boundary'] = {
        'unbundled_roots': list(external_roots),
        'meaning': 'Optional save/editor/custom files are external runtime state. Archive validation cannot certify user-injected files, storage writes or corrupted saves.'}

    def domain(key, paths, reason, optional=False):
        paths = sorted(set(paths))
        report['finite_domains'][key] = paths
        for p in paths: check(p, reason, optional)
        return paths

    # Enumerated data must come from the same immutable game.love snapshot.
    domain('optional_wave_sources', [f'kr3/data/waves/level{i}_waves_{m}{s}'
        for i in range(45, 77) for m in ('campaign', 'heroic', 'iron')
        for s in ('.tsv', '_extra.lua')], 'wave_db optional source branch with existence guard', True)
    domain('optional_profile_files', ['all-desktop/assets/fonts/font_settings.lua',
        'kr3-desktop/data/remote_config_defaults.lua', 'kr3-desktop/assets/icons/icon256.png'],
        'explicit existence-guarded optional profile file', True)
    domain('animation_files', ['kr3/data/game_animations-5.lua'] + [p for p in names
        if p.startswith('kr3/data/animations/') and p.endswith('.lua')],
        'animation_db fixed root plus actual directory enumeration')
    domain('shader_files', [p for p in names if p.startswith('all-desktop/assets/shaders/') and p.endswith('.c')],
        'shader_db preloads every .c in the configured directory')
    check('all/constants.lua', 'LU.eval_file fixed environment module')
    sound_files = set()
    for p in ('kr3-desktop/assets/sounds/sounds.lua', 'kr3-desktop/assets/sounds/extra.lua'):
        if p in names:
            for t in tokens(source(p)):
                if t[0] == 'str' and isinstance(t[1], str) and t[1].endswith(('.ogg', '.wav', '.mp3')):
                    sound_files.add('kr3-desktop/assets/sounds/files/' + t[1])
    domain('sound_files', sound_files, 'all literal file values in the active sound index, including worker inputs')
    atlas_files = sorted(p for p in names if p.startswith('kr3-desktop/assets/images/fullhd/') and p.endswith('.lua'))
    atlas_media = set()
    for p in atlas_files:
        ts = tokens(source(p))
        for i, t in enumerate(ts):
            if t[1] != 'a_name': continue
            # Both original a_name="x" and generated ["a_name"]="x" tables.
            j = i + (3 if i + 1 < len(ts) and ts[i+1][1] == ']' else 2)
            if j >= len(ts) or ts[j][0] != 'str': continue
            name = ts[j][1]
            if not isinstance(name, str): continue
            parent = p.rsplit('/', 1)[0] + '/'
            candidate = parent + name
            if name.lower().endswith(('.dds', '.png')):
                astc = re.sub(r'\.[^.]+$', '.astc', candidate)
                png = re.sub(r'\.[^.]+$', '.png', candidate)
                candidate = astc if astc in names else png if png in names else candidate
            atlas_media.add(candidate)
    domain('atlas_indexes', atlas_files, 'actual packaged atlas index domain')
    domain('atlas_media', atlas_media, 'Android DDS/PNG to ASTC selection from every packaged atlas record')

    # Resolve UI template indirection in the real desktop-first search order.
    # Conservatively include all literal KDB calls in the module closure, even
    # when the custom desktop screen does not enter that particular branch.
    template_paths = ('kr3-desktop/data/kui_templates/', 'all-desktop/data/kui_templates/', 'kr3/data/kui_templates/')
    todo = []
    for row in report['sources']:
        text = source(row['path'])
        aliases = re.findall(r'(\w+)\s*=\s*require\(["\x27](?:klove\.)?kui_db["\x27]\)', text)
        for alias in set(aliases):
            for match in re.finditer(r'\b' + re.escape(alias) + r':(?:get_table|get|read)\(\s*(["\x27])([^"\x27]+)\1', text):
                todo.append(match.group(2))
    seen_templates = set(); resolved_templates = []
    while todo:
        name = todo.pop()
        if name in seen_templates: continue
        seen_templates.add(name)
        p = next((base + name + '.lua' for base in template_paths if base + name + '.lua' in names), None)
        if not p:
            report['errors'].append({'kind': 'missing-ui-template', 'name': name}); continue
        resolved_templates.append(p)
        todo.extend(re.findall(r'\btemplate_name\s*=\s*["\x27]([^"\x27]+)', source(p)))
        for call in calls(source(p)):
            row = {'source': p, **call, 'status': 'unreviewed-direct-call-in-ui-template'}
            report['calls'].append(row); report['unresolved'].append(row)
    domain('ui_templates', resolved_templates, 'literal KDB callers plus recursive template_name in desktop-first lookup')
    shader_names = set()
    for p in set(row['path'] for row in report['sources']) | set(resolved_templates):
        shader_names.update(t[1] for t in tokens(source(p))
            if t[0] == 'str' and isinstance(t[1], str) and re.fullmatch('p_[A-Za-z0-9_]+', t[1]))
    domain('referenced_shader_files', ['all-desktop/assets/shaders/' + name + '.c' for name in shader_names],
        'shader literals from conservative modules and resolved UI templates; GPU compile is separate')

    # Independent template evaluation is supplemental evidence, only accepted
    # when its input hash is the exact package being checked.
    template_evidence = None
    if template_review:
        data_path = Path(template_review).with_name('evaluated-template-data.json')
        review_bytes = Path(template_review).read_bytes()
        review = json.loads(review_bytes)
        data_bytes = data_path.read_bytes(); templates = json.loads(data_bytes)
        fields = []
        def walk(value, where):
            if isinstance(value, dict):
                for key, item in value.items():
                    if key in ('load_file', 'exo', 'shader') and item:
                        fields.append({'field': where + '.' + key, 'value': item})
                    walk(item, where + '.' + key)
            elif isinstance(value, list):
                for i, item in enumerate(value): walk(item, where + '[' + str(i) + ']')
        walk(templates, 'templates')
        template_evidence = {'path': str(Path(template_review).resolve()),
            'review_sha256': hashlib.sha256(review_bytes).hexdigest(),
            'templates_sha256': hashlib.sha256(data_bytes).hexdigest(),
            'content_sha256': review.get('content_sha256'), 'template_count': len(templates), 'load_fields': fields}
        report['template_evidence'] = template_evidence
        if review.get('evaluated_template_data_sha256') != template_evidence['templates_sha256'] or review.get('staging_matches_archive') is not True:
            report['errors'].append({'kind': 'template-evidence-data-binding-mismatch',
                'expected_data_sha256': review.get('evaluated_template_data_sha256'),
                'actual_data_sha256': template_evidence['templates_sha256'],
                'staging_matches_archive': review.get('staging_matches_archive')})
            template_evidence = None
        elif review.get('content_sha256') != report['content_sha256']:
            report['errors'].append({'kind': 'stale-template-evidence', 'expected': report['content_sha256'],
                                     'actual': review.get('content_sha256')})
            template_evidence = None
        else:
            for field in fields:
                if field['field'].endswith('.load_file'):
                    check('kr3/data/levels/' + field['value'] + '.lua', 'effective selected template load_file')
                if field['field'].endswith('.shader') and isinstance(field['value'], str):
                    check('all-desktop/assets/shaders/' + field['value'] + '.c', 'effective selected template shader')

    def rule(predicate, category, evidence, anchors=()):
        matches = [row for row in report['unresolved'] if row['source'] in reviewed['sources'] and predicate(row)]
        if not matches: return
        proofs = [anchor(p, value) for p, value in anchors]
        if any(not p['present'] for p in proofs):
            report['errors'].append({'kind': 'classification-evidence-changed', 'evidence': evidence, 'anchors': proofs})
            return
        for row in matches:
            row.update(status=category, evidence=evidence, evidence_anchors=proofs)
            report['classified_dynamic_calls'].append(row)
        report['unresolved'] = [row for row in report['unresolved'] if row not in matches]

    def at(p, api=None, expr=None):
        return lambda row: row['source'] == p and (api is None or row['api'] == api) and (expr is None or row['expression'] == expr)

    rule(lambda r: r['api'] == 'io.output' and not r['expression'], 'generated-object-no-file',
         'io.output() queries the existing stream; it does not open a filename.')
    rule(at('main.lua', 'love.graphics.setNewFont'), 'generated-object-no-file',
         'Numeric size math.floor(love.window.toPixels(15)); built-in error-screen font.',
         [('main.lua', 'G.setNewFont(math.floor(love.window.toPixels(15)))')])
    rule(lambda r: r['source'].startswith('kr3/game_scripts-') and r['api'] == 'love.graphics.newImage'
         and r['expression'] == 'image_data', 'generated-object-no-file',
         'Local image_data is canvas/path_canvas:newImageData(); no archive path argument.')
    rule(at('kr3/game_scripts-45e.lua', 'love.graphics.newImage', 'data'), 'generated-object-no-file',
         'data is an in-memory ImageData created for the script texture; no filename.')
    rule(lambda r: r['source'] == 'lib/klove/font_db.lua' and r['api'] == 'love.graphics.newImage'
         and r['expression'] in ('data', 'image_data'), 'generated-object-no-file',
         'Font rendering: 1x1 fallback ImageData or c:newImageData() from rendered text.',
         [('lib/klove/font_db.lua', 'local image_data = c:newImageData()'),
          ('lib/klove/font_db.lua', 'pcall(love.image.newImageData, 1, 1)')])
    rule(lambda r: r['source'] == 'lib/klove/image_db.lua' and r['api'] == 'love.graphics.newImage'
         and r['expression'] in ('data', 'imd'), 'generated-object-no-file',
         'ImageData/CompressedImageData returned by image worker or newCompressedData; source media checked in atlas_media.')

    rule(lambda r: r['source'] == 'main.lua' and r['api'] == 'require'
         and r['expression'] in ('"lldebugger"', '"log_levels_debug"', '"profiler"', '"draw_stats"', '"localuser"', 'main.params.custom_script'),
         'profile-disabled', 'Release profile has no debug/profiler/custom-script flags; fused Android GameActivity starts with zero arguments.',
         [('main.lua', 'if version.build == "RELEASE" then'), ('features.lua', 'skip_settings_dialog = true')])
    rule(at('main.lua', 'love.image.newImageData', 'icon'), 'finite-optional-file',
         'Only kr3-desktop/assets/icons/icon256.png; missing file explicitly skips setIcon.',
         [('main.lua', 'if love.filesystem.isFile(icon) then')])
    rule(lambda r: r['source'] == 'main.lua' and r['api'] == 'love.filesystem.load'
         and r['expression'] in ('inputFile', 'outputFile'), 'external-developer-input',
         'Startup conversion utility only enumerates atlas/input, animations/immutable, animations/alterable or dds2pkm_lz4 after directory guards; none are packaged. These are optional external developer inputs, not game resources.')
    rule(at('all/director.lua', 'love.mouse.newCursor'), 'finite-input-domain',
         'cursor_name is hand_32, hand_48 or hand_64; both states plus crosshair are checked by bootstrap-resources.json.')
    rule(lambda r: r['source'] == 'all/director.lua' and r['api'] in ('require', '_require')
         and r['expression'] in ('name', 'props.src'), 'finite-input-domain',
         'Selected director routes resolve to entry_modules: splash/slots/loading/map/game/game_gui/credits/comics/kr1_end/kr1_fa. Selected level outcomes checked separately.')
    rule(at('all/director.lua', 'require', 'string.format("data.levels.level%02i_data",args.level_idx)'),
         'profile-disabled', 'The extra dynamic require is guarded by args.level_idx >= 101; selected range is 45..76.',
         [('all/director.lua', 'if args.level_idx >= 101 then')])
    rule(at('all/director.lua', 'love.filesystem.read'), 'finite-input-domain',
         'Only selected ending comics 19/20/21 are retained. Selected level scripts have no show_comic_idx; generic comic route requires unsupported custom launch arguments.')
    rule(at('all/storage.lua', 'require', 'features.storage_io or "storage_io_generic"'), 'finite-input-domain',
         'features has no storage_io override; storage_io_generic is an explicit audited entry module.')
    rule(lambda r: r['source'] == 'all/storage.lua' and r['api'] in ('io.open', 'require'),
         'profile-disabled', 'Legacy phone plist import requires KR_TARGET=phone; .NET import requires KR_GAME=kr1. Actual profile is desktop/kr3/win.')
    rule(at('all/i18n.lua', 'require'), 'finite-input-domain',
         'Locale is forced to zh-Hans on settings load and supported_locales contains only zh-Hans; its module is seeded and resolved.')
    rule(at('all/platform_services.lua', 'require'), 'profile-disabled',
         'features.platform_services is an empty table; require(v.src) loop has zero entries.')
    rule(at('mod.lua', 'require', 'i'), 'finite-optional-module',
         'mod.globals contains only UH=true. UH.lua is absent and pcall explicitly accepts module-not-found; this is the original optional hook contract.',
         [('mod.lua', 'UH = true'), ('mod.lua', 'package.preload[')])
    rule(at('lib/klove/font_db.lua', 'love.filesystem.load'), 'finite-optional-file',
         'Only all-desktop/assets/fonts/font_settings.lua; font_db checks isFile before reading.')
    rule(lambda r: r['source'] == 'lib/klove/font_db.lua' and r['api'] in ('love.graphics.newImageFont', 'love.graphics.newImage'),
         'profile-disabled', 'Only PNG bitmap-font branch; packaged font directory contains no PNG files.')
    rule(at('lib/klove/font_db.lua'), 'finite-input-domain',
         'Seven enumerated packaged TTF files (or cached FileData made from them); full list and hashes are checked.')
    rule(lambda r: r['source'] in ('all/main_utils.lua', 'all/debug_tools.lua', 'lib/klua/log.lua',
                                  'lib/klua/persistence.lua', 'lib/xmlSimple.lua') and r['api'] in ('io.open', 'io.output', 'loadfile'),
         'external-runtime-state', 'Log output/save serialization/editor export/legacy import helper. Runtime filesystem input or stream, not a required packaged asset; storage uses serialize_to_string rather than persistence.load/store.')
    rule(at('all/main_utils.lua', 'require'), 'profile-disabled',
         'mobdebug and klua.repl are under DEBUG and explicit debug/repl parameter guards; release fused launch supplies neither.')
    rule(lambda r: r['source'] == 'lib/klove/image_db.lua' and (r['api'] == 'require' or r['line'] == 907),
         'unused-legacy-loader', 'image_db:load directory loader has no callers in the selected module graph; director and main use load_atlas. Its directory-enumerated files are not additional selected dependencies.')
    rule(at('lib/klove/image_db.lua'), 'finite-input-domain',
         'Atlas loaders/worker receive group indexes and a_name media from finite atlas_indexes/atlas_media; ASTC fallback resolved against actual ZIP.')
    rule(at('lib/klove/shader_db.lua'), 'finite-input-domain',
         'Configured shader directory contains all literal shader names in audited modules and resolved KDB templates. Final template shaders are checked when supplemental evidence is supplied; GPU compile remains runtime validation.')
    rule(at('lib/klove/kui_db.lua', 'io.open'), 'profile-disabled', 'KDB write/export method exists only under DEBUG.')
    rule(at('lib/klove/kui_db.lua', 'love.filesystem.read'), 'finite-input-domain',
         'ui_templates enumerates every literal KDB caller in audited modules and recursively resolves template_name using actual desktop-first paths.')
    rule(at('all/sound_db.lua'), 'finite-input-domain',
         'Configured sound path and settings/sounds/groups/optional extra are checked; every indexed audio filename is in sound_files, including worker and direct newSource paths.')
    rule(lambda r: r['source'] in ('all/level_utils.lua', 'all/path_db.lua', 'all/grid_db.lua') and r['api'] == 'io.open',
         'external-developer-output', 'Editor/export write path; does not read additional package media.')
    rule(lambda r: r['source'] in ('all/level_utils.lua', 'all/path_db.lua', 'all/grid_db.lua'),
         'finite-input-domain', 'Selected 45..76 level main/data/grid/paths/optional loc and all/constants.lua are enumerated; legacy grid loader names the same finite grid domain.')
    rule(at('all/level_utils_6.lua'), 'profile-disabled',
         'LU6 chosen only when store.level_mode_6 applies to levels 251..269; firstgen selects 45..76.')
    rule(at('all/remote_config.lua'), 'finite-optional-file',
         'Only kr3-desktop/data/remote_config_defaults.lua, with existence guard; remote platform service is disabled.')
    rule(at('all/storage_io_generic.lua'), 'external-runtime-state',
         'Settings, slots, registry and save files are runtime state under the isolated identity. Protected reads return no data on absent or invalid state; no package asset expected.')
    rule(at('all/wave_editor_store.lua'), 'external-runtime-state',
         'Editor registry/list data is optional isolated save state; no assignment returns nil, then the normal 96-file wave loader runs. Source editor reads resolve the same finite wave/path domain. No bundled editor assignment is present.')
    rule(at('all/systems.lua', 'require'), 'finite-input-domain',
         'store.level_name is level45..level76; all 32 _data modules are explicitly checked.')
    rule(at('lib/klove/animation_db.lua'), 'finite-input-domain',
         'animation_files contains the fixed game_animations-5 root and every Lua file in the enumerated kr3/data/animations directory; frame/ANI meaning is checked by the separate template review.')
    rule(at('lib/klua/tsv.lua'), 'finite-input-domain',
         'Wave TSV filenames are the 96 optional sources and comics use the checked ending data; Lua wave fallback is mandatory and checked.')
    rule(at('all/wave_db.lua'), 'finite-input-domain-with-external-override',
         '96 mandatory Lua wave files and optional _extra/TSV are checked. Optional /custom and wave_editor state may override them only when external files/assignments exist; no such overrides are bundled.')
    rule(lambda r: r['source'].startswith('kr3/game_templates-') and r['expression'] == 'balance_override_path',
         'profile-disabled', 'H.command_line_has_arg("balance_override") guard; fused Android launch has no command-line flags.')
    rule(lambda r: r['source'] == 'all/exoskeleton.lua' and r['expression'] in ('"krn"', '"MessagePack"'),
         'profile-disabled', 'KR_NATIVE is unset and local MSGPACK_ON=false; native/MessagePack branches are disabled.',
         [('all/exoskeleton.lua', 'local KRN = KR_NATIVE and require("krn")'), ('all/exoskeleton.lua', 'local MSGPACK_ON = false')])
    if template_evidence:
        rule(lambda r: re.fullmatch(r'all/scripts(?:_[A-Za-z0-9]+)?\.lua', r['source']) is not None
             and r['api'] == 'love.filesystem.load' and r['expression'] == 'fn',
             'finite-input-domain', 'mega_spawner load_file: selected level overrides checked recursively; independently evaluated final template load_file values checked and bound to this content SHA.')
        no_exo = not any(f['field'].endswith('.exo') for f in template_evidence['load_fields'])
        if no_exo:
            rule(at('all/exoskeleton.lua'), 'profile-disabled',
                 'Independent final template data has no render.exo; firstgen exoskeleton list is empty; selected level data has no exo requirements. Native heroes/powers and map_animations1 have no EXO inputs; disabled tower/cheat branches cannot add them.',
                 [('firstgen_dependencies.lua', '["exoskeletons"]={}'),
                  ('firstgen.lua', 'slot.towers = {}'), ('firstgen.lua', 'game.store.dove43_required_exoskeletons = {}')])
    report['classification_summary'] = dict(sorted(Counter(row['status'] for row in report['classified_dynamic_calls']).items()))


def audit(path, template_review=None):
    raw = Path(path).read_bytes()
    report = {'content': str(Path(path).resolve()), 'content_sha256': hashlib.sha256(raw).hexdigest(),
              'audit_script_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'scope': 'finite direct-file contracts and conservative lexical module closure; no Lua/game execution',
              'errors': [], 'unresolved': [], 'known_external_or_optional': [], 'finite_files': [],
              'entry_modules': {}, 'sources': [], 'calls': [], 'compatibility': []}
    with zipfile.ZipFile(io.BytesIO(raw)) as z:
        names = set(z.namelist())
        def read(p): return z.read(p).decode('utf-8-sig')
        def resolve(name):
            for pattern in SEARCH:
                candidate = pattern.replace('?', name.replace('.', '/'))
                if candidate in names: return candidate
            return None
        def check(p, reason, optional=False):
            exists = p in names
            row = {'path': p, 'reason': reason, 'present': exists, 'optional': optional}
            if exists: row['sha256'] = hashlib.sha256(z.read(p)).hexdigest()
            elif not optional: report['errors'].append({'kind': 'missing-file', **row})
            report['finite_files'].append(row)
        seeds = list(SEEDS)
        # FS.load evaluates these files too: atlas aggregators can themselves
        # require sibling indexes, and animation directory entries are code.
        seeds.extend(p for p in sorted(names) if p.endswith('.lua') and
                     (p.startswith('kr3-desktop/assets/images/fullhd/') or p.startswith('kr3/data/animations/')))
        seeds.extend(p for p in ('kr3-desktop/assets/sounds/settings.lua', 'kr3-desktop/assets/sounds/sounds.lua',
                               'kr3-desktop/assets/sounds/groups.lua', 'kr3-desktop/assets/sounds/extra.lua',
                               'all-desktop/assets/fonts/font_settings.lua') if p in names)
        for name in ENTRY_MODULES:
            p = resolve(name); report['entry_modules'][name] = p
            if p: seeds.append(p)
            else: report['errors'].append({'kind': 'missing-entry-module', 'module': name})
        for level in range(45, 77):
            stem = f'kr3/data/levels/level{level}'
            for suffix in ('_data.lua', '_grid.lua', '_paths.lua'):
                check(stem + suffix, '32-level finite data/grid/path domain'); seeds.append(stem + suffix)
            for suffix in ('.lua', '_loc.lua'):
                check(stem + suffix, 'level_utils explicitly accepts an absent script/location', True)
                if stem + suffix in names: seeds.append(stem + suffix)
            for mode in ('campaign', 'heroic', 'iron'):
                p = f'kr3/data/waves/level{level}_waves_{mode}.lua'
                check(p, '32 levels x 3 modes Lua fallback after optional TSV'); seeds.append(p)
        # Selected map data can reference extra spawner files outside the main
        # level filename family; these bypass require and atlas loaders.
        for p in list(seeds):
            if p not in names or not p.startswith('kr3/data/levels/'): continue
            for target in re.findall(r'\bload_file\s*=\s*["\x27]([^"\x27]+)["\x27]', read(p)):
                dep = 'kr3/data/levels/' + target + '.lua'
                check(dep, 'selected level load_file spawner reference from ' + p); seeds.append(dep)
        for i in (19, 20, 21): check(f'kr3-desktop/data/comics/{i:02}.csv', 'KR1 ending/follow-up direct CSV read')
        for name in ('settings.lua', 'sounds.lua', 'groups.lua', 'extra.lua'):
            check('kr3-desktop/assets/sounds/' + name, 'sound_db direct index load', name == 'extra.lua')
        def indexed_keys(p):
            ts = tokens(read(p)); result = set()
            for i in range(len(ts) - 3):
                if ts[i][1] == '[' and ts[i+1][0] == 'str' and ts[i+2][1] == ']' and ts[i+3][1] == '=':
                    result.add(ts[i+1][1])
                elif ts[i][0] == 'id' and ts[i+1][1] == '=': result.add(ts[i][1])
            return result
        sound_keys = indexed_keys('kr3-desktop/assets/sounds/sounds.lua')
        group_keys = indexed_keys('kr3-desktop/assets/sounds/groups.lua')
        if 'kr3-desktop/assets/sounds/extra.lua' in names:
            extra_keys = indexed_keys('kr3-desktop/assets/sounds/extra.lua')
            sound_keys |= extra_keys; group_keys |= extra_keys
        report['ending_contracts'] = []
        for screen, level in (('kr1_end', 56), ('kr1_fa', 75)):
            source = 'kr3/screen_' + screen + '.lua'; text = read(source)
            groups_body = re.search(r'screen\.required_sounds\s*=\s*\{([^}]+)\}', text)
            groups = re.findall(r'["\x27]([^"\x27]+)["\x27]', groups_body.group(1)) if groups_body else []
            sound_ids = re.findall(r'S:queue\(["\x27]([^"\x27]+)["\x27]', text)
            for name in groups:
                if name not in group_keys: report['errors'].append({'kind': 'ending-sound-group-missing', 'source': source, 'name': name})
            for name in sound_ids:
                if name not in sound_keys: report['errors'].append({'kind': 'ending-sound-id-missing', 'source': source, 'name': name})
            return_pattern = r'self\.done_callback\(\{\s*level_idx\s*=\s*' + str(level) + r'\s*,\s*next_item_name\s*=\s*"map"'
            if not re.search(return_pattern, text): report['errors'].append({'kind': 'ending-return-level-mismatch', 'source': source, 'expected_level': level})
            report['ending_contracts'].append({'screen': screen, 'trigger_level': level, 'groups': groups, 'sound_ids': sound_ids, 'returns_to': 'map'})
        bootstrap = json.loads((ROOT / 'ThirdParty/KingdomRushContent/bootstrap-resources.json').read_text())
        for row in bootstrap['files']:
            check(row['path'], 'reviewed direct bootstrap resource')
            if row['path'] in names and hashlib.sha256(z.read(row['path'])).hexdigest() != row['sha256']:
                report['errors'].append({'kind': 'bootstrap-hash-mismatch', 'path': row['path']})
        fonts = sorted(p for p in names if p.startswith('all-desktop/assets/fonts/') and p.endswith(('.ttf', '.otf')))
        for p in fonts: check(p, 'font_db enumerates the packaged font directory')
        if len(fonts) != 7: report['errors'].append({'kind': 'unexpected-font-domain', 'files': fonts})
        for p in ('all-desktop/assets/fonts/HarmonyOS_Sans_SC.ttf', 'all-desktop/assets/fonts/HarmonyOS_Sans_SC_Bold.ttf'):
            check(p, 'Chinese title/error font and fallback')
        report['bitmap_font_legacy_getData_branch_reachable'] = any(p.startswith('all-desktop/assets/fonts/') and p.lower().endswith('.png') for p in names)
        queue = deque(seeds); seen = set()
        while queue:
            p = queue.popleft()
            if p in seen or p not in names: continue
            seen.add(p); text = read(p)
            report['sources'].append({'path': p, 'sha256': hashlib.sha256(z.read(p)).hexdigest()})
            for call in calls(text):
                row = {'source': p, **call}; name = call['literal']; api = call['api']
                if api in ('require', '_require'):
                    if isinstance(name, str):
                        resolved = resolve(name)
                        if resolved: row.update(status='module-present', resolved=resolved); queue.append(resolved)
                        elif name in BUILTINS or name.startswith('love.'):
                            row.update(status='runtime-module', evidence='LuaJIT/LÖVE native module; not a ZIP file')
                        elif name in NATIVE_MODULES: row.update(status='runtime-module', evidence=NATIVE_MODULES[name])
                        else:
                            row['status'] = 'unresolved-module-or-optional-branch'; report['unresolved'].append(row)
                            if p in ('kr3/screen_kr1_end.lua', 'kr3/screen_kr1_fa.lua'):
                                report['errors'].append({'kind': 'missing-reachable-ending-module', **row})
                    else:
                        row['status'] = 'unresolved-dynamic-module'; report['unresolved'].append(row)
                elif isinstance(name, str):
                    if name in names: row.update(status='file-present', resolved=name)
                    elif name.startswith('/') or 'log' in name.lower() or name.startswith(('settings', 'slot', 'remote_config')):
                        row['status'] = 'external-or-generated-state'; report['known_external_or_optional'].append(row)
                    else:
                        row['status'] = 'unresolved-literal-or-optional-resource'; report['unresolved'].append(row)
                elif re.fullmatch(r'\d+(?:\.\d+)?', call['expression']): row['status'] = 'generated-object-no-file'
                else:
                    row['status'] = 'unresolved-dynamic-resource-or-object'; report['unresolved'].append(row)
                report['calls'].append(row)
        report['sources'].sort(key=lambda row: row['path'])
        classify_profile(report, z, check, resolve, read, template_review)
        # Proven direct-load compatibility facts; runtime shader/device success is separate.
        main = read('main.lua')
        for api in ('isFile', 'isDirectory', 'exists'):
            ok = 'function love.filesystem.' + api in main and 'love.filesystem.getInfo' in main
            report['compatibility'].append({'kind': 'legacy-filesystem-' + api, 'source_adapter_present': ok})
            if not ok: report['errors'].append({'kind': 'missing-filesystem-adapter', 'api': api})
        for p in ('lib/klove/image_db.lua', 'all/sound_db.lua'):
            worker = read(p)
            ok = 'KRFLAPK_THREAD_FILESYSTEM_COMPAT' in worker and 'love.filesystem.getInfo' in worker
            report['compatibility'].append({'kind': 'thread-filesystem-adapter', 'source': p, 'present': ok})
            if not ok: report['errors'].append({'kind': 'missing-thread-filesystem-adapter', 'source': p})
        report['compatibility'].append({'kind': 'error-handler-precedence', 'source_defines_legacy_errhand': 'function love.errhand(' in main,
            'source_defines_errorhandler': bool(re.search(r'love\.errorhandler\s*=|function love\.errorhandler\(', main)),
            'limitation': 'Pinned native LÖVE12 boot prefers love.errorhandler; a legacy love.errhand alone does not own fatal diagnostics.'})
        report['compatibility'].append({'kind': 'shader-compilation', 'status': 'runtime-unverified',
            'limitation': 'Packaged shader source existence does not prove compilation on the device GLES driver.'})
    report['status'] = 'FAILED' if report['errors'] else ('INCOMPLETE' if report['unresolved'] else 'FINITE_CHECKS_PASSED')
    report['summary'] = {k: len(report[k]) for k in ('sources', 'calls', 'finite_files', 'errors', 'unresolved',
                                                  'classified_dynamic_calls', 'known_external_or_optional')}
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--content', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--template-review', type=Path, help='Independent evaluated-closure-review.json bound to this exact content SHA; paired evaluated-template-data.json is read beside it.')
    args = parser.parse_args()
    result = audit(args.content, args.template_review)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({k: result[k] for k in ('content_sha256', 'status', 'summary')}, ensure_ascii=False, indent=2))
    raise SystemExit(1 if result['errors'] else 2 if result['unresolved'] else 0)
