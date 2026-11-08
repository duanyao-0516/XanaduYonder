#!/usr/bin/env python3
"""Reject retired game runtimes and assets in source, APK/IPA, or exported player content.

GoldMiner source campaign numbers and attribution remain valid. Checks therefore
use retired runtime symbols and dedicated paths, never the shared word GoldMiner.
Historical documentation and old artifacts are outside the source-input scan.
"""

import argparse
import io
import json
from pathlib import Path
import re
import sys
import zipfile


RETIRED_NAME = re.compile(r"classic[-_ ]?gold[-_ ]?miner|age[-_ ]?of[-_ ]?war|"
                          r"kingdom[-_ ]?rush|fruit[-_ ]?slice", re.IGNORECASE)
RETIRED_LIBRARIES = frozenset((
    "libsdl3.so", "libjni_android.so", "libliblove.so", "liblove.so", "libluajit.so",
    "liboboe.so", "libopenal.so", "librender_sort_android.so", "librender_timsort.so",
))
RUNTIME_PATTERNS = (
    rb"XanaduYonder[./](?:Core|Runtime)[./](?:AgeOfWar|ClassicGoldMiner|KingdomRush|FruitSlice)",
    rb"com[./]xanaduyonder[./](?:classicgoldminer|kingdomrush|fruitslice)",
    rb"org[./](?:libsdl[./]app|love2d[./])",
    rb"(?:ClassicGoldMiner|KingdomRush|FruitSlice)(?:Launcher|Activity|Bridge|BackPressState)",
    rb"AgeOfWar(?:Page|OneBattle|TwoBattle|Selection|Assets|Audio|Frame|Command|Visual)",
    rb"(?:Enter|Ensure|Release|On)(?:ClassicGoldMiner|KingdomRush|FruitSlice)",
    rb"(?:ShowAgeOfWarSelection|EnterAgeOfWar|IsAgeOfWarPage)",
    rb"(?:assets[\\/]|Assets[\\/]StreamingAssets[\\/])(?:classic-gold-miner|fruit-slice)[\\/]",
    rb"(?:Assets[\\/]Resources[\\/])?(?:AgeOfWar[\\/](?:One|Two)|KingdomRush[\\/]Logo)",
    rb"Lobby[\\/](?:AgeOfWarLogo|ClassicGoldMinerLogo|Icons[\\/](?:age-of-war|classic-gold-miner|kingdom-rush|fruit-slice))",
    rb"(?:kingdomrush\.unity_activity|SDL_ENV\.SDL_ANDROID_ALLOW_RECREATE_ACTIVITY)",
)
RUNTIME = re.compile(b"|".join(b"(?:" + pattern + b")" for pattern in RUNTIME_PATTERNS), re.IGNORECASE)
SOURCE_SUFFIXES = {".cs", ".java", ".xml", ".mm", ".h", ".gradle", ".properties", ".asmdef"}
ARCHIVE_SUFFIXES = {".aar", ".jar", ".zip", ".apk", ".ipa"}


def reject_path(name):
    normalized = name.replace("\\", "/")
    if RETIRED_NAME.search(normalized):
        raise ValueError("Retired game path remains: " + name)
    if Path(normalized).name.lower() in RETIRED_LIBRARIES or normalized.lower().endswith("/game.love"):
        raise ValueError("Retired native/content payload remains: " + name)


def scan_stream(stream, label):
    tail = b""
    while True:
        data = stream.read(1024 * 1024)
        if not data:
            return
        block = tail + data
        # Android's binary manifest may use UTF-16 strings; native metadata uses UTF-8.
        match = RUNTIME.search(block) or RUNTIME.search(block.replace(b"\0", b""))
        if match:
            raise ValueError("Retired runtime/resource symbol in " + label + ": " +
                             match.group().decode("ascii", errors="replace"))
        tail = block[-512:]


def scan_archive(archive, label):
    names = archive.namelist()
    if len(names) != len(set(names)):
        raise ValueError("Duplicate ZIP entries: " + label)
    count = 0
    for entry in archive.infolist():
        reject_path(entry.filename)
        if entry.is_dir():
            continue
        member = label + "!" + entry.filename
        with archive.open(entry) as stream:
            if Path(entry.filename).suffix.lower() in ARCHIVE_SUFFIXES:
                with zipfile.ZipFile(io.BytesIO(stream.read())) as nested:
                    count += scan_archive(nested, member)
            else:
                scan_stream(stream, member)
                count += 1
    return count


def verify_source(root):
    root = Path(root).resolve()
    if not (root / "Assets/Scripts").is_dir():
        raise ValueError("Unity source root is missing: " + str(root))
    count = 0
    for folder in ("Assets", "ThirdParty", "Tests", "scripts"):
        directory = root / folder
        if not directory.is_dir():
            continue
        for path in directory.rglob("*"):
            relative = path.relative_to(root).as_posix()
            # Do not inspect caches, generated test binaries, or historical evidence.
            if any(part in {"__pycache__", "bin", "obj"} for part in path.relative_to(directory).parts):
                continue
            reject_path(relative)
            if not path.is_file():
                continue
            if folder == "Assets" and path.suffix.lower() in SOURCE_SUFFIXES:
                with path.open("rb") as stream:
                    scan_stream(stream, relative)
            elif folder == "Assets" and path.suffix.lower() in ARCHIVE_SUFFIXES:
                with zipfile.ZipFile(path) as archive:
                    scan_archive(archive, relative)
            count += 1
    if not any((root / "Assets/Resources/GoldMiner").rglob("*.json")):
        raise ValueError("Native GoldMiner resources are missing")
    return count


def verify_packed_assets(path):
    path = Path(path)
    if not path.is_file():
        raise ValueError("The current build's packed-asset inventory is required: " + str(path))
    names = [line.strip() for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    if not names:
        raise ValueError("Packed-asset inventory is empty")
    for name in names:
        reject_path(name)
    if not any(name.startswith("Assets/Resources/GoldMiner/") for name in names):
        raise ValueError("Native GoldMiner was not recorded in packed assets")
    return len(names)


def verify_artifact(path, packed_assets):
    path = Path(path)
    packed_count = verify_packed_assets(packed_assets)
    if path.is_dir():
        count = 0
        for child in path.rglob("*"):
            reject_path(child.relative_to(path).as_posix())
            if child.is_file():
                if child.suffix.lower() in ARCHIVE_SUFFIXES:
                    with zipfile.ZipFile(child) as archive:
                        count += scan_archive(archive, str(child))
                else:
                    with child.open("rb") as stream:
                        scan_stream(stream, str(child))
                    count += 1
        if not count:
            raise ValueError("Exported artifact directory is empty")
    elif path.is_file() and path.stat().st_size:
        with zipfile.ZipFile(path) as archive:
            count = scan_archive(archive, str(path))
    else:
        raise ValueError("A nonempty APK, IPA, or export directory is required: " + str(path))
    return {"artifactFilesChecked": count, "packedAssetsChecked": packed_count}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path)
    parser.add_argument("--artifact", type=Path)
    parser.add_argument("--packed-assets", type=Path)
    args = parser.parse_args()
    if not args.source_root and not args.artifact:
        parser.error("provide --source-root and/or --artifact")
    if args.artifact and not args.packed_assets:
        parser.error("--artifact requires the current build's --packed-assets")
    result = {"removedGamePayloadsAbsent": True, "gameplayOrDeviceRuntimeTested": False}
    if args.source_root:
        result["sourceFilesChecked"] = verify_source(args.source_root)
    if args.artifact:
        result.update(verify_artifact(args.artifact, args.packed_assets))
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, zipfile.BadZipFile, RuntimeError) as error:
        print("Removed-game verification failed: " + str(error), file=sys.stderr)
        sys.exit(1)
