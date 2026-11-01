#!/usr/bin/env python3
"""Reproducible, hash-pinned offline adaptation of the downloaded Laya release."""
import argparse
import hashlib
import json
import posixpath
import re
import shutil
from pathlib import Path
from content_contract import inspect_runtime, validate_classes, prefab_assets

ROOT = Path(__file__).resolve().parents[2]
SOURCE_MANIFEST_SHA = '5ae7155fa1affc9c0cc12fc9b6ee8368cea3fb8d9fb897ebefaa619ba824d70e'
SOURCE_SHA = '5b525fe5e584c02bce85e18b1369076aa0ed713ff8218c5ce1f6c9a4506e34c0'


def balanced_end(text, start):
    depth, quote, escape = 0, None, False
    for i in range(start, len(text)):
        ch = text[i]
        if quote:
            if escape: escape = False
            elif ch == '\\': escape = True
            elif ch == quote: quote = None
        elif ch in "\"'`": quote = ch
        elif ch == '{': depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0: return i + 1
    raise ValueError('Unbalanced source')


def replace_method(chunk, name, body, remove=False):
    match = re.search(r'(?<![\w.])(?:static )?' + re.escape(name) + r'\([^)]*\)\{', chunk)
    if not match: raise ValueError('Missing method: ' + name)
    end = balanced_end(chunk, match.end()-1)
    replacement = '' if remove else chunk[match.start():match.end()] + '\n' + body + '\n}'
    return chunk[:match.start()] + replacement + chunk[end:]


def exact(text, old, new, count=1):
    if text.count(old) != count: raise ValueError('Source drift: ' + old[:100] + ' count=' + str(text.count(old)))
    return text.replace(old, new)


def transform(source, owned):
    parts = re.split(r'(?=class [A-Za-z_$][\w$]*(?:\{| extends ))', source)
    chunks = {}
    order = []
    for part in parts[1:]:
        name = re.match(r'class (\w+)', part)[1]
        chunks[name] = part
        order.append(name)
    # Entire platform SDK implementations, their dispatcher, ad views and platform privacy view.
    removed = {'i','s','d','g','p','m','y','f','w','I','S','L','b','A','C'}
    chunks['e'] = replace_method(chunks['e'], 'GetData', 'let raw=null; try { raw=Laya.LocalStorage.getItem(e.dataName); } catch (_) {} e.user=window.XanaduFruitContent.readSave(raw,e.userDV);')
    chunks['e'] = replace_method(chunks['e'], 'SaveData', 'try { window.localStorage.setItem(e.dataName,JSON.stringify(e.user)); } catch (error) { console.warn("进度保存失败",error); D.ShowGameTipUI("进度无法保存，请检查可用存储空间"); }')
    chunks['l'] = exact(chunks['l'], 'text:"没有足够的现金 "', 'text:""')
    chunks['D'] = replace_method(chunks['D'], 'yinsiUI', '', remove=True)
    chunks['D'] = replace_method(chunks['D'], 'GoldFormat', '', remove=True)
    for field in ['playerHave','daojuTime','keyNum','jinbi','zuanshi']:
        pattern=r'e\.userDV\.'+field+r'=(?:\[[^\]]*\]|[^,;]*),'
        matches=re.findall(pattern,chunks['e'])
        if len(matches)!=1:raise ValueError('Unexpected economy save declaration: '+field)
        chunks['e']=chunks['e'].replace(matches[0],'')
    for method in ['PlayMusic','stopMusic','ChangeMusicVolume','PlayVibrate','initCountTime','resetCountTime']:
        chunks['N'] = replace_method(chunks['N'], method, '', remove=True)
    chunks['N'] = exact(chunks['N'], 'N.countTime=null,N.preTime=0,', '')
    chunks['N'] = exact(chunks['N'], 'N.doubleGold=!1,', '')
    chunks['N'] = exact(chunks['N'], 'N.daojuTuisong=[17,19,20,21,22,23,24],', '')
    for method in ['setdaojuTime','updataTime']:
        chunks['R'] = replace_method(chunks['R'], method, '', remove=True)
    chunks['B'] = exact(chunks['B'], 'e.GetData(),C.init(),', 'e.GetData(),')
    chunks['B'] = exact(chunks['B'], ',"json/paihang1.json","json/paihang2.json"', '')
    chunks['B'] = replace_method(chunks['B'], 'loadMap', 'this.loadValue=1; D.ShowUIPanel("mainScene");')
    chunks['j'] = exact(chunks['j'], 'C.Recorder(1,function(){}),', '')
    chunks['j'] = exact(chunks['j'], 'C.CloseBannerAD()', '')
    chunks['j'] = exact(chunks['j'], ',N.PlayVibrate()', '')
    chunks['j'] = exact(chunks['j'], 'var e=Laya.stage.mouseX*Laya.stage.clientScaleX,t=Laya.stage.mouseY*Laya.stage.clientScaleY;', 'var e=this.inputX,t=this.inputY;')
    chunks['j'] = replace_method(chunks['j'], 'onMouseUp', 'if(e.touchId===this.curTouchId)this.cancelInput();')
    # Clone cached level arrays: banana insertion must not mutate future rounds.
    chunks['j'] = exact(chunks['j'], 'this.guanka_script=Laya.Loader.getRes(t)', 'this.guanka_script=Laya.Loader.getRes(t).slice()')
    chunks['j'] = exact(chunks['j'], 'this.guanka_script=Laya.Loader.getRes(i)', 'this.guanka_script=Laya.Loader.getRes(i).slice()', 2)
    chunks['P'] = exact(chunks['P'], ',"vivo"==C.platform||"android"==C.platform?N.control&&(C.InsertAD(),C.BannerAD()):C.InsertAD()', '')
    chunks['M'] = exact(chunks['M'], 'this.paihang1(),', '')
    chunks['M'] = exact(chunks['M'], 'this.paihang2(),', '')
    for name in ['paihang1','paihang2','addZuanshi']:
        chunks['M'] = replace_method(chunks['M'], name, '', remove=True)
    chunks['M'] = replace_method(chunks['M'], 'onDisable', 'Laya.timer.clearAll(this); if(N.curScene)N.curScene.removeSelf();')
    chunks['U'] = replace_method(chunks['U'], 'onDisable', '', remove=True)
    for path in sorted((owned/'methods').glob('*.js')):
        cls, method = path.stem.split('.')
        if method in ['renderItems','cancelInput']:
            end = balanced_end(chunks[cls], chunks[cls].index('{'))
            chunks[cls] = chunks[cls][:end-1] + method+'(){\n'+path.read_text()+'\n}' + chunks[cls][end-1:]
        else:
            chunks[cls] = replace_method(chunks[cls], method, path.read_text())
    # Preserve a constant logical stage; letterbox within the native safe viewport.
    chunks['O'] = exact(chunks['O'], 'O.scaleMode="fixedheight"', 'O.scaleMode="showall"')
    chunks['O'] = exact(chunks['O'], 'Laya.alertGlobalError(!0)', 'window.XanaduFruitContent.bind(Laya,function(){if(N.gameUI)N.gameUI.cancelInput();})')
    chunks['O'] = exact(chunks['O'], '(O.debug||"true"==Laya.Utils.getQueryString("debug"))&&Laya.enableDebugPanel(),', '')
    out = parts[0] + ''.join(chunks[name] for name in order if name not in removed)
    out = out.replace('(new Date).getTime()', '(window.XanaduFruitContent.now())')
    for name in removed:
        if re.search(r'\bnew '+re.escape(name)+r'(?=\W)',out):raise ValueError('Surviving constructor reference to removed class: '+name)
    if re.search(r'\bC\.(?:platform|[A-Z]|init|login|tongji)|tdSDK|VideoAD|h5api|https?://|XMLHttpRequest|fetch\(', out):
        raise ValueError('Platform/network code remains in game bundle')
    return out


def clean_scene(data):
    # Reviewed exact source nodes; never delete a similarly named gameplay widget globally.
    economy_nodes={'script/mainScene.js':{16,22},'script/shopScene.js':{20,27},'script/gameOver.js':{21,28,37,125,130}}
    removed_economy=set()
    forbidden_vars = {'moreBtn','yinsiBtn','telBtn','shareBtn','list','list2','chaoyue','chaoyue2'}
    def walk(node, root_runtime):
        props=node.get('props',{})
        if node.get('compId') in economy_nodes.get(root_runtime,set()):
            removed_economy.add(node['compId'])
            return None
        if root_runtime=='script/shopScene.js' and props.get('name')=='wenzi':props['text']='使用'
        if root_runtime=='script/shopScene.js' and props.get('name')=='huodeiBtn':props['skin']='ui2/ButtonYellow.png'
        if root_runtime=='script/gameOver.js' and props.get('var')=='nextBtn':
            props.pop('x',None)
            props['centerX']=0
        var=props.get('var')
        if var in forbidden_vars and (var not in {'list','list2'} or root_runtime == 'script/gameOver.js'): return None
        if props.get('texture') in {'ui2/shipin.png','ui2/phb.png','ui2/tips5.png','ui2/8age.png'}: return None
        if props.get('text','').startswith(('健康游戏忠告','抵制不良游戏')): return None
        if props.get('skin') in {'ui2/phb.png','ui2/tips5.png'} or props.get('text') == '(前50名)': return None
        if 'child' in node: node['child']=[c for ch in node['child'] if (c:=walk(ch,root_runtime)) is not None]
        if var in {'fuhuoBtn','fuhuo2Btn','fuhuo3Btn'}:
            node['child'] = [{'type':'Label','props':{'text':'复活','fontSize':28,'color':'#ffffff','bold':True,'centerX':0,'centerY':0,'mouseEnabled':False}}]
        return node
    runtime=data.get('props',{}).get('runtime')
    data=walk(data,runtime)
    if removed_economy!=economy_nodes.get(runtime,set()):raise ValueError('Economy scene-node contract changed: '+str(runtime))
    if runtime=='script/gameOver.js':
        # Animation nodes still reference stars and result panels only; removed platform widgets are not animated.
        pass
    refs=[]
    def gather(obj):
        if isinstance(obj,dict):
            for key,value in obj.items():
                if key in {'skin','texture','vScrollBarSkin'} and isinstance(value,str) and value: refs.append(value)
                else:gather(value)
        elif isinstance(obj,list):
            for value in obj:gather(value)
    gather(data)
    if 'loadList' in data:data['loadList']=sorted(set(refs))
    return data


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=ROOT)
    parser.add_argument('--output',type=Path)
    args=parser.parse_args(); root=args.root.resolve()
    evidence=root/'ThirdParty/FruitSliceSource'; original=evidence/'original'; owned=root/'ThirdParty/FruitSliceContent'
    target=args.output or root/'Assets/StreamingAssets/fruit-slice'
    manifest_bytes=(evidence/'download-manifest.json').read_bytes()
    if hashlib.sha256(manifest_bytes).hexdigest()!=SOURCE_MANIFEST_SHA:raise ValueError('Unrecognized source manifest')
    manifest=json.loads(manifest_bytes)
    source_files={}
    for entry in manifest['resources']:
        if not entry['path'].startswith('original/') or entry.get('http_status') != 200:continue
        path=evidence/entry['path']; content=path.read_bytes()
        if len(content)!=entry['size_bytes'] or hashlib.sha256(content).hexdigest()!=entry['sha256']:raise ValueError('Source hash mismatch: '+str(path))
        source_files[entry['path'][9:]]=content
    if len(source_files)!=418:raise ValueError('Expected 418 original resources')
    if hashlib.sha256(source_files['js/bundle.js']).hexdigest()!=SOURCE_SHA:raise ValueError('Unrecognized source release')
    bundle=transform(source_files['js/bundle.js'].decode(),owned)
    runtime=inspect_runtime(bundle)
    source_prefabs=set(re.findall(r'\bn\("(ui\.[^"]+)"',source_files['js/bundle.js'].decode()))
    removed_prefabs=source_prefabs-set(runtime['registrations'])
    if removed_prefabs != {'ui.AD.NativeBannerUI','ui.AD.NativeInsertUI','ui.Prefab.yinsizhengceUI','ui.Prefab.handUI'}:raise ValueError('Unexpected removal of registered game prefab: '+str(removed_prefabs))
    # handUI is an unused treasure-box helper: only its original registration references it.
    # Reject retained named references as well as scene/inline runtime references below.
    for name in removed_prefabs:
        if name in bundle:raise ValueError('Reference to removed registered prefab: '+name)
    output={}
    # Runtime dependency closure. Atlas frames are virtual paths resolved through fileconfig.
    config=json.loads(source_files['fileconfig.json'])
    config={k:v for k,v in config.items() if k not in {'res/atlas/ADRes.atlas','res/atlas/baoxiang.atlas','res/atlas/baoxiang/egg.atlas','res/atlas/touxiang.atlas'}}
    frame_to_atlas={prefix+frame:atlas for atlas,(prefix,frames) in config.items() for frame in frames}
    queue=['libs/min/laya.core.min.js','libs/min/laya.ui.min.js','libs/min/laya.d3.min.js','libs/min/laya.physics.min.js','libs/min/laya.physics3D.min.js','map/Conventional/mold.lh','map/Conventional/gameScene.ls']
    queue += ['scene/'+name+'.json' for name in ['firstScene','mainScene','gameScene','gameOver','selectMap','shopScene']]
    queue += ['json/guan'+str(i)+'.json' for i in range(1,51)] + ['json/banana1.json','json/banana2.json']
    # Sound names are composed from counters in the original game. All downloaded sounds are used by gameplay except unused music.
    queue += sorted(p for p in source_files if p.startswith('res/Sounds/') and p.endswith('.mp3'))
    queue += list(config)
    queue += prefab_assets(runtime)
    queue += re.findall(r'"((?:ui2|loading|res)/[^"\n]*\.(?:png|jpg|atlas))"', bundle)
    visited=set()
    def strings(value):
        if isinstance(value,str):yield value
        elif isinstance(value,dict):
            for v in value.values():yield from strings(v)
        elif isinstance(value,list):
            for v in value:yield from strings(v)
    while queue:
        name=queue.pop(0)
        if name in frame_to_atlas: name=frame_to_atlas[name]
        if name in visited:continue
        visited.add(name)
        if name not in source_files:raise ValueError('Missing local dependency: '+name)
        raw=source_files[name]
        if name.endswith(('.json','.atlas','.lh','.ls','.lmat')) and not name.endswith('.ltcb.ls'):
            obj=json.loads(raw)
            if name.startswith('scene/'):obj=clean_scene(obj)
            raw=(json.dumps(obj,ensure_ascii=False,separators=(',',':'))+'\n').encode()
            for ref in strings(obj):
                if not re.search(r'\.(?:png|jpg|jpeg|atlas|lh|ls|lmat|lm|lani|json)$',ref,re.I):continue
                candidate=ref if ref in source_files or ref in frame_to_atlas else posixpath.normpath(str(Path(name).parent/ref))
                # Atlas frame keys are metadata; image references in meta.image are real dependencies.
                if candidate in source_files or candidate in frame_to_atlas:queue.append(candidate)
                elif name.endswith('.atlas') and '/' not in ref:continue
                else:raise ValueError('Unresolved '+name+' -> '+ref)
        output[name]=raw
    output['fileconfig.json']=(json.dumps(config,ensure_ascii=False,separators=(',',':'))+'\n').encode()
    output['version.json']=b'{}\n'
    output['js/bundle.js']=bundle.encode()
    output['index.html']=(owned/'index.html').read_bytes()
    output['host.js']=(owned/'host.js').read_bytes()
    class_refs=validate_classes({name:json.loads(raw) for name,raw in output.items() if name.startswith('scene/') and name.endswith('.json')},runtime,[raw.decode() for name,raw in output.items() if name.startswith('libs/') and name.endswith('.js')])
    build={'schema':1,'class_registrations':runtime['registrations'],'class_references':len(class_refs),'source_game_id':235576,'source_bundle_sha256':SOURCE_SHA,'source_verified_files':len(source_files),'adapter_files':{str(path.relative_to(root)):hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(list(owned.rglob('*.js'))+list(owned.glob('*.html'))+[root/'scripts/fruit-slice'/name for name in ['build-content.py','content_contract.py','inspect-runtime.cjs']])},'files':{name:hashlib.sha256(raw).hexdigest() for name,raw in sorted(output.items())}}
    output['content-manifest.json']=(json.dumps(build,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode()
    if target.exists():shutil.rmtree(target)
    for name,raw in sorted(output.items()):
        path=target/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print(json.dumps({'output':str(target),'files':len(output),'bytes':sum(map(len,output.values())),'sourceVerified':len(source_files)},ensure_ascii=False))

if __name__=='__main__':main()
