#!/usr/bin/env python3
"""Reproducible, offline native asset conversion. Requires FFDec 26.4.0, Java 17, Pillow.
No Flash execution or player is used by the shipped Unity game.
"""
import argparse, concurrent.futures, hashlib, json, math, os, pathlib, re, shutil, struct, subprocess, wave, zipfile
from PIL import Image
from audio_conversion import decode_pcm_wav, trim_pcm_wav
ROOT=pathlib.Path(__file__).resolve().parents[2]
HASHES={1:'5f88517846ff0eb75eee3bd6dfebbf9fad75edbb4dbe20ed8ea4c0afb3dfd3a1',2:'b7dc9f9654add56e87234381e5272695592e3837bbd8489f836ed7225b81960c'}
SOURCE_HASHES=json.loads((ROOT/'scripts/age-of-war/source-hashes.json').read_text())
IDENTITY=[1,0,0,1,0,0]
FMOD_REJECTED_TWO_SOUND_IDS={3,4,6,9,13,17,20,24,32,34,35,47,48,49}
def digest(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def normalize_ttf(data):
 # FFDec writes wall-clock timestamps into head. Normalize metadata, retaining every glyph/table byte.
 result=bytearray(data);count=struct.unpack_from('>H',result,4)[0];head_record=None
 for index in range(count):
  record=12+index*16
  if result[record:record+4]==b'head':head_record=record;break
 if head_record is None:raise ValueError('TrueType export is missing its head table')
 head,length=struct.unpack_from('>II',result,head_record+8)
 struct.pack_into('>I',result,head+8,0)
 struct.pack_into('>QQ',result,head+20,2082844800,2082844800)
 def checksum(payload):
  payload=bytes(payload)+b'\0'*((-len(payload))%4)
  return sum(struct.unpack('>'+str(len(payload)//4)+'I',payload))&0xffffffff
 struct.pack_into('>I',result,head_record+4,checksum(result[head:head+length]))
 struct.pack_into('>I',result,head+8,(0xB1B0AFBA-checksum(result))&0xffffffff)
 return bytes(result)
def write_json(p,data):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data,separators=(',',':')))
def multiply(p,q):
 a,b,c,d,x,y=p;e,f,g,h,u,v=q
 return [a*e+c*f,b*e+d*f,a*g+c*h,b*g+d*h,a*u+c*v+x,b*u+d*v+y]
def transformed(bounds,m):
 a,b,c,d,x,y=m;l,t,r,bt=bounds
 points=[(a*u+c*v+x,b*u+d*v+y) for u in [l,r] for v in [t,bt]]
 return [min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
def merge_bounds(items):return [min(x[0] for x in items),min(x[1] for x in items),max(x[2] for x in items),max(x[3] for x in items)] if items else [0,0,1,1]
class Converter:
 def __init__(self,args):
  self.args=args;self.output=pathlib.Path(args.output);self.src=pathlib.Path(args.source);self.cache=pathlib.Path(args.cache);self.meta=self.cache/'metadata';self.raw=self.cache/'raw';self.jobs={};self.packages={};self.clips={1:[],2:[]};self.scenes={1:[],2:[]};self.sources={1:[],2:[]};self.fonts={1:[],2:[]};self.sounds={1:[],2:[]}
  self.java=args.java;self.cp=args.ffdec+os.pathsep+str(ROOT/'scripts/age-of-war')
  self.excluded_sources={1:[],2:[]}
  self.audio_metadata={}
 def prepare_sources(self):
  categories='image,shape,morphshape,font,sound,script,symbolClass,text'
  exports=[(self.src/'age-of-war-1-7k7k.swf',self.src/'analysis/aow1/export',categories),(self.src/'age-of-war-2-7k7k.swf',self.src/'analysis/aow2/outer',categories+',binaryData')]
  for source,out,types in exports:
   if not (out/'symbolClass').exists():
    out.mkdir(parents=True,exist_ok=True)
    subprocess.run([self.java,'-Djava.awt.headless=true','-jar',self.args.ffdec,'-export',types,str(out),str(source)],check=True,stdout=subprocess.DEVNULL)
  embedded=self.src/'analysis/aow2/embedded-swf';embedded.mkdir(parents=True,exist_ok=True)
  for binary in sorted((self.src/'analysis/aow2/outer/binaryData').glob('*.bin')):
   if binary.read_bytes()[:3] not in [b'FWS',b'CWS',b'ZWS']:continue
   swf=embedded/(binary.stem+'.swf')
   if not swf.exists():shutil.copyfile(binary,swf)
   out=self.src/'analysis/aow2/embedded'/binary.stem
   if not out.exists():
    out.mkdir(parents=True)
    subprocess.run([self.java,'-Djava.awt.headless=true','-jar',self.args.ffdec,'-export',categories,str(out),str(swf)],check=True,stdout=subprocess.DEVNULL)
  sponsor=self.src/'analysis/aow2/supplemental/sponsor-stream-1.wav'
  if not sponsor.exists():
   sponsor.parent.mkdir(parents=True,exist_ok=True)
   swf=next(embedded.glob('*_SPONSOR_SWF_*.swf'))
   subprocess.run([self.java,'-Djava.awt.headless=true','-cp',self.cp,'AowSwfExport','stream',str(swf),str(sponsor)],check=True)
  intro=self.src/'analysis/aow1/supplemental/intro-stream-1008.wav'
  if not intro.exists():
   intro.parent.mkdir(parents=True,exist_ok=True)
   subprocess.run([self.java,'-Djava.awt.headless=true','-cp',self.cp,'AowSwfExport','stream',str(self.src/'age-of-war-1-7k7k.swf'),str(intro)],check=True)
 def load(self):
  self.meta.mkdir(parents=True,exist_ok=True)
  subprocess.run([self.java.replace('/java','/javac'),'-cp',self.args.ffdec,str(ROOT/'scripts/age-of-war/AowSwfExport.java'),str(ROOT/'scripts/age-of-war/AowAudioMetadata.java')],check=True)
  for edition in [1,2]:
   original=self.src/f'age-of-war-{edition}-7k7k.swf'
   if digest(original)!=HASHES[edition]:raise ValueError(f'Wrong fixed original {original}')
   self.sources[edition].append({'name':original.name,'sha256':digest(original),'bytes':original.stat().st_size})
  self.prepare_sources()
  paths=[(1,self.src/'age-of-war-1-7k7k.swf')]+[(2,p) for p in sorted((self.src/'analysis/aow2/embedded-swf').glob('*.swf'))]
  if len(paths)!=31:raise ValueError('Expected original one and all 30 embedded original two packages')
  for edition,path in paths:
   expected=SOURCE_HASHES['One' if edition==1 else 'Two'].get(path.name)
   if expected is None or digest(path)!=expected:raise ValueError(f'Cached SWF does not match the fixed original binary-data payload: {path}')
  def inspect(item):
   edition,p=item;out=self.meta/(p.stem+'.json')
   subprocess.run([self.java,'-Djava.awt.headless=true','-cp',self.cp,'AowSwfExport','inspect',str(p),str(out)],check=True,capture_output=True)
   d=json.loads(out.read_text());d['chars']={c['id']:c for c in d['characters']};d['path']=str(p);d['edition']=edition;d['name']='one' if edition==1 else re.search(r'Ressources_(.+)_SWF|Preloader_(.+)_SWF',p.stem).group(1) or 'PRELOADER'
   return p.stem,d
  with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
   for stem,data in pool.map(inspect,paths):
    self.packages[stem]=data
    if data['edition']==2:self.sources[2].append({'name':pathlib.Path(data['path']).name,'sha256':digest(data['path']),'bytes':pathlib.Path(data['path']).stat().st_size})
  for key,source in [(1,self.src/'age-of-war-1-7k7k.swf'),(2,self.src/'age-of-war-2-7k7k.swf'),('sponsor',pathlib.Path(self.package('SPONSOR')['path']))]:
   output=self.meta/f'audio-{key}.json'
   subprocess.run([self.java,'-Djava.awt.headless=true','-cp',self.cp,'AowAudioMetadata',str(source),str(output)],check=True)
   self.audio_metadata[key]=json.loads(output.read_text())
 def package(self,name):return next(d for d in self.packages.values() if d['name']==name)
 def frames(self,p,cid):return p['root']['frames'] if cid==0 else p['chars'][cid].get('timeline',{}).get('frames',[{'frame':1,'labels':[],'layers':[],'sounds':[]}])
 def job(self,p,cid,frames,layers=None,name=None):
  stem=pathlib.Path(p['path']).stem;name=name or str(cid)
  jobs=self.jobs.setdefault(stem,{})
  if name not in jobs:
   job={'id':cid,'name':name,'frames':[]}
   if layers is not None:
    job['layers']=layers;job['bounds']=merge_bounds([transformed(p['chars'][l['id']].get('bounds',[0,0,1,1]),l.get('matrix',IDENTITY)) for l in layers])
   jobs[name]=job
  jobs[name]['frames']=sorted(set(jobs[name]['frames'])|set(frames))
  return stem+'/'+name
 def clip(self,p,key,action,cid,start=1,end=None,layers=None,name=None,loop=False,events=None):
  end=end or len(self.frames(p,cid));frames=list(range(start,end+1));job=self.job(p,cid,frames,layers,name)
  self.clips[p['edition']].append({'key':key,'action':action,'loop':loop,'events':events if events is not None else ([{'frame':0,'type':'label','value':action},{'frame':end-start,'type':'labelEnd','value':action}] if p['edition']==2 and key.startswith(('unit/','turret/')) else []),'source':{'swf':pathlib.Path(p['path']).name,'characterId':cid,'firstFrame':start,'lastFrame':end,'composition':layers or [],'emptyArtwork':bool(layers) and all(p['chars'][l['id']].get('bounds',[0,0,0,0])==[0,0,0,0] for l in layers)},'_job':job,'_frames':frames})
 def labeled(self,p,key,cid,layers=None,name=None):
  frames=self.frames(p,cid)
  if p['name']=='HUD' and cid in [19,321]:
   child=18 if cid==19 else 318
   self.clip(p,key,'idle',child,1,2,layers=[{'id':cid,'matrix':IDENTITY,'animate':True}],name='hud-toggle-'+str(cid),loop=False)
   return
  if p['chars'][cid]['type'].startswith('DefineButton'):
   for f,action in enumerate(['idle','over','down','hit'],1):self.clip(p,key,action,cid,f,f,layers,name)
   return
  labels=[(f['frame'],n) for f in frames for n in f['labels']]
  if not labels:self.clip(p,key,'idle',cid,layers=layers,name=name,loop=len(frames)>1);return
  for i,(start,label) in enumerate(labels):
   end=labels[i+1][0]-1 if i+1<len(labels) else len(frames)
   self.clip(p,key,label,cid,start,end,layers,name,loop=label in ['idle','walkCycle','anim','move'])
 def scene(self,p,key,cid=0,frame=1,exclude=(),exclude_ids=(),timeline_children=False,deduplicate_variants=False):
  scene={'key':key,'width':650 if p['edition']==1 else 720,'height':450 if p['edition']==1 else 480,'layers':[],'hotspots':[],'texts':[]}
  for l in self.frames(p,cid)[frame-1]['layers']:
   c=p['chars'].get(l['id']);name=l['name'];m=l['matrix']
   if c is None or name in exclude or l['id'] in exclude_ids:continue
   a,b,cc,d,x,y=m;asset='ui/'+p['name'].lower()+'/'+str(l['id'])
   modifierColor=l.get('color')
   hasColor=modifierColor and (modifierColor[:3]!=[256]*3 or modifierColor[4:]!=[0]*4)
   variant=f'instance-{cid}-{frame}-{l["depth"]}-{l["id"]}' if hasColor or l.get('filters') else None
   if variant and deduplicate_variants:
    signature=hashlib.sha256(json.dumps([modifierColor if hasColor else None,l.get('filters')],sort_keys=True).encode()).hexdigest()[:12]
    variant=f'instance-{cid}-{l["depth"]}-{signature}-{l["id"]}'
   if variant:asset='ui/'+p['name'].lower()+'/'+variant+'/'+str(l['id'])
   if not any(z['key']==asset for z in self.clips[p['edition']]):
    self.labeled(p,asset,l['id'],name=variant)
    if variant:
     job=self.jobs[pathlib.Path(p['path']).stem][variant]
     if hasColor:job['color']=modifierColor[:3]+[256]+modifierColor[4:7]+[0]
     if l.get('filters'):job['filterSource']=[cid,frame,l['depth']]
     for clip in self.clips[p['edition']]:
      if clip['key']==asset:clip['source']['instanceModifiers']={k:job[k] for k in ['color','filterSource'] if k in job}
   labels=[n for f in self.frames(p,l['id']) for n in f['labels']];action=labels[0] if labels else 'idle'
   scene['layers'].append({'key':asset,'action':action,'name':name,'x':x,'y':y,'scaleX':a,'scaleY':d,'skewX':cc,'skewY':b,'rotation':0,'alpha':l.get('color',[256]*4)[3]/256,'depth':l['depth'],'frame':0})
   if l.get('clipDepth',0)>0:scene['layers'][-1]['clipDepth']=l['clipDepth']
   if timeline_children:scene['layers'][-1]['frame']=min(l['time'],len(self.frames(p,l['id']))-1)
   if name.startswith('btn') or c['type'].startswith('DefineButton'):
    localBounds=c.get('bounds',[0,0,0,0])
    if c['type'].startswith('DefineButton'):
     hit=self.frames(p,l['id'])[3]['layers'];localBounds=merge_bounds([transformed(p['chars'][v['id']].get('bounds',[0,0,0,0]),v['matrix']) for v in hit])
    bounds=transformed(localBounds,m);scene['hotspots'].append({'action':name or 'button/'+str(l['id']),'_key':asset,'_matrix':m,'_button':c['type'].startswith('DefineButton'),'_hitBounds':bounds,'_stageClip':cid==0,'name':name,'id':l['id'],'x':bounds[0],'y':bounds[1],'w':bounds[2]-bounds[0],'h':bounds[3]-bounds[1]})
   if 'text' in c:
    bounds=transformed(c['bounds'],m);text=dict(c['text']);text.update(characterId=l['id'],filters=l.get('filters',[]),bold=p['chars'].get(text['fontId'],{}).get('font',{}).get('bold',False),italic=p['chars'].get(text['fontId'],{}).get('font',{}).get('italic',False),name=name,x=bounds[0],y=bounds[1],w=bounds[2]-bounds[0],h=bounds[3]-bounds[1],font='font/'+p['name'].lower()+'/'+str(text['fontId']));scene['texts'].append(text)
  self.scenes[p['edition']].append(scene)
 def plan_intros(self):
  p=self.package('one')
  self.scene(p,'preloader',0,1);self.scene(p,'preloader-progress',984,1);self.scene(p,'loaded',0,4)
  self.scene(p,'loaded-lead',0,3)
  self.scene(p,'intro-lead',0,6)
  self.scene(p,'intro-background',0,7,exclude_ids=(1008,))
  holders=[l for l in self.frames(p,1008)[0]['layers'] if l['id']==999]
  firechildren=self.frames(p,999)[0]['layers']
  for holder in holders:
   for child in firechildren:
    key=f'nested/intro/{holder["depth"]}/{child["depth"]}'
    layer={'id':589,'matrix':multiply(holder['matrix'],child['matrix']),'animate':True,'filterSource':[999,1,child['depth']]}
    jobname=f'intro-fire-{holder["depth"]}-{child["depth"]}'
    self.clip(p,key,'idle',589,1,31,layers=[layer],name=jobname,loop=False)
    job=self.jobs[pathlib.Path(p['path']).stem][jobname];b=job['bounds'];job['bounds']=[b[0]-50,b[1]-50,b[2]+50,b[3]+50]
  for frame in range(1,161):
   self.scene(p,'intro/'+str(frame),1008,frame,exclude_ids=(999,),timeline_children=True,deduplicate_variants=True)
   scene=self.scenes[1][-1]
   for layer in scene['layers']:
    layer['depth']*=32
    if layer.get('clipDepth',0)>0:layer['clipDepth']*=32
   for holder in self.frames(p,1008)[frame-1]['layers']:
    if holder['id']!=999:continue
    for child in firechildren:
     scene['layers'].append({'key':f'nested/intro/{holder["depth"]}/{child["depth"]}','action':'idle','name':'','x':0,'y':0,'scaleX':1,'scaleY':1,'skewX':0,'skewY':0,'rotation':0,'alpha':1,'depth':holder['depth']*32+child['depth'],'frame':holder['time'],'clock':'random-fire','birthFrame':frame-1-holder['time']})
   scene['layers'].sort(key=lambda x:x['depth'])
  for name,key,frame in [('PRELOADER','preloader',1),('SPONSOR','sponsor-initial',1),('SPONSOR','sponsor',2)]:self.scene(self.package(name),key,0,frame)
  sponsor=self.package('SPONSOR');stem=pathlib.Path(sponsor['path']).stem
  for job in self.jobs[stem].values():
   if job['id']==58:job['stoppedChildren']=[{'id':57,'frame':14}]
  for clip in self.clips[2]:
   if clip['source']['swf']==pathlib.Path(sponsor['path']).name and clip['source']['characterId']==58:
    clip['loop']=False;clip['source']['stoppedChildren']=[{'id':57,'frame':14}]
    clip['events']=[{'frame':1,'type':'sound','value':'sponsor-stream'},{'frame':185,'type':'complete','value':'author'}]
  author=self.package('AUTHOR')
  for frame in range(1,67):self.scene(author,'author/'+str(frame),0,frame,deduplicate_variants=True)
 def plan_one(self):
  p=self.package('one');chars=p['chars'];actions=json.loads((ROOT/'scripts/age-of-war/one-actions.json').read_text())
  for i,unit in enumerate(actions['units'],1):
   uf=self.frames(p,687)[i-1];holder=next(l for l in uf['layers'] if l['name']=='anims');base= multiply([1,0,0,1,0,-.1],holder['matrix'])
   for action,cid in unit.items():
    state=next(f for f in self.frames(p,holder['id']) if action in f['labels']);layers=[]
    for layer in uf['layers']:
     if layer['name']=='anims':
      for child in state['layers']:
       obj={'id':child['id'],'matrix':multiply(base,child['matrix']),'animate':True}
       if 'color' in child:obj['color']=child['color']
       if holder.get('filters'):obj['filterSource']=[687,i,holder['depth']]
       layers.append(obj)
     else:layers.append({'id':layer['id'],'matrix':multiply([1,0,0,1,0,-.1],layer['matrix'])})
    ev=[{'frame':e[0]-1,'type':e[1],'value':str(e[2])+','+str(e[3])} for e in actions['timelines'][str(cid)]['events']]
    self.clip(p,f'unit/{i}',action,cid,layers=layers,name=f'unit-{i}-{action}',loop=action not in ['die'],events=ev)
    if i==15 and action=='die':
     for suffix,minimum,maximum in [('back',0,11),('front',13,65535)]:
      partlayers=layers if suffix=='back' else [l for l in layers if l['id']==cid]
      name=f'unit-15-die-{suffix}';self.clip(p,'unit/15','die-'+suffix,cid,layers=partlayers,name=name,loop=False,events=ev)
      mask={'id':685,'min':minimum,'max':maximum};self.jobs[pathlib.Path(p['path']).stem][name]['depthMask']=mask;self.clips[1][-1]['source']['depthMask']=mask
  for i,cid in enumerate(actions['turrets'],1):
   layers=[dict(id=l['id'],matrix=multiply([1,0,0,1,-.05,0],l['matrix']),animate=l['name']=='anim') for l in self.frames(p,881)[i-1]['layers']]
   self.clip(p,f'turret/{i}','idle',cid,layers=layers,name=f'turret-{i}',loop=False,events=[{'frame':e[0]-1,'type':e[1],'value':str(e[2])+','+str(e[3])} for e in actions['timelines'][str(cid)]['events']])
  # Base architecture lives in sprite8; slot extensions are independently instantiated.
  for age in range(1,6):self.clip(p,f'base/{age}','idle',8,age,age)
  for cid in [14,20,26]:
   for age in range(1,6):self.clip(p,f'base-extension/{[14,20,26].index(cid)+1}/{age}','idle',cid,age,age)
  for c in p['characters']:
   export=c['export'];key=None
   if export.startswith('bulletspecial'):key='projectile/special'+export[13:]
   elif export.startswith('bullet'):key='projectile/'+export[6:]
   elif export.startswith('part'):key='part/'+export[4:]
   elif export.startswith('special'):key='special/'+export[7:]
   if key:
    if key in ['projectile/10','projectile/11','projectile/12','special/4']:
     child={'projectile/10':872,'projectile/11':175,'projectile/12':175,'special/4':971}[key]
     self.clip(p,key,'idle',child,layers=[{'id':c['id'],'matrix':IDENTITY,'animate':True}],name='nested-cycle-'+str(c['id']),loop=True)
    elif key=='part/1':
     coinlayers=[dict(id=l['id'],matrix=l['matrix'],animate=True) for l in self.frames(p,961)[0]['layers'] if l['id'] not in [950,951]]
     self.clip(p,key,'idle',960,layers=coinlayers,name='reward-coins',loop=True)
    else:self.labeled(p,key,c['id'])
  self.clip(p,'background/1','idle',50,1,1)
  self.scene(p,'background',50)
  for frame,key in [(8,'menu'),(9,'instructions'),(10,'options'),(11,'credits'),(12,'difficulty'),(14,'victory'),(15,'defeat')]:self.scene(p,key,0,frame)
  self.scene(p,'battle-ui',212);self.scene(p,'paused',965);self.scene(p,'build-slots',46)
  self.scene(p,'one-base-enemy',33);self.scene(p,'one-cursor',885);self.scene(p,'one-reward',961)
  for age in range(1,6):self.scene(p,f'one-special/{age}',78,age)
  charge=next(l for l in self.frames(p,78)[0]['layers'] if l['id']==69)
  self.clip(p,'special-charge','idle',69,layers=[{'id':69,'matrix':IDENTITY,'color':charge['color'],'animate':True}],name='special-charge',loop=False)
  for age in range(1,6):
   self.scene(p,f'one-units/{age}',142,age)
   self.scene(p,f'one-turrets/{age}',177,age)
  for frame in range(1,len(self.frames(p,189))+1):self.scene(p,f'one-main/{frame}',189,frame)
  self.clip(p,'aura','idle',698,1,1)
  for parent,depth,unit in [(607,1,12),(607,3,12),(685,12,15),(894,3,0),(896,2,0),(933,2,0)]:
   source=next(l for f in self.frames(p,parent) for l in f['layers'] if l['depth']==depth and l['id']==589)
   matrix=source['matrix']
   if unit:
    holder=next(l for l in self.frames(p,687)[unit-1]['layers'] if l['name']=='anims')
    action=next(l for f in self.frames(p,holder['id']) if 'die' in f['labels'] for l in f['layers'] if l['id']==parent)
    matrix=multiply(multiply(multiply([1,0,0,1,0,-.1],holder['matrix']),action['matrix']),matrix)
   self.clip(p,f'nested/{parent}/{depth}','idle',589,layers=[{'id':589,'matrix':matrix,'animate':True}],name=f'nested-{parent}-{depth}')
  for cid,part in [(949,2),(948,8)]:
   for variant in range(12):
    layers=[dict(id=l['id'],matrix=l['matrix'],frame=variant+1 if l['id']==946 else 1) for l in self.frames(p,cid)[0]['layers']]
    self.clip(p,f'part/{part}','variant'+str(variant),cid,1,1,layers=layers,name=f'part-{part}-variant-{variant}')
  # All in-game button/state timelines used by the native host, including their original age icons.
  for cid in [54,78,142,177,189,195,196,205,211,38,39,40,41,42,43,44,45,885]:
   self.labeled(p,'ui/one/'+str(cid),cid) if not any(c['key']=='ui/one/'+str(cid) for c in self.clips[1]) else None
 def plan_two(self):
  for p in self.packages.values():
   if p['edition']!=2:continue
   name=p['name']
   if re.fullmatch(r'AGE\d_UNITS',name):
    for c in p['characters']:
     for n in c['classes']:
      if re.fullmatch(r'u\d+',n):self.labeled(p,'unit/'+n[1:],c['id'])
   elif re.fullmatch(r'AGE\d_TURRETS',name):
    for c in p['characters']:
     for n in c['classes']:
      if not re.fullmatch(r't\d+',n):continue
      root=self.frames(p,c['id'])[0]['layers'];cannon=next(l for l in root if l['name']=='cannon')
      layers=[dict(id=l['id'],matrix=l['matrix'],animate=True) for l in root if l['name']=='cannon']
      self.labeled(p,'turret/'+n[1:],cannon['id'],layers,'turret-'+n[1:])
      base=[dict(id=l['id'],matrix=l['matrix']) for l in root if l['name']=='base']
      self.clip(p,'turret/'+n[1:],'base',c['id'],1,1,layers=base,name='turret-'+n[1:]+'-base')
      self.scene(p,'turret-sell/'+n[1:],c['id'],exclude=['base','cannon'])
      button=next(l for l in root if l['name']=='btnSell')
      self.scene(p,'turret-sell-control/'+n[1:],button['id'])
      for frame,state in enumerate(['out','over','down'],1):self.scene(p,'turret-sell-control/'+n[1:]+'/'+state,button['id'],frame)
   elif name in ['PROJECTILES','PARTICULES']:
    if name=='PARTICULES':
     for key,instance in [('damage-normal','damageNorm'),('damage-critical','damageCrit')]:
      self.scene(p,key,34)
      layer=next(l for l in self.frames(p,35)[0]['layers'] if l['name']==instance)
      for text in self.scenes[2][-1]['texts']:
       text['x']+=layer['matrix'][4];text['y']+=layer['matrix'][5];text['filters']=layer.get('filters',[])
    for c in p['characters']:
     for n in c['classes']:
      if '.' in n:continue
      key=('projectile/'+n) if name=='PROJECTILES' else ('special/'+n[7:] if n.startswith('special') else 'part/'+n)
      self.labeled(p,key,c['id'])
   elif name=='BASES':
    # The four tower extensions are dynamic in original Base; only the base body is baked here.
    for age,f in enumerate(self.frames(p,59),1):
     body=[dict(id=l['id'],matrix=l['matrix']) for l in f['layers'] if l['name'] not in ['t1','t2','t3','t4']]
     self.clip(p,f'base/{age}','idle',59,age,age,layers=body,name=f'base-{age}')
     for index,cid in enumerate([13,24,35,46],1):self.clip(p,f'base-extension/{index}/{age}','idle',cid,age,age)
    self.scene(p,'build-slots',72,exclude=['base1','base2']);self.scene(p,'build-slot-control',71)
    for frame,state in enumerate(['out','over','down'],1):self.scene(p,'build-slot-control/'+state,71,frame)
   elif name=='BACKGROUNDS':
    for age in range(1,8):self.clip(p,f'background/{age}','idle',11,age,age)
    self.scenes[2].append({'key':'background','width':720,'height':480,'layers':[{'key':'background/1','action':'idle','name':'background','x':0,'y':0,'scaleX':1,'scaleY':1,'alpha':1,'depth':0,'frame':0}],'hotspots':[],'texts':[]})
   elif name in ['MAINMENU','DIFFICULTY','VICTORY','DEFEAT','CREDITS','OPTIONS']:
    key={'MAINMENU':'menu','DIFFICULTY':'difficulty','VICTORY':'victory','DEFEAT':'defeat','CREDITS':'credits','OPTIONS':'options'}[name];self.scene(p,key)
   elif name=='HUD':
    self.scene(p,'battle-ui',332,exclude=['pauseScreen','tutorial','errorPopUp']);self.scene(p,'paused',322);self.scene(p,'instructions',331)
    self.scene(p,'status-progress',346);self.scene(p,'status-queue',341);self.scene(p,'error-popup',8)
    self.clip(p,'ui/hud/74','idle',74,loop=False)
    # Expand each original age's interactive button arrangement for the host.
    for age in range(1,len(self.frames(p,284))+1):self.scene(p,f'buttons/{age}',284,age)
    for c in p['characters']:
     if c['classes'] and c['id']!=332:self.labeled(p,'ui/hud/'+str(c['id']),c['id'])
   elif name=='WINDOWS':
    self.scene(p,'window/sellTurretInfo/onT',40)
    for c in p['characters']:
     for n in c['classes']:
      if '.' not in n:self.scene(p,'window/'+n,c['id'])
 def render(self):
  tasks=[];renderer=digest(ROOT/'scripts/age-of-war/AowSwfExport.java')
  for stem,jobs in self.jobs.items():
   jobfile=self.cache/'jobs'/(stem+'.json');write_json(jobfile,list(jobs.values()));p=self.packages[stem]
   recipes=self.raw/stem/'recipes.json';previous=json.loads(recipes.read_text()) if recipes.exists() else {}
   source=digest(p['path']);signatures={name:hashlib.sha256((source+renderer+json.dumps(job,sort_keys=True,separators=(',',':'))).encode()).hexdigest() for name,job in jobs.items()}
   required=[job for name,job in jobs.items() if not (self.args.skip_render and previous.get(name)==signatures[name] and (self.raw/stem/name/'frames.json').exists() and all((self.raw/stem/name/(str(f)+'.png')).exists() for f in job['frames']))]
   renderfile=self.cache/'render-jobs'/(stem+'.json');write_json(renderfile,required)
   tasks.append((stem,p,renderfile,signatures,recipes,bool(required)))
  tasks.sort(key=lambda x:('UNITS' in x[0] or 'TURRETS' in x[0],x[0]))
  def run(t):
   stem,p,j,signatures,recipes,required=t;out=self.raw/stem;log=self.cache/(stem+'.log')
   if required:
    with log.open('w') as stream:r=subprocess.run([self.java,'-Xmx2g','-Djava.awt.headless=true','-cp',self.cp,'AowSwfExport','render',p['path'],str(j),str(out)],stdout=stream,stderr=subprocess.STDOUT)
    if r.returncode:raise RuntimeError(str(log)+'\n'+log.read_text()[-3000:])
   write_json(recipes,signatures)
   return stem+(' rendered' if required else ' cached (verified recipe)')
  with concurrent.futures.ThreadPoolExecutor(max_workers=self.args.workers) as pool:
   for result in pool.map(run,tasks):print(result,flush=True)
 def originals(self):
  archive=ROOT/'scripts/age-of-war/source-art.zip'
  with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
   bases=[self.src/'analysis/aow1/export',self.src/'analysis/aow2/outer',self.src/'analysis/aow2/embedded']
   for base in bases:
    if not base.exists():continue
    for p in sorted(base.rglob('*')):
     if p.is_file() and (p.suffix.lower() in ['.svg','.png','.jpg','.jpeg','.gif','.bmp','.wav','.mp3','.ttf','.as','.txt'] or 'symbolClass' in p.parts):
      rel=p.relative_to(self.src/'analysis');info=zipfile.ZipInfo(str(rel));info.date_time=(2026,10,30,0,0,0);info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,normalize_ttf(p.read_bytes()) if p.suffix.lower()=='.ttf' else p.read_bytes())
   sponsor=self.src/'analysis/aow2/supplemental/sponsor-stream-1.wav'
   info=zipfile.ZipInfo('aow2/supplemental/sponsor-stream-1.wav');info.date_time=(2026,10,30,0,0,0);info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,sponsor.read_bytes())
   intro=self.src/'analysis/aow1/supplemental/intro-stream-1008.wav'
   info=zipfile.ZipInfo('aow1/supplemental/intro-stream-1008.wav');info.date_time=(2026,10,30,0,0,0);info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,intro.read_bytes())
  for edition in [1,2]:
   folder=self.output/'AgeOfWar'/('One' if edition==1 else 'Two');folder.mkdir(parents=True,exist_ok=True)
   base=self.src/('analysis/aow1/export' if edition==1 else 'analysis/aow2/outer')
   soundpaths=sorted((base/'sounds').glob('*'))
   outputSounds=folder/'Sounds';outputSounds.mkdir(exist_ok=True)
   for old in outputSounds.iterdir():
    if old.suffix.lower() in ['.wav','.mp3','.ogg']:old.unlink()
   if edition==2:soundpaths.append(self.src/'analysis/aow2/supplemental/sponsor-stream-1.wav')
   else:soundpaths.append(self.src/'analysis/aow1/supplemental/intro-stream-1008.wav')
   for p in soundpaths:
    if not p.is_file():continue
    if p.suffix.lower()=='.wav':
     with wave.open(str(p),'rb') as wav:sampleFrames=wav.getnframes()
     if sampleFrames==0:
      self.excluded_sources[edition].append({'source':str(p.relative_to(self.src)),'sha256':digest(p),'sampleFrames':0,'reason':'FFDec empty stream-head export; no playable samples'})
      continue
    sourceNumber=re.match(r'(\d+)',p.stem)
    sourceAudio=next((s for s in self.audio_metadata[edition]['sounds'] if sourceNumber and s['characterId']==int(sourceNumber.group(1))),None)
    outputName=('sound-'+sourceNumber.group(1)+p.suffix if sourceNumber else 'stream-sponsor'+p.suffix) if edition==2 else p.name
    target=folder/'Sounds'/outputName;target.parent.mkdir(exist_ok=True)
    if edition==1 and sourceAudio and sourceAudio['initialLatency']>0 and p.suffix=='.mp3':
     target=target.with_suffix('.wav');provenance=trim_pcm_wav(p,target,sourceAudio['initialLatency'],sourceAudio['sampleCount'])
    elif edition==2 and sourceNumber and int(sourceNumber.group(1)) in FMOD_REJECTED_TWO_SOUND_IDS:
     target=target.with_suffix('.wav');provenance=decode_pcm_wav(p,target)
    else:shutil.copyfile(p,target);provenance={'sha256':digest(p),'sourceSha256':digest(p)}
    number=re.match(r'(\d+)',p.stem);keys=['sound/'+number.group(1),number.group(1)] if number else ['intro-stream' if edition==1 else 'sponsor-stream']
    # Preserve source linkage sound names in addition to numeric original tag IDs.
    if '_' in p.stem:keys.append(p.stem.split('_',1)[1])
    short=re.search(r'_Sound_(.+?)_net\.',p.stem)
    if short:keys.append(short.group(1))
    music=re.search(r'_Music(\d+)_',p.stem)
    if music:keys.append('music/'+music.group(1))
    if edition==1 and p.stem=='1035':keys.append('music/1')
    if number:
     audio=next(s for s in self.audio_metadata[edition]['sounds'] if s['characterId']==int(number.group(1)))
     provenance.update(sampleRate=audio['sampleRate'],channels=audio['channels'],sampleCount=audio['sampleCount'],startSample=0 if 'trimStartSample' in provenance.get('derivation',{}) else audio['initialLatency'] if p.suffix=='.mp3' else 0,sourceAudio=audio)
    else:
     audio=self.audio_metadata[1 if edition==1 else 'sponsor']['streams'][0]
     provenance.update(sampleRate=audio['sampleRate'],channels=audio['channels'],sampleCount=audio['sampleCount'],startSample=audio['seekSamples'],sourceAudio=audio)
    for key in keys:self.sounds[edition].append({'key':key,'resource':str(target.relative_to(self.output).with_suffix('')),'source':str(p.relative_to(self.src)),**provenance})
   fontbases=[('one',base)] if edition==1 else [(d['name'].lower(),self.src/'analysis/aow2/embedded'/stem) for stem,d in self.packages.items() if d['edition']==2]
   for name,fb in fontbases:
    for p in sorted((fb/'fonts').glob('*.ttf')):
     key='font/'+name+'/'+p.stem.split('_')[0];target=folder/'Fonts'/(name+'-'+p.name);target.parent.mkdir(exist_ok=True);target.write_bytes(normalize_ttf(p.read_bytes()))
     self.fonts[edition].append({'key':key,'resource':str(target.relative_to(self.output).with_suffix('')),'sha256':digest(target),'normalizedMetadata':'head timestamps and checkSumAdjustment only'})
     package=next(p for p in self.packages.values() if p['name'].lower()==name);metrics=package['chars'][int(p.stem.split('_')[0])]['font']
     self.fonts[edition][-1].update(metrics)
   for package in self.packages.values():
    if package['edition']!=edition:continue
    for char in package['characters']:
     font=char.get('font')
     if font and font['glyphs']==0:
      self.fonts[edition].append({'key':'font/'+package['name'].lower()+'/'+str(char['id']),'resource':'','device':True,'family':font['family'],'bold':font['bold'],'italic':font['italic'],'portableFallback':'font/one/197' if edition==1 and font['family']=='Arial' else ''})
 def pack(self):
  for edition in [1,2]:
   folder=self.output/'AgeOfWar'/('One' if edition==1 else 'Two');atlasdir=folder/'Atlases';atlasdir.mkdir(parents=True,exist_ok=True)
   for p in atlasdir.glob('*.png'):p.unlink()
   unique={};records={};framecache={}
   for clip in self.clips[edition]:
    job=clip.pop('_job');wanted=clip.pop('_frames');directory=self.raw/job
    if job not in framecache:framecache[job]={f['frame']:f for f in json.loads((directory/'frames.json').read_text())}
    frames=[]
    for index in wanted:
     info=framecache[job][index];path=directory/(str(index)+'.png');image=Image.open(path).convert('RGBA');box=image.getchannel('A').getbbox();empty=box is None
     if empty:box=(0,0,1,1)
     image=image.crop(box);sig=hashlib.sha256(str(image.size).encode()+image.tobytes()).hexdigest()
     unique.setdefault(sig,image)
     record={'_sig':sig,'pixelSha256':sig,'originX':info['originX']-box[0],'originY':info['originY']-box[1],'sourceFrame':index,'ticks':1,'scale':1,'empty':empty}
     if frames and all(record[k]==frames[-1][k] for k in ['_sig','originX','originY','empty']):frames[-1]['ticks']+=1
     else:frames.append(record)
    clip['frames']=frames
   # Deterministic shelf packing with transparent two-pixel gutters, no rotations.
   atlas=None;x=y=row=0;atlases=[];placements={};edge=2048
   def new_atlas():return Image.new('RGBA',(edge,edge),(0,0,0,0))
   def save_atlas(im):
    index=len(atlases);p=atlasdir/f'atlas-{index:03d}.png';im.save(p,compress_level=6);atlases.append({'resource':str(p.relative_to(self.output).with_suffix('')),'width':edge,'height':edge,'sha256':digest(p)})
   for sig,im in sorted(unique.items(),key=lambda item:(-item[1].height,-item[1].width,item[0])):
    w,h=im.size
    if w+4>edge or h+4>edge:
     raise ValueError(f'Original frame {w}x{h} needs larger atlas; max supported is 4096')
    if atlas is None:atlas=new_atlas();x=y=row=2
    if x+w+2>edge:x=2;y+=row+4;row=0
    if y+h+2>edge:save_atlas(atlas);atlas=new_atlas();x=y=2;row=0
    atlas.paste(im,(x,y));placements[sig]={'atlas':len(atlases),'x':x,'y':y,'w':w,'h':h};x+=w+4;row=max(row,h)
   if atlas is not None:save_atlas(atlas)
   for clip in self.clips[edition]:
    for frame in clip['frames']:frame.update(placements[frame.pop('_sig')])
   for scene in self.scenes[edition]:
    for hotspot in scene['hotspots']:
     key=hotspot.pop('_key',None);matrix=hotspot.pop('_matrix',None)
     if key is not None:
      isbutton=hotspot.pop('_button',False);stageclip=hotspot.pop('_stageClip',False)
      clip=next(c for c in self.clips[edition] if c['key']==key and (not isbutton or c['action']=='hit'));f=clip['frames'][0]
      bounds=hotspot.pop('_hitBounds') if isbutton else transformed([-f['originX'],-f['originY'],f['w']-f['originX'],f['h']-f['originY']],matrix)
      hotspot.pop('_hitBounds',None)
      if stageclip:bounds=[max(0,bounds[0]),max(0,bounds[1]),min(scene['width'],bounds[2]),min(scene['height'],bounds[3])]
      hotspot.update(x=bounds[0],y=bounds[1],w=max(0,bounds[2]-bounds[0]),h=max(0,bounds[3]-bounds[1]))
   manifest={'tools':{'ffdecVersion':'26.4.0','ffdecJarSha256':digest(self.args.ffdec),'rendererSha256':digest(ROOT/'scripts/age-of-war/AowSwfExport.java')},'schemaVersion':1,'edition':edition,'stage':{'width':650 if edition==1 else 720,'height':450 if edition==1 else 480,'fps':40 if edition==1 else 30,'scale':1},'sources':self.sources[edition],'atlases':atlases,'clips':self.clips[edition],'sounds':self.sounds[edition],'fonts':self.fonts[edition],'scenes':self.scenes[edition]}
   manifest['excludedSources']=self.excluded_sources[edition];write_json(folder/'manifest.json',manifest)
   print(f'Edition {edition}: {len(unique)} unique images, {len(atlases)} atlases, {len(self.clips[edition])} clips, {len(self.sounds[edition])} sound mappings',flush=True)
 def run(self):
  self.load();self.plan_one();self.plan_two();self.plan_intros()
  for edition in [1,2]:write_json(self.cache/f'planned-scenes-{edition}.json',self.scenes[edition])
  self.render();self.originals();self.pack()
  with zipfile.ZipFile(ROOT/'scripts/age-of-war/source-metadata.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
   for directory in [self.meta,self.cache/'jobs']:
    for source in sorted(directory.glob('*.json')):
     info=zipfile.ZipInfo(directory.name+'/'+source.name);info.date_time=(2026,10,30,0,0,0);info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,source.read_bytes())
def main():
 p=argparse.ArgumentParser();p.add_argument('--output',default=str(ROOT/'Assets/Resources'));p.add_argument('--source',default='/Users/bytedance/Downloads/age-of-war-research-20261030');p.add_argument('--cache',default='/tmp/aow-native');p.add_argument('--ffdec',default='/tmp/aow-tools/ffdec-26.4.0/ffdec.jar');p.add_argument('--java',default='/opt/homebrew/opt/openjdk@17/bin/java');p.add_argument('--workers',type=int,default=3);p.add_argument('--skip-render',action='store_true');Converter(p.parse_args()).run()
if __name__=='__main__':main()
