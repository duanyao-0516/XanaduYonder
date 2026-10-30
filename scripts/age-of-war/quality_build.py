#!/usr/bin/env python3
"""Offline nominal Flash LOW/MEDIUM/HIGH candidates, all at original 1x stage scale.

Only the build-time FFDec copy is patched. No Flash implementation ships in Unity.
The fixed FFDec source patch switches every SerializableImage graphics context,
including filtered intermediate images, to non-antialiased rendering for LOW.
"""
import argparse, concurrent.futures, copy, hashlib, json, pathlib, shutil, subprocess, urllib.request, zipfile
from convert import Converter, ROOT, digest, write_json

SOURCE_URL='https://raw.githubusercontent.com/jindrapetrik/jpexs-decompiler/version26.4.0/libsrc/ffdec_lib/src/com/jpexs/helpers/SerializableImage.java'
SOURCE_SHA256='11e5116b74674b23e039ff2f0c48f4d8dc0ba2a80d575b99cf19cfe292652ec3'
MODES={'Low':(False,1),'Medium':(True,2),'High':(True,4)}

def prepare_renderer(args):
 cache=pathlib.Path(args.cache);cache.mkdir(parents=True,exist_ok=True)
 source=cache/'upstream/SerializableImage.java';source.parent.mkdir(parents=True,exist_ok=True)
 if not source.exists():source.write_bytes(urllib.request.urlopen(SOURCE_URL).read())
 if digest(source)!=SOURCE_SHA256:raise ValueError('Unexpected upstream SerializableImage source hash')
 original=source.read_text()
 needle='RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON'
 if original.count(needle)!=1:raise ValueError('Unexpected FFDec graphics-context implementation')
 patched=original.replace(needle,'RenderingHints.KEY_ANTIALIASING, Boolean.getBoolean("aow.noAntialias") ? RenderingHints.VALUE_ANTIALIAS_OFF : RenderingHints.VALUE_ANTIALIAS_ON')
 java=cache/'java';classes=cache/'classes';java.mkdir(exist_ok=True);classes.mkdir(exist_ok=True)
 (java/'SerializableImage.java').write_text(patched)
 exporter=(ROOT/'scripts/age-of-war/AowSwfExport.java').read_text().replace('public class AowSwfExport','public class AowQualityExport')
 needle='parentColor,null,1.0,true,1)'
 if exporter.count(needle)!=1:raise ValueError('Unexpected raster entry point')
 exporter=exporter.replace(needle,'parentColor,null,1.0,Boolean.parseBoolean(a[4]),Integer.parseInt(a[5]))')
 (java/'AowQualityExport.java').write_text(exporter)
 subprocess.run([str(pathlib.Path(args.java).with_name('javac')),'-cp',args.ffdec,'-d',str(classes),str(java/'SerializableImage.java'),str(java/'AowQualityExport.java')],check=True)
 jar=cache/'ffdec-quality.jar'
 library=pathlib.Path(args.ffdec).parent/'lib/ffdec_lib.jar'
 with zipfile.ZipFile(library) as src,zipfile.ZipFile(jar,'w',zipfile.ZIP_DEFLATED) as dest:
  for entry in src.infolist():
   if entry.filename.startswith('META-INF/') and entry.filename.rsplit('.',1)[-1].upper() in ['SF','RSA','DSA','EC']:continue
   if entry.filename=='com/jpexs/helpers/SerializableImage.class':continue
   dest.writestr(entry,src.read(entry))
  for p in sorted(classes.rglob('*.class')):
   info=zipfile.ZipInfo(str(p.relative_to(classes)));info.date_time=(2026,10,30,0,0,0);info.compress_type=zipfile.ZIP_DEFLATED;dest.writestr(info,p.read_bytes())
 return jar,{'upstreamUrl':SOURCE_URL,'upstreamSha256':digest(source),'upstreamJarSha256':digest(args.ffdec),'upstreamLibrarySha256':digest(library),'patchedSourceSha256':digest(java/'SerializableImage.java'),'exporterSha256':digest(java/'AowQualityExport.java'),'patchedJarSha256':digest(jar)}

def build(args):
 jar,provenance=prepare_renderer(args)
 c=Converter(args);c.load();c.plan_one();c.plan_two();c.plan_intros()
 base=json.loads((pathlib.Path(args.base)/'AgeOfWar/Two/manifest.json').read_text())
 c.fonts[2]=base['fonts'];c.sounds[2]=base['sounds'];c.excluded_sources[2]=base.get('excludedSources',[])
 jobs=[(stem,p,c.jobs[stem]) for stem,p in c.packages.items() if p['edition']==2 and stem in c.jobs]
 jobs.sort(key=lambda t:('UNITS' in t[0] or 'TURRETS' in t[0],t[0]))
 for quality in args.modes:
  smoothing,aa=MODES[quality];cache=pathlib.Path(args.cache)/quality;cache.mkdir(parents=True,exist_ok=True)
  def render(item):
   stem,p,planned=item;out=cache/'raw'/stem;out.mkdir(parents=True,exist_ok=True)
   fingerprint=hashlib.sha256((provenance['patchedJarSha256']+digest(p['path'])+quality+json.dumps(list(planned.values()),sort_keys=True)).encode()).hexdigest()
   recipe=out/'recipe.sha256'
   if args.skip_render and recipe.exists() and recipe.read_text()==fingerprint:return stem+' cached'
   jobfile=cache/'jobs'/(stem+'.json');write_json(jobfile,list(planned.values()))
   log=cache/(stem+'.log')
   with log.open('w') as stream:
    result=subprocess.run([args.java,'-Xmx2g','-Djava.awt.headless=true','-Daow.noAntialias='+str(not smoothing).lower(),'-cp',str(jar)+':'+str(pathlib.Path(args.ffdec).parent/'lib/*'),'AowQualityExport','render',p['path'],str(jobfile),str(out),str(smoothing).lower(),str(aa)],stdout=stream,stderr=subprocess.STDOUT)
   if result.returncode:raise RuntimeError(str(log)+'\n'+log.read_text()[-3000:])
   recipe.write_text(fingerprint);return stem+' rendered'
  with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
   for result in pool.map(render,jobs):print(quality,result,flush=True)
  q=copy.deepcopy(c);q.raw=cache/'raw';q.output=cache/'packed';q.clips[1]=[];q.scenes[1]=[]
  q.pack()
  packed=q.output/'AgeOfWar/Two';manifest=json.loads((packed/'manifest.json').read_text())
  # Hit geometry and runtime text are source semantics, not AA-dependent crop bounds.
  manifest['scenes']=copy.deepcopy(base['scenes'])
  dest=pathlib.Path(args.output)/'AgeOfWar/Two'
  if quality!='High':dest=dest/'Quality'/quality
  dest.mkdir(parents=True,exist_ok=True)
  for old in (dest/'Atlases').glob('*.png'):old.unlink()
  shutil.copytree(packed/'Atlases',dest/'Atlases',dirs_exist_ok=True)
  for atlas in manifest['atlases']:atlas['resource']=str((dest/'Atlases'/pathlib.Path(atlas['resource']).name).relative_to(args.output))
  manifest['quality']={'name':quality,'coverageGrid':aa,'bitmapSmoothingAllowed':smoothing,'stagePixelScale':1,'renderer':provenance,'referenceStatus':'Nominal source quality semantics; original-player pixel comparison still required.'}
  write_json(dest/'manifest.json',manifest)
  print(quality,'READY',len(manifest['atlases']),'atlases',sum(p.stat().st_size for p in (dest/'Atlases').glob('*.png')),'PNG bytes',flush=True)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--output',default='/tmp/aow-native/quality-staging');p.add_argument('--source',default='/tmp/aow-native/reproduction-input');p.add_argument('--cache',default='/tmp/aow-native/quality-build');p.add_argument('--base',default=str(ROOT/'Assets/Resources'));p.add_argument('--ffdec',default='/tmp/aow-tools/ffdec-26.4.0/ffdec.jar');p.add_argument('--java',default='/opt/homebrew/opt/openjdk@17/bin/java');p.add_argument('--workers',type=int,default=3);p.add_argument('--skip-render',action='store_true');p.add_argument('--modes',nargs='+',choices=MODES,default=list(MODES));build(p.parse_args())
