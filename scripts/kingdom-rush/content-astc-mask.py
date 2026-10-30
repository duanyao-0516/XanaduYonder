"""Clear only reviewed unused ASTC blocks; never re-encode retained UI blocks.

Frame geometry remains in original logical coordinates. One physical pixel of
sampling margin protects adjacent bilinear samples, including shared edge blocks.
"""
import hashlib,json,math,re,subprocess,zlib
from pathlib import Path

def digest(data):return hashlib.sha256(data).hexdigest()
def sequence(value):return [value[i] for i in range(1,len(value)+1)]
def normalized(value):
    if not isinstance(value,dict):return value
    if value and set(value)==set(range(1,len(value)+1)):return [normalized(value[i]) for i in range(1,len(value)+1)]
    return {k:normalized(v) for k,v in value.items()}
def texture_name(row):return re.sub(r'\.(dds|png)$','.astc',row['a_name'])

def apply(stage,recipe_path,output,encoder,read_literal,lua_writer):
    from PIL import Image
    if not encoder or not Path(encoder).is_file():raise ValueError('--astcenc is required for physical resource masking and decode verification')
    stage=Path(stage);output=Path(output);output.mkdir(parents=True,exist_ok=True)
    recipe=json.loads(Path(recipe_path).read_text());base=stage/'kr3-desktop/assets/images/fullhd'
    tables={};removed={};removal_rows=[];index_before={}
    for item in recipe['groups']:
        name=item['group'];path=base/(name+'.lua');data=path.read_bytes();index_before[name]=data
        table=read_literal(data.decode('utf-8-sig'));tables[name]=table
        for key in item['frames']:
            if key not in table:raise ValueError('Reviewed removal frame is absent: '+name+'/'+key)
            row=table.pop(key)
            if normalized(row)!=item['original_geometry'][key]:raise ValueError('Reviewed frame geometry drift: '+name+'/'+key)
            removed.setdefault(texture_name(row),[]).append(row)
            removal_rows.append({'group':name,'frame':key,'reason':item['reason'],'original_geometry':normalized(row)})
    # Other literal indexes can share an atlas. Protect every surviving record,
    # including any record in an index outside this removal recipe.
    for path in sorted(base.glob('*.lua')):
        if path.stem in tables:continue
        text=path.read_text(encoding='utf-8-sig')
        if any(Path(name).stem+suffix in text for name in removed for suffix in ('.astc','.dds','.png')):tables[path.stem]=read_literal(text)
    retained={name:[] for name in removed}
    for group,table in tables.items():
        for key,row in table.items():
            name=texture_name(row)
            if name in retained:retained[name].append((group,key,row))
    def run(command):
        result=subprocess.run(command,capture_output=True,text=True)
        if result.returncode:raise RuntimeError(result.stderr or result.stdout)
    reports=[];unshipped=[]
    for name,discarded in sorted(removed.items()):
        path=base/name
        if not path.is_file():
            if retained[name]:raise ValueError('Retained frame requires missing physical texture: '+name)
            # A newly imported shared index may name foreign-only dedicated
            # sheets that were never admitted by the content include manifest.
            unshipped.append({'texture':name,'removed_records':len(discarded),'reason':'Every record referencing this foreign sheet is removed; no bytes from the sheet are admitted'})
            continue
        original=path.read_bytes()
        if original[:4]!=b'\x13\xab\xa1\x5c' or original[6]!=1:raise ValueError('Expected 2D ASTC: '+name)
        bx,by=original[4:6];width=int.from_bytes(original[7:10],'little');height=int.from_bytes(original[10:13],'little')
        cols=math.ceil(width/bx);rows=math.ceil(height/by)
        if len(original)!=16+cols*rows*16:raise ValueError('ASTC block payload length mismatch: '+name)
        work=output/(Path(name).stem+'-'+digest(original)[:16]);work.mkdir(parents=True,exist_ok=True)
        transparent=work/'transparent.png';Image.new('RGBA',(bx,by),(0,0,0,0)).save(transparent)
        run([str(encoder),'-cl',str(transparent),str(work/'transparent.astc'),str(bx)+'x'+str(by),'-fast','-j','2'])
        transparent_astc=(work/'transparent.astc').read_bytes()
        if len(transparent_astc)!=32:raise ValueError('Expected one transparent ASTC block')
        clear_block=transparent_astc[16:]
        def bounds(row,margin=1):
            x,y,w,h=sequence(row['f_quad']);aw,ah=sequence(row['a_size'])
            return (max(0,math.floor(x*width/aw)-margin),max(0,math.floor(y*height/ah)-margin),min(width,math.ceil((x+w)*width/aw)+margin),min(height,math.ceil((y+h)*height/ah)+margin))
        def blocks(rect):
            x0,y0,x1,y1=rect
            return {y*cols+x for y in range(y0//by,math.ceil(y1/by)) for x in range(x0//bx,math.ceil(x1/bx))}
        protected=set();candidates=set()
        for _,_,row in retained[name]:protected.update(blocks(bounds(row)))
        for row in discarded:candidates.update(blocks(bounds(row)))
        clear=candidates-protected
        if not clear:raise ValueError('No unshared physical blocks can be cleared: '+name)
        changed=bytearray(original)
        for index in clear:changed[16+index*16:32+index*16]=clear_block
        changed=bytes(changed)
        assert all(changed[16+i*16:32+i*16]==original[16+i*16:32+i*16] for i in protected)
        (work/'before.astc').write_bytes(original);(work/'after.astc').write_bytes(changed)
        run([str(encoder),'-dl',str(work/'before.astc'),str(work/'before.png'),'-j','2'])
        run([str(encoder),'-dl',str(work/'after.astc'),str(work/'after.png'),'-j','2'])
        before=Image.open(work/'before.png').convert('RGBA');after=Image.open(work/'after.png').convert('RGBA')
        if before.size!=(width,height) or after.size!=before.size:raise ValueError('Decoded ASTC dimensions changed')
        for group,key,row in retained[name]:
            rect=bounds(row)
            if before.crop(rect).tobytes()!=after.crop(rect).tobytes():raise ValueError('Retained frame decoded pixels changed: '+group+'/'+key)
        for index in clear:
            x=(index%cols)*bx;y=(index//cols)*by
            if any(after.crop((x,y,min(x+bx,width),min(y+by,height))).tobytes()):raise ValueError('Cleared block is not transparent black')
        path.write_bytes(changed)
        reports.append({'path':path.relative_to(stage).as_posix(),'before_sha256':digest(original),'after_sha256':digest(changed),'physical_size':[width,height],'block_size':[bx,by],'cleared_blocks':len(clear),'protected_blocks':len(protected),'shared_edge_blocks_preserved':len(candidates&protected),'retained_frames_verified':len(retained[name]),'retained_blocks_byte_identical':True,'retained_decoded_pixels_with_sampling_margin_identical':True,'cleared_decoded_blocks_transparent':True,'before_bytes':len(original),'after_bytes':len(changed),'before_deflate9_bytes':len(zlib.compress(original,9)),'after_deflate9_bytes':len(zlib.compress(changed,9)),'inspection_images':[str(work/'before.png'),str(work/'after.png')]})
    indexes=[]
    for item in recipe['groups']:
        name=item['group'];path=base/(name+'.lua');data=('return '+lua_writer(tables[name])+'\n').encode()
        path.write_bytes(data);indexes.append({'path':path.relative_to(stage).as_posix(),'before_sha256':digest(index_before[name]),'after_sha256':digest(data),'removed_frames':len(item['frames']),'retained_geometry_unchanged':True})
    report={'recipe_sha256':digest(Path(recipe_path).read_bytes()),'method':'Original ASTC blocks outside retained frame sampling areas replaced by a verified transparent block; original dimensions and surviving geometry unchanged','sampling_margin_physical_pixels':1,'removed_frames':removal_rows,'textures':reports,'indexes':indexes,'unshipped_foreign_textures':unshipped,'deflate9_bytes_reduced':sum(r['before_deflate9_bytes']-r['after_deflate9_bytes'] for r in reports)}
    (output/'physical-removal-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    return report
