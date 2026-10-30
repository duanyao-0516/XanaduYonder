#!/usr/bin/env python3
"""Inspect the built APK and its offline payload without launching the game."""
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def digest(stream):
    result = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        result.update(chunk)
    return result.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--aapt", type=Path, required=True)
    parser.add_argument("--package", default="com.duanyao.xanaduyonder.kingdomrushtest")
    parser.add_argument("--game", type=Path, default=ROOT / "artifacts/kingdom-rush-1/game.love")
    parser.add_argument("--allow-load-probe", action="store_true", help="Explicitly verify a diagnostic APK, never a delivery artifact")
    args = parser.parse_args()
    badging = subprocess.check_output([str(args.aapt), "dump", "badging", str(args.apk)], text=True)
    permissions = subprocess.check_output([str(args.aapt), "dump", "permissions", str(args.apk)], text=True)
    manifest = subprocess.check_output([str(args.aapt), "dump", "xmltree", str(args.apk), "AndroidManifest.xml"], text=True)
    assert f"package: name='{args.package}'" in badging, "Unexpected application ID"
    assert "launchable-activity: name='com.xanaduyonder.kingdomrush.KingdomRushLauncherActivity'" in badging, "Launcher must preserve the active game's task stack"
    assert "application-debuggable" not in badging, "Preview must not enable Development/debug runtime"
    for permission in ["INTERNET", "READ_PHONE_STATE", "READ_EXTERNAL_STORAGE", "WRITE_EXTERNAL_STORAGE"]:
        assert "android.permission." + permission not in permissions, "Unexpected permission: " + permission
    assert re.search(r"android:extractNativeLibs\([^)]*\)=\(type 0x12\)0xffffffff", manifest), "Native Lua modules require extracted library paths"
    with args.game.open("rb") as stream:
        expected_game = digest(stream)
    with zipfile.ZipFile(args.game) as content:
        diagnostic = b"KR1_LOAD_PROBE" in content.read("all/director.lua") or b"_load_probe" in content.read("version.lua")
        assert not diagnostic or args.allow_load_probe, "Diagnostic load probe must not be delivered"
    with zipfile.ZipFile(args.apk) as archive:
        paths = archive.namelist()
        assert len(paths) == len(set(paths)), "APK contains duplicate ZIP paths"
        assert "assets/game.love" in paths, "Bundled offline content is missing"
        assert archive.getinfo("assets/game.love").compress_type == zipfile.ZIP_STORED, "Do not double-compress the seekable game archive"
        assert archive.testzip() is None, "APK CRC failure"
        with archive.open("assets/game.love") as stream:
            actual_game = digest(stream)
        assert actual_game == expected_game, "APK does not contain the current generated game package"
        native = [name for name in paths if name.startswith("lib/") and name.endswith(".so")]
        assert native and all(name.startswith("lib/arm64-v8a/") for name in native), "Unexpected native ABI"
        provenance = json.loads((ROOT / "ThirdParty/KingdomRushRuntime/provenance.json").read_text())
        runtime_names = list(provenance["nativeLibraries"]) + ["libjni_android.so"]
        for name in runtime_names + ["libc++_shared.so"]:
            assert "lib/arm64-v8a/" + name in native, "Required runtime library missing: " + name
        runtime_zip_bytes = sum(archive.getinfo("lib/arm64-v8a/" + name).compress_size for name in runtime_names)
        assert all(archive.getinfo(name).compress_type == zipfile.ZIP_DEFLATED for name in native), "Native libraries must use legacy compressed/extracted packaging"
        payload_bytes = archive.getinfo("assets/game.love").file_size
    with args.apk.open("rb") as stream:
        apk_sha = digest(stream)
    result = {
        "apk": str(args.apk.resolve()), "apk_bytes": args.apk.stat().st_size,
        "apk_sha256": apk_sha, "package": args.package,
        "offline_game_bytes": payload_bytes, "offline_game_sha256": actual_game,
        "native_libraries": native, "internet_permission": False,
        "kingdom_rush_native_zip_bytes": runtime_zip_bytes,
        "native_libraries_compressed": True,
        "native_libraries_extracted": True,
        "legacy_phone_and_storage_permissions": False,
        "debuggable": False, "zip_crc_and_content_hash_verified": True,
        "gameplay_or_device_runtime_tested": False,
        "diagnostic_load_probe": diagnostic,
    }
    output = ROOT / "artifacts/kingdom-rush-1/apk-verification.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
