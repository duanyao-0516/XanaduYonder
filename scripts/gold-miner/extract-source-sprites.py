#!/usr/bin/env python3
"""Extract only the 17 used source objects; preserve source dimensions and pixels."""
import hashlib
import json
from pathlib import Path
import sys
from PIL import Image


def extract(atlas_path, catalog_path, output):
    atlas = Image.open(atlas_path).convert('RGBA')
    frames = json.loads(Path(catalog_path).read_text())['frames']
    if len(frames) != 17:
        raise ValueError('Expected exactly 17 source object frames')
    report = []
    for key, frame in sorted(frames.items()):
        if frame['rotated'] or Path(key).name != key:
            raise ValueError('Unexpected source frame: ' + key)
        f, size, trim = frame['frame'], frame['sourceSize'], frame['spriteSourceSize']
        region = atlas.crop((f['x'], f['y'], f['x'] + f['w'], f['y'] + f['h']))
        if region.size != (trim['w'], trim['h']):
            raise ValueError('Trim size mismatch: ' + key)
        image = Image.new('RGBA', (size['w'], size['h']), (0, 0, 0, 0))
        image.paste(region, (trim['x'], trim['y']))
        target = output / ('solo-' + key)
        image.save(target, compress_level=9)
        decoded = Image.open(target).convert('RGBA')
        if decoded.size != image.size or decoded.tobytes() != image.tobytes():
            raise ValueError('PNG pixel mismatch: ' + key)
        report.append({'file': target.name, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                       'rgbaSha256': hashlib.sha256(image.tobytes()).hexdigest(),
                       'width': image.width, 'height': image.height, 'sourceFrame': key})
    return report


if __name__ == '__main__':
    atlas, catalog, output = map(Path, sys.argv[1:])
    print(json.dumps(extract(atlas, catalog, output), indent=2))
