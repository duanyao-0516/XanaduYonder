#!/usr/bin/env python3
"""Validate exported native pixels against the manifest, never accept silent fallback art."""
import argparse, collections, hashlib, json, math, pathlib, wave, zipfile
from PIL import Image
from convert import HASHES, SOURCE_HASHES

def validate(root):
 result={}
 archive=zipfile.ZipFile(pathlib.Path(__file__).with_name('source-art.zip'))
 for edition,name in [(1,'One'),(2,'Two')]:
  manifest=json.loads((root/'AgeOfWar'/name/'manifest.json').read_text())
  assert manifest['schemaVersion']==1 and manifest['edition']==edition
  assert manifest['sources'][0]['sha256']==HASHES[edition]
  assert len(manifest['sources'])==(1 if edition==1 else 31)
  assert {s['name']:s['sha256'] for s in manifest['sources']}==SOURCE_HASHES[name],'Source catalog differs from fixed original binary-data payloads'
  clips={(c['key'],c['action']):c for c in manifest['clips']}
  assert len(clips)==len(manifest['clips']),'Duplicate clip/action'
  for prefix,count in [('unit',16 if edition==1 else 29),('turret',15 if edition==1 else 17),('base',5 if edition==1 else 7)]:
   assert all((f'{prefix}/{i}','idle') in clips for i in range(1,count+1)),f'Missing {prefix} catalog entry'
  grouped=collections.defaultdict(dict)
  for c in clips.values():
   assert c['frames'] and sum(f['ticks'] for f in c['frames'])==c['source']['lastFrame']-c['source']['firstFrame']+1
   for f in c['frames']:
    assert f['w']>0 and f['h']>0 and f['ticks']>0
    assert all(math.isfinite(f[x]) for x in ['originX','originY','scale']) and f['scale']>0
    grouped[f['atlas']][(f['x'],f['y'],f['w'],f['h'])]=f
  for index,atlas in enumerate(manifest['atlases']):
   path=root/(atlas['resource']+'.png');assert hashlib.sha256(path.read_bytes()).hexdigest()==atlas['sha256']
   with Image.open(path) as im:
    im=im.convert('RGBA');assert im.size==(atlas['width'],atlas['height'])
    assert im.width<=4096 and im.height<=4096 and im.width%4==im.height%4==0
    for (x,y,w,h),f in grouped[index].items():
     assert 0<=x and 0<=y and x+w<=im.width and y+h<=im.height
     crop=im.crop((x,y,x+w,y+h));pixelhash=hashlib.sha256(str(crop.size).encode()+crop.tobytes()).hexdigest()
     assert pixelhash==f['pixelSha256'],f'Pixel corruption {name} atlas{index} rect{x,y,w,h}'
     assert (crop.getchannel('A').getbbox() is None)==f['empty'],'Alpha classification mismatch'
  for scene in manifest['scenes']:
   for layer in scene['layers']:assert (layer['key'],layer['action']) in clips,(scene['key'],layer['key'],layer['action'])
   for h in scene['hotspots']:assert h['w']>=0 and h['h']>=0
  audio=set();derived=set()
  for sound in manifest['sounds']:
   matches=[p for p in root.glob(sound['resource']+'.*') if p.suffix.lower() in ['.wav','.mp3','.ogg']];assert len(matches)==1,sound['resource']
   assert hashlib.sha256(matches[0].read_bytes()).hexdigest()==sound['sha256'];audio.add(sound['resource'])
   source_bytes=archive.read(sound['source'].removeprefix('analysis/'))
   assert hashlib.sha256(source_bytes).hexdigest()==sound.get('sourceSha256',sound['sha256'])
   if 'derivation' in sound:
    derived.add(sound['resource']);d=sound['derivation']
    with wave.open(str(matches[0]),'rb') as wav:
     assert wav.getframerate()==d['sampleRate'] and wav.getnchannels()==d['channels'] and wav.getnframes()==d['sampleFrames']
     assert wav.getsampwidth()==2
     assert hashlib.sha256(wav.readframes(wav.getnframes())).hexdigest()==d['pcmSha256']==d.get('sourceSlicePcmSha256',d['sourceDecodedPcmSha256'])
     if 'trimStartSample' in d:
      assert d['decodedPcmSha256']==d['sourceDecodedPcmSha256']
      assert d['trimEndSampleExclusive']-d['trimStartSample']==d['sampleFrames']==sound['sourceAudio']['sampleCount']
      assert d['trimStartSample']==sound['sourceAudio']['initialLatency']
  assert len(audio)==(28 if edition==1 else 55),'Missing original audio source'
  for source in manifest.get('excludedSources',[]):
   assert hashlib.sha256(archive.read(source['source'].removeprefix('analysis/'))).hexdigest()==source['sha256']
   assert source['sampleFrames']==0
  for font in manifest['fonts']:
   if font.get('device'):continue
   matches=list(root.glob(font['resource']+'.ttf'));assert len(matches)==1
   assert hashlib.sha256(matches[0].read_bytes()).hexdigest()==font['sha256']
  result[name]={'clips':len(clips),'sourceFrames':sum(sum(f['ticks'] for f in c['frames']) for c in clips.values()),'uniqueAtlasRects':sum(map(len,grouped.values())),'atlases':len(manifest['atlases']),'originalAudioSources':len(audio),'originalAudioFiles':len(audio)-len(derived),'derivedAudioFiles':len(derived),'soundAliases':len(manifest['sounds']),'scenes':len(manifest['scenes']),'sourceFiles':len(manifest['sources'])}
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--resources',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parents[2]/'Assets/Resources');p.add_argument('--report',type=pathlib.Path);a=p.parse_args();r=validate(a.resources);text=json.dumps(r,indent=2);print(text)
 if a.report:a.report.write_text(text+'\n')
