#!/usr/bin/env python3
"""Create a filtered iOS build copy without modifying the source Unity project."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile


PROJECT_PARTS = ("Assets", "Packages", "ProjectSettings")
EXCLUDED_CONTENT = (
    "Assets/Plugins/Android",
    "Assets/StreamingAssets/fruit-slice",
    "Assets/Resources/KingdomRush",
    "Assets/Resources/LevelDevil",
    "Assets/Resources/ZhaoyunAdou",
    "Assets/Resources/Lobby/ClassicGoldMinerLogo.png",
    "Assets/Resources/Lobby/CutTheRopeLogo.png",
    "Assets/Resources/Lobby/Icons/classic-gold-miner.png",
    "Assets/Resources/Lobby/Icons/cut-the-rope.png",
    "Assets/Resources/Lobby/Icons/fruit-slice.png",
    "Assets/Resources/Lobby/Icons/fruit-slice-logo.jpg",
    "Assets/Resources/Lobby/Icons/kingdom-rush.png",
    "Assets/Resources/Lobby/Icons/level-devil.png",
    "Assets/Resources/Lobby/Icons/zhaoyun-adou.png",
)
EXCLUDED_PATHS = tuple(path for item in EXCLUDED_CONTENT for path in (item, item + ".meta"))
GOLD_MINER = "Assets/Resources/GoldMiner"


def excluded(relative):
    return any(relative == path or relative.startswith(path + "/") for path in EXCLUDED_PATHS)


def check_output_path(root, path):
    """Do not allow an artifact symlink to redirect a write into source assets."""
    if not path.is_relative_to(root / "artifacts") or path.resolve() != path:
        raise RuntimeError(f"Refusing unsafe artifact path: {path}")


def source_files(root):
    files = []
    for part in PROJECT_PARTS:
        directory = root / part
        if not directory.is_dir() or directory.is_symlink():
            raise RuntimeError(f"Required project directory is missing or linked: {directory}")
        for current, directories, names in os.walk(directory):
            for name in list(directories) + names:
                path = Path(current) / name
                relative = path.relative_to(root).as_posix()
                if excluded(relative):
                    if name in directories:
                        directories.remove(name)
                    continue
                if path.is_symlink() or not (path.is_dir() or path.is_file()):
                    raise RuntimeError(f"Unsupported source link or special file: {path}")
                if path.is_file():
                    files.append(relative)
    if not any(path.startswith(GOLD_MINER + "/") for path in files):
        raise RuntimeError("The Unity GoldMiner resources are missing; refusing incomplete staging")
    return sorted(files)


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def prepare(root):
    output = root / "artifacts" / "ios"
    stage = output / "project"
    manifest = output / "staging-manifest.json"
    for path in (output, stage, manifest):
        check_output_path(root, path)
    if stage.exists() and not stage.is_dir():
        raise RuntimeError(f"Staging destination is not a directory: {stage}")
    if manifest.exists() and not manifest.is_file():
        raise RuntimeError(f"Manifest destination is not a file: {manifest}")
    files = source_files(root)
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix=".prepare-", dir=output) as temporary:
        temporary = Path(temporary)
        candidate = temporary / "project"

        def ignore(directory, names):
            return [name for name in names if excluded((Path(directory) / name).relative_to(root).as_posix())]

        for part in PROJECT_PARTS:
            shutil.copytree(root / part, candidate / part, ignore=ignore)

        for path in EXCLUDED_PATHS:
            if (candidate / path).exists():
                raise RuntimeError(f"Excluded content remains in staging: {path}")
        copied = sorted(path.relative_to(candidate).as_posix() for path in candidate.rglob("*") if path.is_file())
        if copied != files:
            raise RuntimeError("Copied file inventory differs from the filtered source inventory")
        byte_count = 0
        for relative in files:
            source, target = root / relative, candidate / relative
            byte_count += target.stat().st_size
            if digest(source) != digest(target):
                raise RuntimeError(f"Source changed or copy verification failed: {relative}")

        data = {
            "schema_version": 1,
            "created_at": datetime.now(timezone.utc).isoformat(),
            "source_root": str(root),
            "staging_root": str(stage),
            "excluded_paths": list(EXCLUDED_PATHS),
            "excluded_source_paths_present": [path for path in EXCLUDED_PATHS if (root / path).exists()],
            "copied_file_count": len(files),
            "copied_bytes": byte_count,
            "gold_miner_file_count": sum(path.startswith(GOLD_MINER + "/") for path in files),
            "copied_files": files,
        }
        candidate_manifest = temporary / "staging-manifest.json"
        candidate_manifest.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

        # Publish only a completely verified copy. Preserve the previous copy on failure.
        backup = temporary / "previous-project"
        if stage.exists():
            stage.rename(backup)
        try:
            candidate.rename(stage)
            os.replace(candidate_manifest, manifest)
        except BaseException:
            if stage.exists():
                shutil.rmtree(stage)
            if backup.exists():
                backup.rename(stage)
            raise
    print(f"iOS staging: {stage}")
    print(f"Verified {len(files)} files ({byte_count} bytes); manifest: {manifest}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    try:
        prepare(Path(__file__).resolve().parent.parent)
    except (OSError, RuntimeError, shutil.Error) as error:
        print(f"iOS staging failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
