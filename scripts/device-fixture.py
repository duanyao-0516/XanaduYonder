#!/usr/bin/env python3
"""Fixed development-device fixtures; `self-test` never invokes adb."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest

PACKAGE = "com.duanyao.xanaduyonder"
ACTIVITY = PACKAGE + "/com.unity3d.player.UnityPlayerActivity"
DIRECTORY = "/storage/emulated/0/Android/data/" + PACKAGE + "/files"
PRIMARY = "xanaduyonder-v1.json"
NAMES = (PRIMARY, PRIMARY + ".bak", PRIMARY + ".tmp")
BACKUP_ROOT = Path(__file__).resolve().parent.parent / "artifacts" / "device-fixtures"


class FixtureError(RuntimeError):
    pass


def fixture(name):
    boards = {
        "merge": [2, 2, 4, 0] + [0] * 12,
        "noop": [2, 4, 0, 0, 4, 2, 0, 0] + [0] * 8,
        "win": [1024, 1024, 0, 0] + [0] * 12,
        "loss": [2, 2, 8, 16, 32, 64, 128, 256, 512, 1024, 2, 4, 8, 16, 32, 64],
    }
    if name not in boards:
        raise FixtureError("Unknown fixture: " + name)
    value = {
        "schemaVersion": 1,
        "saveKind": "xanaduyonder",
        "game": {"schemaVersion": 1, "gameId": "2048", "cells": boards[name], "score": "0", "winState": 1},
        "bestScore": "0",
        "soundState": 1,
        "hapticsState": 1,
    }
    return json.dumps(value, separators=(",", ":")).encode("utf-8")


class DeviceFixtures:
    def __init__(self, serial, adb="adb", backup_root=BACKUP_ROOT):
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:-]{0,127}", serial or ""):
            raise FixtureError("An explicit, valid physical device serial is required.")
        if serial.startswith("emulator-"):
            raise FixtureError("Emulator targets are not supported.")
        self.serial = serial
        self.adb = adb
        self.backup_directory = Path(backup_root) / serial

    def backup(self, overwrite=False):
        destination = self.backup_directory
        if destination.is_symlink():
            raise FixtureError("Backup directory must not be a symbolic link.")
        if destination.exists() and not overwrite:
            raise FixtureError("Backup already exists; it was preserved. Use backup --overwrite only to replace it intentionally.")
        if destination.exists() and not destination.is_dir():
            raise FixtureError("Backup destination is not a directory.")
        self.stop()
        contents = {name: self.read(name) for name in NAMES}
        manifest = {
            "schemaVersion": 1, "package": PACKAGE, "serial": self.serial, "directory": DIRECTORY,
            "state": "backed_up", "touchedFiles": [],
            "files": {name: {"exists": data is not None,
                             "sha256": hashlib.sha256(data).hexdigest() if data is not None else None}
                      for name, data in contents.items()},
        }
        destination.parent.mkdir(parents=True, exist_ok=True)
        staging = Path(tempfile.mkdtemp(prefix="." + self.serial + ".new-", dir=str(destination.parent)))
        previous = None
        published = False
        try:
            for name, content in contents.items():
                if content is not None:
                    (staging / name).write_bytes(content)
            (staging / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
            if destination.exists():
                previous = Path(tempfile.mkdtemp(prefix="." + self.serial + ".old-", dir=str(destination.parent)))
                previous.rmdir()
                os.replace(str(destination), str(previous))
            try:
                os.replace(str(staging), str(destination))
                published = True
            except OSError:
                if previous is not None:
                    os.replace(str(previous), str(destination))
                    previous = None
                raise
        finally:
            if staging.exists(): shutil.rmtree(str(staging))
            # If rollback itself fails, preserve the old directory for manual recovery.
            if published and previous is not None and previous.exists(): shutil.rmtree(str(previous))

    def apply(self, name):
        content = fixture(name)
        manifest, _ = self.load_backup()
        self.stop()
        manifest["state"] = "applying"
        manifest["fixture"] = name
        self.save_manifest(manifest)
        # Journal each fixed path before its mutation, including a partially failed write.
        for destination in (PRIMARY, NAMES[1]):
            self.mark_touched(manifest, NAMES[2])
            self.write(NAMES[2], content)
            self.mark_touched(manifest, destination)
            self.rename(NAMES[2], destination)
        if any(self.read(destination) != content for destination in NAMES[:2]):
            raise FixtureError("Fixture read-back differs from the requested data; original backup is preserved.")
        manifest["state"] = "applied"
        self.save_manifest(manifest)
        self.start()

    def restore(self):
        manifest, contents = self.load_backup()
        if manifest["state"] not in ("applying", "applied", "restoring"):
            raise FixtureError("No applied fixture to restore; original backups were preserved.")
        self.stop()
        manifest["state"] = "restoring"
        self.save_manifest(manifest)
        for name in NAMES:
            if contents[name] is not None:
                self.write(name, contents[name])
            elif name in manifest["touchedFiles"]:
                # Delete only these fixed paths that were absent at backup and claimed by apply.
                self.remove(name)
        for name in NAMES:
            if contents[name] is not None or name in manifest["touchedFiles"]:
                if self.read(name) != contents[name]:
                    raise FixtureError("Restored file verification failed: " + name)
        manifest["state"] = "restored"
        self.save_manifest(manifest)
        self.start()

    def run_as_command(self, script):
        command = "run-as " + shlex.quote(PACKAGE) + " sh -c " + shlex.quote(script)
        return [self.adb, "-s", self.serial, "shell", "-T", command]

    def file_command(self, script):
        # OPPO Android 16 permits shell access to this external app directory while
        # runas_app cannot traverse it. Keep run-as only for the development-APK check.
        command = "sh -c " + shlex.quote(script)
        return [self.adb, "-s", self.serial, "shell", "-T", command]

    def validate_device(self):
        result = self.invoke([self.adb, "-s", self.serial, "get-state"])
        if result.returncode != 0 or result.stdout.strip() != b"device":
            raise FixtureError("Device is not connected and authorized: " + self.serial)
        for property_name in ("ro.kernel.qemu", "ro.boot.qemu"):
            value = self.execute([self.adb, "-s", self.serial, "shell", "getprop", property_name])
            if value.strip() == b"1":
                raise FixtureError("Refusing an emulator target: " + self.serial)
        uid = self.execute(self.run_as_command("id -u")).strip()
        if not uid.isdigit() or int(uid) < 10000:
            raise FixtureError("run-as did not return an application UID; install the development APK first.")

    @staticmethod
    def invoke(arguments, input=None):
        try:
            return subprocess.run(arguments, input=input, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=45)
        except (OSError, subprocess.TimeoutExpired) as exception:
            raise FixtureError("adb invocation failed: " + str(exception)) from exception

    def execute(self, arguments, input=None):
        result = self.invoke(arguments, input=input)
        if result.returncode != 0:
            detail = (result.stderr or result.stdout).decode("utf-8", errors="replace").strip()
            raise FixtureError("adb command failed (" + str(result.returncode) + "): " + detail)
        return result.stdout

    def stop(self):
        self.execute([self.adb, "-s", self.serial, "shell", "am", "force-stop", PACKAGE])

    def start(self):
        output = self.execute([self.adb, "-s", self.serial, "shell", "am", "start", "-W", "-n", ACTIVITY])
        if b"Status: ok" not in output or b"Error:" in output:
            raise FixtureError("Activity launch was not confirmed: " + output.decode("utf-8", errors="replace"))

    @staticmethod
    def path(name):
        if name not in NAMES:
            raise FixtureError("Only the three fixed save filenames are allowed.")
        return DIRECTORY + "/" + name

    def file_guard(self, name):
        path = shlex.quote(self.path(name))
        return ("if [ -L " + path + " ] || { [ -e " + path + " ] && [ ! -f " + path + " ]; }; "
                "then echo 'Unexpected save file type' >&2; exit 72; fi; ")

    def read(self, name):
        path = shlex.quote(self.path(name))
        directory = shlex.quote(DIRECTORY)
        # Inaccessible storage must not be mistaken for an originally absent save.
        accessible = ("if [ ! -d " + directory + " ] || [ ! -r " + directory + " ] || [ ! -x " + directory +
                      " ]; then echo 'Cannot inspect expected persistence directory' >&2; exit 73; fi; ")
        script = accessible + self.file_guard(name) + "if [ -f " + path + " ]; then cat " + path + "; else exit 44; fi"
        result = self.invoke(self.file_command(script))
        if result.returncode == 44:
            return None
        if result.returncode != 0:
            raise FixtureError("Cannot read " + self.path(name) + ": " + result.stderr.decode("utf-8", errors="replace"))
        return result.stdout

    def write(self, name, content):
        script = (self.file_guard(name) + "mkdir -p " + shlex.quote(DIRECTORY) +
                  " && cat > " + shlex.quote(self.path(name)))
        self.execute(self.file_command(script), input=content)

    def rename(self, source, destination):
        if source != NAMES[2] or destination not in NAMES[:2]:
            raise FixtureError("Only fixed fixture staging renames are allowed.")
        script = (self.file_guard(source) + self.file_guard(destination) + "mv -f " +
                  shlex.quote(self.path(source)) + " " + shlex.quote(self.path(destination)))
        self.execute(self.file_command(script))

    def remove(self, name):
        self.execute(self.file_command(self.file_guard(name) + "rm -f " + shlex.quote(self.path(name))))

    def mark_touched(self, manifest, name):
        self.path(name)
        if name not in manifest["touchedFiles"]:
            manifest["touchedFiles"].append(name)
        self.save_manifest(manifest)

    def save_manifest(self, manifest):
        temporary = self.backup_directory / ".manifest.json.tmp"
        with temporary.open("w", encoding="utf-8") as stream:
            json.dump(manifest, stream, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(str(temporary), str(self.backup_directory / "manifest.json"))

    def load_backup(self):
        directory = self.backup_directory
        if directory.is_symlink():
            raise FixtureError("Backup directory must not be a symbolic link.")
        try:
            manifest_path = directory / "manifest.json"
            if manifest_path.is_symlink():
                raise ValueError("Backup manifest must not be a symbolic link.")
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            if (manifest["schemaVersion"] != 1 or manifest["package"] != PACKAGE or
                    manifest["serial"] != self.serial or manifest["directory"] != DIRECTORY or
                    set(manifest["files"]) != set(NAMES) or
                    manifest["state"] not in ("backed_up", "applying", "applied", "restoring", "restored") or
                    not isinstance(manifest["touchedFiles"], list) or
                    not all(name in NAMES for name in manifest["touchedFiles"])):
                raise ValueError("Backup manifest does not match this tool/device.")
            contents = {}
            for name in NAMES:
                metadata = manifest["files"][name]
                local = directory / name
                if local.is_symlink() or type(metadata["exists"]) is not bool:
                    raise ValueError("Invalid backup file metadata.")
                if metadata["exists"]:
                    contents[name] = local.read_bytes()
                    if hashlib.sha256(contents[name]).hexdigest() != metadata["sha256"]:
                        raise ValueError("Backup hash mismatch for " + name)
                else:
                    if local.exists() or metadata["sha256"] is not None:
                        raise ValueError("Unexpected backup for originally absent file.")
                    contents[name] = None
            return manifest, contents
        except (OSError, ValueError, KeyError, TypeError) as exception:
            raise FixtureError("Complete original backup required; run backup first. " + str(exception)) from exception


class FixtureTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="xanadu-fixture-tests-")
        self.addCleanup(self.temporary.cleanup)

    def tool(self, files=None):
        class MemoryDevice(DeviceFixtures):
            def __init__(self, directory, initial):
                super().__init__("physical-1", backup_root=directory)
                self.files = dict(initial or {})
                self.events = []

            def stop(self): self.events.append("stop")
            def start(self): self.events.append("start")
            def read(self, name): return self.files.get(name)
            def write(self, name, content):
                self.events.append(("write", name))
                self.files[name] = content
            def rename(self, source, destination):
                self.files[destination] = self.files.pop(source)
            def remove(self, name):
                self.events.append(("remove", name))
                self.files.pop(name, None)
        return MemoryDevice(self.temporary.name, files)

    def test_fixture_contract_and_loss_after_either_spawn(self):
        expected = {
            "merge": [2, 2, 4, 0] + [0] * 12,
            "noop": [2, 4, 0, 0, 4, 2, 0, 0] + [0] * 8,
            "win": [1024, 1024, 0, 0] + [0] * 12,
            "loss": [2, 2, 8, 16, 32, 64, 128, 256, 512, 1024, 2, 4, 8, 16, 32, 64],
        }
        for name, cells in expected.items():
            value = json.loads(fixture(name))
            self.assertEqual(cells, value["game"]["cells"])
            self.assertEqual("0", value["game"]["score"])
            self.assertEqual("0", value["bestScore"])
            self.assertEqual("xanaduyonder", value["saveKind"])
            self.assertEqual("2048", value["game"]["gameId"])
            self.assertEqual(1, value["schemaVersion"])
            self.assertEqual(1, value["game"]["schemaVersion"])
            self.assertEqual(1, value["game"]["winState"])
            self.assertEqual(1, value["soundState"])
            self.assertEqual(1, value["hapticsState"])
        for spawn in (2, 4):
            after_left = [4, 8, 16, spawn] + expected["loss"][4:]
            self.assertNotIn(0, after_left)
            for row in range(4):
                for column in range(4):
                    cell = row * 4 + column
                    if column < 3: self.assertNotEqual(after_left[cell], after_left[cell + 1])
                    if row < 3: self.assertNotEqual(after_left[cell], after_left[cell + 4])

    def test_serial_and_commands_are_explicit_and_quoted(self):
        for invalid in ("", "../other", "a/b", "a b", "emulator-5554", "a;reboot"):
            with self.assertRaises(FixtureError): DeviceFixtures(invalid)
        tool = DeviceFixtures("physical-1", adb="/path with spaces/adb")
        script = "cat " + shlex.quote(DIRECTORY + "/" + PRIMARY)
        command = tool.run_as_command(script)
        self.assertEqual(["/path with spaces/adb", "-s", "physical-1", "shell", "-T"], command[:5])
        remote = shlex.split(command[5])
        self.assertEqual(["run-as", PACKAGE, "sh", "-c", script], remote)

    def test_backup_does_not_overwrite_without_flag(self):
        tool = self.tool({PRIMARY: b"original", NAMES[1]: b"older", NAMES[2]: b"pending"})
        tool.backup()
        tool.files[PRIMARY] = b"new"
        with self.assertRaises(FixtureError): tool.backup()
        self.assertEqual(b"original", (tool.backup_directory / PRIMARY).read_bytes())
        tool.backup(overwrite=True)
        self.assertEqual(b"new", (tool.backup_directory / PRIMARY).read_bytes())

    def test_apply_requires_backup_and_preserves_originals(self):
        tool = self.tool({PRIMARY: b"original"})
        with self.assertRaises(FixtureError): tool.apply("merge")
        self.assertEqual([], tool.events)
        tool.backup()
        tool.apply("merge")
        self.assertEqual(fixture("merge"), tool.files[PRIMARY])
        self.assertEqual(fixture("merge"), tool.files[NAMES[1]])
        self.assertNotIn(NAMES[2], tool.files)
        self.assertEqual(b"original", (tool.backup_directory / PRIMARY).read_bytes())

    def test_restore_preserves_bytes_and_original_absence(self):
        for original in ({}, {PRIMARY: b"original"}, {PRIMARY: b"invalid json", NAMES[1]: b"older", NAMES[2]: b"pending"}):
            with self.subTest(original=original):
                tool = self.tool(original)
                tool.backup(overwrite=True)
                tool.apply("win")
                tool.files[NAMES[2]] = b"fixture save in progress"
                tool.restore()
                self.assertEqual(original, tool.files)
                self.assertEqual("start", tool.events[-1])
                self.assertTrue((tool.backup_directory / "manifest.json").exists())

    def test_tampered_backup_fails_before_device_changes(self):
        tool = self.tool({PRIMARY: b"original"})
        tool.backup()
        (tool.backup_directory / PRIMARY).write_bytes(b"tampered")
        tool.events.clear()
        with self.assertRaises(FixtureError): tool.apply("loss")
        self.assertEqual([], tool.events)

    def test_unauthorized_or_qemu_device_is_rejected(self):
        tool = DeviceFixtures("physical-1")
        tool.invoke = lambda arguments, **kwargs: subprocess.CompletedProcess(arguments, 1, b"", b"unauthorized")
        with self.assertRaises(FixtureError): tool.validate_device()
        def qemu(arguments, **kwargs):
            output = b"device\n" if arguments[-1] == "get-state" else b"1\n"
            return subprocess.CompletedProcess(arguments, 0, output, b"")
        tool.invoke = qemu
        with self.assertRaises(FixtureError): tool.validate_device()

    def test_partial_apply_is_recoverable(self):
        tool = self.tool({PRIMARY: b"original"})
        tool.backup()
        original_rename = tool.rename
        def fail_second_rename(source, destination):
            if destination == NAMES[1]: raise FixtureError("simulated connection interruption")
            original_rename(source, destination)
        tool.rename = fail_second_rename
        with self.assertRaises(FixtureError): tool.apply("merge")
        self.assertNotIn("start", tool.events)
        tool.restore()
        self.assertEqual({PRIMARY: b"original"}, tool.files)

    def test_restore_never_deletes_unclaimed_files(self):
        tool = self.tool()
        tool.backup()
        with self.assertRaises(FixtureError): tool.restore()
        manifest, _ = tool.load_backup()
        manifest["state"] = "applying"
        manifest["touchedFiles"] = [PRIMARY]
        tool.save_manifest(manifest)
        tool.files = {PRIMARY: b"tool fixture", NAMES[1]: b"unclaimed"}
        tool.restore()
        self.assertEqual({NAMES[1]: b"unclaimed"}, tool.files)
        for unknown in ("/tmp/arbitrary", "../other", "prefs.json"):
            with self.assertRaises(FixtureError): tool.path(unknown)

    def test_write_read_back_mismatch_prevents_launch(self):
        tool = self.tool({PRIMARY: b"original"})
        tool.backup()
        original_write = tool.write
        tool.write = lambda name, content: original_write(name, content + b"corruption")
        with self.assertRaises(FixtureError): tool.apply("loss")
        self.assertNotIn("start", tool.events)
        self.assertEqual(b"original", (tool.backup_directory / PRIMARY).read_bytes())

    def test_external_file_operations_use_shell_but_uid_check_uses_run_as(self):
        tool = DeviceFixtures("physical-1")
        commands = []
        def capture(arguments, input=None):
            commands.append(arguments)
            if arguments[-1] == "get-state": output = b"device\n"
            elif "getprop" in arguments: output = b"0\n"
            elif "id -u" in arguments[-1]: output = b"10377\n"
            else: output = b"original score 220"
            return subprocess.CompletedProcess(arguments, 0, output, b"")
        tool.invoke = capture
        tool.validate_device()
        validation = shlex.split(commands[-1][-1])
        self.assertEqual(["run-as", PACKAGE, "sh", "-c", "id -u"], validation)
        commands.clear()
        self.assertEqual(b"original score 220", tool.read(PRIMARY))
        tool.write(NAMES[2], fixture("merge"))
        tool.rename(NAMES[2], PRIMARY)
        tool.remove(NAMES[2])
        self.assertEqual(4, len(commands))
        for command in commands:
            self.assertEqual([tool.adb, "-s", "physical-1", "shell", "-T"], command[:5])
            remote = shlex.split(command[5])
            self.assertEqual(["sh", "-c"], remote[:2])
            self.assertEqual(3, len(remote))
            self.assertIn(DIRECTORY + "/" + PRIMARY, remote[2])
            self.assertNotIn("run-as", remote[2])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", help="Required explicit physical-device serial; never auto-selected.")
    parser.add_argument("--adb", default=os.environ.get("ADB") or shutil.which("adb") or "adb", help="adb executable path.")
    commands = parser.add_subparsers(dest="command", required=True)
    backup_parser = commands.add_parser("backup", help="Stop the app and preserve original files locally.")
    backup_parser.add_argument("--overwrite", action="store_true", help="Explicitly replace an existing local original backup.")
    apply_parser = commands.add_parser("apply", help="Install a fixed fixture after a verified backup.")
    apply_parser.add_argument("fixture", choices=("merge", "noop", "win", "loss"))
    commands.add_parser("restore", help="Restore original contents/existence and launch the app.")
    commands.add_parser("self-test", help="Run offline unit checks; never invokes adb.")
    arguments = parser.parse_args(argv)
    if arguments.command == "self-test":
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(FixtureTests)
        return 0 if unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful() else 1
    if not arguments.serial:
        parser.error("--serial is required for every device operation.")
    try:
        tool = DeviceFixtures(arguments.serial, arguments.adb)
        tool.validate_device()
        if arguments.command == "backup":
            tool.backup(arguments.overwrite)
            print("Original backup: " + str(tool.backup_directory) + ". The target app remains stopped.")
        elif arguments.command == "apply":
            tool.apply(arguments.fixture)
            print("Applied " + arguments.fixture + ". Tap 2048 in the lobby, then swipe LEFT on the board.")
        else:
            tool.restore()
            print("Original file state restored before launch; the app can now create its normal saves.")
        return 0
    except (FixtureError, OSError) as exception:
        print("Error: " + str(exception), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
