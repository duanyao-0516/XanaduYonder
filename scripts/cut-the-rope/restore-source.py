#!/usr/bin/env python3
"""Restore the pinned original game snapshot offline, without replacing local files."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "ThirdParty/CutTheRopeContent/source/cut-the-rope-4399-142014.zip"
ARCHIVE_SHA256 = "e8e5e8295dbcf12b99e513beb909fbd1411f8deae722bbccc5f1afdb192083c8"
MANIFEST = "evidence/download-manifest.json"
MANIFEST_SHA256 = "3cde84de9ffb00f4e63f6a9b29e5d6e4f72aedbeb12fcfea0686d7e23d073545"


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def safe_name(name):
    if not re.fullmatch(r"[a-zA-Z0-9._~/-]+", name) or any(
        part in ("", ".", "..") for part in name.split("/")
    ) or PurePosixPath(name).is_absolute():
        raise ValueError("Unsafe archive path: " + name)
    return name


def payloads():
    if digest(ARCHIVE) != ARCHIVE_SHA256:
        raise ValueError("Source archive SHA-256 differs from the pinned snapshot")
    with zipfile.ZipFile(ARCHIVE) as archive:
        entries = archive.infolist()
        names = [safe_name(entry.filename) for entry in entries]
        if len(names) != len(set(names)):
            raise ValueError("Duplicate archive paths")
        for entry in entries:
            kind = stat.S_IFMT(entry.external_attr >> 16)
            if entry.is_dir() or kind not in (0, stat.S_IFREG) or entry.flag_bits & 1:
                raise ValueError("Archive entry is not a regular unencrypted file: " + entry.filename)
        manifest = archive.read(MANIFEST)
        if hashlib.sha256(manifest).hexdigest() != MANIFEST_SHA256:
            raise ValueError("Original download manifest SHA-256 differs")
        records = [entry for entry in json.loads(manifest)["files"] if entry.get("status") == 200]
        expected = {"game/" + safe_name(entry["path"]): entry for entry in records}
        if len(records) != 218 or len(expected) != 218 or set(names) != set(expected) | {MANIFEST}:
            raise ValueError("Archive inventory must be exactly 218 original files and the download manifest")
        result = {MANIFEST: manifest}
        for name, entry in expected.items():
            if archive.getinfo(name).file_size != entry["bytes"]:
                raise ValueError("Source size differs: " + name)
            data = archive.read(name)
            if hashlib.sha256(data).hexdigest() != entry["sha256"]:
                raise ValueError("Source SHA-256 differs: " + name)
            result[name] = data
        return result


def check_directories(path):
    # Do not resolve away symlinks in a user-supplied output path.
    for directory in reversed((path, *path.parents)):
        try:
            mode = directory.lstat().st_mode
        except FileNotFoundError:
            continue
        if not stat.S_ISDIR(mode):
            raise ValueError("Output conflict: directory is a symlink or non-directory: " + str(directory))


def restore(output):
    data = payloads()
    pending = []
    # Check every existing destination before creating any output files.
    for name, expected in data.items():
        destination = output / name
        check_directories(destination.parent)
        try:
            mode = destination.lstat().st_mode
        except FileNotFoundError:
            pending.append((destination, expected))
            continue
        if not stat.S_ISREG(mode) or destination.read_bytes() != expected:
            raise ValueError("Output conflict; existing file was not changed: " + str(destination))
    for destination, expected in pending:
        check_directories(destination.parent)
        destination.parent.mkdir(parents=True, exist_ok=True)
        check_directories(destination.parent)
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(prefix=".ctr-restore-", dir=destination.parent, delete=False) as stream:
                temporary = Path(stream.name)
                stream.write(expected)
                stream.flush()
                os.fsync(stream.fileno())
            # Atomic creation: a concurrently created destination is never overwritten.
            os.link(temporary, destination)
        finally:
            if temporary is not None:
                temporary.unlink()
    return {"output": str(output), "files": len(data), "restored": len(pending),
            "retained": len(data) - len(pending), "archiveSha256": ARCHIVE_SHA256,
            "manifestSha256": MANIFEST_SHA256}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/cut-the-rope-4399-142014",
                        help="Snapshot root; game/ and evidence/ are restored beneath it")
    arguments = parser.parse_args()
    output = Path(os.path.abspath(arguments.output.expanduser()))
    try:
        # Resolve parent aliases such as macOS /var -> /private/var, but never
        # follow a symlink at the selected output root or inside the snapshot.
        output = output.parent.resolve() / output.name
        print(json.dumps(restore(output), indent=2))
    except (OSError, ValueError, KeyError, RuntimeError, zipfile.BadZipFile) as error:
        print("restore-source: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
