#!/usr/bin/env python3
"""Exercise the Cut the Rope build input cache using small trust-verifier fixtures."""
import hashlib
import json
from pathlib import Path
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
import zipfile


SCRIPT = Path(__file__).with_name("prepare-build-input.py")
RUNTIME = "Assets/Plugins/Android/CutTheRopeRuntime.aar"
CONTENT = "Assets/Plugins/Android/CutTheRopeContent.aar"
REPORT = "artifacts/cut-the-rope/runtime-build.json"
PREFIX = "assets/cut-the-rope/"
CONTENT_INPUTS = (
    "scripts/cut-the-rope/build-content.py",
    "scripts/cut-the-rope/decode-hx.cjs",
    "ThirdParty/CutTheRopeContent/index.html",
    "ThirdParty/CutTheRopeContent/offline-interface.js",
    "ThirdParty/CutTheRopeContent/start.js",
)
RUNTIME_INPUTS = (
    "scripts/cut-the-rope/build-runtime.py",
    "ThirdParty/CutTheRopeRuntime/AndroidManifest.xml",
    "ThirdParty/CutTheRopeRuntime/src/Host.java",
)


def digest(contents):
    return hashlib.sha256(contents).hexdigest()


def write(root, relative, contents):
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(contents)
    return path


def archive(root, relative, entries):
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w") as output:
        for name, payload in entries.items():
            output.writestr(name, payload)
    return digest(path.read_bytes())


class PrepareBuildInputTests(unittest.TestCase):
    def setUp(self):
        scratch = tempfile.TemporaryDirectory(prefix="ctr-cache-tests-")
        self.addCleanup(scratch.cleanup)
        self.scratch = Path(scratch.name).resolve()
        self.local = self.scratch / "local"
        self.cold = self.scratch / "cold"
        self.cache = self.scratch / "cache"
        self.portable = self.scratch / "portable.zip"
        for name in (*CONTENT_INPUTS, *RUNTIME_INPUTS):
            contents = (name + "\n").encode()
            if name.endswith("build-runtime.py"):
                contents = b"def plugin_meta(path, guid):\n    path.with_suffix('.aar.meta').write_text(guid)\n"
            write(self.local, name, contents)
        original = b"exact pinned JS"
        zip_name = "ThirdParty/CutTheRopeContent/source/cut-the-rope-4399-142014.zip"
        source_zip = archive(self.local, zip_name, {"game/ctr.min.js": original})
        write(self.local, "scripts/cut-the-rope/restore-source.py", f"ARCHIVE_SHA256 = {source_zip!r}\n".encode())
        shutil.copytree(self.local, self.cold)
        html = b"local converted html"
        manifest = json.dumps({
            "levelCount": 425, "sourceSha256": digest(original),
            "buildInputs": {name: digest((self.local / name).read_bytes()) for name in CONTENT_INPUTS},
            "files": [{"path": "index.html", "bytes": len(html), "sha256": digest(html)}],
        }).encode()
        manifest_xml = (self.local / RUNTIME_INPUTS[1]).read_bytes()
        runtime = archive(self.local, RUNTIME, {"AndroidManifest.xml": manifest_xml, "classes.jar": b"test classes", "proguard.txt": b"rules"})
        assets = {PREFIX + "index.html": html, PREFIX + "manifest.json": manifest}
        content = archive(self.local, CONTENT, {"AndroidManifest.xml": b"content xml", **assets})
        report = {"compileSdk": 36, "minSdk": 26, "targetSdk": 36,
                  "runtimeAar": RUNTIME, "runtimeAarSha256": runtime, "contentAar": CONTENT, "contentAarSha256": content,
                  "sourceSha256": {name: digest((self.local / name).read_bytes()) for name in RUNTIME_INPUTS},
                  "contentAssets": [{"path": name, "bytes": len(data), "sha256": digest(data)} for name, data in assets.items()]}
        write(self.local, REPORT, json.dumps(report).encode())

    def run_tool(self, root, *flags, success=True, allow_rebuild=False):
        rebuild = [] if allow_rebuild else ["--no-rebuild"]
        process = subprocess.run([sys.executable, str(SCRIPT), "--root", str(root), "--cache-dir", str(self.cache),
                                  *rebuild, *flags], capture_output=True, text=True)
        self.assertEqual(process.returncode == 0, success, process.stdout + process.stderr)
        return process.stdout + process.stderr

    def test_verified_reuse_from_cold_worktree(self):
        self.run_tool(self.local)
        self.assertEqual(len(list(self.cache.glob("*.zip"))), 1)
        self.run_tool(self.cold)
        for name in (RUNTIME, CONTENT, REPORT):
            self.assertEqual((self.cold / name).read_bytes(), (self.local / name).read_bytes())
        self.assertTrue((self.cold / (RUNTIME + ".meta")).is_file())
        self.assertFalse((self.cold / "artifacts/cut-the-rope-4399-142014").exists())

    def test_source_change_cannot_reuse_previous_artifacts(self):
        self.run_tool(self.local, "--export-bundle", str(self.portable))
        write(self.cold, RUNTIME_INPUTS[-1], b"newer host source")
        self.run_tool(self.cold, success=False)
        portable_flags = ("--bundle", str(self.portable), "--bundle-sha256", digest(self.portable.read_bytes()))
        self.run_tool(self.cold, *portable_flags, success=False)
        result = self.run_tool(self.cold, *portable_flags, "--android-player", str(self.scratch / "missing-player"),
                               allow_rebuild=True, success=False)
        self.assertIn("No Unity Android SDK 36/OpenJDK found", result)
        self.assertFalse((self.cold / RUNTIME).exists())
        self.assertFalse((self.cold / REPORT).exists())

    def test_portable_file_requires_pinned_hash(self):
        self.run_tool(self.local, "--export-bundle", str(self.portable))
        self.cache = self.scratch / "another-cache"
        self.run_tool(self.cold, "--bundle", str(self.portable), "--bundle-sha256", "0" * 64, success=False)
        self.assertFalse((self.cold / RUNTIME).exists())
        self.run_tool(self.cold, "--bundle", str(self.portable), "--bundle-sha256", digest(self.portable.read_bytes()))
        self.assertEqual((self.cold / RUNTIME).read_bytes(), (self.local / RUNTIME).read_bytes())

    def test_portable_archive_rejects_tampered_and_extra_paths(self):
        self.run_tool(self.local, "--export-bundle", str(self.portable))
        self.cache = self.scratch / "another-cache"
        with zipfile.ZipFile(self.portable) as source:
            entries = {name: source.read(name) for name in source.namelist()}
        filename = next(name for name in entries if name.endswith("CutTheRopeRuntime.aar"))
        tampered = self.scratch / "tampered.zip"
        for changes in ({filename: b"changed archive"}, {"../escaped": b"path traversal"}):
            sha = archive(self.scratch, tampered.name, {**entries, **changes})
            result = self.run_tool(self.cold, "--bundle", str(tampered), "--bundle-sha256", sha,
                                   "--android-player", str(self.scratch / "missing-player"), allow_rebuild=True, success=False)
            self.assertNotIn("No Unity Android SDK 36/OpenJDK found", result)
            self.assertFalse((self.cold / RUNTIME).exists())
            self.assertFalse((self.scratch / "escaped").exists())
        with zipfile.ZipFile(tampered, "w") as output:
            for name, data in entries.items():
                entry = zipfile.ZipInfo(name)
                if name == filename:
                    entry.create_system = 3
                    entry.external_attr = (stat.S_IFLNK | 0o777) << 16
                output.writestr(entry, data)
        self.run_tool(self.cold, "--bundle", str(tampered), "--bundle-sha256", digest(tampered.read_bytes()), success=False)
        self.assertFalse((self.cold / RUNTIME).exists())

    def test_export_in_ignored_artifacts_cannot_replace_an_existing_file(self):
        output = self.local / "artifacts/portable.zip"
        self.run_tool(self.local, "--export-bundle", str(output))
        expected = digest(output.read_bytes())
        self.run_tool(self.local, "--export-bundle", str(output), success=False)
        self.assertEqual(expected, digest(output.read_bytes()))
        source = self.local / "ThirdParty/untracked.zip"
        self.run_tool(self.local, "--export-bundle", str(source), success=False)
        self.assertFalse(source.exists())

    def test_restore_refuses_symlinked_destination(self):
        self.run_tool(self.local)
        external = write(self.scratch, "untouched", b"sentinel")
        target = self.cold / RUNTIME
        target.parent.mkdir(parents=True, exist_ok=True)
        target.symlink_to(external)
        self.run_tool(self.cold, success=False)
        self.assertEqual(external.read_bytes(), b"sentinel")
        self.assertTrue(target.is_symlink())
        self.assertFalse((self.cold / REPORT).exists())


if __name__ == "__main__":
    unittest.main()
