#!/usr/bin/env python3
"""Verify the actual user-preview APK, including Unity's selected native runtime."""

import argparse
from pathlib import Path
import re
import subprocess
import sys
import zipfile


def output(*args):
    return subprocess.run(args, check=True, text=True, capture_output=True).stdout


def verify(apk, aapt, apksigner):
    if not apk.is_file() or apk.stat().st_size == 0:
        raise ValueError("A fresh, nonempty Zuma preview APK is required.")
    badging = output(aapt, "dump", "badging", str(apk))
    package = re.search(r"^package: name='([^']+)'", badging, re.MULTILINE)
    if package is None or package.group(1) != "com.duanyao.xanaduyonder.zumapreview":
        raise ValueError("Preview must use the isolated .zumapreview application ID.")
    if "application-debuggable" in badging:
        raise ValueError("User preview is debuggable; rebuild without Development mode.")
    if not re.search(r"^native-code: 'arm64-v8a'\s*$", badging, re.MULTILINE):
        raise ValueError("Expected the Android ARM64 preview.")
    permissions = output(aapt, "dump", "permissions", str(apk))
    if "uses-permission:" in permissions:
        raise ValueError("Offline user preview unexpectedly requests Android permissions.")
    with zipfile.ZipFile(apk) as archive:
        unity = archive.read("lib/arm64-v8a/libunity.so")
        if b"Development Console" in unity:
            raise ValueError("Unity's Development Console is still present in the native runtime.")
    output(apksigner, "verify", "--verbose", str(apk))
    print("Zuma user preview verified: isolated package, ARM64, non-debuggable, "
          "no permissions, no Development Console runtime, valid APK signature.")
    print(f"APK: {apk} ({apk.stat().st_size} bytes)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--aapt", required=True)
    parser.add_argument("--apksigner", required=True)
    args = parser.parse_args()
    try:
        verify(args.apk, args.aapt, args.apksigner)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print(f"Zuma preview verification failed: {error}", file=sys.stderr)
        sys.exit(1)
