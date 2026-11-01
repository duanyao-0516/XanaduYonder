"""Validate Laya class-name dependencies, including JSON runtime strings and inline prefabs."""
import json
import re
import subprocess
from pathlib import Path


def inspect_runtime(bundle):
    result = subprocess.run(['node', str(Path(__file__).with_name('inspect-runtime.cjs')), '-'],
                            input=bundle, text=True, capture_output=True, check=True)
    return json.loads(result.stdout)


def validate_classes(scenes, runtime, libraries):
    registered = set(runtime['registrations'])
    # Match the actual ClassUtils.getClass fallback: registered name, Laya.<name>, then export.
    for library in libraries:
        names = re.findall(r'\.regClass\("([^"]+)"', library)
        registered.update(names)
        registered.update(name[5:] for name in names if name.startswith('Laya.'))
        registered.update(re.findall(r'(?:^|[,{])(\w+):\["draw\w+"', library))
    registered.add('Graphic')  # ClassUtils._addGraphicsToSprite special case.
    references = []

    def visit(value, location):
        if isinstance(value, dict):
            if isinstance(value.get('type'), str) and isinstance(value.get('props'), dict):
                name = value['props'].get('runtime') or value['type']
                if name == 'UIView' and value['props'].get('pageData'):
                    pass  # SceneUtils.getCompInstance recursively creates pageData instead.
                elif name not in registered:
                    raise ValueError('Unregistered Laya class: ' + location + ' -> ' + name)
                references.append({'source': location, 'class': name})
            for key, child in value.items():
                visit(child, location + '.' + str(key))
        elif isinstance(value, list):
            for index, child in enumerate(value):
                visit(child, location + '[' + str(index) + ']')

    for name, scene in scenes.items():
        visit(scene, name)
    for name, view in runtime['views'].items():
        visit(view, 'bundle.uiView.' + name)
    return references


def prefab_assets(runtime):
    def visit(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key in {'skin', 'texture', 'vScrollBarSkin'} and isinstance(child, str) and child:
                    yield child
                elif key in {'loadList', 'loadList3D'}:
                    yield from child
                else:
                    yield from visit(child)
        elif isinstance(value, list):
            for child in value:
                yield from visit(child)
    return sorted(set(visit(runtime['views'])))
