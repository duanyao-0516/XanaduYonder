#!/usr/bin/env python3
"""Verify generated content integrity, local resource closure and offline contract."""
import argparse
import hashlib
import json
import posixpath
import re
import subprocess
from pathlib import Path
from content_contract import inspect_runtime, validate_classes, prefab_assets

ROOT=Path(__file__).resolve().parents[2]

def require(condition,message):
    if not condition:raise ValueError(message)

def strings(value):
    if isinstance(value,str):yield value
    elif isinstance(value,dict):
        for v in value.values():yield from strings(v)
    elif isinstance(value,list):
        for v in value:yield from strings(v)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=ROOT)
    parser.add_argument('--content',type=Path)
    args=parser.parse_args();root=args.root.resolve();content=args.content or root/'Assets/StreamingAssets/fruit-slice'
    manifest=json.loads((content/'content-manifest.json').read_text())
    require(manifest['source_verified_files']==418,'Unexpected source file count')
    expected=set(manifest['files'])|{'content-manifest.json'}
    actual={str(p.relative_to(content)) for p in content.rglob('*') if p.is_file() and p.suffix!='.meta'}
    require(actual==expected,'File set mismatch: '+str(actual^expected))
    for name,digest in manifest['files'].items():require(hashlib.sha256((content/name).read_bytes()).hexdigest()==digest,'Content hash mismatch: '+name)
    for name,digest in manifest['adapter_files'].items():require(hashlib.sha256((root/name).read_bytes()).hexdigest()==digest,'Rebuild content after editing '+name)
    config=json.loads((content/'fileconfig.json').read_text())
    frames={prefix+frame:atlas for atlas,(prefix,items) in config.items() for frame in items}
    for atlas,(prefix,items) in config.items():
        atlas_data=json.loads((content/atlas).read_text())
        require(set(items).issubset(atlas_data['frames']), 'Atlas frame mismatch: '+atlas)
        for image in atlas_data['meta']['image'].split(','):
            require((content/Path(atlas).parent/image).is_file(),'Atlas image missing: '+image)
    references=0
    for name in manifest['files']:
        if name != 'fileconfig.json' and name.endswith(('.json','.atlas','.lh','.ls','.lmat')) and not name.endswith('.ltcb.ls'):
            obj=json.loads((content/name).read_bytes())
            if name.startswith('scene/'):
                def node_ids(node):
                    result={node['compId']} if 'compId' in node else set()
                    for child in node.get('child',[]):result.update(node_ids(child))
                    return result
                ids=node_ids(obj)
                for animation in obj.get('animations',[]):
                    for node in animation.get('nodes',[]):require(node['target'] in ids,'Animation target removed: '+name+' -> '+str(node['target']))
            for ref in strings(obj):
                if not re.search(r'\.(?:png|jpg|jpeg|atlas|lh|ls|lmat|lm|lani|json)$',ref,re.I):continue
                resolved=ref if ref in expected or ref in frames else posixpath.normpath(str(Path(name).parent/ref))
                require(resolved in expected or resolved in frames,'Unresolved local resource: '+name+' -> '+ref)
                references+=1
    bundle=(content/'js/bundle.js').read_text();host=(content/'host.js').read_text();entry=(content/'index.html').read_text()
    runtime=inspect_runtime(bundle)
    class_refs=validate_classes({name:json.loads((content/name).read_bytes()) for name in expected if name.startswith('scene/') and name.endswith('.json')},runtime,[(content/name).read_text() for name in expected if name.startswith('libs/') and name.endswith('.js')])
    require(runtime['registrations']==manifest['class_registrations'] and len(class_refs)==manifest['class_references'],'Class registry manifest mismatch')
    for ref in prefab_assets(runtime):require(ref in expected or ref in frames,'Inline prefab resource missing: '+ref)
    for value in strings(runtime['views']):require(not any(token in value for token in ['现金','钻石','金币','免费获得','后获得','ui2/Gem.png','ui2/zuanshi.png']),'Removed inline economy UI remains: '+value)
    require(not re.search(r'tdSDK|VideoAD|h5api|HttpRequest|XMLHttpRequest|https?://|window\.(?:wx|qg|tt|qq|android)|C\.platform|Recorder|yinsiUI',bundle),'Platform/network code remains')
    economy_tokens=['zuanshi','jinbi','playerHave','daojuTime','addZuanshi','rewardPaid','rewardMultiplier','doublBtn','clearConfirmation','initCountTime','resetCountTime','daojuTuisong','没有足够的现金','金币不足','钻石不足']
    for token in economy_tokens:require(token not in bundle,'Removed economy behavior remains: '+token)
    for name in expected:
        if name.startswith('scene/') and name.endswith('.json'):
            scene=json.loads((content/name).read_text())
            for value in strings(scene):
                require(not any(token in value for token in ['zuanshi','daojuBox','doublBtn','ui2/Gem.png','ui2/shipin.png','免费获得','确认获得','后获得']),'Removed economy UI remains: '+name+' -> '+value)
    model_names=set(strings(json.loads((content/'map/Conventional/mold.lh').read_text())))
    for blade in range(1,28):
        require('Trail'+str(blade) in model_names,'Missing usable blade model: '+str(blade))
        require('ui2/shop/icon'+str(blade)+'.png' in frames,'Missing usable blade icon: '+str(blade))
    require('O.scaleMode="showall"' in bundle,'Portrait fit missing')
    require('(new Date).getTime()' not in bundle,'Game wall clock bypasses pause')
    require('multiTouchEnabled = false' in host,'Single touch owner missing')
    require('connect-src \'self\'' in entry and 'frame-src \'none\'' in entry,'CSP missing')
    require(not re.search(r'<script[^>]+src=["\'](?:https?:)?//',entry),'Remote script')
    require(not any(p.startswith('ADRes/') or 'ADRes.atlas' in p for p in expected),'Ad resources packaged')
    for i in range(1,51):require((content/('json/guan'+str(i)+'.json')).is_file(),'Missing level')
    for name in ['js/bundle.js','host.js']:subprocess.run(['node','--check',str(content/name)],check=True,capture_output=True)
    print(json.dumps({'status':'PASS','files':len(expected),'resourceReferences':references,'classReferences':len(class_refs),'registeredGameClasses':len(runtime['registrations']),'levels':50,'bytes':sum((content/name).stat().st_size for name in expected)},ensure_ascii=False))

if __name__=='__main__':main()
