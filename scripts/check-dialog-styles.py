#!/usr/bin/env python3
"""Exercise modal presentations and collect the rendered frames in one fresh review directory."""
import argparse
from contextlib import contextmanager
from datetime import datetime
from functools import partial
from html import escape
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import os
from pathlib import Path
import subprocess
import sys
from threading import Thread
from urllib.parse import quote


ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "scripts"


@contextmanager
def offline_game():
    handler = partial(SimpleHTTPRequestHandler, directory=str(ROOT / "artifacts/cut-the-rope"))
    with ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
        worker = Thread(target=server.serve_forever, daemon=True)
        worker.start()
        try:
            yield f"http://127.0.0.1:{server.server_port}/offline/"
        finally:
            server.shutdown()
            worker.join()


def run(command, log, environment):
    print("Checking", log.stem, flush=True)
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(command, cwd=ROOT, env=environment, stdout=output, stderr=subprocess.STDOUT)
    if result.returncode:
        print(f"Failed ({result.returncode}): {log}", file=sys.stderr)
    return result.returncode == 0


def write_index(folder):
    parts = ['<!doctype html><html lang="zh"><meta charset="utf-8"><title>弹窗验收</title>',
             '<style>body{max-width:1100px;margin:2rem auto;padding:1rem;font:16px system-ui}'
             'section{display:flex;gap:12px;flex-wrap:wrap}figure{margin:0;width:200px}img{width:100%;height:220px;'
             'object-fit:contain;background:#eee}figcaption{overflow-wrap:anywhere}</style><h1>弹窗验收</h1>']
    if (folder / "unity/index.html").is_file():
        parts.append('<p><a href="unity/index.html">Unity 逐游戏、逐弹窗检查与缺项清单</a></p>')
    platforms = {"bomb-it": "Q 版泡泡堂", "level-devil": "只有一道门", "zhaoyun-adou": "赵云与阿斗",
                 "cut-the-rope": "割绳子", "ios": "iOS 宿主", "android": "Android 宿主"}
    for directory, title in platforms.items():
        images = sorted((folder / directory).rglob("*.png"))
        if not images:
            continue
        parts.append(f"<h2>{escape(title)}</h2><section>")
        for path in images:
            url = quote(path.relative_to(folder).as_posix())
            parts.append(f'<figure><a href="{url}"><img loading="lazy" src="{url}"></a>'
                         f'<figcaption>{escape(path.stem)}</figcaption></figure>')
        parts.append("</section>")
    (folder / "index.html").write_text("\n".join(parts) + "</html>\n", encoding="utf-8")


def browser_environment(environment):
    env = environment.copy()
    if not env.get("PLAYWRIGHT_MODULE"):
        installed = subprocess.run(["node", "-p", "require.resolve('playwright-core')"], env=env, cwd=ROOT,
                                   capture_output=True, text=True)
        bundled = Path.home() / ".cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright-core"
        if installed.returncode == 0:
            env["PLAYWRIGHT_MODULE"] = installed.stdout.strip()
        elif bundled.is_dir():
            env["PLAYWRIGHT_MODULE"] = str(bundled)
    return env


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="new directory for this run")
    parser.add_argument("--simulator", help="booted iOS simulator UDID for native error panels")
    parser.add_argument("--android-serial", help="disposable Android emulator serial for native error panels")
    parser.add_argument("--apk", type=Path, help="signed Android package for the emulator")
    parser.add_argument("--only", choices=("unity", "web", "ios"), help="run a single presentation platform")
    options = parser.parse_args()
    if options.only == "ios" and not options.simulator:
        parser.error("--only ios requires --simulator")
    if bool(options.android_serial) != bool(options.apk):
        parser.error("supply both --android-serial and --apk")
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    folder = (options.output or ROOT / "artifacts/validation/dialog-styles" / stamp).resolve()
    if folder.exists():
        parser.error(f"choose a new output directory to exclude stale captures: {folder}")
    folder.mkdir(parents=True)
    print("Review directory", folder, flush=True)
    environment = dict(os.environ)
    checks = []
    if options.only in (None, "unity"):
        samples = folder / "unity"
        env = dict(environment, XANADU_DIALOG_AUDIT=str(samples))
        checks.append(run([sys.executable, SCRIPTS / "test.py", "--group", "unity"], folder / "unity.log", env))
        checks.append(run([sys.executable, SCRIPTS / "dialog-audit-report.py", samples, "--require-complete"],
                          folder / "coverage.log", env))
    if options.only in (None, "web"):
        with offline_game() as url:
            env = dict(browser_environment(environment), BOMB_IT_CHECK_OUTPUT=str(folder / "bomb-it"),
                       LEVEL_DEVIL_CHECK_OUTPUT=str(folder / "level-devil"),
                       ZHAOYUN_DIALOG_OUTPUT=str(folder / "zhaoyun-adou"),
                       CTR_DIALOG_OUTPUT=str(folder / "cut-the-rope"), CTR_URL=url)
            for game, script in (("bomb-it", "check-feedback.cjs"), ("level-devil", "check-menu-settings.cjs"),
                                 ("zhaoyun-adou", "test-save-editor.cjs"), ("cut-the-rope", "verify-language.cjs")):
                checks.append(run(["node", SCRIPTS / game / script], folder / (game + ".log"), env))
            env["ZHAOYUN_DIALOG_OUTPUT"] = str(folder / "zhaoyun-adou/canvas")
            checks.append(run(["node", SCRIPTS / "zhaoyun-adou/test-save-editor.cjs", "--canvas-dialogs"],
                              folder / "zhaoyun-canvas.log", env))
    if options.only in (None, "ios") and options.simulator:
        checks.append(run(["bash", ROOT / "Tests/IosDialogs/check.sh", options.simulator, folder / "ios"],
                          folder / "ios.log", environment))
    elif options.only is None:
        print("iOS native panels were not run. Supply --simulator to include them.")
    if options.android_serial:
        checks.append(run([sys.executable, ROOT / "Tests/AndroidDialogs/run.py", "--serial", options.android_serial,
                           "--apk", options.apk.resolve(), "--output", folder / "android"], folder / "android.log", environment))
    elif options.only is None:
        print("Android native panels were not run. Supply --android-serial and --apk to include them.")
    write_index(folder)
    return int(not all(checks))


if __name__ == "__main__":
    sys.exit(main())
