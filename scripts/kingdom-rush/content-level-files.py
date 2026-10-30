"""Resolve literal mega-spawner files from selected level sources, without Lua execution."""
import re


def references(source, source_name):
    # Preserve quoted text while excluding both line and long Lua comments.
    tokens = re.compile(r'''"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|--\[(?P<eq>=*)\[[\s\S]*?\](?P=eq)\]|--[^\n]*''')
    source = tokens.sub(lambda m: '' if m.group().startswith('--') else m.group(), source)
    assignment = r'(?:\bload_file|\[\s*["\x27]load_file["\x27]\s*\])\s*=\s*'
    literal = re.compile(assignment + r'["\x27]([A-Za-z0-9_]+)["\x27]')
    matches = list(literal.finditer(source))
    if len(matches) != len(re.findall(assignment, source)):
        raise ValueError('Unresolved dynamic level load_file: ' + source_name)
    return [{'source': source_name, 'path': 'kr3/data/levels/' + m.group(1) + '.lua'} for m in matches]
