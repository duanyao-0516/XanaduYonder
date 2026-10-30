#!/usr/bin/env python3
"""Assemble the actual FL8.0.5 KR1-only LÖVE content, without launching the game.
Requires the user's immutable APK and the measured compression source directory.
Outputs an auditable game.love, per-file reasons/hashes and source patch report.
"""
import argparse, hashlib, importlib.util, io, json, re, shutil, struct, sys, zipfile
from pathlib import Path
from content_stable_baseline import validate_archive, validate_baseline_archive, menu_module

REPO = Path(__file__).resolve().parents[2]
PROFILE = REPO/'ThirdParty/KingdomRushContent/profile'
BOOTSTRAP_MANIFEST = REPO/'ThirdParty/KingdomRushContent/bootstrap-resources.json'
APK_SHA = 'ac4abdd13e162a13fad5d07234324b2e3953978b53c513b20c61a6b9de3bb0e1'

def load(path): return json.loads(Path(path).read_text())
def sha(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()

class SliceFile(io.RawIOBase):
    def __init__(self,path,offset,size):self.file=open(path,'rb');self.offset=offset;self.size=size;self.pos=0
    def readable(self):return True
    def seekable(self):return True
    def tell(self):return self.pos
    def seek(self,n,w=0):
        self.pos=n if w==0 else self.pos+n if w==1 else self.size+n
        if self.pos<0:raise ValueError('negative archive seek')
        return self.pos
    def read(self,n=-1):
        n=min(self.size-self.pos,n if n>=0 else self.size)
        if n<=0:return b''
        self.file.seek(self.offset+self.pos);data=self.file.read(n);self.pos+=len(data);return data
    def close(self):self.file.close();super().close()

def open_game(apk):
    with zipfile.ZipFile(apk) as z:
        entry=z.getinfo('assets/game.love')
        if entry.compress_type!=zipfile.ZIP_STORED:raise ValueError('Expected original stored nested game.love')
        with open(apk,'rb') as f:f.seek(entry.header_offset);header=f.read(30)
        n,e=struct.unpack('<HH',header[26:30])
        return zipfile.ZipFile(SliceFile(apk,entry.header_offset+30+n+e,entry.file_size))

def lua(v):
    if isinstance(v,dict):return '{'+','.join('['+lua(k)+']='+lua(x) for k,x in v.items())+'}'
    if isinstance(v,list):return '{'+','.join(lua(x) for x in v)+'}'
    if isinstance(v,str):return json.dumps(v,ensure_ascii=False)
    if isinstance(v,bool):return 'true' if v else 'false'
    if v is None:return 'nil'
    return str(v)

def filter_atlas(data, allowed_textures):
    """Keep complete original top-level records; authored geometry remains verbatim."""
    text=data.decode('utf-8-sig').replace('\r\n','\n')
    # Some genuine top-level records are indented with spaces or two tabs, and
    # others use quoted keys. Match brace depth instead of indentation.
    records=[];position=text.index('{')+1
    key=re.compile(r'(?:\["[^"\n]+"\]|\w+)\s*=\s*\{')
    lexical=re.compile(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|--\[\[.*?\]\]|--[^\n]*|[{}]',re.S)
    while position<len(text):
        while position<len(text) and text[position] in ' \t\r\n,':position+=1
        if text.startswith('--',position):
            comment=lexical.match(text,position)
            if not comment:raise ValueError('Unrecognized atlas comment')
            position=comment.end();continue
        if text[position]=='}':break
        match=key.match(text,position)
        if not match:raise ValueError('Unsupported top-level atlas record: '+repr(text[position:position+80]))
        depth=1;end=match.end()
        for token in lexical.finditer(text,end):
            if token.group()=='{':depth+=1
            elif token.group()=='}':depth-=1
            if depth==0:end=token.end();break
        if depth:raise ValueError('Unbalanced atlas record')
        record=text[position:end]
        atlas=re.search(r'(?:a_name|\["a_name"\])\s*=\s*"([^"]+)"',record)
        if atlas and atlas.group(1) in allowed_textures:records.append('\t'+record)
        position=end
    if not records:raise ValueError('Atlas filter retained no original records')
    return ('return {\n'+',\n'.join(records)+'\n}\n').encode()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-apk',type=Path,required=True)
    parser.add_argument('--compression-source',type=Path,required=True)
    parser.add_argument('--output',type=Path,default=REPO/'artifacts/kingdom-rush-1')
    parser.add_argument('--dependency-manifest',type=Path,default=REPO/'ThirdParty/KingdomRushContent/content-dependencies.json')
    parser.add_argument('--astcenc',type=Path)
    args=parser.parse_args(); source=args.compression_source.resolve(); out=args.output.resolve(); stage=out/'staging'
    if not args.dependency_manifest.is_file():raise ValueError('Required reviewed dependency manifest is missing')
    if sha(args.source_apk)!=APK_SHA:raise ValueError('Source APK does not match audited FL8.0.5 bytes')
    if stage.exists():shutil.rmtree(stage)
    stage.mkdir(parents=True);reasons={};patches={}; inputs=[]
    def put(path,data,reason):
        dest=stage/path;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
        reasons.setdefault(path,[]).append(reason)
    def merge_zip(path,reason):
        inputs.append({'path':str(path),'sha256':sha(path)})
        with zipfile.ZipFile(path) as z:
            if z.testzip():raise ValueError('Corrupt component '+str(path))
            for info in z.infolist():
                if not info.is_dir():put(info.filename,z.read(info),reason)
    merge_zip(source/'source/core-data-zh.zip','small shared code + 32 complete levels / 96 mode waves + Chinese strings')
    merge_zip(source/'source/selected-atlas-indexes.zip','authored atlas metadata retained for uniform texture scaling')
    # All 227 previously measured textures retain animation frames and authored UVs.
    for folder in ['textures/baseline','textures/lossless-actors','textures/75-large']:
        for path in sorted((source/folder).rglob('*.astc')):
            name=path.name
            put('kr3-desktop/assets/images/fullhd/'+name,path.read_bytes(),'measured first-generation atlas: '+folder)
    measured_textures=load(source/'textures/manifest.json')['files']
    evaluated_indexes=load(source/'textures/evaluated-indexes.json')
    texture_groups={}
    for group,frames in evaluated_indexes.items():
        for frame in frames.values():
            name=re.sub(r'\.(dds|png)$','.astc',frame['a_name'])
            texture_groups.setdefault(name,set()).add(group)
    for row in measured_textures:
        accepted={row['baseline_sha256']} | {item['sha256'] for item in row.get('outputs',{}).values()}
        if sha(stage/row['path']) not in accepted:raise ValueError('Measured texture hash mismatch: '+row['path'])
        reasons[row['path']].append('authored atlas groups: '+', '.join(sorted(texture_groups.get(Path(row['path']).name,[]))))
    inputs.append({'path':str(source/'textures/manifest.json'),'sha256':sha(source/'textures/manifest.json')})
    merge_zip(source/'nontexture/fonts-compact-alias.zip','Chinese fixed text font subsets; explicit foreign/font aliases resolved to Harmony')
    merge_zip(source/'nontexture/audio-firstgen-index-candidate.zip','selected level/hero/UI audio plus matching explicit sound index')
    with open_game(args.source_apk) as game:
        allnames=set(game.namelist())
        level_file_spec=importlib.util.spec_from_file_location('content_level_files',Path(__file__).with_name('content-level-files.py'))
        level_file_module=importlib.util.module_from_spec(level_file_spec);level_file_spec.loader.exec_module(level_file_module)
        level_file_profile=load(PROFILE/'level-file-dependencies.json')
        level_file_rows={row['path']:row for row in level_file_profile['files']}
        pending=sorted((stage/'kr3/data/levels').glob('*.lua'));scanned=set();level_references=[];included=set()
        while pending:
            file=pending.pop(0);name=file.relative_to(stage).as_posix()
            if name in scanned:continue
            scanned.add(name)
            for edge in level_file_module.references(file.read_text(encoding='utf-8-sig'),name):
                level_references.append(edge);path=edge['path']
                if path not in level_file_rows:raise ValueError('Unreviewed direct level-file dependency: '+path)
                data=game.read(path);row=level_file_rows[path]
                if len(data)!=row['bytes'] or hashlib.sha256(data).hexdigest()!=row['sha256']:raise ValueError('Direct level data differs from original reviewed bytes: '+path)
                put(path,data,'original direct mega_spawner load_file from '+name)
                included.add(path);pending.append(stage/path)
        if included!=set(level_file_rows):raise ValueError('Level-file allowlist is not exactly reachable from selected levels')
        (out/'level-file-dependencies.json').write_text(json.dumps({'references':level_references,'original_files':level_file_profile['files'],'scanned_level_sources':len(scanned)},ensure_ascii=False,indent=2))
        inputs.append({'path':str(PROFILE/'level-file-dependencies.json'),'sha256':sha(PROFILE/'level-file-dependencies.json')})
        bootstrap=load(BOOTSTRAP_MANIFEST)
        inputs.append({'path':str(BOOTSTRAP_MANIFEST),'sha256':sha(BOOTSTRAP_MANIFEST)})
        direct_sound_files=[]
        for row in bootstrap['files']:
            path=row['path']
            if path not in allnames:raise ValueError('Missing original bootstrap resource '+path)
            data=game.read(path)
            if len(data)!=row['bytes'] or hashlib.sha256(data).hexdigest()!=row['sha256']:raise ValueError('Original bootstrap resource differs from reviewed bytes: '+path)
            put(path,data,row['reason'])
            if path.startswith('kr3-desktop/assets/sounds/files/'):
                direct_sound_files.append(path.removeprefix('kr3-desktop/assets/sounds/files/'))
        (out/'bootstrap-resources.json').write_text(json.dumps(bootstrap,ensure_ascii=False,indent=2))
        # Resolve real filename typo; do not create a fake missing/empty sound.
        sounds_dir=stage/'kr3-desktop/assets/sounds'
        mapping=load(source/'nontexture/audio-path-map.json')['sound_files_relative_map']
        summary=load(source/'nontexture/firstgen-audio-index-summary.json')
        typo_fixes={}
        for missing in summary['missing_selected_filenames']:
            fixed=re.sub(r'(?<!\.)ogg$', '.ogg', missing)
            original='kr3-desktop/assets/sounds/files/'+fixed
            if original not in allnames:raise ValueError('Unresolved original sound '+missing)
            mapped=mapping.get(fixed)
            if mapped:
                data=(source/'nontexture/audio'/Path(mapped).name).read_bytes()
                dest='kr3-desktop/assets/sounds/files/'+mapped
            else:
                data=game.read(original);dest=original;mapped=fixed
            put(dest,data,'original sound-index filename repair: '+missing+' -> '+fixed)
            typo_fixes[missing]=mapped
        for path in sounds_dir.glob('*.lua'):
            text=path.read_text()
            for old,new in typo_fixes.items():text=text.replace('"'+old+'"','"'+new+'"')
            path.write_text(text)
        # Review-proven additional assets; never include all other generations as a fallback.
        if args.dependency_manifest and args.dependency_manifest.exists():
            deps=load(args.dependency_manifest)
            audio_spec=importlib.util.spec_from_file_location('content_audio',Path(__file__).with_name('content-audio.py'))
            audio_module=importlib.util.module_from_spec(audio_spec);audio_spec.loader.exec_module(audio_module)
            audio_report=audio_module.prune(sounds_dir,deps,lua,direct_sound_files)
            (out/'audio-pruning.json').write_text(json.dumps(audio_report,ensure_ascii=False,indent=2))
            for name in ['sounds.lua','groups.lua','extra.lua']:reasons['kr3-desktop/assets/sounds/'+name].append('reviewed exclusions and shared-file-preserving index pruning; see audio-pruning.json')
            texture_spec=importlib.util.spec_from_file_location('content_textures',Path(__file__).with_name('content-textures.py'))
            texture_module=importlib.util.module_from_spec(texture_spec);texture_spec.loader.exec_module(texture_module)
            transformations=[]
            for row in deps.get('files',[]):
                path=row['path']
                if path not in allnames:raise ValueError('Missing proven dependency '+path)
                if not (stage/path).exists():
                    data=game.read(path)
                    if row.get('literal_atlas_overlay'):
                        if hashlib.sha256(data).hexdigest()!=row['original_sha256']:raise ValueError('Expanded atlas original source drift: '+path)
                        overlay=PROFILE/row['literal_atlas_overlay']
                        data=overlay.read_bytes()
                        inputs.append({'path':str(overlay),'sha256':sha(overlay)})
                    if row.get('atlas_texture_filter'):
                        data=filter_atlas(data,row['atlas_texture_filter'])
                    if path.endswith('.astc'):
                        data,record=texture_module.transform(data,out/'additional-texture-cache',args.astcenc,preserve=row.get('preserve',False),background=row.get('background',False))
                        record['path']=path;transformations.append(record)
                    put(path,data,row['reason'])
            (out/'additional-textures.json').write_text(json.dumps(transformations,ensure_ascii=False,indent=2))
            (out/'dependency-input.json').write_text(json.dumps(deps,ensure_ascii=False,indent=2))
            dependency_profile={key:deps.get(key,[]) for key in ['game_texture_groups','map_texture_groups','exoskeletons','enemy_names']}
            dependency_profile['native_animation_overrides']=deps['native_animation_overrides']
            achievements=load(PROFILE/'achievements-allowlist.json')
            if hashlib.sha256(game.read('kr3/data/achievements_data.lua')).hexdigest()!=achievements['original_data_sha256']:raise ValueError('Achievement source data differs from reviewed original')
            dependency_profile['achievement_rows']=[{key:row.get(key) for key in ['original_index','name','icon','goal']} for row in achievements['rows']]
            if len({row['name'] for row in achievements['rows']})!=len(achievements['rows']):raise ValueError('Achievement allowlist contains duplicate names')
            inputs.append({'path':str(PROFILE/'achievements-allowlist.json'),'sha256':sha(PROFILE/'achievements-allowlist.json')})
            put('firstgen_dependencies.lua',('return '+lua(dependency_profile)+'\n').encode(),'reviewed bounded dependency profile')
        # Lua profile and source adaptations are persisted in the repository.
        spec=importlib.util.spec_from_file_location('content_patches',Path(__file__).with_name('content-patches.py'))
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        patches=module.patch_tree(stage,PROFILE/'firstgen.lua')
        branding_spec=importlib.util.spec_from_file_location('content_branding',Path(__file__).with_name('content-branding.py'))
        branding_module=importlib.util.module_from_spec(branding_spec);branding_spec.loader.exec_module(branding_module)
        branding_changes=branding_module.patch_tree(stage)
        for path,details in branding_changes.items():
            patches.setdefault(path,{})['branding']=details
        for merge in deps.get('atlas_record_merges',[]):
            atlas_base=stage/'kr3-desktop/assets/images/fullhd'
            target=atlas_base/(merge['target']+'.lua')
            target_table=audio_module.read_literal(target.read_text(encoding='utf-8-sig'))
            source_path='kr3-desktop/assets/images/fullhd/'+merge['source']+'.lua'
            source_data=(stage/source_path).read_bytes() if (stage/source_path).is_file() else game.read(source_path)
            source_table=audio_module.read_literal(source_data.decode('utf-8-sig'))
            for frame in merge['frames']:
                if frame not in source_table:raise ValueError('Missing original atlas merge record: '+frame)
                if frame in target_table and target_table[frame]!=source_table[frame]:
                    old=target_table[frame];new=source_table[frame]
                    alias_only=merge.get('allow_original_alias_extension') and {k:v for k,v in old.items() if k!='alias'}=={k:v for k,v in new.items() if k!='alias'} and set(old.get('alias',{}).values())<=set(new.get('alias',{}).values())
                    if not alias_only:raise ValueError('Conflicting atlas merge record: '+frame)
                target_table[frame]=source_table[frame]
            put(target.relative_to(stage).as_posix(),('return '+lua(target_table)+'\n').encode(),merge['reason'])
        masking_spec=importlib.util.spec_from_file_location('content_astc_mask',Path(__file__).with_name('content-astc-mask.py'))
        masking_module=importlib.util.module_from_spec(masking_spec);masking_spec.loader.exec_module(masking_module)
        masking_report=masking_module.apply(stage,PROFILE/'frame-removals.json',out/'physical-media-removal',args.astcenc,audio_module.read_literal,lua)
        for row in masking_report['textures']+masking_report['indexes']:
            reasons.setdefault(row['path'],[]).append('reviewed foreign frame physical removal; retained geometry and decoded pixels unchanged; see physical-media-removal/physical-removal-report.json')
        inputs.append({'path':str(PROFILE/'frame-removals.json'),'sha256':sha(PROFILE/'frame-removals.json')})
        for row in deps.get('atlas_excluded_textures',[]):
            path=row['path'];data=(stage/path).read_bytes()
            all_textures=set(re.findall(r'(?:a_name|\["a_name"\])\s*=\s*"([^"]+)"',data.decode('utf-8-sig')))
            excluded=set(row['textures'])
            if not excluded<=all_textures:raise ValueError('Reviewed excluded sheet is absent from '+path)
            put(path,filter_atlas(data,all_textures-excluded),row['reason'])
        for row in deps.get('excluded_files',[]):
            path=stage/row['path']
            if not path.is_file():raise ValueError('Reviewed excluded file is absent: '+row['path'])
            path.unlink()
        for path in patches:reasons.setdefault(path,[]).append('explicit standalone profile patch; see source-patches.json')
        reasons['firstgen.lua']=['isolated first-generation content and storage policy']
        bridge=REPO/'ThirdParty/KingdomRushRuntime/lua/jni_android.lua'
        if bridge.exists():put('jni_android.lua',bridge.read_bytes(),'real Android platform bridge')
        # Optional generated dependency profile consumed by atlas/exoskeleton loading.
        profile_extra=PROFILE/'firstgen_dependencies.lua'
        if profile_extra.exists():put('firstgen_dependencies.lua',profile_extra.read_bytes(),'static first-generation dependency closure')
    # No generated sound file reference may be unresolved.
    sound_refs=set()
    for name in ['sounds.lua', 'groups.lua', 'extra.lua']:
        path=stage/'kr3-desktop/assets/sounds'/name
        sound_refs.update(re.findall(r'"([^"\n]+\.(?:ogg|mp3|wav))"',path.read_text(encoding='utf-8-sig')))
    missing_audio=sorted(n for n in sound_refs if not (stage/'kr3-desktop/assets/sounds/files'/n).is_file())
    if missing_audio:raise ValueError('Missing final audio references: '+repr(missing_audio[:25]))
    # Only selected level source is admitted.
    level_files=list((stage/'kr3/data/levels').rglob('*'))
    selected_level_ids=sorted({int(m.group(1)) for p in level_files if (m:=re.search(r'level(\d+)',p.name))})
    if selected_level_ids!=list(range(45,77)):raise ValueError('Level coverage mismatch: '+repr(selected_level_ids))
    dest=out/'game.love'
    archive=out/'game.love.partial'
    def write_archive(target):
        manifest=[]
        with zipfile.ZipFile(target,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
            for path in sorted(stage.rglob('*')):
                if not path.is_file():continue
                name=path.relative_to(stage).as_posix();data=path.read_bytes()
                info=zipfile.ZipInfo(name,date_time=(2026,10,29,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16
                z.writestr(info,data,compresslevel=9)
                manifest.append({'path':name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'reasons':reasons.get(name,['generated profile source'])})
        return manifest
    # Prove the accepted game before making the one authorized title-screen change.
    write_archive(archive)
    validate_baseline_archive(archive)
    baseline=out/'playable-baseline/game.love'
    baseline.parent.mkdir(parents=True,exist_ok=True)
    archive.replace(baseline)
    menu_report=menu_module().apply_menu_patch(stage)
    for name in menu_report['modified_files']+menu_report['added_files']:
        reasons[name]=['approved title-screen help/settings overlay; battle source and media unchanged']
    manifest=write_archive(archive)
    with zipfile.ZipFile(archive) as z:
        if z.testzip():raise ValueError('Final ZIP CRC failed')
        for row in manifest:
            if hashlib.sha256(z.read(row['path'])).hexdigest()!=row['sha256']:raise ValueError('Final ZIP data mismatch')
    stable_baseline=validate_archive(archive)
    archive.replace(dest)
    report={'source_apk_sha256':APK_SHA,'output':str(dest),'bytes':dest.stat().st_size,'sha256':sha(dest),'files':len(manifest),'level_ids':selected_level_ids,'missing_sound_files':missing_audio,'original_sound_filename_repairs':typo_fixes,'component_inputs':inputs,'stable_baseline':stable_baseline,'start_menu':menu_report,'validation':'Exact accepted baseline plus approved title menu, ZIP CRC and every entry hash verified; static source/profile checks only. No game/device execution.'}
    (out/'content-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
    (out/'source-patches.json').write_text(json.dumps(patches,ensure_ascii=False,indent=2))
    (out/'content-build.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
