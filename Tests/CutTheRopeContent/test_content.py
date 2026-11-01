import hashlib,json,pathlib,re,unittest
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/cut-the-rope/offline'
class ContentTests(unittest.TestCase):
 def test_generated_content_exists(self): self.assertTrue((OUT/'index.html').is_file(),'offline content has not been generated')
 def test_all_ordinary_levels_and_resources(self):
  self.assertTrue((OUT/'manifest.json').is_file(),'missing content manifest')
  m=json.loads((OUT/'manifest.json').read_text());self.assertEqual(m['levelCount'],425)
  for b in range(1,18):
   self.assertEqual(len(json.loads((OUT/f'res/box{b:02}/maps.json').read_text())),25)
   for name in ['bg.png','support.png','cover.png']:self.assertTrue((OUT/f'res/box{b:02}'/name).is_file())
  for f in m['files']:
   p=OUT/f['path'];self.assertTrue(p.is_file(),f['path']);self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest(),f['sha256'],f['path'])
 def test_all_converted_pixels_and_atlas_coordinates(self):
  self.assertTrue((OUT/'manifest.json').is_file(),'missing content manifest')
  records=json.loads((OUT/'manifest.json').read_text())['conversions'];self.assertEqual(len(records),114)
  for record in records:
   image=Image.open(OUT/record['output']).convert('RGBA');self.assertEqual(list(image.size),record['size'])
   if record['codec'].startswith('DXT'):image=image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
   self.assertEqual(hashlib.sha256(image.tobytes()).hexdigest(),record['rgbaSha256'],record['output'])
   atlas=(OUT/record['output']).with_suffix('.json')
   if atlas.is_file():
    data=json.loads(atlas.read_text())
    for sprite in data.get('frames',[]):
     f=sprite['frame'];self.assertLessEqual(f['x']+f['w'],image.width);self.assertLessEqual(f['y']+f['h'],image.height)
 def test_no_portal_scripts_or_remote_endpoints(self):
  self.assertTrue(OUT.is_dir(),'missing offline content')
  for f in OUT.rglob('*'):
   if f.suffix in {'.js','.html','.json'}:
    self.assertNotRegex(f.read_text(),r'https?://|viverse-game-interface|famobi\.com|4399\.com')
  self.assertFalse(list(OUT.rglob('*.dds')));self.assertFalse(list(OUT.rglob('*.avif')))
if __name__=='__main__':unittest.main()
