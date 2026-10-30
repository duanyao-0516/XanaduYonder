import hashlib, importlib.util, pathlib, shutil, tempfile, unittest, wave, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[2]
class OriginalAudioConversionTests(unittest.TestCase):
 @unittest.skipUnless(shutil.which('ffmpeg') and shutil.which('ffprobe'),'Requires the offline ffmpeg decoder')
 def test_source_declared_sample_interval_is_exact(self):
  spec=importlib.util.spec_from_file_location('audio_conversion',ROOT/'scripts/age-of-war/audio_conversion.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
  self.assertTrue(hasattr(module,'trim_pcm_wav'),'Source-directed PCM trimming is missing')
  with tempfile.TemporaryDirectory() as work:
   source=pathlib.Path(work)/'234.mp3';target=pathlib.Path(work)/'234.wav'
   with zipfile.ZipFile(ROOT/'scripts/age-of-war/source-art.zip') as archive:source.write_bytes(archive.read('aow1/export/sounds/234.mp3'))
   report=module.trim_pcm_wav(source,target,1661,5106)
   with wave.open(str(target),'rb') as wav:
    self.assertEqual(5106,wav.getnframes());self.assertEqual(11025,wav.getframerate());self.assertEqual(1,wav.getnchannels())
    self.assertEqual(report['derivation']['sourceSlicePcmSha256'],hashlib.sha256(wav.readframes(wav.getnframes())).hexdigest())
   self.assertEqual(6912,report['derivation']['decodedSampleFrames']);self.assertEqual(1661,report['derivation']['trimStartSample']);self.assertEqual(6767,report['derivation']['trimEndSampleExclusive'])
 @unittest.skipUnless(shutil.which('ffmpeg') and shutil.which('ffprobe'),'Requires the offline ffmpeg decoder')
 def test_fmod_rejected_source_preserves_decoded_pcm_samples(self):
  modulepath=ROOT/'scripts/age-of-war/audio_conversion.py'
  self.assertTrue(modulepath.exists(),'PCM conversion with source/sample provenance is missing')
  spec=importlib.util.spec_from_file_location('audio_conversion',modulepath);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
  with tempfile.TemporaryDirectory() as work:
   source=pathlib.Path(work)/'sound-6.mp3';target=pathlib.Path(work)/'sound-6.wav'
   with zipfile.ZipFile(ROOT/'scripts/age-of-war/source-art.zip') as archive:
    name=next(n for n in archive.namelist() if n.startswith('aow2/outer/sounds/6_') and n.endswith('.mp3'));source.write_bytes(archive.read(name))
   report=module.decode_pcm_wav(source,target)
   self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(),report['sourceSha256'])
   self.assertEqual(hashlib.sha256(target.read_bytes()).hexdigest(),report['sha256'])
   self.assertEqual(44100,report['derivation']['sampleRate']);self.assertEqual(2,report['derivation']['channels'])
   with wave.open(str(target),'rb') as audio:
    self.assertEqual(2,audio.getsampwidth());self.assertGreater(audio.getnframes(),0)
    self.assertEqual(report['derivation']['sampleFrames'],audio.getnframes())
    self.assertEqual(report['derivation']['pcmSha256'],hashlib.sha256(audio.readframes(audio.getnframes())).hexdigest())
   self.assertEqual(report['derivation']['sourceDecodedPcmSha256'],report['derivation']['pcmSha256'])
