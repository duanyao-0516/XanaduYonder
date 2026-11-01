#!/usr/bin/env python3
"""Verify the delivered APK's offline Level Devil assets and complete permission set."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PREFIX = "assets/level-devil/"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verify_payload(apk, content):
    names = apk.namelist()
    require(len(names) == len(set(names)), "Duplicate paths in APK")
    expected_names = {name for name in content.namelist() if name.startswith(PREFIX)}
    actual_names = {name for name in names if name.startswith(PREFIX)}
    require(actual_names == expected_names, "APK content differs from the current content AAR")
    manifest = json.loads(content.read(PREFIX + "manifest.json"))
    files = manifest["files"]
    require(len({item["path"] for item in files}) == len(files), "Duplicate manifest paths")
    require(expected_names == {PREFIX + item["path"] for item in files} | {PREFIX + "manifest.json"},
            "AAR contains files outside the game manifest")
    for name in sorted(expected_names):
        require(apk.read(name) == content.read(name), "Stale or modified APK asset: " + name)
    for item in files:
        data = apk.read(PREFIX + item["path"])
        require(len(data) == item["bytes"] and sha(data) == item["sha256"],
                "Asset size/hash mismatch: " + item["path"])
    require(not any("poki-sdk" in name.lower() for name in names), "Advertising SDK packaged in APK")
    for item in files:
        if item["path"].endswith(".js"):
            data = apk.read(PREFIX + item["path"])
            require(b"window.__test" not in data and b"window.__restoreStorage" not in data,
                    "Browser verification hook packaged in APK: " + item["path"])
    require(not any(name.startswith(PREFIX) and any(word in name.lower() for word in
                ("probe", "fixture", "instrumented", "research", "verification")) for name in names),
            "Research fixture packaged in APK")
    # Both the level database and all original sounds must survive packaging.
    require(PREFIX + "lib/default.pak" in names, "Original scene/resource archive is missing")
    require(sum(item["path"].endswith(".mp3") for item in files) == 19, "Expected 19 original MP3 files")
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--aapt", type=Path, required=True)
    args = parser.parse_args()
    permissions = subprocess.check_output([str(args.aapt), "dump", "permissions", str(args.apk)], text=True)
    used = set(re.findall(r"uses-permission(?:-sdk-\d+)?: name='([^']+)'", permissions))
    # VIBRATE belongs to the collection's existing optional haptics. This game
    # introduces no permissions; any transitive-library addition fails closed.
    require(used <= {"android.permission.VIBRATE"}, "Unexpected APK permission(s): " + ", ".join(sorted(used - {"android.permission.VIBRATE"})))
    badging = subprocess.check_output([str(args.aapt), "dump", "badging", str(args.apk)], text=True)
    require("application-debuggable" not in badging, "Delivery APK must not enable debugging")
    manifest = subprocess.check_output([str(args.aapt), "dump", "xmltree", str(args.apk), "AndroidManifest.xml"], text=True)
    activities = re.split(r"\n\s*E: activity(?:\s|\()", manifest)
    game_activities = [block for block in activities[1:] if "com.xanaduyonder.leveldevil.LevelDevilActivity" in block]
    require(len(game_activities) == 1, "Expected one Level Devil Activity")
    require(re.search(r"android:exported\([^)]*\)=\(type 0x12\)0x0\b", game_activities[0]),
            "Game Activity must not be exported")
    require("launchable-activity: name='com.xanaduyonder.kingdomrush.KingdomRushLauncherActivity'" in badging,
            "Collection launcher/task behavior changed")
    content_path = ROOT / "Assets/Plugins/Android/LevelDevilContent.aar"
    with zipfile.ZipFile(args.apk) as apk, zipfile.ZipFile(content_path) as content:
        require(apk.testzip() is None, "APK CRC failure")
        files = verify_payload(apk, content)
        # This feature must not remove an existing external game to make a build pass.
        require("assets/game.love" in apk.namelist(), "Existing Kingdom Rush content is missing")
    result = {
        "apk": str(args.apk.resolve()), "apk_bytes": args.apk.stat().st_size,
        "apk_sha256": sha(args.apk.read_bytes()), "permissions": sorted(used),
        "level_devil_files": len(files), "asset_hashes_match_content_aar": True,
        "sdk_and_research_files_absent": True, "device_gameplay_validated_by_this_check": False,
    }
    output = ROOT / "artifacts/level-devil/apk-verification.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
