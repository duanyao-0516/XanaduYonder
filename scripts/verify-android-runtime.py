#!/usr/bin/env python3
"""Verify an offline Android APK uses Unity's non-Development ARM64 runtime."""

import argparse
import importlib.util
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile


def default_aapt():
    if os.environ.get("AAPT"):
        return os.environ["AAPT"]
    editor = Path(os.environ.get("UNITY_EDITOR") or
                  "/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity")
    contents = editor.parent.parent
    android_player = contents / "PlaybackEngines/AndroidPlayer"
    if not android_player.is_dir():
        android_player = contents.parent.parent / "PlaybackEngines/AndroidPlayer"
    return str(android_player / "SDK/build-tools/36.0.0/aapt")


def output(*args):
    return subprocess.run(args, check=True, text=True, capture_output=True).stdout


def verify(apk, aapt, expected_package=None, packed_assets=None):
    if not apk.is_file() or apk.stat().st_size == 0:
        raise ValueError("A nonempty Android APK is required.")
    badging = output(aapt, "dump", "badging", str(apk))
    package = re.search(r"^package: name='([^']+)'", badging, re.MULTILINE)
    if package is None:
        raise ValueError("Cannot read the APK application ID.")
    if expected_package is not None and package.group(1) != expected_package:
        raise ValueError(f"Expected application ID {expected_package}, got {package.group(1)}.")
    if "application-debuggable" in badging:
        raise ValueError("APK is debuggable; rebuild without Development mode.")
    permissions = output(aapt, "dump", "permissions", str(apk))
    if re.search(r"^uses-permission[^:]*:.*\bname='android\.permission\.INTERNET'",
                 permissions, re.MULTILINE):
        raise ValueError("Offline APK unexpectedly requests android.permission.INTERNET.")
    with zipfile.ZipFile(apk) as archive:
        runtime_path = "lib/arm64-v8a/libunity.so"
        if runtime_path not in archive.namelist():
            raise ValueError("APK is missing the ARM64 Unity runtime (lib/arm64-v8a/libunity.so).")
        unity = archive.read(runtime_path)
        if not unity:
            raise ValueError("The ARM64 Unity runtime is empty.")
        if b"Development Console" in unity:
            raise ValueError("Unity's Development Console is still present in the native runtime.")
    gate_path = Path(__file__).with_name("verify-removed-games.py")
    spec = importlib.util.spec_from_file_location("removed_games", gate_path)
    gate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gate)
    gate.verify_artifact(apk, packed_assets or Path(str(apk) + ".packed-assets.txt"))
    print(f"Android runtime verified: {package.group(1)}, ARM64, non-debuggable, "
          "no INTERNET permission, no Development Console runtime.")
    print(f"APK: {apk} ({apk.stat().st_size} bytes)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--aapt", default=default_aapt(),
                        help="aapt executable (default: AAPT or the UNITY_EDITOR Android toolchain)")
    parser.add_argument("--expected-package", help="required APK application ID")
    parser.add_argument("--packed-assets", type=Path, help="current build's packed-asset inventory")
    args = parser.parse_args()
    try:
        verify(args.apk, args.aapt, args.expected_package, args.packed_assets)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print(f"Android runtime verification failed: {error}", file=sys.stderr)
        sys.exit(1)
