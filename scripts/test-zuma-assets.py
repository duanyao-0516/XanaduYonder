#!/usr/bin/env python3
"""Importer regression tests: DAT boundaries and real imported source integrity."""
import importlib.util
import json
import pathlib
import os
import subprocess
import tempfile
import struct
import unittest
from PIL import Image

SCRIPT = pathlib.Path(__file__).with_name('import-zuma-assets.py')

class ZumaAssetsTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(SCRIPT.exists(), 'Production Zuma asset importer must exist')
        spec = importlib.util.spec_from_file_location('zuma_importer', SCRIPT)
        self.importer = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.importer)

    def dat(self, points=2, segment=bytes((1, 0, 156, 50)), endpoint=bytes((0, 1))):
        return bytes(16) + struct.pack('<iiff', 0, points, 10, 20) + segment + endpoint

    def test_signed_deltas_and_endpoint_flags_are_preserved(self):
        points = self.importer.parse_dat(self.dat())
        self.assertEqual(len(points), 2)
        self.assertEqual(points[0], {'x':171.0,'y':30.0,'hidden':True,'drawPriority':False})
        self.assertEqual(points[1], {'x':169.5,'y':30.75,'hidden':False,'drawPriority':True})

    def test_malformed_counts_lengths_flags_and_nonfinite_fail(self):
        valid = self.dat()
        cases = [valid[:20], valid[:-1], valid+b'!', self.dat(points=0), self.dat(segment=bytes((2,0,0,0))), bytes(16)+struct.pack('<i',-1), bytes(16)+struct.pack('<iiff',0,2,float('nan'),1)+bytes(6)]
        for data in cases:
            with self.subTest(data=data), self.assertRaises(ValueError):
                self.importer.parse_dat(data)

    def test_content_manifest_has_all_original_resources(self):
        root = pathlib.Path(__file__).resolve().parents[1]
        self.importer.validate(root / 'Assets/Resources/Zuma')

    def test_decoded_music_is_audible_without_saturated_pcm(self):
        root = pathlib.Path(__file__).resolve().parents[1] / 'Assets/Resources/Zuma'
        tracks = json.loads((root / 'manifest.json').read_text())['music']
        for track in tracks:
            with self.subTest(track=track['id']):
                self.assertGreater(track['decodedRms'], 64)
                self.assertLess(track['decodedPeak'], 32767, 'Offline conversion must preserve mix headroom')
                self.assertEqual(self.importer.ogg_samples(root / (track['resource']+'.ogg')), track['loopEndSample'])

    def test_every_shipped_audio_file_decodes_with_unity_fsbtool(self):
        tool = pathlib.Path(os.environ.get('UNITY_FSB_TOOL', '/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/Helpers/FSBTool/arm64/FSBTool'))
        if not tool.is_file():
            self.skipTest('Unity FSBTool unavailable; Editor prebuild still requires all AudioClip assets')
        root = pathlib.Path(__file__).resolve().parents[1] / 'Assets/Resources/Zuma'
        manifest = json.loads((root / 'manifest.json').read_text())
        with tempfile.TemporaryDirectory(prefix='zuma-audio-check-') as temp:
            for entry in manifest['files']:
                if not entry['path'].startswith('Audio/'):
                    continue
                with self.subTest(audio=entry['path']):
                    target = pathlib.Path(temp) / 'decoded.wav'
                    if target.exists(): target.unlink()
                    result = subprocess.run([str(tool), '-i', str(root / entry['path']), '-o', str(target), '-l', str(tool.parent), '-h', temp, '-c', 'pcm', '-C', 'wav', '-R'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
                    self.assertNotIn('ERROR:', result.stdout, result.stdout)
                    self.assertTrue(target.is_file() and target.stat().st_size > 44, 'Unity FSBTool produced no audio: '+result.stdout)

    def test_every_texture_has_etc2_block_aligned_dimensions(self):
        root = pathlib.Path(__file__).resolve().parents[1] / 'Assets/Resources/Zuma'
        manifest = json.loads((root / 'manifest.json').read_text())
        for entry in manifest['files']:
            if 'width' not in entry:
                continue
            with self.subTest(texture=entry['path']):
                self.assertEqual(entry['width'] % 4, 0, 'ETC2 silently falls back when width is not a multiple of four')
                self.assertEqual(entry['height'] % 4, 0, 'ETC2 silently falls back when height is not a multiple of four')

    def test_padding_retains_every_original_pixel_and_only_extends_edges(self):
        root = pathlib.Path(__file__).resolve().parents[1] / 'Assets/Resources/Zuma'
        manifest = json.loads((root / 'manifest.json').read_text())
        padded = [e for e in manifest['files'] if e.get('padding') in ('transparent','edge')]
        self.assertEqual(len(padded),28)
        source = pathlib.Path(os.environ.get('ZUMA_UPSTREAM_SOURCE','/tmp/xanadu-zuma-audit.67h8aU/Zuma-Deluxe-HD'))
        for entry in padded:
            with self.subTest(texture=entry['path']):
                self.importer.validate_texture_pixels(root / entry['path'],entry)
                self.assertEqual(entry['width'], (entry['sourceWidth']+3)//4*4)
                self.assertEqual(entry['height'], (entry['sourceHeight']+3)//4*4)
                if source.is_dir():
                    with Image.open(source/entry['sourcePaths'][0]) as original, Image.open(root/entry['path']) as actual:
                        self.assertEqual(original.convert(entry['pixelMode']).tobytes(),actual.convert(entry['pixelMode']).crop((0,0,entry['sourceWidth'],entry['sourceHeight'])).tobytes())
                if entry['path'].startswith('Thumbnails/'):
                    self.assertEqual((entry['sourceWidth'],entry['sourceHeight']), (476,305))
                    self.assertFalse(entry['hasAlpha'])
                    self.assertEqual(entry['pixelMode'],'RGB')
                    self.assertFalse((root / entry['path']).with_suffix('.jpg').exists())
                    self.assertFalse((root / entry['path']).with_suffix('.jpg.meta').exists())

    def test_padding_preserves_transparent_rgb_and_top_left_origin(self):
        with tempfile.TemporaryDirectory(prefix='zuma-pad-') as temp:
            source = pathlib.Path(temp)/'source.png'
            pixels = Image.new('RGBA',(3,5))
            pixels.putdata([(x*17,y*23,41,(x+y)%2*255) for y in range(5) for x in range(3)])
            pixels.save(source)
            path, metadata = self.importer.import_texture(source,pathlib.Path(temp)/'target.png')
            with Image.open(path) as padded:
                self.assertEqual(padded.size,(4,8))
                self.assertEqual(padded.crop((0,0,3,5)).tobytes(),pixels.tobytes())
                self.assertEqual(padded.getpixel((3,0)),(0,0,0,0))
                self.assertEqual(padded.getpixel((0,7)),(0,0,0,0))
            self.assertEqual(metadata['padding'],'transparent')

if __name__ == '__main__':
    unittest.main()
