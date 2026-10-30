import argparse, importlib.util, json, pathlib, sys, unittest, zipfile

ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/age-of-war'))
import convert

class SourceSceneMetadataTests(unittest.TestCase):
 def test_scene_preserves_original_mask_depth(self):
  with zipfile.ZipFile(ROOT/'scripts/age-of-war/source-metadata.zip') as archive:
   package=json.loads(archive.read('metadata/age-of-war-1-7k7k.json'))
  package.update(name='one',edition=1,path='one.swf');package['chars']={c['id']:c for c in package['characters']}
  converter=convert.Converter(argparse.Namespace(output='unused',source='unused',cache='unused',java='java',ffdec='ffdec.jar'))
  converter.scene(package,'intro',1008,1)
  mask=next(l for l in converter.scenes[1][0]['layers'] if l['depth']==2)
  self.assertEqual(80,mask.get('clipDepth'))
 def test_dynamic_text_keeps_the_actual_placed_instance_filters(self):
  with zipfile.ZipFile(ROOT/'scripts/age-of-war/source-metadata.zip') as archive:
   name=next(n for n in archive.namelist() if n.startswith('metadata/81_') and n.endswith('.json'))
   package=json.loads(archive.read(name))
  package.update(name='HUD',edition=2,path='81_HUD.swf')
  package['chars']={c['id']:c for c in package['characters']}
  converter=convert.Converter(argparse.Namespace(output='unused',source='unused',cache='unused',java='java',ffdec='ffdec.jar'))
  converter.scene(package,'battle-ui',332)
  texts={t['name']:t for t in converter.scenes[2][0]['texts']}
  source=package['chars'][332]['timeline']['frames'][0]['layers']
  filtered=[l for l in source if l.get('filters') and 'text' in package['chars'][l['id']]]
  self.assertEqual({'base2Hp','base1Hp','moneyTXT','xpTXT','popTXT'},{l['name'] for l in filtered})
  for layer in filtered:self.assertEqual(layer['filters'],texts[layer['name']].get('filters'),layer['name'])

if __name__=='__main__':unittest.main()
