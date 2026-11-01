#!/usr/bin/env python3
"""Compile Cut the Rope's dependency-free Android host and package its offline assets."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ThirdParty/CutTheRopeRuntime"
OUTPUT = ROOT / "Assets/Plugins/Android"
ANDROID_NS = "{http://schemas.android.com/apk/res/android}"


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def archive(path, entries):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
        for name, value in sorted(entries.items()):
            info = zipfile.ZipInfo(name, (2026, 11, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            if isinstance(value, Path):
                with value.open("rb") as source, output.open(info, "w", force_zip64=True) as target:
                    shutil.copyfileobj(source, target, 1024 * 1024)
            else:
                output.writestr(info, value)
    with zipfile.ZipFile(temporary) as output:
        bad = output.testzip()
        if bad is not None:
            raise SystemExit("Archive failed integrity check: " + bad)
    temporary.replace(path)


def plugin_meta(path, guid):
    """Stable plugin identity and Android-only importer, also reproducible on a fresh checkout."""
    meta = path.with_suffix(path.suffix + ".meta")
    expected = f"""fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 3
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {{}}
  - first:
      Android: Android
    second:
      enabled: 1
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    if not meta.exists():
        meta.write_text(expected)


def validate_manifest():
    root = ET.parse(SOURCE / "AndroidManifest.xml").getroot()
    if root.findall("uses-permission"):
        raise SystemExit("Offline runtime may not request Android permissions")
    activities = root.findall("application/activity")
    if len(activities) != 1 or activities[0].get(ANDROID_NS + "exported") != "false":
        raise SystemExit("Runtime must contain exactly one internal Activity")
    if activities[0].get(ANDROID_NS + "enableOnBackInvokedCallback") != "true":
        raise SystemExit("Completed predictive Back callback must be enabled")
    if root.find("uses-sdk").get(ANDROID_NS + "minSdkVersion") != "26":
        raise SystemExit("Runtime must support Android API 26 and later")


def content_entries(folder):
    if not (folder / "index.html").is_file():
        raise SystemExit("Offline index missing; run content generation first: " + str(folder))
    files = []
    for path in sorted(folder.rglob("*")):
        if path.is_symlink():
            raise SystemExit("Offline content may not contain symlinks: " + str(path))
        if not path.is_file():
            continue
        relative = path.relative_to(folder).as_posix()
        if not re.fullmatch(r"[a-zA-Z0-9._~/-]+", relative) or any(x in (".", "..", "") for x in relative.split("/")):
            raise SystemExit("Asset path is not compatible with the strict runtime router: " + relative)
        files.append(("assets/cut-the-rope/" + relative, path))
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--android-player", type=Path, default=Path(os.environ.get("UNITY_ANDROID_PLAYER", "/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer")))
    parser.add_argument("--content", type=Path, default=ROOT / "artifacts/cut-the-rope/offline")
    parser.add_argument("--runtime-only", action="store_true", help="Compile runtime without changing the content AAR")
    args = parser.parse_args()
    validate_manifest()
    java = args.android_player / "OpenJDK/bin"
    android_jar = args.android_player / "SDK/platforms/android-36/android.jar"
    for path in [java / "javac", java / "javap", android_jar]:
        if not path.is_file():
            raise SystemExit("Required Unity Android toolchain file missing: " + str(path))
    sources = sorted((SOURCE / "src").rglob("*.java"))
    with tempfile.TemporaryDirectory(prefix="cut-the-rope-runtime-") as temp:
        classes = Path(temp) / "classes"
        classes.mkdir()
        subprocess.run([str(java / "javac"), "-encoding", "UTF-8", "-source", "8", "-target", "8", "-Xlint:-options", "-classpath", str(android_jar), "-d", str(classes)] + [str(p) for p in sources], check=True)
        jar = Path(temp) / "classes.jar"
        archive(jar, {p.relative_to(classes).as_posix(): p for p in classes.rglob("*.class")})
        declarations = subprocess.check_output([str(java / "javap"), "-classpath", str(jar), "-s", "com.xanaduyonder.cuttherope.CutTheRopeBridge"], text=True)
        for name, signature in [("open", "(Landroid/app/Activity;Z)Z"), ("isRunning", "()Z"), ("getLastError", "()Ljava/lang/String;")]:
            if not re.search(r"\b" + name + r"\([^\n]*\);\n\s+descriptor: " + re.escape(signature), declarations):
                raise SystemExit("Unity JNI bridge contract missing: " + name + signature)
        runtime = OUTPUT / "CutTheRopeRuntime.aar"
        archive(runtime, {"AndroidManifest.xml": SOURCE / "AndroidManifest.xml", "classes.jar": jar,
                          "proguard.txt": b"-keep class com.xanaduyonder.cuttherope.** { *; }\n"})
        plugin_meta(runtime, "113559820c7b4506afdd251cabd4b95e")
    result = {
        "compileSdk": 36, "minSdk": 26, "targetSdk": 36,
        "runtimeAar": str(runtime.relative_to(ROOT)), "runtimeAarSha256": digest(runtime),
        "sourceSha256": {str(p.relative_to(ROOT)): digest(p) for p in sources + [SOURCE / "AndroidManifest.xml", Path(__file__).resolve()]},
        "validation": "Host policy tests are separate. Java compilation, bridge signatures, permission/export manifest validation, ZIP integrity. No Android gameplay claim."
    }
    if not args.runtime_only:
        entries = content_entries(args.content)
        content = OUTPUT / "CutTheRopeContent.aar"
        manifest = b'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.xanaduyonder.cuttherope.content"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="36"/></manifest>'
        archive(content, dict(entries, **{"AndroidManifest.xml": manifest}))
        plugin_meta(content, "c4fa968eb6e64ea18c5d20c94016cd6c")
        result.update({"contentAar": str(content.relative_to(ROOT)), "contentAarSha256": digest(content),
                       "contentSource": str(args.content.resolve()),
                       "contentAssets": [{"path": name, "bytes": path.stat().st_size, "sha256": digest(path)} for name, path in entries]})
    report = ROOT / "artifacts/cut-the-rope/runtime-build.json"
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({key: value for key, value in result.items() if key not in ("sourceSha256", "contentAssets")}, ensure_ascii=False, indent=2))
    if "contentAssets" in result:
        print("Packaged " + str(len(result["contentAssets"])) + " content assets; exact hashes: " + str(report))


if __name__ == "__main__":
    main()
