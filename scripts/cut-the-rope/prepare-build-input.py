#!/usr/bin/env python3
"""Prepare verified Cut the Rope Android inputs, reusing builds across worktrees."""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import shutil
import ssl
import stat
import subprocess
import sys
import tempfile
from urllib.parse import urlsplit
from urllib.request import HTTPRedirectHandler, HTTPSHandler, build_opener
import zipfile

ROOT = Path(__file__).resolve().parents[2]
VERIFY = runpy.run_path(str(ROOT / "scripts/verify-cut-the-rope-input.py"))
checked_path = VERIFY["checked_path"]
digest = VERIFY["digest"]
require = VERIFY["require"]
verify = VERIFY["verify"]
RUNTIME, CONTENT, REPORT = (VERIFY[name] for name in ("RUNTIME", "CONTENT", "REPORT"))
FILES = (RUNTIME, CONTENT, REPORT)
META = "bundle.json"
LIMIT = 256 * 1024 * 1024
INPUT_ERRORS = (OSError, ValueError, KeyError, TypeError, AttributeError, RuntimeError, zipfile.BadZipFile)
PREPARE_ERRORS = INPUT_ERRORS + (subprocess.CalledProcessError,)
PLUGINS = {
    RUNTIME: "113559820c7b4506afdd251cabd4b95e",
    CONTENT: "c4fa968eb6e64ea18c5d20c94016cd6c",
}


class IncompatibleBundle(ValueError):
    pass


def canonical_json(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def absolute(path):
    return Path(os.path.abspath(Path(path).expanduser()))


def confined_absolute(path, directory=False):
    path = absolute(path)
    return checked_path(Path(path.anchor), Path(*path.parts[1:]), directory=directory)


def fingerprint(root):
    paths = (*VERIFY["runtime_inputs"](root), *VERIFY["CONTENT_INPUTS"],
             VERIFY["SOURCE_ARCHIVE"], VERIFY["SOURCE_RESTORER"])
    hashes = VERIFY["input_hashes"](root, paths)
    return {"schema": 1, "fingerprint": hashlib.sha256(canonical_json(hashes)).hexdigest(), "inputs": hashes}


def file_record(path):
    return {"bytes": path.stat().st_size, "sha256": digest(path)}


def origin_revision(root):
    git = shutil.which("git")
    if not git:
        return None
    commit = subprocess.run([git, "rev-parse", "--verify", "HEAD"], cwd=root, capture_output=True, text=True)
    return commit.stdout.strip() if commit.returncode == 0 else None


def write_bundle(root, generated, output, identity, replace=False):
    checked_path(output.parent, output.name)
    manifest = {**identity, "producerRevision": origin_revision(root),
                "files": {name: file_record(generated / name) for name in FILES}}
    with tempfile.NamedTemporaryFile(prefix=".ctr-bundle-", dir=output.parent, delete=False) as temporary:
        pending = Path(temporary.name)
    try:
        with pending.open("wb") as stream:
            with zipfile.ZipFile(stream, "w", compression=zipfile.ZIP_STORED) as bundle:
                bundle.writestr(META, canonical_json(manifest))
                for name in FILES:
                    bundle.write(generated / name, name)
            stream.flush()
            os.fsync(stream.fileno())
        if replace:
            # Replace only the exact current fingerprint's private cache entry.
            os.replace(pending, output)
        else:
            os.link(pending, output)
    finally:
        pending.unlink(missing_ok=True)


def extract_bundle(source, stage, identity):
    require(source.stat().st_size <= LIMIT, "Bundle exceeds the maximum size")
    with zipfile.ZipFile(source) as bundle:
        require(VERIFY["checked_archive"](bundle, LIMIT) == set(FILES) | {META}, "Unexpected bundle inventory")
        require(bundle.getinfo(META).file_size <= 64 * 1024, "Bundle manifest is too large")
        manifest = json.loads(bundle.read(META))
        require(manifest.get("schema") == identity["schema"], "Unsupported bundle schema")
        inputs = manifest.get("inputs")
        require(isinstance(inputs, dict) and inputs and all(
            VERIFY["safe_member"](name) and isinstance(sha, str) and re.fullmatch(r"[0-9a-f]{64}", sha)
            for name, sha in inputs.items()), "Invalid bundle source inventory")
        require(manifest.get("fingerprint") == hashlib.sha256(canonical_json(inputs)).hexdigest(),
                "Bundle provenance fingerprint is inconsistent")
        require(set(manifest["files"]) == set(FILES), "Unexpected bundle manifest files")
        for name in FILES:
            target = stage / name
            target.parent.mkdir(parents=True, exist_ok=True)
            with bundle.open(name) as payload, target.open("xb") as output:
                shutil.copyfileobj(payload, output, 1024 * 1024)
            require(file_record(target) == manifest["files"][name], "Bundle digest mismatch: " + name)
        if inputs != identity["inputs"]:
            raise IncompatibleBundle("Bundle was built from different inputs")


def default_cache():
    base = os.environ.get("XDG_CACHE_HOME")
    if base:
        base = Path(base)
    elif sys.platform == "darwin":
        base = Path.home() / "Library/Caches"
    else:
        base = Path.home() / ".cache"
    return base / "XanaduYonder/cut-the-rope"


def open_cache(root, value):
    cache = confined_absolute(value, directory=True)
    require(cache != root and root not in cache.parents, "Cache must be outside the checkout")
    cache.mkdir(mode=0o700, parents=True, exist_ok=True)
    info = cache.lstat()
    require(stat.S_ISDIR(info.st_mode) and info.st_uid == os.getuid() and info.st_mode & 0o077 == 0,
            "Cache must be a private directory owned by the build user: " + str(cache))
    return cache


def https_url(url):
    parts = urlsplit(url)
    return (parts.scheme == "https" and parts.hostname and parts.username is None and parts.password is None)


class HttpsRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        require(https_url(new_url), "Bundle redirects must use HTTPS")
        return super().redirect_request(request, response, code, message, headers, new_url)


def portable_source(value, pinned, destination):
    require(bool(re.fullmatch(r"[0-9a-fA-F]{64}", pinned or "")), "Set the bundle's pinned SHA-256")
    if value.startswith("https://"):
        require(https_url(value), "Bundle URL must use HTTPS without user information")
        context = ssl.create_default_context(cafile=os.environ.get("SSL_CERT_FILE") or None)
        opener = build_opener(HttpsRedirect(), HTTPSHandler(context=context))
        with opener.open(value, timeout=30) as response, destination.open("xb") as output:
            for block in iter(lambda: response.read(1024 * 1024), b""):
                require(output.tell() + len(block) <= LIMIT, "Bundle download exceeds maximum size")
                output.write(block)
    else:
        require("://" not in value, "Bundle URL must use HTTPS")
        source = confined_absolute(value)
        require(source.stat().st_size <= LIMIT, "Bundle exceeds the maximum size")
        shutil.copyfile(source, destination)
    require(digest(destination) == pinned.lower(), "Bundle SHA-256 does not match its pin")
    return destination


def plugin_metadata(root, stage):
    missing = [name for name in PLUGINS if not checked_path(root, name + ".meta").exists()]
    if not missing:
        return []
    builder = runpy.run_path(str(checked_path(root, VERIFY["RUNTIME_BUILDER"])))
    for name in missing:
        plugin = stage / name
        plugin.parent.mkdir(parents=True, exist_ok=True)
        builder["plugin_meta"](plugin, PLUGINS[name])
    return [name + ".meta" for name in missing]


def install(root, stage, restore_artifacts=True):
    paths = [(name, True) for name in plugin_metadata(root, stage)]
    if restore_artifacts:
        paths.extend((name, False) for name in FILES)
    pending = []
    try:
        # Prepare every copy before publishing. The evidence report is published last.
        for name, only_if_missing in paths:
            destination = checked_path(root, name)
            if only_if_missing and destination.exists():
                continue
            destination.parent.mkdir(parents=True, exist_ok=True)
            with tempfile.NamedTemporaryFile(prefix=".ctr-input-", dir=destination.parent, delete=False) as output:
                pending.append((name, Path(output.name), only_if_missing))
                with (stage / name).open("rb") as source:
                    shutil.copyfileobj(source, output, 1024 * 1024)
                output.flush()
                os.fsync(output.fileno())
        for name, temporary, only_if_missing in pending:
            destination = checked_path(root, name)
            if only_if_missing:
                os.link(temporary, destination)
            else:
                os.replace(temporary, destination)
    finally:
        for _, temporary, _ in pending:
            if temporary.exists():
                temporary.unlink()


def player_path(root, configured):
    if configured:
        candidates = [Path(configured).expanduser()]
    else:
        editor = os.environ.get("UNITY_EDITOR")
        candidates = [parent / "PlaybackEngines/AndroidPlayer" for parent in Path(editor).resolve().parents] if editor else []
        version_file = root / "ProjectSettings/ProjectVersion.txt"
        if version_file.is_file():
            version = re.search(r"^m_EditorVersion: ([0-9.]+[abfp][0-9]+)$", version_file.read_text(), re.M)
            if version:
                candidates.append(Path("/Applications/Unity/Hub/Editor") / version[1] / "PlaybackEngines/AndroidPlayer")
    for path in candidates:
        if all((path / name).is_file() for name in ("OpenJDK/bin/javac", "OpenJDK/bin/javap", "SDK/platforms/android-36/android.jar")):
            return path.resolve()
    raise ValueError("No Unity Android SDK 36/OpenJDK found; set UNITY_ANDROID_PLAYER to rebuild")


def rebuild(root, stage, android_player):
    android_player = player_path(root, android_player)
    require(shutil.which("node") is not None, "Node.js is required for original content conversion")
    try:
        import PIL  # Only needed when no compatible artifact exists.
    except ImportError as error:
        raise ValueError("Pillow with AVIF support is required to rebuild the content") from error
    original = stage / "original"
    offline = stage / "offline"
    for name in VERIFY["runtime_inputs"](root):
        destination = stage / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(checked_path(root, name), destination)
    commands = (
        (root / VERIFY["SOURCE_RESTORER"], "--output", original),
        (root / VERIFY["CONTENT_INPUTS"][0], "--source", original / "game", "--output", offline),
        (stage / VERIFY["RUNTIME_BUILDER"], "--android-player", android_player, "--content", offline),
    )
    for command in commands:
        subprocess.run([sys.executable, *map(str, command)], cwd=root, check=True)


def prepare(root, cache, options, scratch):
    identity = fingerprint(root)
    entry = checked_path(cache, identity["fingerprint"] + ".zip")
    for name in (*FILES, *(name + ".meta" for name in PLUGINS)):
        checked_path(root, name)
    try:
        verify(root)
    except INPUT_ERRORS as error:
        print("Cut the Rope worktree inputs require preparation: " + str(error), file=sys.stderr)
    else:
        install(root, scratch / "metadata", restore_artifacts=False)
        if not entry.is_file():
            write_bundle(root, root, entry, identity, replace=True)
        print("Using verified Cut the Rope inputs already in this worktree.")
        return root, identity

    stage = scratch / "verified"
    if entry.is_file():
        try:
            extract_bundle(entry, stage, identity)
            verify(root, stage)
        except INPUT_ERRORS as error:
            print("Rejected Cut the Rope cache entry: " + str(error), file=sys.stderr)
            if stage.exists():
                shutil.rmtree(stage)
        else:
            install(root, stage)
            print("Restored verified Cut the Rope inputs from the shared cache.")
            return stage, identity

    action = None
    if options.bundle:
        source = portable_source(options.bundle, options.bundle_sha256, scratch / "portable.zip")
        try:
            extract_bundle(source, stage, identity)
        except IncompatibleBundle as error:
            if options.no_rebuild:
                raise
            print(str(error) + "; rebuilding Cut the Rope from the current pinned sources.", file=sys.stderr)
            shutil.rmtree(stage)
        else:
            verify(root, stage)
            action = "Restored verified Cut the Rope inputs from the pinned bundle."
    if action is None:
        require(not options.no_rebuild, "No compatible Cut the Rope bundle is available")
        rebuild(root, stage, options.android_player)
        verify(root, stage)
        action = "Rebuilt and verified Cut the Rope inputs from pinned sources."
    require(fingerprint(root) == identity, "Cut the Rope sources changed during preparation; retry")
    install(root, stage)
    write_bundle(root, stage, entry, identity, replace=True)
    print(action)
    return stage, identity


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--cache-dir", default=os.environ.get("XANADU_CTR_CACHE_DIR") or default_cache())
    parser.add_argument("--bundle", default=os.environ.get("XANADU_CTR_BUNDLE"), help="Use this local ZIP or HTTPS URL on cache miss")
    parser.add_argument("--bundle-sha256", default=os.environ.get("XANADU_CTR_BUNDLE_SHA256"))
    parser.add_argument("--export-bundle", type=Path, help="Write a new portable ZIP outside the checkout or under artifacts/")
    parser.add_argument("--android-player", default=os.environ.get("UNITY_ANDROID_PLAYER"))
    parser.add_argument("--no-rebuild", action="store_true", help="Fail on cache miss instead of running the official builders")
    options = parser.parse_args()
    root = options.root.resolve()
    try:
        cache = open_cache(root, options.cache_dir)
        output = None
        if options.export_bundle:
            output = confined_absolute(options.export_bundle)
            in_ignored_artifacts = root / "artifacts" in output.parents
            require((root not in output.parents or in_ignored_artifacts) and not output.exists(),
                    "Export must be a new file outside the checkout or under artifacts/")
        lock = checked_path(root, "artifacts/cut-the-rope/.prepare.lock")
        lock.parent.mkdir(parents=True, exist_ok=True)
        descriptor = os.open(lock, os.O_NOFOLLOW | os.O_CREAT | os.O_WRONLY, 0o600)
        with os.fdopen(descriptor, "w") as handle, tempfile.TemporaryDirectory(prefix=".ctr-stage-", dir=cache) as scratch:
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
            generated, identity = prepare(root, cache, options, Path(scratch))
            if output:
                write_bundle(root, generated, output, identity)
                print("Portable bundle SHA-256: " + digest(output))
        return 0
    except PREPARE_ERRORS as error:
        print("prepare-build-input: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
