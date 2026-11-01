#!/usr/bin/env python3
"""Reject stale or changed generated Cut the Rope build inputs before Unity packaging."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise SystemExit(message + ". Regenerate offline content and run scripts/cut-the-rope/build-runtime.py.")


def verify(root):
    report_path = root / "artifacts/cut-the-rope/runtime-build.json"
    require(report_path.is_file(), "Missing Cut the Rope runtime build report")
    report = json.loads(report_path.read_text())
    for key in ("runtimeAar", "contentAar"):
        require(key in report, "Incomplete runtime/content build report")
        path = root / report[key]
        require(path.is_file() and digest(path) == report[key + "Sha256"], "Changed " + key)
    require(bool(report.get("sourceSha256")), "Missing runtime source provenance")
    recorded_java = {name for name in report["sourceSha256"] if name.endswith(".java")}
    actual_java = {path.relative_to(root).as_posix() for path in (root / "ThirdParty/CutTheRopeRuntime/src").rglob("*.java")}
    require(recorded_java == actual_java, "Runtime Java source inventory changed")
    require("scripts/cut-the-rope/build-runtime.py" in report["sourceSha256"], "Missing runtime builder provenance")
    for name, expected in report["sourceSha256"].items():
        path = root / name
        require(path.is_file() and digest(path) == expected, "Stale runtime source " + name)
    with zipfile.ZipFile(root / report["contentAar"]) as archive:
        require(archive.testzip() is None, "Invalid content archive CRC")
        prefix = "assets/cut-the-rope/"
        manifest = json.loads(archive.read(prefix + "manifest.json"))
        require(manifest.get("levelCount") == 425, "Incomplete ordinary level set")
        require(bool(manifest.get("buildInputs")), "Missing content builder provenance")
        for name, expected in manifest["buildInputs"].items():
            path = root / name
            require(path.is_file() and digest(path) == expected, "Stale content input " + name)
        original = root / "artifacts/cut-the-rope-4399-142014/game/ctr.min.js"
        require(original.is_file() and digest(original) == manifest["sourceSha256"], "Changed original game source")
        expected_paths = {prefix + file["path"] for file in manifest["files"]} | {prefix + "manifest.json"}
        actual_paths = {name for name in archive.namelist() if name.startswith(prefix) and not name.endswith("/")}
        require(actual_paths == expected_paths, "Content file set differs from conversion manifest")
        for file in manifest["files"]:
            payload = archive.read(prefix + file["path"])
            require(len(payload) == file["bytes"] and hashlib.sha256(payload).hexdigest() == file["sha256"], "Content hash mismatch " + file["path"])
        require({item["path"] for item in report.get("contentAssets", [])} == actual_paths, "Runtime report asset set differs")
        for item in report["contentAssets"]:
            require(hashlib.sha256(archive.read(item["path"])).hexdigest() == item["sha256"], "Runtime report asset hash differs " + item["path"])
    print("Verified current Cut the Rope runtime sources, conversion inputs and 425-level content archive.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    verify(parser.parse_args().root.resolve())
