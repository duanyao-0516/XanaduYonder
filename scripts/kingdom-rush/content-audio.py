"""Prune review-proven foreign sounds from generated literal indexes.
The reader accepts only data literals, never executes source or game code.
"""
import json,re

def read_literal(text):
    token=re.compile(r'\s+|--\[(?P<eq>=*)\[[\s\S]*?\](?P=eq)\]|--[^\n]*|"(?:\\.|[^"\\])*"|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|[A-Za-z_]\w*|[{}\[\]=,;]')
    tokens=[];position=0
    while position<len(text):
        match=token.match(text,position)
        if not match:raise ValueError('Unsupported generated data syntax at '+repr(text[position:position+40]))
        word=match.group();position=match.end()
        if not word.isspace() and not word.startswith('--'):tokens.append(word)
    index=0
    def take(expected=None):
        nonlocal index
        word=tokens[index];index+=1
        if expected is not None and word!=expected:raise ValueError('Expected '+expected+', got '+word)
        return word
    def value():
        word=take()
        if word=='{':
            result={};sequence=1
            while tokens[index]!='}':
                if tokens[index]=='[':
                    take('[');key=value();take(']');take('=');result[key]=value()
                elif index+1<len(tokens) and tokens[index+1]=='=':
                    key=take();take('=');result[key]=value()
                else:result[sequence]=value();sequence+=1
                if tokens[index] in (',',';'):take()
                elif tokens[index]!='}':raise ValueError('Missing literal table separator')
            take('}');return result
        if word.startswith('"'):return json.loads(word)
        if word=='true':return True
        if word=='false':return False
        if word=='nil':return None
        return float(word) if any(c in word for c in '.eE') else int(word)
    take('return');result=value()
    if index!=len(tokens):raise ValueError('Trailing executable text in generated index')
    return result

def file_refs(value):
    if isinstance(value,dict):
        return {item for child in value.values() for item in file_refs(child)}
    return {value} if isinstance(value,str) and value.endswith(('.ogg','.wav','.mp3')) else set()

def prune(root,dependencies,lua_writer,direct_files=()):
    excluded_ids=set(dependencies.get('excluded_sound_ids',[]))
    excluded_groups=set(dependencies.get('excluded_sound_groups',[]))
    tables={name:read_literal((root/name).read_text()) for name in ['sounds.lua','groups.lua','extra.lua']}
    sounds=tables['sounds.lua'];groups=tables['groups.lua']
    unknown=excluded_ids-set(sounds)
    if unknown:raise ValueError('Reviewed sound exclusions not present: '+repr(sorted(unknown)))
    excluded_files=set()
    for name in sorted(excluded_ids):excluded_files.update(file_refs(sounds.pop(name)))
    for name in excluded_groups:groups.pop(name,None)
    # Shared files (e.g. the genuine first-generation level-up and Malik voices)
    # survive whenever any retained sound ID still refers to them.
    excluded_files-=file_refs(sounds)
    for group in groups.values():
        for field,excluded in [('sounds',excluded_ids),('files',excluded_files)]:
            if field in group:
                values=[v for _,v in sorted(group[field].items()) if v not in excluded]
                group[field]={i+1:v for i,v in enumerate(values)}
    for name,data in tables.items():(root/name).write_text('-- Reviewed first-generation sound data; no game code executed.\nreturn '+lua_writer(data)+'\n')
    # The original splash creates logo_shimmer.ogg directly with newSource;
    # bootstrap/API call-site dependencies are independent of sound index IDs.
    retained=file_refs(tables)|set(direct_files);removed=[]
    for path in sorted((root/'files').rglob('*')):
        if path.is_file() and path.relative_to(root/'files').as_posix() not in retained:
            removed.append(path.relative_to(root/'files').as_posix());path.unlink()
    return {'excluded_sound_ids':sorted(excluded_ids),'excluded_sound_groups':sorted(excluded_groups),'evidence':dependencies.get('excluded_sound_evidence'),'direct_file_dependencies':sorted(direct_files),'removed_files':removed,'remaining_files':len(retained),'remaining_sound_ids':len(sounds)}
