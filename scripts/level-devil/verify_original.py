"""Verify every packed gzip member and scene pair without changing the release."""
import gzip
import json
from pathlib import Path
import re
from urllib.parse import unquote

class ManifestReader:
    def __init__(self, text):
        self.text, self.offset, self.strings = text, 0, []
    def number(self):
        match = re.match(r'-?\d+', self.text[self.offset:])
        assert match, 'Invalid packed manifest integer'
        self.offset += len(match[0])
        return int(match[0])
    def read(self):
        token = self.text[self.offset]; self.offset += 1
        if token in ('n','t','f','z'): return {'n':None,'t':True,'f':False,'z':0}[token]
        if token == 'i': return self.number()
        if token == 'R': return self.strings[self.number()]
        if token == 'y':
            size = self.number(); assert self.text[self.offset] == ':'; self.offset += 1
            value = unquote(self.text[self.offset:self.offset+size]); self.offset += size
            self.strings.append(value); return value
        if token == 'a':
            value = []
            while self.text[self.offset] != 'h': value.append(self.read())
            self.offset += 1; return value
        if token == 'o':
            value = {}
            while self.text[self.offset] != 'g':
                key = self.read(); value[key] = self.read()
            self.offset += 1; return value
        raise AssertionError('Unexpected packed manifest token: '+token)

def verify(root):
    root = Path(root)
    text = (root/'Level Devil.js').read_text()
    match = re.search(r"a=Oc\.parse\('(\{\"name\":null,\"assets\".*?\})'",text,re.S)
    assert match, 'Embedded manifest missing'
    manifest=json.loads(match[1]);reader=ManifestReader(manifest['assets']);assets=reader.read()
    assert reader.offset == len(reader.text)
    assert manifest['libraryType']=='lime.utils.PackedAssetLibrary' and manifest['libraryArgs'][1]=='gzip'
    pak=(root/manifest['libraryArgs'][0]).read_bytes();end=15;packed=0
    for item in sorted((a for a in assets if 'position' in a),key=lambda a:a['position']):
        assert item['position']==end and item['length']>0, item['id']
        end += item['length'];assert end<=len(pak), item['id']
        data=gzip.decompress(pak[item['position']:end])
        if item['id'].endswith('.png'): assert data.startswith(b'\x89PNG\r\n\x1a\n'),item['id']
        packed+=1
    assert end==len(pak) and packed==2138, 'Packed release changed'
    scene_ids=lambda suffix: sorted(re.findall(r'scene-(\d+)\.'+suffix+'$',a['id'])[0] for a in assets if re.search(r'scene-\d+\.'+suffix+'$',a['id']))
    scenes=scene_ids('mbs');assert scenes==scene_ids('scn') and len(scenes)==184
    sounds=[a for a in assets if 'position' not in a and a['id'].endswith('.mp3')]
    assert len(sounds)==19
    for sound in sounds: assert (root/sound['id']).stat().st_size==sound['size'],sound['id']
    return {'packedMembers':packed,'scenePairs':len(scenes),'mp3Files':len(sounds)}

if __name__=='__main__':
    print(json.dumps(verify(Path(__file__).resolve().parents[2]/'ThirdParty/LevelDevil/original')))
