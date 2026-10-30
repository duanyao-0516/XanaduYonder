"""Reproducible uniform ASTC reduction for newly proved dependencies.
Preserves authored atlas metadata and every frame; alpha is never sharpened.
"""
import hashlib,json,subprocess
from pathlib import Path

def transform(data, output_root, encoder, preserve=False, background=False):
    if data[:4] != b'\x13\xab\xa1\x5c':raise ValueError('Not an ASTC texture')
    width=int.from_bytes(data[7:10],'little');height=int.from_bytes(data[10:13],'little')
    scale=1 if preserve or max(width,height)<=512 else .5 if background else .75
    record={'input_sha256':hashlib.sha256(data).hexdigest(),'source_size':[width,height],'linear_scale':scale,'policy':'whole atlas, no removed frames, Lanczos RGBA and mild RGB-only sharpening'}
    if scale==1:return data,record
    if not encoder or not Path(encoder).is_file():raise ValueError('--astcenc is required to encode additional large textures')
    from PIL import Image,ImageFilter
    key=record['input_sha256']+'-'+str(scale)+'-v1'
    work=Path(output_root)/key;work.mkdir(parents=True,exist_ok=True)
    dest=work/'output.astc';record_path=work/'record.json'
    if dest.exists() and record_path.exists():return dest.read_bytes(),json.loads(record_path.read_text())
    source=work/'source.astc';source.write_bytes(data)
    def run(command):
        result=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
        if result.returncode:raise RuntimeError(result.stderr or result.stdout)
    run([str(encoder),'-dl',str(source),str(work/'decoded.png'),'-j','2'])
    im=Image.open(work/'decoded.png').convert('RGBA')
    if im.size!=(width,height):raise ValueError('ASTC decoded size differs')
    resized=im.resize((round(width*scale),round(height*scale)),Image.Resampling.LANCZOS)
    rgb=resized.convert('RGB');alpha=resized.getchannel('A')
    sharp=rgb.filter(ImageFilter.UnsharpMask(radius=.5,percent=80,threshold=3))
    mask=alpha.filter(ImageFilter.MinFilter(3)).point(lambda a:255 if a==255 else 0)
    final=Image.composite(sharp,rgb,mask).convert('RGBA');final.putalpha(alpha)
    final.save(work/'encode.png')
    run([str(encoder),'-cl',str(work/'encode.png'),str(dest),'8x8','-medium','-j','2'])
    run([str(encoder),'-dl',str(dest),str(work/'verify.png'),'-j','2'])
    if Image.open(work/'verify.png').size!=final.size:raise ValueError('Encoded texture verification failed')
    result=dest.read_bytes();record.update({'output_size':list(final.size),'output_sha256':hashlib.sha256(result).hexdigest(),'output_bytes':len(result),'encoder':str(encoder),'decoded_verification':True})
    record_path.write_text(json.dumps(record,indent=2));return result,record
