#!/usr/bin/env python3
"""Exercise native error dialogs in a disposable emulator using the supplied Android build."""
import argparse
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import tempfile
import zipfile

HERE = Path(__file__).resolve().parent
HOST = 'com.duanyao.xanaduyonder'
RUNNER = 'net.xanadu.validation'


def output(command):
    return subprocess.check_output(list(map(str, command)), text=True).strip()


def execute(command, **kwargs):
    subprocess.run(list(map(str, command)), check=True, **kwargs)


def latest_version(directory, pattern):
    candidates = [(tuple(int(n) for n in match.groups()), path)
                  for path in directory.iterdir() if (match := re.fullmatch(pattern, path.name))]
    if not candidates:
        raise RuntimeError(f'Missing Android SDK tools under {directory}')
    return max(candidates)[1]


def build_test_apks(work, apk, sdk):
    tools = latest_version(sdk / 'build-tools', r'(\d+)\.(\d+)\.(\d+)')
    android = latest_version(sdk / 'platforms', r'android-(\d+)') / 'android.jar'
    javac = Path(os.environ['JAVA_HOME']) / 'bin/javac' if 'JAVA_HOME' in os.environ else shutil.which('javac')
    if not javac:
        raise RuntimeError('Set JAVA_HOME to a JDK')
    java = Path(javac).resolve().parent
    classes, dex = work / 'classes', work / 'dex'
    classes.mkdir()
    dex.mkdir()
    unsigned, aligned = work / 'runner-unsigned.apk', work / 'runner-aligned.apk'
    execute([tools / 'aapt2', 'link', '-o', unsigned, '--manifest', HERE / 'AndroidManifest.xml', '-I', android])
    execute([java / 'javac', '-encoding', 'UTF-8', '-source', '8', '-target', '8', '-Xlint:-options',
             '-cp', android, '-d', classes, HERE / 'Runner.java'])
    execute([java / 'jar', 'cf', work / 'runner.jar', '-C', classes, '.'])
    execute([tools / 'd8', '--lib', android, '--min-api', '26', '--output', dex, work / 'runner.jar'])
    with zipfile.ZipFile(unsigned, 'a') as archive:
        archive.write(dex / 'classes.dex', 'classes.dex')
    execute([tools / 'zipalign', '-f', '-p', '4', unsigned, aligned])

    # Instrumentation and target must share a certificate. Both copies are temporary.
    signing_env = {**os.environ, 'DIALOG_TEST_KEY_PASSWORD': secrets.token_urlsafe(24)}
    keystore = work / 'test.p12'
    execute([java / 'keytool', '-genkeypair', '-noprompt', '-alias', 'validation', '-keystore', keystore,
             '-storepass:env', 'DIALOG_TEST_KEY_PASSWORD', '-keyalg', 'RSA', '-keysize', '2048',
             '-validity', '2', '-dname', 'CN=Android Dialog Validation'], env=signing_env)
    for source, target in [(apk, work / 'host.apk'), (aligned, work / 'runner.apk')]:
        execute([tools / 'apksigner', 'sign', '--ks', keystore, '--ks-pass', 'env:DIALOG_TEST_KEY_PASSWORD',
                 '--out', target, source], env=signing_env)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True, help='disposable emulator serial, e.g. emulator-5586')
    parser.add_argument('--apk', required=True, type=Path, help='signed project APK')
    parser.add_argument('--output', type=Path, default=HERE.parents[1] / 'artifacts/validation/android-dialogs')
    args = parser.parse_args()
    if not re.fullmatch(r'emulator-\d+', args.serial) or not args.apk.is_file():
        parser.error('Provide a running emulator serial and an existing project APK')
    adb = shutil.which('adb')
    if not adb:
        parser.error('Add Android SDK platform-tools to PATH')
    sdk_path = os.environ.get('ANDROID_SDK_ROOT') or os.environ.get('ANDROID_HOME')
    sdk = Path(sdk_path) if sdk_path else Path(adb).resolve().parents[1]
    device = [adb, '-s', args.serial]
    if output([*device, 'shell', 'getprop', 'ro.kernel.qemu']) != '1':
        parser.error('The selected target must be a booted emulator')
    for package in [HOST, RUNNER]:
        if output([*device, 'shell', 'pm', 'list', 'packages', package]).splitlines().count('package:' + package):
            parser.error(f'Use a separate emulator without {package}; each run installs fresh temporary test packages')

    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='android-dialogs-') as folder:
        work = Path(folder)
        build_test_apks(work, args.apk.resolve(), sdk)
        installed = []
        try:
            for package, file in [(HOST, 'host.apk'), (RUNNER, 'runner.apk')]:
                execute([*device, 'install', work / file])
                installed.append(package)
            result = output([*device, 'shell', 'am', 'instrument', '-w', RUNNER + '/.Runner'])
            (args.output / 'run.log').write_text(result + '\n')
            shots = '/sdcard/Android/data/' + HOST + '/files/web-host-qa'
            if subprocess.run([*device, 'shell', 'test', '-d', shots]).returncode == 0:
                execute([*device, 'pull', shots + '/.', args.output])
            if 'INSTRUMENTATION_RESULT: result=PASS' not in result:
                raise RuntimeError(result)
            print(f"Passed {result.count('INSTRUMENTATION_STATUS: passed=')} assertions. Evidence in {args.output}")
        finally:
            for package in reversed(installed):
                execute([*device, 'uninstall', package])


if __name__ == '__main__':
    main()
