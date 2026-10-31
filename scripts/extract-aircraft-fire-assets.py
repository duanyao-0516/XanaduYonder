#!/usr/bin/env python3
"""Reproduce the gameplay-only resources from the user-provided Aircraft Fire APK.

Requires Python 3 and Pillow. No executable code, SDK, advertising or online-service
resource is copied. The APK is intentionally not included in this repository.
"""

import argparse
import hashlib
import io
import json
import plistlib
import re
import uuid
import zipfile
from pathlib import Path

from PIL import Image


EXPECTED_SHA256 = "052519b7cd43722a99eb079a349fb85b2218234c7e77594a8125e2a1160ed142"
ROOT = Path(__file__).resolve().parents[1]
RESOURCE_ROOT = Path("Assets/Resources/AircraftFire")
AUDIO = ("bg_music", "boss_appear", "boss_blowup", "enemy_blowup", "hero_blowup")


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "XanaduYonder/" + path.as_posix()).hex


def write_meta(path, kind):
    target = ROOT / path
    prefix = "fileFormatVersion: 2\nguid: " + guid(path) + "\n"
    if kind == "folder":
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif kind == "audio":
        body = """AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.8
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    else:
        body = """TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  compressionQuality: 100
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    if path.name in ("button_normal.png", "button_highlight.png"):
        # Native CCScale9Sprite cap inset is Rect(54, 0, 1, 70).
        body = body.replace("spriteBorder: {x: 0, y: 0, z: 0, w: 0}",
                            "spriteBorder: {x: 54, y: 0, z: 53, w: 0}")
    target.with_name(target.name + ".meta").write_text(prefix + body, encoding="utf-8")


def integers(value):
    return tuple(map(int, re.findall(r"-?\d+", value)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path, help="Aircraft+Fire_2.5_APKPure.apk supplied locally")
    args = parser.parse_args()
    apk_hash = hashlib.sha256(args.apk.read_bytes()).hexdigest()
    if apk_hash != EXPECTED_SHA256:
        parser.error("APK SHA-256 differs from the researched 2.5 reference: " + apk_hash)

    for folder in (RESOURCE_ROOT, RESOURCE_ROOT / "Sprites", RESOURCE_ROOT / "Audio"):
        (ROOT / folder).mkdir(parents=True, exist_ok=True)
        write_meta(folder, "folder")

    entries = []

    def save(relative, content, source, kind, dimensions=None):
        destination = RESOURCE_ROOT / relative
        (ROOT / destination).write_bytes(content)
        write_meta(destination, kind)
        entry = {"path": destination.as_posix(), "source": source,
                 "sha256": hashlib.sha256(content).hexdigest()}
        if dimensions:
            entry["width"], entry["height"] = dimensions
        entries.append(entry)

    with zipfile.ZipFile(args.apk) as apk:
        frames = plistlib.loads(apk.read("assets/GameArts.plist"))["frames"]
        atlas = Image.open(io.BytesIO(apk.read("assets/GameArts.png"))).convert("RGBA")
        for name, frame in sorted(frames.items()):
            if name.startswith("loading") or name == "enemy_bullet.png":
                continue
            x, y, width, height = integers(frame["textureRect"])
            cropped = atlas.crop((x, y, x + width, y + height))
            if frame.get("textureRotated", False):
                cropped = cropped.transpose(Image.Transpose.ROTATE_90)
            source_size = integers(frame["spriteSourceSize"])
            color_x, color_y, color_w, color_h = integers(frame["spriteColorRect"])
            if cropped.size != (color_w, color_h):
                raise ValueError("Atlas dimensions do not match: " + name)
            image = Image.new("RGBA", source_size)
            image.paste(cropped, (color_x, color_y))
            output = io.BytesIO()
            image.save(output, format="PNG")
            save(Path("Sprites") / name, output.getvalue(),
                 "assets/GameArts.png#" + name, "sprite", source_size)

        # This atlas entry points at an empty region. The standalone PNG is the
        # actual red projectile resource; preserve its original pixels instead.
        for source, destination in (
                ("assets/enemy_bullet.png", "Sprites/enemy_bullet.png"),
                ("assets/button_normal.png", "Sprites/button_normal.png"),
                ("assets/button_highlight.png", "Sprites/button_highlight.png"),
                ("res/mipmap-xhdpi-v4/icon.png", "Logo.png"),
                ("assets/logo.png", "Title.png")):
            content = apk.read(source)
            dimensions = Image.open(io.BytesIO(content)).size
            save(Path(destination), content, source, "sprite", dimensions)
        for name in AUDIO:
            source = "assets/" + name + ".mp3"
            save(Path("Audio") / (name + ".mp3"), apk.read(source), source, "audio")

    manifest = {"reference": "Aircraft Fire 2.5 (czh.aircraftwar, versionCode 11)",
                "apkSha256": apk_hash,
                "extraction": "Atlas rotation and trimming restored; original standalone icon/title/audio bytes retained.",
                "excluded": ["native binaries", "DEX", "advertising", "purchases", "GameCenter", "more recommendations", "loading graphics"],
                "files": entries}
    manifest_path = ROOT / "docs/aircraft-fire-assets.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Extracted " + str(len(entries)) + " gameplay resources; manifest " + str(manifest_path))


if __name__ == "__main__":
    main()
