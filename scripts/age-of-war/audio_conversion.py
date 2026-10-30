"""Losslessly preserve the decoded sample sequence for original MP3s rejected by Unity FMOD."""
import hashlib, json, pathlib, subprocess, tempfile, wave

def trim_pcm_wav(source,target,start_sample,sample_count):
 """Apply the explicit SWF sample window; never guess or pad unavailable samples."""
 target=pathlib.Path(target)
 with tempfile.TemporaryDirectory() as temp:
  decoded=pathlib.Path(temp)/'decoded.wav';report=decode_pcm_wav(source,decoded)
  with wave.open(str(decoded),'rb') as wav:
   params=wav.getparams();pcm=wav.readframes(params.nframes)
  if start_sample<0 or sample_count<=0 or start_sample+sample_count>params.nframes:
   raise ValueError(f'Source interval [{start_sample},{start_sample+sample_count}) exceeds {params.nframes} decoded frames')
  stride=params.nchannels*params.sampwidth;trimmed=pcm[start_sample*stride:(start_sample+sample_count)*stride]
  target.parent.mkdir(parents=True,exist_ok=True)
  with wave.open(str(target),'wb') as wav:
   wav.setparams(params);wav.writeframes(trimmed)
  d=report['derivation'];d.update(method='MP3 decode to PCM S16LE WAV with SWF sample interval',reason='Original DefineSound InitialLatency and SoundSampleCount',decodedWavSha256=report['sha256'],decodedSampleFrames=params.nframes,decodedPcmSha256=d['pcmSha256'],trimStartSample=start_sample,trimEndSampleExclusive=start_sample+sample_count,sampleFrames=sample_count,pcmSha256=hashlib.sha256(trimmed).hexdigest(),sourceSlicePcmSha256=hashlib.sha256(trimmed).hexdigest())
  report['sha256']=hashlib.sha256(target.read_bytes()).hexdigest()
  return report

def decode_pcm_wav(source, target):
 source=pathlib.Path(source);target=pathlib.Path(target)
 info=json.loads(subprocess.check_output(['ffprobe','-v','error','-select_streams','a:0','-show_entries','stream=codec_name,sample_rate,channels','-of','json',str(source)],text=True))['streams'][0]
 if info['codec_name']!='mp3':raise ValueError('This workaround is restricted to verified original MP3 sources')
 target.parent.mkdir(parents=True,exist_ok=True)
 subprocess.run(['ffmpeg','-nostdin','-v','error','-y','-i',str(source),'-map','0:a:0','-map_metadata','-1','-fflags','+bitexact','-flags:a','+bitexact','-c:a','pcm_s16le',str(target)],check=True)
 source_pcm=subprocess.check_output(['ffmpeg','-nostdin','-v','error','-i',str(source),'-map','0:a:0','-c:a','pcm_s16le','-f','s16le','-'])
 with wave.open(str(target),'rb') as result:
  rate=result.getframerate();channels=result.getnchannels();frames=result.getnframes();width=result.getsampwidth();pcm=result.readframes(frames)
 if rate!=int(info['sample_rate']) or channels!=info['channels'] or width!=2:raise ValueError('PCM conversion changed the source format')
 if pcm!=source_pcm:raise ValueError('WAV sample sequence differs from an independent source decode')
 sha=lambda b:hashlib.sha256(b).hexdigest()
 return {'sha256':sha(target.read_bytes()),'sourceSha256':sha(source.read_bytes()),'derivation':{'method':'MP3 decode to PCM S16LE WAV','reason':'Unity 6000.3 FSBTool FMOD import failure','decoder':subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],'sampleRate':rate,'channels':channels,'sampleFrames':frames,'pcmSha256':sha(pcm),'sourceDecodedPcmSha256':sha(source_pcm)}}
