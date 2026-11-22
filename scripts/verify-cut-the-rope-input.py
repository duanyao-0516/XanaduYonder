#!/usr/bin/env python3
"""Reject stale or changed generated Cut the Rope build inputs before Unity packaging."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import runpy
import stat
import sys
import zipfile

RUNTIME = "Assets/Plugins/Android/CutTheRopeRuntime.aar"
CONTENT = "Assets/Plugins/Android/CutTheRopeContent.aar"
REPORT = "artifacts/cut-the-rope/runtime-build.json"
RUNTIME_SOURCE = "ThirdParty/CutTheRopeRuntime"
RUNTIME_MANIFEST = RUNTIME_SOURCE + "/AndroidManifest.xml"
RUNTIME_BUILDER = "scripts/cut-the-rope/build-runtime.py"
SOURCE_ARCHIVE = "ThirdParty/CutTheRopeContent/source/cut-the-rope-4399-142014.zip"
SOURCE_RESTORER = "scripts/cut-the-rope/restore-source.py"
CONTENT_INPUTS = (
    "scripts/cut-the-rope/build-content.py",
    "scripts/cut-the-rope/decode-hx.cjs",
    "ThirdParty/CutTheRopeContent/index.html",
    "ThirdParty/CutTheRopeContent/offline-interface.js",
    "ThirdParty/CutTheRopeContent/start.js",
)


def digest_stream(source):
    value = hashlib.sha256()
    for block in iter(lambda: source.read(1024 * 1024), b""):
        value.update(block)
    return value.hexdigest()


def digest(path):
    with path.open("rb") as source:
        return digest_stream(source)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def checked_path(root, relative, directory=False):
    """Resolve fixed relative paths without following existing symlinks under root."""
    parts = Path(relative).parts
    require(parts and not Path(relative).is_absolute() and ".." not in parts, "Invalid generated path")
    current = root
    for index, part in enumerate(parts):
        current = current / part
        try:
            mode = current.lstat().st_mode
        except FileNotFoundError:
            continue
        expected = stat.S_ISDIR if directory or index < len(parts) - 1 else stat.S_ISREG
        require(expected(mode), "Path is a symlink or unexpected file type: " + str(current))
    return current


def runtime_inputs(root):
    source = checked_path(root, RUNTIME_SOURCE + "/src", directory=True)
    java = [path.relative_to(root).as_posix() for path in source.rglob("*.java")]
    require(bool(java), "Missing runtime Java sources")
    return (RUNTIME_BUILDER, RUNTIME_MANIFEST, *sorted(java))


def input_hashes(root, paths):
    return {name: digest(checked_path(root, name)) for name in paths}


def safe_member(name):
    return bool(re.fullmatch(r"[a-zA-Z0-9._~/-]+", name)) and all(
        part not in ("", ".", "..") for part in name.split("/"))


def checked_archive(archive, byte_limit):
    entries = archive.infolist()
    names = {entry.filename for entry in entries}
    require(len(entries) == len(names), "Duplicate archive entry")
    require(sum(entry.file_size for entry in entries) <= byte_limit, "Archive exceeds expected size")
    for entry in entries:
        kind = stat.S_IFMT(entry.external_attr >> 16)
        require(safe_member(entry.filename) and kind in (0, stat.S_IFREG) and not entry.flag_bits & 1,
                "Unsafe archive entry: " + entry.filename)
    return names


def pinned_original(root):
    restore_script = checked_path(root, SOURCE_RESTORER)
    expected = runpy.run_path(str(restore_script))["ARCHIVE_SHA256"]
    source = checked_path(root, SOURCE_ARCHIVE)
    require(digest(source) == expected, "Original game ZIP differs from the pinned snapshot")
    with zipfile.ZipFile(source) as archive, archive.open("game/ctr.min.js") as member:
        return digest_stream(member)


def verify(root, generated_root=None):
    generated_root = generated_root or root
    report = json.loads(checked_path(generated_root, REPORT).read_text(encoding="utf-8"))
    runtime = checked_path(generated_root, RUNTIME)
    content = checked_path(generated_root, CONTENT)
    for key, name, path in (("runtimeAar", RUNTIME, runtime), ("contentAar", CONTENT, content)):
        require(report.get(key) == name and digest(path) == report.get(key + "Sha256"), "Changed " + key)
    require(report.get("sourceSha256") == input_hashes(root, runtime_inputs(root)), "Runtime source provenance differs")
    with zipfile.ZipFile(runtime) as archive:
        require(checked_archive(archive, 4 * 1024 * 1024) == {"AndroidManifest.xml", "classes.jar", "proguard.txt"},
                "Unexpected runtime archive inventory")
        require(archive.testzip() is None and archive.read("AndroidManifest.xml") == checked_path(root, RUNTIME_MANIFEST).read_bytes(),
                "Runtime archive integrity or Android manifest differs")

    with zipfile.ZipFile(content) as archive:
        actual_paths = checked_archive(archive, 256 * 1024 * 1024) - {"AndroidManifest.xml"}
        prefix = "assets/cut-the-rope/"
        manifest_path = prefix + "manifest.json"
        manifest = json.loads(archive.read(manifest_path))
        require(manifest.get("levelCount") == 425, "Incomplete ordinary level set")
        require(manifest.get("buildInputs") == input_hashes(root, CONTENT_INPUTS), "Content builder provenance differs")
        require(manifest.get("sourceSha256") == pinned_original(root), "Changed original game source")
        files = {prefix + file["path"]: file for file in manifest["files"]}
        require(len(files) == len(manifest["files"]) and manifest_path not in files and set(files) | {manifest_path} == actual_paths,
                "Content file set differs from conversion manifest")
        asset_list = report["contentAssets"]
        assets = {item["path"]: item for item in asset_list}
        require(len(assets) == len(asset_list) and set(assets) == actual_paths, "Runtime report asset set differs")
        for name in sorted(actual_paths):
            item = assets[name]
            with archive.open(name) as member:
                sha = digest_stream(member)
            size = archive.getinfo(name).file_size
            require(size == item["bytes"] and sha == item["sha256"], "Runtime report asset hash differs " + name)
            if name != manifest_path:
                require(size == files[name]["bytes"] and sha == files[name]["sha256"], "Content hash mismatch " + name)
        require("AndroidManifest.xml" in archive.namelist() and bool(archive.read("AndroidManifest.xml")),
                "Missing content Android manifest")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    try:
        verify(parser.parse_args().root.resolve())
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RuntimeError, zipfile.BadZipFile) as error:
        print(str(error) + ". Run scripts/cut-the-rope/prepare-build-input.py to restore or rebuild the inputs.", file=sys.stderr)
        return 1
    print("Verified current Cut the Rope runtime sources, conversion inputs and 425-level content archive.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
