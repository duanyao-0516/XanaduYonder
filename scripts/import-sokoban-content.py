#!/usr/bin/env python3
"""Reproduce the release bundle, verify it with production C#, then atomically publish.

No network access or runtime solver. Source rows and selected CC0 media bytes are
copied unchanged. A failed validation never replaces the last good directory.
"""
import argparse
import ast
import ctypes
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[1]
VERSION = "sokoban-campaign-v1"
PROTOTYPE_FIXTURES = {"tutorial-001": "compact", "tutorial-008": "tall"}
SOURCE_HASH = "272928a4e7c185fdf84daa523b298750b6ff08703cb0c20d3be7eff93acc5256"
SOURCE_COMMIT = "78011d8c86ad423bfb2e1005012393b4c3c24409"
BOXOBAN = "boxoban-unfiltered-test-000"
ART = "ThirdParty/Sokoban/kenney-sokoban"
SOUND = "ThirdParty/Sokoban/kenney-interface-sounds"
UNITY_VERSION = "6000.3.27f1"
SPRITE_META_SOURCE = "Data/Sokoban/campaign/unity-importer"
MEDIA = {
    "Sprites/wall.png": (ART, "PNG/Default size/Blocks/block_06.png"),
    "Sprites/crate.png": (ART, "PNG/Default size/Crates/crate_02.png"),
    "Sprites/crate-goal.png": (ART, "PNG/Default size/Crates/crate_05.png"),
    "Sprites/player-down.png": (ART, "PNG/Default size/Player/player_05.png"),
    "Sprites/player-up.png": (ART, "PNG/Default size/Player/player_08.png"),
    "Sprites/player-right.png": (ART, "PNG/Default size/Player/player_17.png"),
    "Sprites/player-left.png": (ART, "PNG/Default size/Player/player_20.png"),
    "Audio/move.ogg": (SOUND, "Audio/tick_001.ogg"),
    "Audio/push.ogg": (SOUND, "Audio/click_001.ogg"),
    "Audio/goal.ogg": (SOUND, "Audio/confirmation_001.ogg"),
    "Audio/complete.ogg": (SOUND, "Audio/confirmation_002.ogg"),
}
LICENSES = {
    "Licenses/Kenney-Sokoban.txt": f"{ART}/License.txt",
    "Licenses/Kenney-Interface-Sounds.txt": f"{SOUND}/License.txt",
    "Licenses/Boxoban-Apache-2.0.txt": "Data/Sokoban/boxoban/LICENSE",
    "Licenses/Boxoban-Apache-2.0-original.txt": "Data/Sokoban/boxoban/LICENSE.txt",
    "Licenses/Boxoban-Upstream-README.txt": "Data/Sokoban/boxoban/UPSTREAM-README.md",
    "Licenses/Xanadu-Yonder-MIT.txt": "LICENSE",
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def map_hash(rows):
    return digest(("\n".join(rows) + "\n").encode("utf-8"))


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON property: {key}")
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def replay(rows, solution):
    """Independent 2-D replay; deliberately does not share production helpers."""
    require(isinstance(rows, list) and 3 <= len(rows) <= 14, "Invalid map height")
    require(all(isinstance(row, str) for row in rows), "Rows must be strings")
    width = len(rows[0])
    require(3 <= width <= 10 and all(len(row) == width for row in rows), "Invalid rectangle")
    require(all(c in "# .$@*+" for row in rows for c in row), "Invalid map character")
    require(all(c == "#" for c in rows[0] + rows[-1]) and all(row[0] == row[-1] == "#" for row in rows), "Open map boundary")
    players = [(x, y) for y, row in enumerate(rows) for x, c in enumerate(row) if c in "@+"]
    boxes = {(x, y) for y, row in enumerate(rows) for x, c in enumerate(row) if c in "$*"}
    goals = {(x, y) for y, row in enumerate(rows) for x, c in enumerate(row) if c in ".*+"}
    require(len(players) == 1 and len(boxes) == len(goals) and len(boxes) > 0, "Invalid player/box/goal count")
    require(boxes != goals, "Initially completed level")
    require(isinstance(solution, str) and 0 < len(solution) <= 50000 and set(solution) <= set("LURD"), "Invalid solution")
    player, pushes = players[0], 0
    for step, letter in enumerate(solution):
        require(boxes != goals, "Solution has actions after victory")
        dx, dy = {"L": (-1, 0), "U": (0, -1), "R": (1, 0), "D": (0, 1)}[letter]
        target = player[0] + dx, player[1] + dy
        require(0 <= target[0] < width and 0 <= target[1] < len(rows) and rows[target[1]][target[0]] != "#", f"Blocked witness step {step + 1}")
        if target in boxes:
            after = target[0] + dx, target[1] + dy
            require(0 <= after[0] < width and 0 <= after[1] < len(rows) and rows[after[1]][after[0]] != "#" and after not in boxes, f"Blocked push at step {step + 1}")
            boxes.remove(target)
            boxes.add(after)
            pushes += 1
        player = target
    require(boxes == goals, "Witness does not solve map")
    return {"moves": len(solution), "pushes": pushes, "finalPlayer": player[1] * width + player[0], "finalBoxes": sorted(y * width + x for x, y in boxes)}


def prototype_fixture_rows(prototype):
    """Read the prototype's static fixture literal without executing its script."""
    match = re.search(r"\bconst\s+fallbackLevels\s*=\s*(\[.*?\])\s*;", prototype, re.DOTALL)
    require(match is not None, "Prototype fallbackLevels fixture array missing")
    try:
        literal = ast.parse(match.group(1), mode="eval")
        # JavaScript permits bare object keys; the fixture values are literals.
        for node in ast.walk(literal):
            if isinstance(node, ast.Dict):
                node.keys = [ast.Constant(key.id) if isinstance(key, ast.Name) else key for key in node.keys]
        fixtures = ast.literal_eval(literal)
    except (SyntaxError, ValueError) as error:
        raise ValueError("Prototype fallbackLevels must contain static literal fixtures") from error
    require(isinstance(fixtures, list), "Prototype fallbackLevels must be a list")
    by_id = {}
    for fixture in fixtures:
        require(isinstance(fixture, dict) and isinstance(fixture.get("levelId"), str), "Prototype fixture requires a levelId")
        level_id = fixture["levelId"]
        require(level_id not in by_id, f"Duplicate prototype fixture: {level_id}")
        rows = fixture.get("rows")
        require(isinstance(rows, list) and rows and all(isinstance(row, str) for row in rows), f"Prototype fixture rows missing or invalid: {level_id}")
        by_id[level_id] = rows
    for level_id in PROTOTYPE_FIXTURES:
        require(level_id in by_id, f"Prototype fixture missing: {level_id}")
    return {level_id: by_id[level_id] for level_id in PROTOTYPE_FIXTURES}


def build_campaign(tutorials_path):
    source_path = ROOT / "Data/Sokoban/boxoban/unfiltered-test-000.txt"
    spec = importlib.util.spec_from_file_location("boxoban_source", ROOT / "Data/Sokoban/verification/verify_boxoban.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    source, source_hash = module.parse_source(source_path)
    require(source_hash == SOURCE_HASH, "Source archive changed")
    source_by_id = {item["source_id"]: item["rows"] for item in source}
    tutorials = read_json(tutorials_path)
    require(tutorials["schemaVersion"] == 1 and tutorials["contentVersion"] == VERSION and len(tutorials["levels"]) == 8, "Expected eight versioned tutorials")
    candidates = read_json(ROOT / "Data/Sokoban/verification/verified-candidates.json")
    require(candidates["source_sha256"] == SOURCE_HASH and candidates["source_commit"] == SOURCE_COMMIT, "Candidate provenance changed")
    require(len(candidates["levels"]) == 100, "Exactly 100 candidate witnesses are required")
    records = []
    for index, level in enumerate(tutorials["levels"]):
        level_id = f"tutorial-{index + 1:03d}"
        require(level["levelId"] == level_id, "Tutorial IDs must remain stable")
        records.append({"schemaVersion": 1, "levelId": level_id, "contentVersion": VERSION, "chapter": "tutorial", "order": index + 1, "title": level["title"], "source": {"collection": "xanadu-original", "sourceId": level_id}, "rows": level["rows"], "solution": level["solution"]})
    # The two visual review fixtures are compared to the actual approved prototype.
    prototype = (ROOT / "docs/prototypes/sokoban/prototype.js").read_text(encoding="utf-8")
    records_by_id = {record["levelId"]: record for record in records}
    for level_id, rows in prototype_fixture_rows(prototype).items():
        require(records_by_id[level_id]["rows"] == rows, f"Tutorial {level_id} differs from approved {PROTOTYPE_FIXTURES[level_id]} map")
    previous_id = -1
    for index, level in enumerate(candidates["levels"]):
        source_id = level["source_id"]
        require(type(source_id) is int and source_id > previous_id, "Candidates must retain strict source order")
        previous_id = source_id
        require(level["rows"] == source_by_id[source_id], "Candidate rows differ from upstream source")
        require(level["id"] == f"boxoban-unfiltered-test-000-{source_id:04d}", "Source ID was renamed")
        checked = replay(level["rows"], level["moves_lurd"])
        require(checked["moves"] == level["replay"]["steps"] and checked["pushes"] == level["replay"]["pushes"], "Candidate witness counts changed")
        records.append({"schemaVersion": 1, "levelId": level["id"], "contentVersion": VERSION, "chapter": f"warehouse-{index // 20 + 1:02d}", "order": index + 9, "title": f"仓库 {index // 20 + 1} · {index % 20 + 1:02d}", "source": {"collection": BOXOBAN, "sourceId": str(source_id)}, "rows": level["rows"], "solution": level["moves_lurd"]})
    identities, sources, maps = set(), set(), set()
    proofs = []
    for record in records:
        identity = record["levelId"]
        source_key = tuple(record["source"].values())
        normalized = map_hash(record["rows"])
        require(identity not in identities and source_key not in sources and normalized not in maps, "Duplicate identity, source or exact map")
        identities.add(identity)
        sources.add(source_key)
        maps.add(normalized)
        proofs.append({"levelId": identity, "mapSha256": normalized, **replay(record["rows"], record["solution"])})
    return {"schemaVersion": 1, "contentVersion": VERSION, "levels": records}, proofs


def sprite_meta(path, directory=None):
    """Preserve Unity's complete normalized importer state, including generated IDs."""
    directory = ROOT / SPRITE_META_SOURCE if directory is None else directory
    manifest = read_json(directory / "manifest.json")
    require(type(manifest["schemaVersion"]) is int and manifest["schemaVersion"] == 1 and manifest["unityVersion"] == UNITY_VERSION, "Unsupported canonical Unity importer version")
    entries = manifest["files"]
    expected_paths = {name + ".meta" for name in MEDIA if name.endswith(".png")}
    require(len(entries) == len(expected_paths) and {entry["path"] for entry in entries} == expected_paths, "Canonical sprite metadata set is incomplete or duplicated")
    entry = next((entry for entry in entries if entry["path"] == path + ".meta"), None)
    require(entry is not None, f"No canonical Unity metadata for {path}")
    data = (directory / entry["path"]).read_bytes()
    require(digest(data) == entry["sha256"], f"Canonical Unity metadata hash mismatch: {path}")
    return data.decode("utf-8")


def meta(path, folder=False):
    stable_id = uuid.uuid5(uuid.NAMESPACE_URL, "xanadu-yonder/sokoban/" + path).hex
    prefix = f"fileFormatVersion: 2\nguid: {stable_id}\n"
    if folder:
        return prefix + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if path.endswith(".png"):
        return sprite_meta(path)
    if path.endswith(".ogg"):
        return prefix + """AudioImporter:
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
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""
    return prefix + "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def ogg_duration(data):
    identification = data.index(b"\x01vorbis")
    rate = struct.unpack_from("<I", data, identification + 12)[0]
    at, granules = 0, []
    while at < len(data):
        require(data[at:at + 4] == b"OggS", "Invalid OGG page")
        value = struct.unpack_from("<Q", data, at + 6)[0]
        if value < 2**63:
            granules.append(value)
        segments = data[at + 26]
        at += 27 + segments + sum(data[at + 27:at + 27 + segments])
    return round(max(granules) / rate, 6)


def unchanged_ids(catalog, output):
    existing = output / "campaign.json"
    if not existing.is_file():
        return
    old = read_json(existing)
    old_maps = {level["levelId"]: map_hash(level["rows"]) for level in old["levels"]}
    if old["contentVersion"] == catalog["contentVersion"]:
        require(set(old_maps) == {level["levelId"] for level in catalog["levels"]}, "Catalog membership changed under an existing contentVersion")
        for level in catalog["levels"]:
            require(level["levelId"] not in old_maps or old_maps[level["levelId"]] == map_hash(level["rows"]), "Map changed under an existing contentVersion/levelId")


def generate(stage, tutorials, output):
    catalog, proofs = build_campaign(tutorials)
    unchanged_ids(catalog, output)
    write_json(stage / "campaign.json", catalog)
    subprocess.run(["bash", str(ROOT / "scripts/test-sokoban-content.sh"), "--catalog", str(stage / "campaign.json"), "--report", str(stage / "verification-report.json")], check=True, cwd=ROOT)
    report = read_json(stage / "verification-report.json")
    require(report["levels"] == proofs, "Independent replay and production C# replay disagree")
    assets = []
    source_manifests = {base: read_json(ROOT / base / "source-manifest.json") for base in (ART, SOUND)}
    source_manifests["Data/Sokoban/boxoban"] = read_json(ROOT / "Data/Sokoban/boxoban/source-manifest.json")
    for destination, (base, source) in MEDIA.items():
        data = (ROOT / base / source).read_bytes()
        pinned = next(record["sha256"] for record in source_manifests[base]["files"] if record["path"] == source)
        require(digest(data) == pinned, f"Source media hash mismatch: {source}")
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        entry = {"path": destination, "source": f"{base}/{source}", "sha256": digest(data), "bytes": len(data), "license": "CC0-1.0", "modified": False}
        if destination.endswith(".png"):
            entry["width"], entry["height"] = struct.unpack_from(">II", data, 16)
        else:
            entry["durationSeconds"] = ogg_duration(data)
            require(entry["durationSeconds"] < 1, "Only short feedback clips are selected")
        assets.append(entry)
    licenses = []
    for destination, source in LICENSES.items():
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        data = (ROOT / source).read_bytes()
        for base, archived in source_manifests.items():
            if source.startswith(base + "/"):
                relative = source[len(base) + 1:]
                pinned = next(record["sha256"] for record in archived["files"] if record["path"] == relative)
                require(digest(data) == pinned, f"Source license/attribution hash mismatch: {source}")
        target.write_bytes(data)
        licenses.append({"path": destination, "source": source, "sha256": digest(data), "modified": False})
    # Preserve the upstream research citation alongside the Apache license.
    (stage / "Licenses/Boxoban-Attribution.txt").write_text("Boxoban levels — Google DeepMind\nhttps://github.com/google-deepmind/boxoban-levels\nCommit: " + SOURCE_COMMIT + "\nSource: unfiltered/test/000.txt\nLicense: Apache-2.0 (both upstream license files are included).\nOnly the selected level data is reused; game code and tutorial levels are original.\nThe complete upstream citation is preserved in Boxoban-Upstream-README.txt.\n", encoding="utf-8")
    for target in sorted(stage.rglob("*")):
        relative = target.relative_to(stage).as_posix()
        Path(str(target) + ".meta").write_text(meta(relative, target.is_dir()), encoding="utf-8")
    # Self-manifest is excluded from its own hash list; every other release file,
    # including import metadata, licenses and production report, is covered.
    (stage / "manifest.json.meta").write_text(meta("manifest.json"), encoding="utf-8")
    manifest = {
        "schemaVersion": 1, "contentVersion": VERSION,
        "catalogSha256": digest((stage / "campaign.json").read_bytes()),
        "levelCount": 108, "tutorialCount": 8, "boxobanCount": 100,
        "ordering": "Tutorial order, then preserved Boxoban source ID order in five neutral chapters of 20.",
        "humanDifficultyGraded": False, "optimalMovesClaimed": False,
        "chapters": [{"id": "tutorial", "title": "初识搬运", "count": 8}] + [{"id": f"warehouse-{i:02d}", "title": f"仓库 {i}", "count": 20} for i in range(1, 6)],
        "source": {"repository": "https://github.com/google-deepmind/boxoban-levels", "commit": SOURCE_COMMIT, "path": "unfiltered/test/000.txt", "sha256": SOURCE_HASH, "license": "Apache-2.0"},
        "inputs": [{"path": path, "sha256": digest((ROOT / path).read_bytes())} for path in ["Data/Sokoban/campaign/tutorials.json", "Data/Sokoban/verification/verified-candidates.json", "Data/Sokoban/boxoban/source-manifest.json", f"{ART}/source-manifest.json", f"{SOUND}/source-manifest.json", f"{SPRITE_META_SOURCE}/manifest.json"] + [f"{SPRITE_META_SOURCE}/{path}.meta" for path in sorted(MEDIA) if path.endswith(".png")]],
        "unitySpriteImporterVersion": UNITY_VERSION,
        "media": assets,
        "licenses": licenses,
        "levels": [{"levelId": level["levelId"], "source": level["source"], "mapSha256": proof["mapSha256"], "solutionSha256": digest(level["solution"].encode("ascii")), "moves": proof["moves"], "pushes": proof["pushes"]} for level, proof in zip(catalog["levels"], proofs)],
        "files": [{"path": path.relative_to(stage).as_posix(), "sha256": digest(path.read_bytes())} for path in sorted(stage.rglob("*")) if path.is_file()],
    }
    # Alternate tutorial inputs are used by fault tests and must retain honest provenance.
    manifest["inputs"][0] = {"path": "Data/Sokoban/campaign/tutorials.json", "sha256": digest(tutorials.read_bytes())}
    write_json(stage / "manifest.json", manifest)
    return manifest


def tree_hashes(root):
    return {p.relative_to(root).as_posix(): digest(p.read_bytes()) for p in root.rglob("*") if p.is_file()}


def atomic_publish(stage, output):
    """Same-filesystem atomic directory exchange; never a delete-then-copy window."""
    if not output.exists():
        os.replace(stage, output)
        return
    libc = ctypes.CDLL(None, use_errno=True)
    if sys.platform == "darwin":
        swap = libc.renamex_np
        swap.argtypes = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint]
        status = swap(os.fsencode(stage), os.fsencode(output), 2)  # RENAME_SWAP
    elif sys.platform.startswith("linux") and hasattr(libc, "renameat2"):
        swap = libc.renameat2
        swap.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
        status = swap(-100, os.fsencode(stage), -100, os.fsencode(output), 2)  # RENAME_EXCHANGE
    else:
        raise RuntimeError("Atomic directory exchange is unavailable; previous output is untouched")
    if status != 0:
        raise OSError(ctypes.get_errno(), "Atomic publication failed; previous output is untouched")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Build and verify in staging, then compare without modifying output")
    parser.add_argument("--tutorials", type=Path, default=ROOT / "Data/Sokoban/campaign/tutorials.json")
    parser.add_argument("--output", type=Path, default=ROOT / "Assets/Resources/Sokoban")
    args = parser.parse_args()
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    # Stage beside the output so the final rename cannot cross filesystem boundaries.
    with tempfile.TemporaryDirectory(prefix=".sokoban-import-", dir=output.parent) as temp:
        stage = Path(temp) / "Sokoban"
        stage.mkdir()
        generate(stage, args.tutorials.resolve(), output)
        if args.check:
            require(output.is_dir() and tree_hashes(stage) == tree_hashes(output), "Published bundle differs from reproducible import")
            print("PASS: imported bundle, hashes and production report reproduce exactly")
        else:
            atomic_publish(stage, output)
            folder_meta = Path(str(output) + ".meta")
            if not folder_meta.exists():
                folder_meta.write_text(meta("", True), encoding="utf-8")
            print(f"Published 108 verified levels, 7 sprites and 4 short clips: {output}")


if __name__ == "__main__":
    main()
