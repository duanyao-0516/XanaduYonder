#!/usr/bin/env python3
"""Compile Lua and inspect pure atlas/sound tables; never initialize or run the game.
LuaJIT 2.1 compiles every chunk without invoking it. Lupa Lua54 only evaluates
the pure atlas and sound data (or --lua-pythonlib for a local wheel tree).
"""
import argparse,hashlib,importlib.util,json,re,struct,subprocess,sys,tempfile,zipfile
from pathlib import Path
from content_stable_baseline import validate_archive
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--content',type=Path,required=True)
p.add_argument('--lua-pythonlib',type=Path)
p.add_argument('--luajit',type=Path,required=True,help='Host LuaJIT 2.1 executable; compile-only, never runs game chunks')
p.add_argument('--report',type=Path)
p.add_argument('--bootstrap-manifest',type=Path,default=Path(__file__).resolve().parents[2]/'ThirdParty/KingdomRushContent/bootstrap-resources.json')
a=p.parse_args()
stable_baseline=validate_archive(a.content)
if a.lua_pythonlib:sys.path.insert(0,str(a.lua_pythonlib))
from lupa.lua54 import LuaRuntime
L=LuaRuntime(unpack_returned_tuples=True)
# This evaluator accepts only the atlas index's literal Lua data. No runtime/global APIs.
literal=L.eval('function(s,n,env) local f,e=load(s,n,"t",env); assert(f,e); return f() end')
env=L.table()
for key in ['string','math','table','ipairs','pairs','tonumber','tostring','type','assert','error','next','setmetatable','getmetatable','rawget','rawset','select','pcall','xpcall']:env[key]=L.globals()[key]
report={'content':str(a.content),'content_sha256':hashlib.sha256(a.content.read_bytes()).hexdigest(),'content_bytes':a.content.stat().st_size,'scope':'static Lua compilation and pure data tables only; no gameplay execution','errors':[]}
report['stable_baseline']=stable_baseline
version=subprocess.run([str(a.luajit),'-e','io.write(jit.version)'],capture_output=True,text=True,check=True).stdout
if not version.startswith('LuaJIT 2.1'):raise ValueError('Expected LuaJIT 2.1 compiler, got '+version)
report['syntax_compiler']={'version':version,'path':str(a.luajit),'sha256':hashlib.sha256(a.luajit.read_bytes()).hexdigest()}
with zipfile.ZipFile(a.content) as z:
 names=set(z.namelist())
 level_file_path=Path(__file__).resolve().parents[2]/'ThirdParty/KingdomRushContent/profile/level-file-dependencies.json'
 level_file_profile=json.loads(level_file_path.read_text())
 level_file_spec=importlib.util.spec_from_file_location('content_level_files',Path(__file__).with_name('content-level-files.py'))
 level_file_module=importlib.util.module_from_spec(level_file_spec);level_file_spec.loader.exec_module(level_file_module)
 level_edges=[]
 for name in sorted(n for n in names if n.startswith('kr3/data/levels/') and n.endswith('.lua')):
  level_edges.extend(level_file_module.references(z.read(name).decode('utf-8-sig'),name))
 expected_level_files={row['path'] for row in level_file_profile['files']}
 if {edge['path'] for edge in level_edges}!=expected_level_files:report['errors'].append({'kind':'level-load-file-closure-changed'})
 for row in level_file_profile['files']:
  if row['path'] not in names:report['errors'].append({'kind':'missing-direct-level-file','path':row['path']})
  else:
   if hashlib.sha256(z.read(row['path'])).hexdigest()!=row['sha256']:report['errors'].append({'kind':'direct-level-file-original-bytes','path':row['path']})
   spawner=literal(z.read(row['path']).decode('utf-8-sig'),row['path'],env)
   modes={str(mode):len(list(waves.values())) for mode,waves in spawner['waves'].items()}
   if modes!=row['mode_wave_counts']:report['errors'].append({'kind':'direct-level-file-mode-waves','path':row['path'],'actual':modes})
 report['direct_level_files']={'manifest_sha256':hashlib.sha256(level_file_path.read_bytes()).hexdigest(),'references':level_edges,'original_files':len(expected_level_files)}
 bootstrap=json.loads(a.bootstrap_manifest.read_text())
 report['bootstrap_manifest_sha256']=hashlib.sha256(a.bootstrap_manifest.read_bytes()).hexdigest()
 cursor_source=z.read(bootstrap['cursor_source']).decode('utf-8-sig')
 cursor_families=set(re.findall(r'"(hand_\d+)"',cursor_source))
 cursor_suffixes=re.findall(r'love\.mouse\.newCursor\s*\(\s*(?:string\.format\s*\(\s*)?KR_PATH_ALL_TARGET\s*\.\.\s*"/assets/cursors/([^"\n]+)"',cursor_source)
 cursor_call_count=len(re.findall(r'love\.mouse\.newCursor\s*\(',cursor_source))
 required_cursors=set()
 for suffix in cursor_suffixes:
  if '%s' in suffix:
   if suffix.count('%s')!=1 or not cursor_families:report['errors'].append({'kind':'cursor-unresolved-pattern','pattern':suffix})
   for family in cursor_families:required_cursors.add(bootstrap['cursor_root']+suffix.replace('%s',family))
  else:required_cursors.add(bootstrap['cursor_root']+suffix)
 if cursor_call_count!=len(cursor_suffixes):report['errors'].append({'kind':'cursor-unresolved-call','api_calls':cursor_call_count,'recognized_file_calls':len(cursor_suffixes)})
 bootstrap_paths={row['path'] for row in bootstrap['files']}
 for path in sorted(required_cursors-bootstrap_paths):report['errors'].append({'kind':'cursor-unmanifested-source-dependency','path':path})
 report['bootstrap_files']=[]
 for row in bootstrap['files']:
  path=row['path'];entry={'path':path}
  if path not in names:report['errors'].append({'kind':'missing-bootstrap-file','path':path})
  else:
   data=z.read(path);entry['bytes']=len(data);entry['sha256']=hashlib.sha256(data).hexdigest()
   if entry['sha256']!=row['sha256']:report['errors'].append({'kind':'bootstrap-original-byte-mismatch','path':path})
   if 'dimensions' in row:
    entry['expected_dimensions']=row['dimensions'];entry['hotspot']=row['hotspot']
    valid_header=len(data)>=33 and data[:8]==b'\x89PNG\r\n\x1a\n' and data[12:16]==b'IHDR'
    entry['dimensions']=list(struct.unpack('>II',data[16:24])) if valid_header else None
    if entry['dimensions']!=row['dimensions']:report['errors'].append({'kind':'bootstrap-image-size','path':path,'actual':entry['dimensions']})
    if not entry['dimensions'] or not all(0<=point<size for point,size in zip(row['hotspot'],entry['dimensions'])):report['errors'].append({'kind':'cursor-hotspot-bounds','path':path})
   if path.endswith('.ogg') and not data.startswith(b'OggS'):report['errors'].append({'kind':'bootstrap-audio-format','path':path})
  report['bootstrap_files'].append(entry)
 report['cursor_source_paths']=sorted(required_cursors)
 direct_audio=set()
 for name in sorted(n for n in names if n.endswith('.lua')):
  source=z.read(name).decode('utf-8-sig')
  if name!=bootstrap['cursor_source'] and re.search(r'love\.mouse\.newCursor\s*\(',source):report['errors'].append({'kind':'unreviewed-cursor-call','source':name})
  for root,suffix in re.findall(r'love\.audio\.newSource\s*\(\s*(KR_PATH_\w+)\s*\.\.\s*"([^"\n]+)"',source):
   if root not in bootstrap['source_roots']:report['errors'].append({'kind':'unresolved-direct-audio-root','source':name,'root':root});continue
   path=bootstrap['source_roots'][root]+suffix;direct_audio.add(path)
   if path not in bootstrap_paths:report['errors'].append({'kind':'unmanifested-direct-audio','source':name,'path':path})
 report['direct_audio_source_paths']=sorted(direct_audio)
 cache={}
 def atlas(group):
  if group not in cache:cache[group]=literal(z.read('kr3-desktop/assets/images/fullhd/'+group+'.lua').decode('utf-8-sig'),group,env)
  return cache[group]
 def atlas_require(name):
  if not name.startswith('assets.images.fullhd.'):raise ValueError('Non-atlas require '+name)
  return atlas(name.split('assets.images.fullhd.',1)[1])
 env['require']=atlas_require
 report['zip_crc_ok']=z.testzip() is None; report['lua_files']=0
 with tempfile.TemporaryDirectory(prefix='kr1-lua-compile-') as work:
  work=Path(work); lua_names=sorted(n for n in names if n.endswith('.lua'))
  for name in lua_names:
   target=work/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z.read(name))
  (work/'file-list.txt').write_text('\n'.join(lua_names)+'\n')
  compiler=work/'compile-only.lua'
  compiler.write_text('for name in io.lines(arg[1]) do local f,e=loadfile(arg[2].."/"..name); if not f then io.write(name,"\\t",tostring(e),"\\n") end end\n')
  result=subprocess.run([str(a.luajit),str(compiler),str(work/'file-list.txt'),str(work)],capture_output=True,text=True,check=True)
  report['lua_files']=len(lua_names)
  for line in result.stdout.splitlines():
   name,_,error=line.partition('\t');report['errors'].append({'kind':'syntax','path':name,'error':error})
  policy_check=work/'check-profile.lua'
  handler_source=z.read('kr3/achievements_handlers.lua').decode('utf-8-sig')
  progress_start=handler_source.index('\tlocal done_c, done_h, done_i, done_g, done_imp =')
  progress_end=handler_source.index('\n\tif store.level_mode == GAME_MODE_CAMPAIGN then',progress_start)
  (work/'check-campaign-progress.lua').write_text('return function(slot, store)\nlocal GAME_MODE_CAMPAIGN,GAME_MODE_HEROIC,GAME_MODE_IRON=1,2,3\nlocal DIFFICULTY_HARD,DIFFICULTY_IMPOSSIBLE=3,4\nlocal log={debug=function() end}\n'+handler_source[progress_start:progress_end]+'\nreturn done_c,done_h,done_i,done_g,done_imp\nend\n')
  scripts_source=z.read('all/scripts.lua').decode('utf-8-sig')
  passive_start=scripts_source.index('function scripts.enemy_passive.update(this, store, script)')
  passive_end=scripts_source.index('\nscripts.enemy_mixed = {}',passive_start)
  (work/'check-original-passive.lua').write_text(scripts_source[passive_start:passive_end].replace('function scripts.enemy_passive.update(this, store, script)','return function(this, store, script)',1))
  policy_check.write_text('''package.path = arg[1] .. "/?.lua"
P_LEVEL=2; P_WAVE=3; KR_TARGET="desktop"
local policy=require("firstgen")
local full=assert(loadfile(arg[1].."/kr3/data/achievements_data.lua"))()
local selected=policy.achievement_rows(full)
local manifest=require("firstgen_dependencies").achievement_rows
assert(#selected==#manifest)
assert(policy.achievement_placeholder()=="achievement_icons_0016")
assert(not policy.achievement_allowed("GET_PARTY_STARTED"))
assert(not policy.achievement_allowed("NOT_A_REAL_ACHIEVEMENT"))
local count=0
for i,row in ipairs(selected) do
  assert(row==full[manifest[i].original_index])
  assert(policy.achievement_allowed(row.name))
  if row.name=="DEATH_FROM_ABOVE" then assert(row.icon==415); count=count+1 end
end
assert(count==1)
assert(policy.level_count({[45]={},[76]={}})==2)
assert(policy.highest_level({[45]={},[76]={}})==76)
assert(loadfile(arg[1].."/lib/klua/table.lua"))()
local native_animations=assert(loadfile(arg[1].."/kr3/data/game_animations-1.lua"))().animations
local restored=policy.animations({animations={}}).animations
local override_count=0
for name,expected in pairs(require("firstgen_dependencies").native_animation_overrides) do
  override_count=override_count+1
  for key,value in pairs(native_animations[name]) do assert(expected[key]==value and restored[name][key]==value) end
  for key,value in pairs(expected) do assert(native_animations[name][key]==value) end
  assert(restored[name]~=expected)
end
assert(override_count==5)
local transform_death=assert(loadfile(arg[1].."/kr3/data/game_animations-rebbborn.lua"))().animations.eb_hobtransform_death
assert(transform_death.prefix=="eb_hobtransform" and transform_death.from==1 and transform_death.to==106)
package.loaded["data.slot_template"]=assert(loadfile(arg[1].."/kr3/data/slot_template.lua"))()
local save=policy.slot({liuhui={balance=true,g4range_balance=true,hero_enhance=true},levels={[45]={stars=3},[76]={stars=2}},heroes={selected="hero_gerald",status={hero_gerald={xp=123,skills={}}}}})
assert(save.liuhui.balance==false and save.liuhui.g4range_balance==false and save.liuhui.hero_enhance==false)
assert(save.levels[45].stars==3 and save.levels[76].stars==2 and save.heroes.status.hero_gerald.xp==123)
local campaign_progress=assert(loadfile(arg[1].."/check-campaign-progress.lua"))()
local store={level_idx=45,level_mode=1,level_difficulty=1}
local c,h,i,g,imp=campaign_progress({levels={[45]={}}},store)
assert(c==false and h==false and i==false and g==false and imp==false)
local completed={levels={}}
for index=45,56 do completed.levels[index]={[1]=3} end
store.level_idx=56;store.level_difficulty=3
c,h,i,g,imp=campaign_progress(completed,store)
assert(c==true and not h and not i and g==true and imp==false)
for index=45,56 do completed.levels[index][1]=4 end
store.level_difficulty=4
c,h,i,g,imp=campaign_progress(completed,store)
assert(c==true and g==true and imp==true)
completed.levels[50]=nil
c,h,i,g,imp=campaign_progress(completed,store)
assert(c==false and g==false and imp==false)
-- Only the wrapper and an extracted original callback run. Engine services are
-- bounded spies; this proves coroutine handoff, not gameplay equivalence.
local entity={render={sprites={{name="idle",animated=true}}},health={dead=false}}
local store_arg,script_arg={},{}
local calls=0
local co=coroutine.create(policy.hobboss_death_update(function(this,store,script)
  calls=calls+1
  assert(this==entity and store==store_arg and script==script_arg)
  assert(this.render.sprites[1].animated==true)
  this.render.sprites[1].name="death"
  coroutine.yield("original-death")
  return "original-return"
end))
assert(coroutine.resume(co,entity,store_arg,script_arg))
assert(calls==0 and entity.render.sprites[1].name==nil and entity.render.sprites[1].animated==false)
assert(coroutine.resume(co));assert(calls==0)
entity.health.dead=true
local ok,value=coroutine.resume(co)
assert(ok and value=="original-death" and calls==1 and entity.render.sprites[1].name=="death")
ok,value=coroutine.resume(co)
assert(ok and value=="original-return" and calls==1 and coroutine.status(co)=="dead")
local original_loader=assert(loadfile(arg[1].."/check-original-passive.lua"))
local deaths=0
setfenv(original_loader,{SU={
  y_enemy_death=function(store,this)
    assert(store==store_arg and this==entity and this.render.sprites[1].animated)
    deaths=deaths+1
  end,
  y_enemy_walk_until_blocked=function() error("unexpected pre-death walk") end
}})
entity.health.dead=false
co=coroutine.create(policy.hobboss_death_update(original_loader()))
assert(coroutine.resume(co,entity,store_arg,script_arg));assert(deaths==0)
entity.health.dead=true
assert(coroutine.resume(co));assert(deaths==1 and coroutine.status(co)=="dead")
io.write("pure profile, sparse-level, native animation and death-controller coroutine cases passed")
''')
  result=subprocess.run([str(a.luajit),str(policy_check),str(work)],capture_output=True,text=True)
  report['pure_profile_checks']={'passed':result.returncode==0,'result':result.stdout,'errors':result.stderr,'scope':'Only profile/data functions; no gameplay modules or engine initialized'}
  if result.returncode:report['errors'].append({'kind':'profile-policy-regression','error':result.stderr})
 report['atlas_groups']=0;report['atlas_frames']=0;report['missing_atlas_files']=[]
 for name in sorted(names):
  if not name.startswith('kr3-desktop/assets/images/fullhd/') or not name.endswith('.lua'):continue
  data=atlas(Path(name).stem);report['atlas_groups']+=1
  for frame,row in data.items():
   report['atlas_frames']+=1
   texture=row['a_name']; base=str(Path(name).parent)+'/'
   candidates=[texture,re.sub(r'\.(dds|png)$','.astc',texture),re.sub(r'\.dds$','.png',texture)]
   if not any(base+c in names for c in candidates):report['missing_atlas_files'].append({'group':name,'frame':frame,'texture':texture})
 report['errors'] += [{'kind':'atlas',**x} for x in report['missing_atlas_files']]
 frame_recipe_path=Path(__file__).resolve().parents[2]/'ThirdParty/KingdomRushContent/profile/frame-removals.json'
 frame_recipe=json.loads(frame_recipe_path.read_text())
 report['removed_atlas_frames']=sum(len(row['frames']) for row in frame_recipe['groups'])
 for item in frame_recipe['groups']:
  data=atlas(item['group'])
  for key in item['frames']:
   if data[key] is not None:report['errors'].append({'kind':'foreign-frame-still-indexed','group':item['group'],'frame':key})
 physical_path=a.content.parent/'physical-media-removal/physical-removal-report.json'
 physical=json.loads(physical_path.read_text())
 if physical['recipe_sha256']!=hashlib.sha256(frame_recipe_path.read_bytes()).hexdigest():report['errors'].append({'kind':'physical-removal-recipe-mismatch'})
 for row in physical['textures']+physical['indexes']:
  if hashlib.sha256(z.read(row['path'])).hexdigest()!=row['after_sha256']:report['errors'].append({'kind':'physical-removal-archive-mismatch','path':row['path']})
 report['physical_removal_report_sha256']=hashlib.sha256(physical_path.read_bytes()).hexdigest()
 map_source=z.read('all-desktop/screen_map.lua').decode('utf-8-sig')
 map_groups=re.findall(r'"([^"\n]+)"',re.search(r'screen_map.required_textures\s*=\s*\{([^}]+)\}',map_source).group(1))
 dependencies=literal(z.read('firstgen_dependencies.lua').decode('utf-8-sig'),'firstgen_dependencies',env)
 achievement_path=Path(__file__).resolve().parents[2]/'ThirdParty/KingdomRushContent/profile/achievements-allowlist.json'
 achievement_profile=json.loads(achievement_path.read_text())
 report['achievement_allowlist_sha256']=hashlib.sha256(achievement_path.read_bytes()).hexdigest()
 if hashlib.sha256(z.read('kr3/data/achievements_data.lua')).hexdigest()!=achievement_profile['original_data_sha256']:report['errors'].append({'kind':'achievement-internal-data-changed'})
 env['P_LEVEL']=2;env['P_WAVE']=3;env['KR_TARGET']='desktop'
 achievement_data=literal(z.read('kr3/data/achievements_data.lua').decode('utf-8-sig'),'achievements_data',env)
 expected_rows=achievement_profile['rows']
 actual_rows=list(dependencies['achievement_rows'].values())
 if len(actual_rows)!=len(expected_rows):report['errors'].append({'kind':'achievement-allowlist-count'})
 for expected,actual in zip(expected_rows,actual_rows):
  row=achievement_data[expected['original_index']]
  for key in ('original_index','name','icon','goal'):
   if actual[key]!=expected.get(key):report['errors'].append({'kind':'achievement-profile-identity','name':expected['name'],'field':key})
  for key in ('name','icon','goal','period','order'):
   if row[key]!=expected['original_row'].get(key):report['errors'].append({'kind':'achievement-original-row-drift','name':expected['name'],'field':key})
 allowed_icons={row['icon'] for row in expected_rows}
 expected_icon_frames={f'{prefix}{icon:04d}' for prefix in ('achievement_icons_','achievement_icons_disabled_') for icon in allowed_icons}
 indexed_achievement_frames={key for key,_ in atlas('achievements').items() if key.startswith('achievement_icons_')}
 if indexed_achievement_frames!=expected_icon_frames:report['errors'].append({'kind':'achievement-atlas-profile','extra':sorted(indexed_achievement_frames-expected_icon_frames),'missing':sorted(expected_icon_frames-indexed_achievement_frames)})
 for path,anchor in [('all/achievements.lua','function A:got(id)\n\tif not require("firstgen").achievement_allowed(id) then return end'),('all-desktop/screen_map.lua','achievements_data = require("firstgen").achievement_rows(require("data.achievements_data"))')]:
  if anchor not in z.read(path).decode('utf-8-sig'):report['errors'].append({'kind':'achievement-gate-missing','path':path})
 for path in ('all-desktop/screen_map.lua','all-desktop/game_gui.lua'):
  source=z.read(path).decode('utf-8-sig')
  if 'KImageView:new(require("firstgen").achievement_placeholder())' not in source or 'KImageView:new("achievement_icons_0001")' in source:report['errors'].append({'kind':'achievement-placeholder','path':path})
 report['achievements']={'visible_rows':len(expected_rows),'unique_icons':len(allowed_icons),'original_data_preserved':True,'award_gate':'allowed names only','selection':'original index + name + icon + goal; original periods unchanged'}
 map_groups.extend(dependencies['map_texture_groups'].values())
 report['map_preload_groups']=map_groups
 required_frames=['Generation_Upgrade_Sell','heroroom_thumbs_0008']+[f'{prefix}{i:04d}' for prefix in ('mapButtons_portrait_hero_','portrait_notxt_','heroroom_portraits_') for i in range(33,46)]+[f'stage_thumbs_{i:04d}' for i in range(45,77)]+[f'encyclopedia_tower_specials_{i:04d}' for i in (105,107,114)]+sorted(expected_icon_frames)
 for key in required_frames:
  if not any(atlas(group)[key] is not None for group in map_groups):report['errors'].append({'kind':'map-bootstrap-frame-not-preloaded','frame':key})
 report['required_map_frame_bindings']=len(required_frames)
 gui_source=z.read('all-desktop/game_gui.lua').decode('utf-8-sig')
 gui_groups=re.findall(r'"([^"\n]+)"',re.search(r'game_gui.required_textures\s*=\s*\{([^}]+)\}',gui_source).group(1))
 gui_frames=set()
 for group in gui_groups:
  for key,row in atlas(group).items():
   gui_frames.add(key)
   if row['alias'] is not None:gui_frames.update(row['alias'].values())
 power_frames=['fire_0001','reinforcements_0001','spell_selection_icon_empty','achievement_icons_0016']+[f'power_button_mask_{i:04d}' for i in range(1,46)]
 for key in power_frames:
  if key not in gui_frames:report['errors'].append({'kind':'native-hud-construction-frame','frame':key})
 report['native_hud_construction_frames']=power_frames
 gameplay_groups=re.findall(r'"([^"\n]+)"',re.search(r'game.required_textures\s*=\s*\{([^}]+)\}',z.read('firstgen.lua').decode('utf-8-sig')).group(1))
 gameplay_groups.extend(dependencies['game_texture_groups'].values())
 gameplay_frames=set()
 for group in gameplay_groups:
  for key,row in atlas(group).items():
   gameplay_frames.add(key)
   if row['alias'] is not None:gameplay_frames.update(row['alias'].values())
 native_effect_frames=[]
 for _,row in dependencies['native_animation_overrides'].items():
  native_effect_frames.extend(f"{row['prefix']}_{i:04d}" for i in range(row['from'],row['to']+1))
 native_effect_frames.extend(f'bossJuggernaut_smoke_{i:04d}' for i in range(1,15))
 for key in native_effect_frames:
  if key not in gameplay_frames:report['errors'].append({'kind':'native-gameplay-effect-frame','frame':key})
 if 'return require("firstgen").animations(d1)' not in z.read('kr3/data/game_animations.lua').decode('utf-8-sig'):report['errors'].append({'kind':'native-animation-profile-hook'})
 for i in range(1,15,2):
  key=f'bossJuggernaut_smoke_{i:04d}'
  original=atlas('go_stage50')[key];merged=atlas('go_enemies_common')[key]
  def lua_value(value):
   return {k:lua_value(v) for k,v in value.items()} if hasattr(value,'items') else value
  if merged is None or lua_value(original)!=lua_value(merged):report['errors'].append({'kind':'native-smoke-record-changed','frame':key})
 report['native_gameplay_effect_frames']=native_effect_frames
 for frame in ('build_terrain_0014','build_terrain_0014_over'):
  row=atlas('go_towers_1')[frame];expected=frame.replace('build_terrain_', 'build_terrain_blocked_')
  if row is None or expected not in list(row['alias'].values()):report['errors'].append({'kind':'original-blocked-terrain-alias','frame':frame,'alias':expected})
 native_templates=z.read('kr3/game_templates-1.lua').decode('utf-8-sig')
 gold_count=native_templates[native_templates.index('tt = RT("decal_gold_bag_iron_count", "decal_tween")'):native_templates.index('tt = RT("decal_goldbag_bl", "decal")')]
 for statement in ('tt.render.sprites[1].name = nil','tt.render.sprites[1].hidden = true','tt.render.sprites[2] = E.clone_c(E, "sprite")','tt.texts.list[1].sprite_id = 2'):
  if statement not in gold_count:report['errors'].append({'kind':'iron-gold-count-render-contract','statement':statement})
 for invalid in ('"time_wizard_sandstorm_proj"','"pirateTower_bombasticwhat"'):
  if invalid in native_templates:report['errors'].append({'kind':'source-nonexistent-projectile-image','name':invalid})
 for begin,end,required in [
  ('tt = E.register_t(E, "time_wizard_sandstorm", "rock_entwood")','tt = RT("fx_teleport_ancient_guardian"',('tt.bullet.flight_time = fts(1)','tt.bullet.hit_payload = "aura_time_wizard_sandstorm"','tt.bullet.hit_fx = "fx_sand_worm"','tt.render.sprites[1].name = nil')),
  ('tt = RT("airstrike_steam_troop", "g1_bomb")','tt = RT("mod_steam_troop_freeze"',('tt.bullet.flight_time = fts(1)','tt.main_script.update = scripts_rebbborn.airstrike.update','tt.bullet.hit_fx = "fx_explosion_airstrike"','tt.render.sprites[1].name = nil'))]:
  section=native_templates[native_templates.index(begin):native_templates.index(end,native_templates.index(begin))]
  for statement in required:
   if statement not in section:report['errors'].append({'kind':'effect-only-projectile-contract','template':begin,'statement':statement})
 for name,end in [('enemy_tp_aura','enemy_curse_aura'),('enemy_curse_aura','missile_hobgob'),('hobgoblin_bomb_spawner','hobgoblin_meteor_spawner')]:
  section=native_templates[native_templates.index('"'+name+'",'):native_templates.index('"'+end+'",')]
  required=['tt.render.sprites[1].name = nil','tt.render.sprites[1].animated = false']
  if name.startswith('enemy_'):required += ['tt.unit.death_animation = nil','tt.health.hp_max = 0','tt.motion.max_speed = 0','tt.main_script.update = scripts.enemy_passive.update']
  else:required += ['tt.spawner.count = 1','tt.spawner.cycle_time = fts(6)','tt.spawner.entity = "enemy_tp_aura"','tt.spawner.initial_spawn_animation = nil','tt.tween.remove = true']
  for statement in required:
   if statement not in section:report['errors'].append({'kind':'effect-only-controller-contract','template':name,'statement':statement})
 swap=native_templates[native_templates.index('tt = RT("enemy_hobboss_swap",'):native_templates.index('tt = RT("enemy_tp_aura",')]
 for statement in ('tt.main_script.update = require("firstgen").hobboss_death_update(scripts.enemy_passive.update)','tt.health.dead_lifetime = 3.95','tt.health.hp_max = 0','tt.motion.max_speed = 0','tt.sound_events.death = "HobgobBossSpawn"','tt.render.sprites[1].prefix = "eb_hobtransform"'):
  if statement not in swap:report['errors'].append({'kind':'hobboss-original-death-contract','statement':statement})
 profile_source=z.read('firstgen.lua').decode('utf-8-sig')
 for key in ('balance','g4range_balance','hero_enhance'):
  if 'lh.'+key+' = false' not in profile_source:report['errors'].append({'kind':'native-balance-profile','flag':key})
 if 'tower_select_add_quick_balance_buttons' in map_source:report['errors'].append({'kind':'foreign-balance-buttons-retained'})
 hero_room=map_source[map_source.index('function HeroRoomView:initialize('):map_source.index('function HeroRoomView:update_hero_enhance_button')]
 if 'local hero_enhance_button' in hero_room:report['errors'].append({'kind':'hero-UH-button-retained'})
 if 'upgrades_FL:range_g4()' in z.read('kr3/upgrades.lua').decode('utf-8-sig'):report['errors'].append({'kind':'foreign-range-template-rewrite-retained'})
 handler=z.read('kr3/achievements_handlers.lua').decode('utf-8-sig')
 for anchor in ('for i = 45, 76 do','for i = 45, 56 do','done_c, done_h, done_i, done_g, done_imp = false, false, false, false, false'):
  if anchor not in handler:report['errors'].append({'kind':'achievement-native-progress-range','anchor':anchor})
 loading=literal(z.read('kr3-desktop/data/director_data.lua').decode('utf-8-sig'),'director_data',env)['loading_image_name']
 report['loading_default']=loading['default'];report['loading_groups_by_level']={}
 if loading['default']!='loading_grass':report['errors'].append({'kind':'foreign-loading-default','name':loading['default']})
 for idx in range(45,77):
  matches=[row[1] for _,row in loading.items() if hasattr(row,'items') and row[2] is not None and idx in list(row[2].values())]
  selected=matches[0] if matches else loading['default'];report['loading_groups_by_level'][idx]=selected
  if len(set(matches))>1:report['errors'].append({'kind':'ambiguous-level-loading','level':idx,'groups':matches})
  if 'kr3-desktop/assets/images/fullhd/'+selected+'.lua' not in names:report['errors'].append({'kind':'missing-level-loading','level':idx,'group':selected})
 report['sound_files']=0;report['sound_ids']=0;report['missing_sound_files']=[]
 sound_base='kr3-desktop/assets/sounds/'
 sounds=literal(z.read(sound_base+'sounds.lua').decode(),'sounds',env);groups=literal(z.read(sound_base+'groups.lua').decode(),'groups',env)
 ending_source=z.read('kr3/screen_kr1_fa.lua').decode('utf-8-sig')
 for old in ('data.credits_data_fa','music_screen_kr1_end_fa','MusicEndCreditsFA','level_idx = 33'):
  if old in ending_source:report['errors'].append({'kind':'native-ending-source-omission','reference':old})
 for expected in ('require("data.credits_data")','"music_screen_kr1_end"','"MusicEndCredits1"','level_idx = 75'):
  if expected not in ending_source:report['errors'].append({'kind':'native-ending-contract','reference':expected})
 if groups['music_screen_kr1_end'] is None or sounds['MusicEndCredits1'] is None:report['errors'].append({'kind':'native-ending-audio'})
 for path in ('kr3-desktop/data/credits_data.lua','kr3-desktop/data/comics/21.csv','kr3-desktop/assets/images/fullhd/comic_end_fa.lua'):
  if path not in names:report['errors'].append({'kind':'native-ending-resource','path':path})
 report['native_extension_ending']={'level':75,'credits_module':'data.credits_data','music_group':'music_screen_kr1_end','music_id':'MusicEndCredits1','original_comic':'21.csv','return_level':75}
 sound_refs=set(direct_audio)
 for _,row in sounds.items():
  report['sound_ids']+=1
  for _,file in row['files'].items():
   sound_refs.add(sound_base+'files/'+file)
   if sound_base+'files/'+file not in names:report['missing_sound_files'].append(file)
 for _,row in groups.items():
  if row['sounds']:
   for _,name in row['sounds'].items():
    if sounds[name] is None:report['errors'].append({'kind':'sound-group-id','name':name})
  if row['files']:
   for _,file in row['files'].items():
    sound_refs.add(sound_base+'files/'+file)
    if sound_base+'files/'+file not in names:report['missing_sound_files'].append(file)
 report['sound_files']=sum(n.startswith(sound_base+'files/') for n in names)
 report['unused_sound_files']=sorted(n for n in names if n.startswith(sound_base+'files/') and not n.endswith('/') and n not in sound_refs)
 report['errors'] += [{'kind':'unused-audio','path':n} for n in report['unused_sound_files']]
 report['fonts']=sorted(n for n in names if n.endswith(('.ttf','.otf')))
 if any('NotoSansCJK' in n or '/msyh.' in n for n in report['fonts']):report['errors'].append({'kind':'foreign-font-profile'})
 report['errors'] += [{'kind':'audio','path':x} for x in sorted(set(report['missing_sound_files']))]
 report['mode_waves']={}
 for level in range(45,77):
  for mode in ['campaign','heroic','iron']:
   matches=[n for n in names if re.search(r'/level0?'+str(level)+r'_waves_'+mode+r'\.lua$',n)]
   report['mode_waves'][f'{level}:{mode}']=matches
   if len(matches)!=1:report['errors'].append({'kind':'mode-coverage','level':level,'mode':mode,'paths':matches})
report['passed']=not report['errors'] and report['zip_crc_ok']
if a.report:a.report.write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='mode_waves'},ensure_ascii=False,indent=2))
sys.exit(0 if report['passed'] else 1)
