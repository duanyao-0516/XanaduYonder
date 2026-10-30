#!/usr/bin/env python3
"""Check three actual vector-raster candidates against the same original catalog."""
import argparse, hashlib, json, pathlib
from PIL import Image

def validate(root):
 manifests={q:json.loads((root/'AgeOfWar/Two'/('manifest.json' if q=='High' else 'Quality/'+q+'/manifest.json')).read_text()) for q in ['Low','Medium','High']}
 high=manifests['High'];reference={(c['key'],c['action']):c for c in high['clips']};reports={};samples={}
 for quality,m in manifests.items():
  assert m['sources']==high['sources'] and m['sounds']==high['sounds'] and m['fonts']==high['fonts']
  assert m['scenes']==high['scenes'] and m['stage']==high['stage']
  clips={(c['key'],c['action']):c for c in m['clips']};assert set(clips)==set(reference)
  images={};rects=set();bytes_=0
  for index,a in enumerate(m['atlases']):
   path=root/(a['resource']+'.png');assert hashlib.sha256(path.read_bytes()).hexdigest()==a['sha256']
   image=Image.open(path).convert('RGBA');assert image.size==(a['width'],a['height']);assert image.width<=4096 and image.height<=4096;assert image.width%4==image.height%4==0
   images[index]=image;bytes_+=path.stat().st_size
  for key,c in clips.items():
   ref=reference[key];assert c['source']==ref['source'] and c['events']==ref['events'] and c['loop']==ref['loop'],key
   assert sum(f['ticks'] for f in c['frames'])==sum(f['ticks'] for f in ref['frames']),key
   source=c['source']['firstFrame']
   for f in c['frames']:
    assert f['sourceFrame']==source and f['scale']==1;source+=f['ticks']
    rect=(f['atlas'],f['x'],f['y'],f['w'],f['h']);im=images[f['atlas']]
    assert f['x']>=0 and f['y']>=0 and f['x']+f['w']<=im.width and f['y']+f['h']<=im.height
    if rect not in rects:
     crop=im.crop((f['x'],f['y'],f['x']+f['w'],f['y']+f['h']))
     assert hashlib.sha256(str(crop.size).encode()+crop.tobytes()).hexdigest()==f['pixelSha256']
     assert (crop.getchannel('A').getbbox() is None)==f['empty'];rects.add(rect)
  f=clips['unit/2','idle']['frames'][0];im=images[f['atlas']].crop((f['x'],f['y'],f['x']+f['w'],f['y']+f['h']));alpha=list(im.getchannel('A').getdata());partial=sum(0<a<255 for a in alpha)
  samples[quality]=f['pixelSha256'];reports[quality]={'clips':len(clips),'atlases':len(images),'uniqueRects':len(rects),'pngBytes':bytes_,'rgbaGpuBytes':sum(i.width*i.height*4 for i in images.values()),'etc2GpuBytes':sum(i.width*i.height for i in images.values()),'unit2IdlePartialAlpha':partial}
 assert reports['Low']['unit2IdlePartialAlpha']==0, 'LOW still applies vector AA'
 assert reports['Medium']['unit2IdlePartialAlpha']>0 and reports['High']['unit2IdlePartialAlpha']>0
 assert len(set(samples.values()))==3,'Quality candidates did not change original vector pixels'
 return reports

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=pathlib.Path,default=pathlib.Path('/tmp/aow-native/quality-staging'));p.add_argument('--report',type=pathlib.Path);a=p.parse_args();report=validate(a.root);text=json.dumps(report,indent=2);print(text)
 if a.report:a.report.write_text(text+'\n')
