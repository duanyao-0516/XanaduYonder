#!/usr/bin/env python3
"""Verify the delivered APK and exact locally built Cut the Rope payload."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ANDROID = Path("/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--tools", type=Path, default=ANDROID / "SDK/build-tools/36.0.0")
    args = parser.parse_args()
    apk = args.apk.resolve()
    aapt = str(args.tools / "aapt")
    badging = subprocess.check_output([aapt, "dump", "badging", str(apk)], text=True)
    permissions = subprocess.check_output([aapt, "dump", "permissions", str(apk)], text=True)
    manifest = subprocess.check_output([aapt, "dump", "xmltree", str(apk), "AndroidManifest.xml"], text=True)
    package = "com.duanyao.xanaduyonder.cuttheropetest"
    require("package: name='" + package + "'" in badging, "Unexpected application ID")
    require("application-label:'割绳子测试'" in badging, "Unexpected application label")
    require("application-debuggable" not in badging, "Delivery APK must not be a Development build")
    for permission in ("INTERNET", "READ_PHONE_STATE", "READ_EXTERNAL_STORAGE", "WRITE_EXTERNAL_STORAGE"):
        require("android.permission." + permission not in permissions, "Unexpected permission: " + permission)
    # Inspect the Activity's own block, not another component's exported flag.
    activity_blocks = re.split(r"(?m)^\s*E: activity(?:\s|$)", manifest)[1:]
    activity = next((block for block in activity_blocks if '"com.xanaduyonder.cuttherope.CutTheRopeActivity"' in block.split("E: ", 1)[0]), "")
    require(activity, "Cut the Rope Activity missing")
    require(re.search(r"android:exported\([^)]*\)=\(type 0x12\)0x0\b", activity), "Cut the Rope Activity must not be exported")
    require(re.search(r"android:enableOnBackInvokedCallback\([^)]*\)=\(type 0x12\)0xffffffff", activity), "Modern system Back callback missing")
    environment = dict(os.environ)
    environment["JAVA_HOME"] = str(ANDROID / "OpenJDK")
    signature = subprocess.check_output([str(args.tools / "apksigner"), "verify", "--verbose", "--print-certs", str(apk)], env=environment, text=True)
    content_aar = ROOT / "Assets/Plugins/Android/CutTheRopeContent.aar"
    with zipfile.ZipFile(content_aar) as content, zipfile.ZipFile(apk) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate ZIP entries")
        require(archive.testzip() is None, "APK CRC check failed")
        expected = {name for name in content.namelist() if name.startswith("assets/cut-the-rope/") and not name.endswith("/")}
        actual = {name for name in names if name.startswith("assets/cut-the-rope/") and not name.endswith("/")}
        require(actual == expected and len(actual) > 100, "APK content differs from the current content AAR")
        hashes = {}
        for name in sorted(expected):
            payload = archive.read(name)
            require(payload == content.read(name), "APK content mismatch: " + name)
            hashes[name] = {"bytes": len(payload), "sha256": sha(payload)}
        require("assets/cut-the-rope/index.html" in actual, "Offline entry missing")
        game_code = archive.read("assets/cut-the-rope/ctr.js")
        for probe in (b"window.__ctr", b"globalThis.testGame"):
            require(probe not in game_code, "Browser verification hook leaked into delivery content")
        forbidden = ("viverse", "famobi", "storage-sdk", "gamesnacks", "admob", "analytics", "index.umd")
        require(not any(any(token in name.lower() for token in forbidden) for name in actual), "Portal/advertising SDK included in game payload")
        levels = [name for name in actual if re.search(r"/box\d\d/maps\.json$", name)]
        require(len(levels) == 17, "Expected all 17 ordinary level boxes")
        level_counts = {name: len(json.loads(archive.read(name))) for name in levels}
        require(set(level_counts.values()) == {25}, "Expected exactly 425 ordinary levels")
    result = {
        "apk": str(apk), "bytes": apk.stat().st_size, "sha256": sha(apk.read_bytes()),
        "package": package, "label": "割绳子测试", "internet_permission": False,
        "debuggable": False, "game_activity_exported": False,
        "signature_verified": True, "signature_details": signature.strip().splitlines(),
        "content_aar_sha256": sha(content_aar.read_bytes()), "content_files": len(hashes),
        "ordinary_levels": sum(level_counts.values()), "payload": hashes,
        "scope": "APK structure/signature/permissions/content integrity; runtime evidence is reported separately"
    }
    output = ROOT / "artifacts/cut-the-rope/apk-verification.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({k: v for k, v in result.items() if k not in ("payload", "signature_details")}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
