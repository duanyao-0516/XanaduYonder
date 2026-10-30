#!/usr/bin/env python3
"""Build the pinned FL8 SDL3 runtime plugin and package the offline KR1 game."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import zipfile
from content_stable_baseline import validate_certificate

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ThirdParty/KingdomRushRuntime"
OUTPUT = ROOT / "Assets/Plugins/Android"


def run(arguments):
    subprocess.run([str(value) for value in arguments], check=True)


def symbols(nm, library, defined):
    output = subprocess.check_output([str(nm), "-D", "--defined-only" if defined else "--undefined-only", str(library)], text=True)
    return {line.split()[-1].split("@")[0] for line in output.splitlines() if line.strip()}


def archive(path, entries, stored=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_STORED if stored else zipfile.ZIP_DEFLATED, compresslevel=None if stored else 6) as output:
        for name, value in sorted(entries.items()):
            info = zipfile.ZipInfo(name, (2026, 10, 29, 0, 0, 0))
            info.compress_type = zipfile.ZIP_STORED if stored else zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            if isinstance(value, Path):
                with value.open("rb") as source, output.open(info, "w", force_zip64=True) as destination:
                    shutil.copyfileobj(source, destination, 1024 * 1024)
            else:
                output.writestr(info, value)
    temporary.replace(path)


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as source:
        for data in iter(lambda: source.read(1024 * 1024), b""):
            result.update(data)
    return result.hexdigest()


def prepare_native(native, metadata, source_apk):
    missing = [name for name in metadata["nativeLibraries"] if not (native / name).is_file()]
    if source_apk is not None and (not source_apk.is_file() or digest(source_apk) != metadata["sourceApkSha256"]):
        raise SystemExit("Source APK is missing or does not match the pinned FL8.0.5 SHA256")
    if not missing:
        return
    if source_apk is None:
        raise SystemExit("Original FL8.0.5 runtime libraries are missing. Run again with --source-apk /path/to/FL8.0.5.apk; the exact APK hash is pinned in provenance.json.")
    native.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(source_apk) as source:
        for name in missing:
            expected = metadata["nativeLibraries"][name]
            entry = "lib/arm64-v8a/" + name
            if source.getinfo(entry).file_size != expected["bytes"]:
                raise SystemExit("Original APK library size mismatch: " + name)
            temporary = native / (name + ".importing")
            try:
                with source.open(entry) as library, temporary.open("wb") as destination:
                    shutil.copyfileobj(library, destination, 1024 * 1024)
                if digest(temporary) != expected["sha256"]:
                    raise SystemExit("Original APK library hash mismatch: " + name)
                temporary.replace(native / name)
            finally:
                if temporary.exists():
                    temporary.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--android-player", type=Path, default=Path(os.environ.get("UNITY_ANDROID_PLAYER", "/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer")))
    parser.add_argument("--game", type=Path, default=ROOT / "artifacts/kingdom-rush-1/game.love")
    parser.add_argument("--source-apk", type=Path, help="Original user-supplied FL8.0.5 APK, required to import missing pinned native libraries on a fresh checkout")
    parser.add_argument("--runtime-only", action="store_true", help="Compile only; does not create or remove the separate content plugin")
    parser.add_argument("--validation-report", type=Path, help="Passing content-validate.py report for the exact playable stable baseline")
    args = parser.parse_args()
    if not args.runtime_only:
        validation_path = args.validation_report or args.game.parent / "static-validation.json"
        validation = json.loads(validation_path.read_text())
        validate_certificate(validation, args.game)
    java = args.android_player / "OpenJDK/bin"
    sdk = args.android_player / "SDK"
    ndk = args.android_player / "NDK"
    toolchain = ndk / "toolchains/llvm/prebuilt/darwin-x86_64"
    android_jar = sdk / "platforms/android-35/android.jar"
    nm = toolchain / "bin/llvm-nm"
    clang = toolchain / "bin/aarch64-linux-android26-clang"
    cxx = toolchain / "sysroot/usr/lib/aarch64-linux-android/libc++_shared.so"
    for dependency in [java / "javac", android_jar, nm, clang, cxx]:
        if not dependency.is_file():
            raise SystemExit("Required Unity Android dependency missing: " + str(dependency))

    metadata = json.loads((SOURCE / "provenance.json").read_text())
    native = SOURCE / "prebuilt/arm64-v8a"
    prepare_native(native, metadata, args.source_apk)
    for name, expected in metadata["nativeLibraries"].items():
        if digest(native / name) != expected["sha256"]:
            raise SystemExit("Pinned original native library changed: " + name)
    missing = set(metadata["cxxRequiredSymbols"]) - symbols(nm, cxx, True)
    if missing:
        raise SystemExit("Unity libc++ does not export required original-runtime symbols: " + ", ".join(sorted(missing)))

    with tempfile.TemporaryDirectory(prefix="kingdom-rush-build-") as temporary:
        stage = Path(temporary)
        classes = stage / "classes"
        classes.mkdir()
        java_files = sorted((SOURCE / "src").rglob("*.java"))
        run([java / "javac", "-encoding", "UTF-8", "-source", "8", "-target", "8", "-Xlint:-options", "-classpath", android_jar, "-d", classes] + java_files)
        classes_jar = stage / "classes.jar"
        archive(classes_jar, {str(path.relative_to(classes)): path for path in classes.rglob("*.class")})
        contract = json.loads((SOURCE / "jni-contract.json").read_text())["nativeMethods"]
        for class_name, expected in contract.items():
            declarations = subprocess.check_output([str(java / "javap"), "-classpath", str(classes_jar), "-s", "-p", class_name], text=True)
            actual = sorted(name + descriptor for name, descriptor in re.findall(r" native [^\n]+? (\w+)\([^\n]*\);\n\s+descriptor: ([^\n]+)", declarations))
            if actual != expected:
                raise SystemExit("JNI declarations differ from original APK: " + class_name)

        jni = stage / "libjni_android.so"
        run([clang, "-shared", "-fPIC", "-O2", "-Wall", "-Wextra", "-Werror", "-fvisibility=hidden", "-I", SOURCE / "include", SOURCE / "native/jni_android.c", "-L", native, "-lluajit", "-lSDL3", "-Wl,--no-undefined", "-Wl,-z,max-page-size=16384", "-Wl,-soname,libjni_android.so", "-o", jni])
        entries = {"AndroidManifest.xml": SOURCE / "AndroidManifest.xml", "classes.jar": classes_jar,
                   "proguard.txt": b"-keep class org.libsdl.app.** { *; }\n-keep class org.love2d.** { *; }\n-keep class com.xanaduyonder.kingdomrush.** { *; }\n",
                   "jni/arm64-v8a/libjni_android.so": jni}
        entries.update({"jni/arm64-v8a/" + name: native / name for name in metadata["nativeLibraries"]})
        archive(OUTPUT / "KingdomRushRuntime.aar", entries)

    result = {"runtimeAarSha256": digest(OUTPUT / "KingdomRushRuntime.aar"), "originalApkSha256": metadata["sourceApkSha256"],
              "unityCxxSha256": digest(cxx), "verifiedRequiredCxxSymbolCount": len(metadata["cxxRequiredSymbols"]),
              "verifiedOriginalJniMethodCount": sum(len(methods) for methods in contract.values()),
              "javaSourceCount": len(java_files), "validation": "Java and native bridge compilation; source binary hashes; C++ symbol coverage. No gameplay execution."}
    if not args.runtime_only:
        if not args.game.is_file():
            raise SystemExit("Missing offline game package; build content first: " + str(args.game))
        with zipfile.ZipFile(args.game) as game:
            for required in ["main.lua", "conf.lua", "jni_android.lua"]:
                if required not in game.namelist():
                    raise SystemExit("Offline game package missing " + required)
            if game.read("jni_android.lua").strip() != b'return require("libjni_android")':
                raise SystemExit("jni_android.lua must load the real native module")
        content_manifest = b'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.xanaduyonder.kingdomrush.content"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36"/></manifest>'
        archive(OUTPUT / "KingdomRushContent.aar", {"AndroidManifest.xml": content_manifest, "assets/game.love": args.game}, stored=True)
        result.update({"gamePath": str(args.game.resolve()), "gameSha256": digest(args.game), "contentAarSha256": digest(OUTPUT / "KingdomRushContent.aar"), "contentValidationSha256": digest(validation_path), "contentValidationReport": str(validation_path.resolve())})
    report = ROOT / "artifacts/kingdom-rush-1/runtime-build.json"
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
