import json, pathlib, unittest, os
ROOT=pathlib.Path(__file__).resolve().parents[2]
RESOURCE_ROOT=pathlib.Path(os.environ.get('AOW_ASSET_ROOT',str(ROOT/'Assets/Resources')))
class ReleaseAssetTests(unittest.TestCase):
 def test_both_originals_have_complete_release_manifests(self):
  expected={1:(range(1,17),range(1,16),range(1,6)),2:(range(1,30),range(1,18),range(1,8))}
  for edition, (units,turrets,bases) in expected.items():
   path=RESOURCE_ROOT/'AgeOfWar'/('One' if edition==1 else 'Two')/'manifest.json'
   self.assertTrue(path.exists(), f'Missing release manifest for original {edition}')
   data=json.loads(path.read_text()); self.assertEqual(1,data['schemaVersion'])
   keys={c['key'] for c in data['clips']}
   for prefix,ids in [('unit',units),('turret',turrets),('base',bases)]:
    self.assertFalse({f'{prefix}/{i}' for i in ids}-keys, f'Missing {prefix} originals')
   self.assertTrue(data['sources']); self.assertTrue(data['atlases'])
   expected_sources=json.loads((ROOT/'scripts/age-of-war/source-hashes.json').read_text())['One' if edition==1 else 'Two']
   self.assertEqual(expected_sources,{s['name']:s['sha256'] for s in data['sources']})
   for source in data['sources']: self.assertEqual(64,len(source['sha256']))
   for c in data['clips']:
    self.assertTrue(c['frames'],c['key']); self.assertTrue(c['source'])
    for frame in c['frames']:
     self.assertGreater(frame['w'],0); self.assertGreater(frame['h'],0)
     atlas=data['atlases'][frame['atlas']]
     self.assertGreaterEqual(frame['x'],0);self.assertGreaterEqual(frame['y'],0)
     self.assertLessEqual(frame['x']+frame['w'],atlas['width'])
     self.assertLessEqual(frame['y']+frame['h'],atlas['height'])
     self.assertIn('originX',frame);self.assertIn('originY',frame)
   for atlas in data['atlases']:
    self.assertEqual(0,atlas['width']%4);self.assertEqual(0,atlas['height']%4)
    self.assertLessEqual(atlas['width'],4096);self.assertLessEqual(atlas['height'],4096)

class TimelineMotionTests(unittest.TestCase):
 def test_original_clubman_walk_does_not_collapse_to_static_frame(self):
  p=ROOT/'Assets/Resources/AgeOfWar/One/manifest.json'
  self.assertTrue(p.exists())
  manifest=json.loads(p.read_text())
  clip=next(c for c in manifest['clips'] if c['key']=='unit/1' and c['action']=='walk')
  self.assertGreater(len({(f['atlas'],f['x'],f['y']) for f in clip['frames']}),10,'Original 43-frame walk lost temporal motion')

class AssetIntegrityTests(unittest.TestCase):
 def load(self,edition):return json.loads((RESOURCE_ROOT/'AgeOfWar'/edition/'manifest.json').read_text())
 def test_every_required_actor_has_visible_original_pixels(self):
  for edition in ['One','Two']:
   m=self.load(edition)
   for c in m['clips']:
    if c['key'].startswith(('unit/','turret/','base/')) and c['action']!='empty' and not c['source'].get('emptyArtwork',False):
     self.assertTrue(any(not f['empty'] for f in c['frames']),f"{edition}/{c['key']}/{c['action']} is entirely invisible")
 def test_menu_transparent_buttons_keep_original_hit_geometry(self):
  m=self.load('One');scene=next(s for s in m['scenes'] if s['key']=='menu');play=next(h for h in scene['hotspots'] if h['id']==1019)
  logo=next(h for h in scene['hotspots'] if h['id']==992);self.assertGreater(logo['w'],100);self.assertGreater(play['w'],100);self.assertGreater(play['h'],20);self.assertGreaterEqual(play['x'],0);self.assertLessEqual(play['x']+play['w'],650)
 def test_clip_identity_and_frame_accounting(self):
  for edition in ['One','Two']:
   m=self.load(edition);keys=[(c['key'],c['action']) for c in m['clips']];self.assertEqual(len(keys),len(set(keys)))
   for c in m['clips']:self.assertEqual(c['source']['lastFrame']-c['source']['firstFrame']+1,sum(f['ticks'] for f in c['frames']),c['key'])
 def test_original_audio_names_resolve(self):
  keys={s['key'] for s in self.load('Two')['sounds']}
  self.assertTrue({'hit4','gun1','lightning','music/1','music/2'}.issubset(keys))
  self.assertIn('sound/1035',{s['key'] for s in self.load('One')['sounds']})
 def test_empty_export_does_not_hide_the_real_original_intro_stream(self):
  m=self.load('One');sounds={s['key']:s for s in m['sounds']}
  self.assertNotIn('sponsor-stream',sounds)
  self.assertIn('intro-stream',sounds)
  excluded=next(s for s in m.get('excludedSources',[]) if s['source'].endswith('/-1.wav'))
  self.assertEqual(0,excluded['sampleFrames'])
 def test_only_fmod_rejected_originals_are_decoded_with_provenance(self):
  sounds={s['key']:s for s in self.load('Two')['sounds']}
  rejected={3,4,6,9,13,17,20,24,32,34,35,47,48,49}
  self.assertEqual(rejected,{int(k.split('/')[1]) for k,s in sounds.items() if k.startswith('sound/') and 'derivation' in s})
  for i in rejected:
   sound=sounds['sound/'+str(i)]
   self.assertEqual(sound['derivation']['pcmSha256'],sound['derivation']['sourceDecodedPcmSha256'])
   self.assertNotEqual(sound['sourceSha256'],sound['sha256'])
 def test_damage_text_retains_original_layout(self):
  scenes={s['key']:s for s in self.load('Two')['scenes']}
  for key in ['damage-normal','damage-critical']:
   field=next(t for t in scenes[key]['texts'] if t['name']=='damageTXT')
   self.assertEqual(1,field.get('letterSpacing'))
   self.assertTrue({'wordWrap','multiline','leftMargin','rightMargin','indent','leading'}.issubset(field))
 def test_dynamic_hud_sources_are_present(self):
  one={(c['key'],c['action']):c for c in self.load('One')['clips']};two={(c['key'],c['action']):c for c in self.load('Two')['clips']}
  self.assertIn(('special-charge','idle'),set(one))
  self.assertEqual(1000,sum(f['ticks'] for f in one['special-charge','idle']['frames']))
  self.assertIn(('ui/hud/74','idle'),set(two))
  self.assertEqual(301,sum(f['ticks'] for f in two['ui/hud/74','idle']['frames']))
 def test_original_positive_latency_sounds_use_exact_sample_windows(self):
  m=self.load('One');sounds={s['key']:s for s in m['sounds']}
  expected={234,235,239,240,241,242,243,269,316,342,382,451,470,591,680,733,745}
  self.assertEqual(expected,{int(k.split('/')[1]) for k,s in sounds.items() if k.startswith('sound/') and 'derivation' in s})
  for i in expected:
   sound=sounds['sound/'+str(i)];d=sound['derivation'];source=sound['sourceAudio']
   self.assertEqual(source['initialLatency'],d['trimStartSample'])
   self.assertEqual(source['sampleCount'],d['sampleFrames'])
   self.assertEqual(source['initialLatency']+source['sampleCount'],d['trimEndSampleExclusive'])
   self.assertEqual(d['pcmSha256'],d['sourceSlicePcmSha256'])
 def test_font_metrics_are_from_original_layout_tags(self):
  expected={'One':{'font/one/197':(927,217,120)},'Two':{'font/windows/2':(1408,256,640),'font/particules/29':(788,236,0)}}
  for edition,values in expected.items():
   fonts={f['key']:f for f in self.load(edition)['fonts']}
   for key,metrics in values.items():
    font=fonts[key];self.assertTrue(font.get('hasLayout'),key)
    self.assertEqual(1024,font['unitsPerEm']);self.assertEqual(metrics,tuple(font[k] for k in ['ascent','descent','leading']))
 def test_hud_dynamic_text_keeps_original_instance_drop_shadow(self):
  scene=next(s for s in self.load('Two')['scenes'] if s['key']=='battle-ui')
  fields={t['name']:t for t in scene['texts']}
  for name in ['base1Hp','base2Hp','moneyTXT','xpTXT','popTXT']:
   filters=fields[name].get('filters',[]);self.assertEqual(1,len(filters),name)
   self.assertEqual('DROPSHADOWFILTER',filters[0]['type'])
   self.assertEqual(10 if name in ['base1Hp','base2Hp'] else 1,filters[0]['strength'])
 def test_original_openings_keep_independent_fire_and_stopped_spark(self):
  one=self.load('One');scenes={s['key']:s for s in one['scenes']}
  for n in range(1,161):
   fires=[l for l in scenes['intro/'+str(n)]['layers'] if l.get('clock')=='random-fire']
   self.assertEqual(26,len(fires));self.assertTrue(all('birthFrame' in l for l in fires))
  two=self.load('Two');scenes={s['key']:s for s in two['scenes']}
  self.assertTrue(all('author/'+str(n) in scenes for n in range(1,67)))
  clip=next(c for c in two['clips'] if c['key']=='ui/sponsor/instance-0-2-2-58/58')
  self.assertFalse(clip['loop']);self.assertEqual(186,sum(f['ticks'] for f in clip['frames']))

class NestedMotionTests(unittest.TestCase):
 def test_static_parent_preserves_original_child_animation_period(self):
  m=json.loads((RESOURCE_ROOT/'AgeOfWar/One/manifest.json').read_text());clips={(c['key'],c['action']):c for c in m['clips']}
  for key,count in [('projectile/10',2),('projectile/11',2),('projectile/12',2),('part/1',40),('special/4',3)]:
   self.assertEqual(count,sum(f['ticks'] for f in clips[key,'idle']['frames']),key)
 def test_war_machine_fire_keeps_original_front_and_back_layers(self):
  m=json.loads((RESOURCE_ROOT/'AgeOfWar/One/manifest.json').read_text());clips={(c['key'],c['action']):c for c in m['clips']}
  self.assertTrue(('unit/15','die-back') in clips and ('unit/15','die-front') in clips)
  self.assertEqual(11,clips['unit/15','die-back']['source']['depthMask']['max'])
  self.assertEqual(13,clips['unit/15','die-front']['source']['depthMask']['min'])
 def test_all_original_turret_sell_overlays_exist(self):
  m=json.loads((RESOURCE_ROOT/'AgeOfWar/Two/manifest.json').read_text());scenes={s['key'] for s in m['scenes']}
  self.assertTrue(all('turret-sell-control/'+str(i) in scenes for i in range(1,18)))
  for state in ['out','over','down']:
   self.assertIn('build-slot-control/'+state,scenes)
   self.assertTrue(all('turret-sell-control/'+str(i)+'/'+state in scenes for i in range(1,18)))
 def test_second_original_build_slots_have_authored_controls(self):
  m=json.loads((RESOURCE_ROOT/'AgeOfWar/Two/manifest.json').read_text());scenes={s['key']:s for s in m['scenes']}
  self.assertIn('build-slots',set(scenes))
  self.assertEqual(4,len(scenes['build-slots']['hotspots']))
  self.assertIn('build-slot-control',set(scenes))

class FontReproductionTests(unittest.TestCase):
 def test_font_export_timestamps_are_normalized_without_changing_glyphs(self):
  import sys, struct
  sys.path.insert(0,str(ROOT/'scripts/age-of-war'))
  import convert
  self.assertTrue(hasattr(convert,'normalize_ttf'),'Reproducible TTF metadata normalization is missing')
  data=next((RESOURCE_ROOT/'AgeOfWar/One/Fonts').glob('*.ttf')).read_bytes()
  records={}
  for i in range(struct.unpack_from('>H',data,4)[0]):
   tag,checksum,offset,length=struct.unpack_from('>4sIII',data,12+i*16);records[tag]=(offset,length)
  head=records[b'head'][0];variant=bytearray(data);struct.pack_into('>QQ',variant,head+20,3900000000,3900000500)
  first=convert.normalize_ttf(data);second=convert.normalize_ttf(bytes(variant))
  self.assertEqual(first,second)
  for tag,(offset,length) in records.items():
   if tag!=b'head':self.assertEqual(data[offset:offset+length],first[offset:offset+length],tag)
  padded=first+b'\0'*((-len(first))%4)
  self.assertEqual(0xB1B0AFBA,sum(struct.unpack('>'+str(len(padded)//4)+'I',padded))&0xffffffff)

if __name__=='__main__':unittest.main()
