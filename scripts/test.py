#!/usr/bin/env python3
"""List and run the repository's local checks from any working directory."""

import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "scripts"
GROUPS = ("dotnet", "data", "platform", "unity")
EDITOR_CHECKS = {
    "bakery-basket-ui": ("BakeryBasketChecks.Run", "BAKERY_BASKET_CHECKS_PASS"),
    "classic-boards-ui": ("ClassicBoardGamesUiChecks.Run", "CLASSIC_BOARD_GAMES_UI_CHECKS_PASS"),
    "feihualing-ui": ("FeihualingUiChecks.Run", "FEIHUALING_UI_CHECKS_PASS"),
    "host-save-isolation": ("MiniGameUiChecks.CheckHostSaveIsolation", "HOST_SAVE_ISOLATION_PASS"),
}
PLATFORM_CHECKS = {
    "bomb-it-package": [sys.executable, "scripts/bomb-it/build-content.py", "--verify"],
    "cut-the-rope-cache": [sys.executable, "scripts/cut-the-rope/test_prepare_build_input.py"],
    "cut-the-rope-content": [sys.executable, "Tests/CutTheRopeContent/test_content.py"],
    "cut-the-rope-runtime": [sys.executable, "Tests/CutTheRopeRuntime/run-tests.py"],
    "cut-the-rope-web": ["node", "--test", "Tests/CutTheRopeContent/offline-contract.test.cjs",
                         "Tests/CutTheRopeContent/english-only.test.cjs",
                         "Tests/CutTheRopeContent/original-game-regressions.test.cjs"],
    "level-devil-package": [sys.executable, "scripts/level-devil/build-content.py", "--verify"],
    "zhaoyun-apk-verifier": [sys.executable, "scripts/zhaoyun-adou/test-apk-verifier.py"],
    "zhaoyun-build-gates": [sys.executable, "scripts/zhaoyun-adou/test-build-gates.py"],
    "zhaoyun-host": [sys.executable, "scripts/zhaoyun-adou/test-host.py"],
    "zhaoyun-runtime-package": [sys.executable, "scripts/zhaoyun-adou/test-runtime-package.py"],
}
UNITY_SUITES = {
    "dino-ground", "dino-page", "dino-visual", "lobby-design", "startup-handoff",
    "startup-overlay", "watermelon-physics", "watermelon-pointer", "zuma-navigation",
}


def dotnet_executable():
    candidates = []
    if os.environ.get("DOTNET_ROOT"):
        candidates.append(Path(os.environ["DOTNET_ROOT"]) / "dotnet")
    candidates.append(Path.home() / ".dotnet" / "dotnet")
    return next((str(p) for p in candidates if p.is_file() and os.access(p, os.X_OK)),
                shutil.which("dotnet") or "dotnet")


def suites():
    entries = {}
    for project in sorted((ROOT / "Tests").glob("*/*Tests.csproj")):
        name = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "-", project.parent.name).lower()
        if name == "gold-miner-source-campaign":
            name = "gold-miner-source"
        entries[name] = ("dotnet", [dotnet_executable(), "run", "--project", str(project),
                                    "--configuration", "Release"])

    # Prefer project wrappers: resource paths and fixtures are part of their contracts.
    for path in sorted(SCRIPTS.glob("test-*")):
        if path.suffix not in {".sh", ".py"}:
            continue
        name = path.stem[len("test-"):]
        if name in entries:
            group = entries[name][0]
        elif name.endswith("-ui") or name in UNITY_SUITES:
            group = "unity"
        else:
            group = "data"
        interpreter = "bash" if path.suffix == ".sh" else sys.executable
        entries[name] = (group, [interpreter, str(path)])
    editor = os.environ.get("UNITY_EDITOR") or (
        "/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity")
    for name, (method, _) in EDITOR_CHECKS.items():
        command = [editor, "-batchmode", "-projectPath", str(ROOT), "-buildTarget", "Android"]
        if name == "host-save-isolation":
            command.append("-quit")
        entries[name] = ("unity", [*command, "-executeMethod", "XanaduYonder.EditorTools." + method])
    for name, command in PLATFORM_CHECKS.items():
        entries[name] = ("platform", command)
    return entries


def run_suite(name, command):
    if name not in EDITOR_CHECKS:
        return subprocess.run(command, cwd=ROOT).returncode == 0

    # A fresh log belongs to exactly this run, including when Unity fails before startup.
    folder = ROOT / "artifacts" / "logs"
    folder.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(prefix=name + "-", suffix=".log", dir=folder, delete=False) as output:
        log = Path(output.name)
    print(f"Unity log: {log}", flush=True)
    if subprocess.run([*command, "-logFile", str(log)], cwd=ROOT).returncode != 0:
        return False
    marker = EDITOR_CHECKS[name][1]
    with log.open(encoding="utf-8", errors="replace") as output:
        if any(re.match(rf"^{marker}(?:$|[:\s])", line) for line in output):
            return True
    print(f"Unity exited without {marker} in {log}", file=sys.stderr)
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__, epilog=(
        "Platform checks use Node.js, Pillow and Unity's Android OpenJDK (UNITY_ANDROID_PLAYER). "
        "Content checks read the prepared Cut the Rope offline directory, Zhao Yun content/reports "
        "and the Bomb It and Level Devil content AARs and extracted files. See docs/building.md. "
        "The runner never prepares these inputs."))
    parser.add_argument("--list", action="store_true", help="show available suites")
    parser.add_argument("--group", choices=GROUPS, help="select a complete group")
    parser.add_argument("names", nargs="*", metavar="suite")
    args = parser.parse_args()
    entries = suites()
    if args.group and args.names:
        parser.error("choose either a group or named suites")
    unknown = sorted(set(args.names) - entries.keys())
    if unknown:
        parser.error("unknown suites: " + ", ".join(unknown))
    if not args.list and not args.group and not args.names:
        parser.error("select a group or suite, or use --list")
    names = list(dict.fromkeys(args.names)) if args.names else sorted(
        name for name, (group, _) in entries.items() if not args.group or group == args.group)
    if args.list:
        for name in names:
            print(f"{entries[name][0]:8} {name}")
        return 0

    failures = []
    for name in names:
        print(f"Running {name}", flush=True)
        try:
            passed = run_suite(name, entries[name][1])
        except OSError as error:
            print(error, file=sys.stderr)
            passed = False
        if not passed:
            failures.append(name)
    print(f"{len(names) - len(failures)}/{len(names)} suites passed", flush=True)
    if failures:
        print("Failed: " + ", ".join(failures), file=sys.stderr)
    return int(bool(failures))


if __name__ == "__main__":
    sys.exit(main())
