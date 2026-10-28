#!/usr/bin/env python3
"""Reproducible, whitelist-only Zuma-Deluxe-HD content import.

Requires Python 3 with Pillow, oggenc/oggdec (vorbis-tools), and libopenmpt >= 0.8 (offline tools).
  python3 scripts/import-zuma-assets.py --source /path/to/Zuma-Deluxe-HD
  python3 scripts/import-zuma-assets.py --validate
The source checkout is never modified. No decoder is shipped with the game.
"""
import argparse
import array
import ctypes
import ctypes.util
import hashlib
import json
import math
import pathlib
import re
import shutil
import struct
import subprocess
import tempfile
import uuid
import wave
import xml.etree.ElementTree as ET
from PIL import Image, PngImagePlugin

COMMIT = '28001e0e5061341d502116586626150027242cce'
MAPS = ('spiral','claw','riverbed','targetglyph','turnaround','longrange','tiltspiral','underover','warshak','loopy','groovefest','spaceinvaders','triangle','coaster','squaresville','tunnellevel','overunder','inversespiral')
FONTS = ('arial12bold','cancun10','cancun12','cancun13','cancun8','cancunfloat14','nativealien48','nativealienextended16','nativealienextended18')
MUSIC = (('game',0),('load_screen',24),('main_menu',28),('adventure',32),('gauntlet',34),('near_hole',36),('win',38),('game_over',39))
ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Zuma'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dump(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, separators=(',',':'), allow_nan=False)+'\n', encoding='utf8')


def image_size(path):
    data = path.read_bytes()
    if data[:8] == b'\x89PNG\r\n\x1a\n':
        return struct.unpack_from('>II',data,16)
    if data[:2] == b'\xff\xd8':
        offset=2
        while offset < len(data):
            if data[offset] != 255:
                raise ValueError('Invalid JPEG marker: '+str(path))
            while data[offset] == 255: offset += 1
            marker=data[offset]; offset += 1
            size=struct.unpack_from('>H',data,offset)[0]
            if marker in (192,193,194,195,197,198,199,201,202,203,205,206,207):
                h,w=struct.unpack_from('>HH',data,offset+3); return w,h
            offset += size
    raise ValueError('Unsupported image: '+str(path))


def image_has_alpha(path):
    with Image.open(path) as image:
        return 'A' in image.getbands() or 'transparency' in image.info


def import_texture(source, target):
    """Only pad right/bottom to ETC2 block boundaries; never resample pixels."""
    with Image.open(source) as original:
        alpha='A' in original.getbands() or 'transparency' in original.info
        mode='RGBA' if alpha else 'RGB'
        pixels=original.convert(mode)
        width,height=pixels.size
        padded_width=(width+3)//4*4; padded_height=(height+3)//4*4
        info=dict(sourceWidth=width,sourceHeight=height,paddingRight=padded_width-width,paddingBottom=padded_height-height,padding='none',hasAlpha=alpha,pixelMode=mode,sourcePixelsSha256=hashlib.sha256(pixels.tobytes()).hexdigest())
        target.parent.mkdir(parents=True,exist_ok=True)
        if (padded_width,padded_height)==pixels.size:
            shutil.copyfile(source,target)
        else:
            old_target=target
            # JPEG cannot retain the exact decoded source region when re-encoded.
            target=target.with_suffix('.png')
            padded=Image.new(mode,(padded_width,padded_height),(0,0,0,0) if alpha else (0,0,0))
            padded.paste(pixels,(0,0))
            if alpha:
                info['padding']='transparent'
            else:
                info['padding']='edge'
                for x in range(width,padded_width): padded.paste(pixels.crop((width-1,0,width,height)),(x,0))
                for y in range(height,padded_height): padded.paste(padded.crop((0,height-1,padded_width,height)),(0,y))
            png_info=PngImagePlugin.PngInfo()
            if 'srgb' in original.info: png_info.add(b'sRGB',bytes([original.info['srgb']]))
            padded.save(target,format='PNG',compress_level=9,optimize=False,pnginfo=png_info,icc_profile=original.info.get('icc_profile'))
            if old_target!=target:
                if old_target.exists():
                    if sha(old_target)!=sha(source): raise ValueError('Unexpected modified old texture '+str(old_target))
                    old_target.unlink()
                old_meta=old_target.with_name(old_target.name+'.meta')
                if old_meta.exists(): old_meta.unlink()
        info['width'],info['height']=padded_width,padded_height
    validate_texture_pixels(target,info)
    return target,info


def validate_texture_pixels(path, info):
    with Image.open(path) as image:
        actual=image.convert(info['pixelMode'])
        width,height=info['sourceWidth'],info['sourceHeight']
        if actual.width%4 or actual.height%4: raise ValueError('Texture not ETC2-block aligned '+str(path))
        if actual.size!=(width+info['paddingRight'],height+info['paddingBottom']): raise ValueError('Texture padding bounds changed')
        if image_has_alpha(path)!=info['hasAlpha']: raise ValueError('Texture alpha mode changed')
        if hashlib.sha256(actual.crop((0,0,width,height)).tobytes()).hexdigest()!=info['sourcePixelsSha256']: raise ValueError('Original texture pixel region changed '+str(path))
        for area in ((width,0,actual.width,height),(0,height,actual.width,actual.height)):
            if area[0]==area[2] or area[1]==area[3]: continue
            strip=actual.crop(area)
            if info['padding']=='transparent':
                if any(strip.tobytes()): raise ValueError('Nontransparent texture padding')
            elif info['padding']=='edge':
                for y in range(area[1],area[3]):
                    for x in range(area[0],area[2]):
                        if actual.getpixel((x,y))!=actual.getpixel((min(x,width-1),min(y,height-1))): raise ValueError('RGB padding does not extend the original edge')
            else: raise ValueError('Unexpected padded area')


def ogg_samples(path):
    """Read the final Ogg granule count, independent of external media tools."""
    data=path.read_bytes(); offset=0; last=0
    while offset<len(data):
        if data[offset:offset+4]!=b'OggS' or offset+27>len(data): raise ValueError('Invalid Ogg page')
        granule=struct.unpack_from('<Q',data,offset+6)[0]
        segments=data[offset+26]
        end=offset+27+segments
        if end>len(data): raise ValueError('Truncated Ogg table')
        offset=end+sum(data[end-segments:end])
        if offset>len(data): raise ValueError('Truncated Ogg payload')
        if granule != 0xffffffffffffffff: last=granule
    return last


def vorbis_vendor(path):
    data=path.read_bytes()
    marker=data.find(b'\x03vorbis')
    if marker<0 or marker+11>len(data): raise ValueError('Missing Vorbis comment header')
    offset=marker+7; length=struct.unpack_from('<I',data,offset)[0]
    if offset+4+length>len(data): raise ValueError('Truncated Vorbis vendor')
    return data[offset+4:offset+4+length].decode('ascii')


def normalize_legacy_sound(source, target):
    """Decode obsolete 20010615 Vorbis RC1 into lossless PCM for Unity FSBTool.

    The original samples, channel count and rate are retained. Unity performs the
    only subsequent Vorbis encode; no placeholder sound or lossy intermediary.
    """
    if not shutil.which('oggdec'): raise RuntimeError('Install vorbis-tools for legacy Vorbis decoding')
    target.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(['oggdec','--quiet','--output',str(target),str(source)],check=True)
    with wave.open(str(target),'rb') as wav:
        frames=wav.getnframes(); rate=wav.getframerate();channels=wav.getnchannels();bits=wav.getsampwidth()*8
        pcm=wav.readframes(frames)
    if frames!=ogg_samples(source) or frames<=0 or bits!=16 or not any(pcm): raise ValueError('Legacy sound PCM conversion changed length or produced invalid data')
    version=subprocess.check_output(['oggdec','--version'],stderr=subprocess.STDOUT,text=True).strip()
    return dict(id=source.stem,source=source.name,sourceSha256=sha(source),sourceVendor=vorbis_vendor(source),output=target.name,conversion='Vorbis RC1 to lossless PCM WAV for Unity FSBTool compatibility',decoder=version,sampleRate=rate,channels=channels,sampleCount=frames,pcmSha256=hashlib.sha256(pcm).hexdigest())


def parse_dat(data):
    if len(data)<32: raise ValueError('Truncated DAT header')
    count=struct.unpack_from('<i',data,16)[0]
    if not 0 <= count <= 10000: raise ValueError('Invalid DAT control count')
    offset=20+count*10
    if offset+12 > len(data): raise ValueError('Truncated DAT controls')
    count,x,y=struct.unpack_from('<iff',data,offset)
    if not 2 <= count <= 20000 or not math.isfinite(x+y): raise ValueError('Invalid DAT points')
    offset += 12
    # N vertices carry N flag pairs and N-1 signed delta pairs. The final two
    # bytes are the endpoint's flags, not arbitrary trailing bytes.
    if len(data) != offset+(count-1)*4+2: raise ValueError('DAT length does not match point count')
    points=[]
    for i in range(count):
        t1,t2=struct.unpack_from('<BB',data,offset)
        if t1 not in (0,1) or t2 not in (0,1): raise ValueError('Unknown DAT flags')
        px,py=(x+104)*1.5,y*1.5
        if not -256<=px<=1536 or not -256<=py<=976: raise ValueError('DAT coordinate out of supported canvas bounds')
        points.append(dict(x=round(px,6),y=round(py,6),hidden=bool(t1),drawPriority=bool(t2)))
        if i<count-1:
            dx,dy=struct.unpack_from('<bb',data,offset+2)
            x += dx/100; y += dy/100
        offset += 4
    return points


def parse_font(path):
    tokens=re.split(r'[ \t\r\n]+',path.read_text(encoding='latin1').strip(' \t\r\n')); blocks={}; i=0
    dimensions={'CharList':1,'WidthList':1,'RectList':4,'OffsetList':2,'KerningPairs':1,'KerningValues':1,'OutlineRectList':4,'OutlineOffsetList':2}
    while i<len(tokens):
        key=tokens[i]; count=int(tokens[i+1]); i+=2
        if key not in dimensions or key in blocks or count<0: raise ValueError('Invalid font block')
        length=count*dimensions[key]; blocks[key]=tokens[i:i+length]; i+=length
        if len(blocks[key])!=length: raise ValueError('Truncated font')
    chars=blocks['CharList']; n=len(chars)
    if any(len(blocks[k]) != n*d for k,d in (('WidthList',1),('RectList',4),('OffsetList',2))): raise ValueError('Font field count mismatch')
    glyphs=[]
    w,h=image_size(path.with_suffix('.png'))
    for j,c in enumerate(chars):
        x,y,gw,gh=map(int,blocks['RectList'][j*4:j*4+4]); ox,oy=map(int,blocks['OffsetList'][j*2:j*2+2])
        if len(c)!=1 or min(x,y,gw,gh)<0 or x+gw>w or y+gh>h: raise ValueError('Glyph bounds invalid: '+path.stem+' '+c)
        glyphs.append(dict(character=c,x=x,y=y,width=gw,height=gh,advance=int(blocks['WidthList'][j]),offsetX=ox,offsetY=oy))
    pairs=blocks.get('KerningPairs',[]); vals=blocks.get('KerningValues',[])
    if path.stem=='cancun12' and len(pairs)==154 and len(vals)==153:
        # Upstream has an unpaired final kerning key: discard it, never read beyond values.
        pairs=pairs[:len(vals)]
    elif len(pairs)!=len(vals): raise ValueError('Kerning count mismatch')
    return dict(id=path.stem,texture='Fonts/'+path.stem,lineHeight=max(g['height'] for g in glyphs),spaceAdvance=int(blocks['WidthList'][0]),glyphs=glyphs,kerning=[dict(pair=k,amount=int(v)) for k,v in zip(pairs,vals)])


def slices():
    result=[]
    def add(id,tex,x,y,w,h,frames=1,axis='y'):
        result.append(dict(id=id,texture='Images/'+tex,x=x,y=y,width=w,height=h,frames=frames,axis=axis,pivotX=0.5 if tex in ('frog','gameobjects') else 0.0,pivotY=0.5 if tex in ('frog','gameobjects') else 0.0))
    add('selection_background','menu',1282,1440,1280,720)
    add('selection_sky','menu',0,1442,1280,250)
    for name,x,y,w,h in [('main_menu',1280,2160,211,84),('back',1924,2260,139,48),('play',1922,2210,157,49),('next',1922,2161,141,48),('rabbit',1608,2336,328,63),('eagle',1538,2399,258,49),('jaguar',1485,2448,205,43),('sun_god',1439,2491,159,54),('button',0,532,180,72)]:
        add(name,'menu',x,y,w,h,3,'x')
    add('dialog','menu',0,0,363,363);add('dialog_god','menu',363,0,234,210)
    add('checkbox','menu',0,462,75,69,2,'x')
    add('hud_border','gameinterface',0,51,1280,730)
    add('hud_menu','gameinterface',925,15,118,37,3,'x')
    add('hud_progress','gameinterface',773,28,96,28)
    add('hud_progress_full','gameinterface',773,0,96,28)
    add('frog_body','frog',0,0,162,162);add('frog_foundation','frog',162,162,162,162)
    add('frog_tongue','frog',162,0,162,162);add('frog_eyes','frog',0,162,162,162,3)
    for c in range(6):
        add('ball_'+str(c),'gameobjects',48*c,0,48,48,47 if c==0 else 50)
        add('next_ball_'+str(c),'frog',234+15*c,633,15,15)
    add('coin','gameobjects',340,0,54,54,30)
    add('finish_base','gameobjects',629,0,132,132)
    add('finish','gameobjects',629,132,132,132,13)
    add('ball_shadow','gameobjects',134,2595,48,50)
    add('ball_explosion','gameobjects',395,0,105,120,16)
    add('outro_explosion','gameobjects',528,0,100,130,17)
    add('sparkle','gameobjects',732,2253,30,30,14)
    return result


class ModuleDecoder:
    def __init__(self, path):
        name=ctypes.util.find_library('openmpt')
        if not name and pathlib.Path('/opt/homebrew/lib/libopenmpt.dylib').exists(): name='/opt/homebrew/lib/libopenmpt.dylib'
        if not name: raise RuntimeError('Install libopenmpt >=0.8 (brew install libopenmpt); no music stub is permitted')
        self.lib=ctypes.CDLL(name); P=ctypes.c_void_p; I=ctypes.c_int32; D=ctypes.c_double
        api=[('create_from_memory2',[P,ctypes.c_size_t,P,P,P,P,P,P,P],P),('destroy',[P],None),('select_subsong',[P,I],I),('get_num_subsongs',[P],I),('get_current_order',[P],I),('get_current_row',[P],I),('get_restart_order',[P,I],I),('get_restart_row',[P,I],I),('get_duration_seconds',[P],D),('set_position_order_row',[P,I,I],D),('set_repeat_count',[P,I],I),('set_render_param',[P,I,I],I),('ctl_set_text',[P,ctypes.c_char_p,ctypes.c_char_p],I),('ctl_set_integer',[P,ctypes.c_char_p,ctypes.c_int64],I),('read_interleaved_stereo',[P,I,ctypes.c_size_t,ctypes.POINTER(ctypes.c_int16)],ctypes.c_size_t)]
        for name,args,result in api:
            fn=getattr(self.lib,'openmpt_module_'+name);fn.argtypes=args;fn.restype=result;setattr(self,name,fn)
        self.lib.openmpt_get_string.argtypes=[ctypes.c_char_p];self.lib.openmpt_get_string.restype=ctypes.c_char_p
        self.version=self.lib.openmpt_get_string(b'library_version').decode()
        self.data=path.read_bytes();self.mod=self.create_from_memory2(self.data,len(self.data),None,None,None,None,None,None,None)
        if not self.mod: raise RuntimeError('MO3 decoder rejected source')
        self.ctl_set_text(self.mod,b'play.at_end',b'stop')
        self.ctl_set_integer(self.mod,b'dither',0)
        self.set_repeat_count(self.mod,0)
        # Preserve headroom before the decoder converts its mix to 16-bit PCM.
        if not self.set_render_param(self.mod,1,-600): raise RuntimeError('Unable to configure decoder mix headroom')
        if self.get_num_subsongs(self.mod)!=8: raise ValueError('Expected 8 source subsongs')

    def render(self, subsong, order):
        self.select_subsong(self.mod,subsong)
        if self.get_current_order(self.mod)!=order: raise ValueError('Music enum order does not match decoded subsong')
        restart=self.get_restart_order(self.mod,subsong); row=self.get_restart_row(self.mod,subsong)
        restart_seconds=self.set_position_order_row(self.mod,restart,row)
        self.set_position_order_row(self.mod,order,0)
        pcm=bytearray(); buf=(ctypes.c_int16*8192)(); first_loop_sample=0 if restart==order else None
        while True:
            # Inspect the actual decoder order transition one sample at a time
            # around the loop point. Do not confuse order IDs with seconds.
            near=first_loop_sample is None and len(pcm)/4/44100>=restart_seconds-.1
            size=1 if near else 4096
            frames=self.read_interleaved_stereo(self.mod,44100,size,buf)
            if not frames: break
            if first_loop_sample is None and self.get_current_order(self.mod)==restart and self.get_current_row(self.mod)==row:
                first_loop_sample=len(pcm)//4
            pcm.extend(bytes(buf)[:frames*4])
            if len(pcm)>44100*4*600: raise ValueError('Unbounded music render')
        if first_loop_sample is None or not pcm or not any(pcm): raise ValueError('Missing/non-audible music segment')
        return bytes(pcm), first_loop_sample, restart, row

    def close(self):
        self.destroy(self.mod)


def encode_music(source, out):
    if not shutil.which('oggenc'): raise RuntimeError('Install vorbis-tools (brew install vorbis-tools); oggenc is an offline tool only')
    decoder=ModuleDecoder(source); entries=[]
    try:
        with tempfile.TemporaryDirectory(prefix='zuma-music-') as temp:
            for subsong,(id,order) in enumerate(MUSIC):
                pcm,loop_start,restart,row=decoder.render(subsong,order)
                pieces=[(id,pcm)] if not loop_start else [(id+'_intro',pcm[:loop_start*4]),(id+'_loop',pcm[loop_start*4:])]
                for name,samples in pieces:
                    wav=pathlib.Path(temp)/(name+'.wav')
                    with wave.open(str(wav),'wb') as w:
                        w.setnchannels(2);w.setsampwidth(2);w.setframerate(44100);w.writeframes(samples)
                    target=out/'Audio/Music'/(name+'.ogg'); target.parent.mkdir(parents=True,exist_ok=True)
                    subprocess.run(['oggenc','--quiet','--quality','4','--serial','0','--output',str(target),str(wav)],check=True)
                entry=dict(renderGainMillibel=-600,id=id,order=order,subsong=subsong,restartOrder=restart,restartRow=row,sampleRate=44100,channels=2,resource='Audio/Music/'+(id+'_loop' if loop_start else id),introResource='Audio/Music/'+id+'_intro' if loop_start else '',introSamples=loop_start,loopStartSample=0,loopEndSample=len(pcm)//4-loop_start,loop=id not in ('win','game_over'),decoder='libopenmpt '+decoder.version,source='content/music/zuma.mo3',sourceSha256=sha(source))
                samples=array.array('h',pcm)
                entry['decodedPcmSha256']=hashlib.sha256(pcm).hexdigest()
                entry['decodedPeak']=max(abs(v) for v in samples)
                entry['decodedRms']=round(math.sqrt(sum(v*v for v in samples)/len(samples)),6)
                if entry['decodedRms']<64 or entry['decodedPeak']>=32767: raise ValueError('Decoded music is silent or clipped')
                entries.append(entry); print('Music',id,'order',order,'intro',loop_start,'loop',entry['loopEndSample'])
    finally: decoder.close()
    return entries


def meta(path, is_folder=False):
    rel=str(path.relative_to(ROOT)); guid=uuid.uuid5(uuid.NAMESPACE_URL,'xanadu-zuma/'+rel).hex
    top='fileFormatVersion: 2\nguid: '+guid+'\n'
    if is_folder: content=top+'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.suffix.lower() in ('.png','.jpg'):
        template=(ROOT/'Assets/Resources/Sheep/Tiles/block_1.png.meta').read_text()
        import re
        content=re.sub(r'guid: [0-9a-f]+','guid: '+guid,template)
        maxsize=4096 if max(image_size(path))>2048 else 2048
        content=content.replace('maxTextureSize: 2048','maxTextureSize: '+str(maxsize)).replace('compressionQuality: 50','compressionQuality: 100')
        # Explicit ETC2 rather than Unity's platform-dependent automatic choice.
        a=content.index('    buildTarget: Android'); b=content.index('  spriteSheet:',a)
        android=content[a:b].replace('textureFormat: -1','textureFormat: '+('47' if image_has_alpha(path) else '45')).replace('overridden: 0','overridden: 1')
        content=content[:a]+android+content[b:]
        content=content.replace('alphaIsTransparency: 1','alphaIsTransparency: '+('1' if image_has_alpha(path) else '0'))
    elif path.suffix in ('.ogg','.wav'):
        content=(ROOT/'Assets/Resources/Jump/Audio/pop.mp3.meta').read_text()
        import re
        content=re.sub(r'guid: [0-9a-f]+','guid: '+guid,content).replace('quality: 1','quality: 0.7').replace('  3D: 1','  3D: 0')
        if '/Music/' in str(path): content=content.replace('loadType: 0','loadType: 1')
    elif path.suffix=='.cs': content=top+'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else: content=top+'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    path.with_name(path.name+'.meta').write_text(content)


def generate(source, out):
    actual=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()
    if actual!=COMMIT: raise ValueError('Source commit mismatch: '+actual)
    if subprocess.check_output(['git','-C',str(source),'status','--porcelain','--','content'],text=True).strip(): raise ValueError('Source content checkout has modifications')
    out.mkdir(parents=True,exist_ok=True); sources={}; files=[]; provenance={}; textures={}
    def record(path):
        relative=path.relative_to(source).as_posix(); sources[relative]=dict(path=relative,sha256=sha(path),bytes=path.stat().st_size)
    def copy(relative,target):
        path=source/relative
        if not path.is_file(): raise ValueError('Missing exact-case source '+relative)
        # Reject case-insensitive lookups on macOS.
        cursor=source
        for part in pathlib.Path(relative).parts:
            if part not in {p.name for p in cursor.iterdir()}: raise ValueError('Source filename case mismatch '+relative)
            cursor /= part
        record(path); dest=out/target
        if path.suffix.lower() in ('.png','.jpg'):
            dest,info=import_texture(path,dest);target=dest.relative_to(out).as_posix();textures[target]=info
        else:
            dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,dest)
        provenance[target]=[relative]
    xml=source/'content/levels/levels.xml';record(xml); tree=ET.parse(xml).getroot();graphics={g.attrib['id']:g for g in tree.findall('Graphics')}
    maps=[]
    for index,id in enumerate(MAPS):
        g=graphics[id];a=g.attrib
        if 'curve2' in a: raise ValueError('Whitelist includes dual-chain map')
        path=source/f'content/levels/{id}/{a["curve"]}.dat';record(path);points=parse_dat(path.read_bytes())
        level=dict(id=id,displayName=a['dispname'],frogX=(float(a['gx'])+106)*1.5,frogY=float(a['gy'])*1.5,points=points,coins=[dict(x=(float(c.attrib['x'])+104)*1.5,y=float(c.attrib['y'])*1.5) for c in g.findall('TreasurePoint')])
        dump(out/f'Levels/{id}.json',level);provenance[f'Levels/{id}.json']=[path.relative_to(source).as_posix(),'content/levels/levels.xml']
        copy(f'content/levels/{id}/{a["image"]}.jpg',f'Maps/{id}/background.jpg')
        overlay=''
        if 'image-top' in a:
            copy(f'content/levels/{id}/{a["image-top"]}.png',f'Maps/{id}/overlay.png');overlay=f'Maps/{id}/overlay'
        copy(f'content/images/thumbnails/thumb ({index+1}).jpg',f'Thumbnails/{id}.jpg')
        maps.append(dict(id=id,displayName=a['dispname'],level=f'Levels/{id}',background=f'Maps/{id}/background',overlay=overlay,thumbnail=f'Thumbnails/{id}',pointCount=len(points),hiddenSegments=sum(p['hidden'] for p in points[:-1]),prioritySegments=sum(p['drawPriority'] for p in points[:-1])))
    for id in ('menu','frog','gameobjects','gameinterface'): copy(f'content/images/{id}.png',f'Images/{id}.png')
    for id in FONTS:
        copy(f'content/fonts/{id}.png',f'Fonts/{id}.png');path=source/f'content/fonts/{id}.txt';record(path);dump(out/f'Fonts/{id}.json',parse_font(path));provenance[f'Fonts/{id}.json']=[path.relative_to(source).as_posix()]
    sounds=sorted((source/'content/sounds').glob('*.ogg'))
    if len(sounds)!=38: raise ValueError('Expected 38 source sounds')
    audio_conversions=[]
    for path in sounds:
        relative=path.relative_to(source).as_posix()
        if vorbis_vendor(path)=='Xiphophorus libVorbis I 20010615':
            record(path);target='Audio/Sounds/'+path.stem+'.wav'
            audio_conversions.append(normalize_legacy_sound(path,out/target));provenance[target]=[relative]
            # Remove only this known obsolete imported variant and its metadata.
            old=out/('Audio/Sounds/'+path.name)
            if old.exists():
                if sha(old)!=sha(path): raise ValueError('Unexpected modified legacy audio variant '+str(old))
                old.unlink()
            oldmeta=old.with_name(old.name+'.meta')
            if oldmeta.exists(): oldmeta.unlink()
        else: copy(relative,'Audio/Sounds/'+path.name)
    if len(audio_conversions)!=11: raise ValueError('The fixed upstream must contain exactly eleven RC1 sound effects')
    record(source/'content/music/zuma.mo3'); music=encode_music(source/'content/music/zuma.mo3',out)
    for track in music:
        for resource in (track['resource'],track['introResource']):
            if resource: provenance[resource+'.ogg']=['content/music/zuma.mo3']
    slice_list=slices()
    for item in slice_list:
        tex=textures[item['texture']+'.png'];w,h=tex['sourceWidth'],tex['sourceHeight']
        item['clippedHeight']=min(item['height'],h-item['y'])
        if item['x']+item['width']*(item['frames'] if item['axis']=='x' else 1)>w or item['y']+item['height']*(item['frames'] if item['axis']=='y' else 1)>h:
            if item['id'] not in ('hud_border','sun_god'): raise ValueError('Invalid source slice '+item['id'])
            item['sourceBoundsCorrection']='Clip unavailable bottom pixels; retain requested layout extent (upstream SDL clipping).'
    for path in sorted(out.rglob('*')):
        if path.is_file() and path.suffix!='.meta' and path.name!='manifest.json':
            relative=path.relative_to(out).as_posix()
            if relative not in provenance: raise ValueError('Unexpected output outside whitelist '+relative)
            entry=dict(path=relative,sha256=sha(path),bytes=path.stat().st_size,sourcePaths=provenance[relative])
            if path.suffix in ('.png','.jpg'): entry.update(textures[relative])
            files.append(entry)
    manifest=dict(audioConversions=audio_conversions,importCorrections=['Pad 28 textures on right/bottom to multiples of four for actual ETC2; preserve original top-left decoded pixels and source rectangles. RGB JPEG thumbnails become lossless RGB PNG with edge padding.','Decode eleven 20010615 Vorbis RC1 sounds to PCM WAV without changing samples; Unity FSBTool cannot import their original bitstream.','Clip the five unavailable bottom pixels of hud_border and one unavailable bottom pixel of sun_god; preserve layout extent.','Drop cancun12 final kerning key without corresponding value; retain 153 valid pairs.','Read signed DAT deltas and preserve endpoint flags; reject malformed/truncated data.'],contentVersion=1,sourceUrl='https://github.com/GalaxyShad/Zuma-Deluxe-HD',sourceCommit=COMMIT,coordinateSystem='1280x720 top-left; sample-index positions; paths/coins=(x+104,y)*1.5; frog=(x+106,y)*1.5',maps=maps,difficulties=['rabbit','eagle','jaguar','sun_god'],fonts=list(FONTS),sounds=[p.stem for p in sounds],music=music,slices=slice_list,sources=sorted(sources.values(),key=lambda x:x['path']),files=files,rights='Code: MIT GalaxyShad 2023. Content: PopCap/EA; local implementation authorized; public redistribution not established. See THIRD_PARTY_NOTICES.md.')
    dump(out/'manifest.json',manifest)
    for path in sorted(out.rglob('*')):
        if path.suffix!='.meta': meta(path,path.is_dir())
    meta(out,True)
    validate(out)


def validate(out):
    if not (out/'manifest.json').exists(): raise ValueError('Missing Zuma manifest')
    m=json.loads((out/'manifest.json').read_text())
    if m['contentVersion']!=1 or m['sourceCommit']!=COMMIT: raise ValueError('Manifest version mismatch')
    if [x['id'] for x in m['maps']]!=list(MAPS) or m['difficulties']!=['rabbit','eagle','jaguar','sun_god']: raise ValueError('18 x 4 whitelist mismatch')
    def resource(name):
        matches=[out/(name+ext) for ext in ('.json','.png','.jpg','.ogg','.wav') if (out/(name+ext)).is_file()]
        # Fonts intentionally have texture and JSON metadata under same stem.
        if not matches: raise ValueError('Missing resource '+name)
    sources={entry['path']:entry for entry in m['sources']}
    for item in m['files']:
        if not item.get('sourcePaths') or any(p not in sources for p in item['sourcePaths']): raise ValueError('Missing source provenance')
        if (pathlib.Path(item['path']).suffix in ('.png','.jpg') and item.get('padding')=='none') or (item['path'].startswith('Audio/Sounds/') and item['path'].endswith('.ogg')):
            if len(item['sourcePaths'])!=1 or item['sha256']!=sources[item['sourcePaths'][0]]['sha256']: raise ValueError('Original media pixels/bytes changed')
        path=out/item['path']
        if not path.is_file() or sha(path)!=item['sha256'] or path.stat().st_size!=item['bytes']: raise ValueError('Resource hash mismatch '+item['path'])
        if 'width' in item:
            if image_size(path)!=(item['width'],item['height']): raise ValueError('Image dimensions mismatch')
            validate_texture_pixels(path,item)
    expected={f['path'] for f in m['files']}|{'manifest.json'}
    actual={p.relative_to(out).as_posix() for p in out.rglob('*') if p.is_file() and p.suffix!='.meta'}
    if actual!=expected: raise ValueError('Unmanifested content: '+str(actual^expected))
    for map in m['maps']:
        for name in ('level','background','thumbnail'): resource(map[name])
        if map['overlay']: resource(map['overlay'])
        level=json.loads((out/(map['level']+'.json')).read_text());points=level['points']
        if len(points)!=map['pointCount'] or len(points)<2 or level['id']!=map['id'] or not level['coins']: raise ValueError('Invalid level definition')
        for p in points:
            if not isinstance(p['hidden'],bool) or not isinstance(p['drawPriority'],bool) or not all(math.isfinite(p[k]) for k in ('x','y')) or not -256<=p['x']<=1536 or not -256<=p['y']<=976: raise ValueError('Invalid point')
        for p in level['coins']+[dict(x=level['frogX'],y=level['frogY'])]:
            if not 0<=p['x']<=1280 or not 0<=p['y']<=720: raise ValueError('Invalid frog/coin position')
        if any(p['hidden'] or p['drawPriority'] for p in points) and not map['overlay']: raise ValueError('Tunnel missing overlay')
        for difficulty in m['difficulties']:
            if difficulty not in ('rabbit','eagle','jaguar','sun_god'): raise ValueError('Invalid difficulty')
    if len(m['sounds'])!=38 or len(m['music'])!=8: raise ValueError('Audio whitelist mismatch')
    for sound in m['sounds']:
        candidates=[out/('Audio/Sounds/'+sound+ext) for ext in ('.ogg','.wav') if (out/('Audio/Sounds/'+sound+ext)).exists()]
        if len(candidates)!=1: raise ValueError('Missing or ambiguous sound resource '+sound)
    conversions=m.get('audioConversions',[])
    if len(conversions)!=11: raise ValueError('Missing legacy Vorbis conversion evidence')
    for item in conversions:
        with wave.open(str(out/'Audio/Sounds'/item['output']),'rb') as wav:
            pcm=wav.readframes(wav.getnframes())
            if wav.getnframes()!=item['sampleCount'] or wav.getframerate()!=item['sampleRate'] or wav.getnchannels()!=item['channels'] or wav.getsampwidth()!=2 or hashlib.sha256(pcm).hexdigest()!=item['pcmSha256']:
                raise ValueError('Normalized original sound PCM mismatch '+item['id'])
        if sources['content/sounds/'+item['source']]['sha256']!=item['sourceSha256']: raise ValueError('Normalized sound source mismatch')

    for item,(id,order) in zip(m['music'],MUSIC):
        if item['id']!=id or item['order']!=order or item['loopEndSample']<=0 or item['sampleRate']!=44100: raise ValueError('Invalid music metadata')
        resource(item['resource'])
        if ogg_samples(out/(item['resource']+'.ogg'))!=item['loopEndSample']: raise ValueError('Music loop sample count mismatch')
        if item['introResource']:
            resource(item['introResource'])
            if ogg_samples(out/(item['introResource']+'.ogg'))!=item['introSamples']: raise ValueError('Music intro sample count mismatch')
        if item.get('decodedRms',0)<64 or not 64<=item.get('decodedPeak',0)<32767: raise ValueError('Missing audible decoded PCM evidence')
    for p in out.rglob('*'):
        if p.is_file() and p.suffix in ('.jpg','.png'):
            md=p.with_name(p.name+'.meta').read_text()
            required=('enableMipMap: 0','isReadable: 0','nPOTScale: 0','buildTarget: Android','textureFormat: '+('47' if image_has_alpha(p) else '45'))
            if any(s not in md for s in required): raise ValueError('Unsafe import settings '+str(p))
            if max(image_size(p))>2048 and 'maxTextureSize: 4096' not in md: raise ValueError('Atlas downsample risk')
    print('Verified 18 maps x 4 difficulties,',sum(x['pointCount'] for x in m['maps']),'vertices,',len(m['files']),'hashed files, 38 sounds, 8 decoded music segments')
    return m


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--source',type=pathlib.Path);parser.add_argument('--validate',action='store_true');args=parser.parse_args()
    if args.validate: validate(OUT)
    elif args.source: generate(args.source.resolve(),OUT)
    else: parser.error('Supply --source or --validate')

if __name__=='__main__': main()
