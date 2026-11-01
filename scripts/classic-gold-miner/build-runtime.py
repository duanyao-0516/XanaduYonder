#!/usr/bin/env python3
"""Compile the system-WebView Android host and bundle tracked offline assets into a deterministic AAR."""
import argparse
import os
from pathlib import Path
import subprocess
import shutil
import tempfile
from runtime_tools import AAR, CONTENT, REPORT_DIR, RUNTIME, archive, content_hashes, digest, encode_json, files_under, input_hashes, verify_aar, verify_content


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--android-player', type=Path, default=Path(os.environ.get('UNITY_ANDROID_PLAYER', '/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer')))
    parser.add_argument('--output', type=Path, default=AAR)
    args = parser.parse_args()
    report = verify_content()
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    (REPORT_DIR / 'content-validation.json').write_bytes(encode_json(report))
    if report['errors']:
        raise SystemExit('Content audit failed:\n' + '\n'.join(report['errors']))
    javac = args.android_player / 'OpenJDK/bin/javac'
    javap = args.android_player / 'OpenJDK/bin/javap'
    android_jar = args.android_player / 'SDK/platforms/android-36/android.jar'
    for dependency in (javac, javap, android_jar):
        if not dependency.is_file(): raise SystemExit('Missing Unity Android toolchain: ' + str(dependency))
    sources = sorted((RUNTIME / 'src').rglob('*.java'))
    with tempfile.TemporaryDirectory(prefix='classic-gold-miner-build-') as directory:
        temporary = Path(directory)
        classes = temporary / 'classes'
        classes.mkdir()
        subprocess.run([str(javac), '-encoding', 'UTF-8', '-source', '8', '-target', '8', '-Xlint:-options', '-classpath', str(android_jar), '-d', str(classes)] + [str(p) for p in sources], check=True)
        classes_jar = temporary / 'classes.jar'
        archive(classes_jar, {p.relative_to(classes).as_posix(): p for p in files_under(classes)})
        declarations = subprocess.check_output([str(javap), '-classpath', str(classes_jar), '-s', 'com.xanaduyonder.classicgoldminer.ClassicGoldMinerBridge'], text=True)
        required = ['public static synchronized boolean open(android.app.Activity, boolean, boolean);', 'public static synchronized boolean isRunning();', 'public static synchronized java.lang.String getLastError();']
        if any(signature not in declarations for signature in required): raise SystemExit('Unity bridge public API is incomplete')
        provenance = {
            'schema': 1, 'systemWebView': True, 'embeddedBrowserEngine': False,
            'minSdk': 26, 'targetSdk': 36, 'compileSdk': 36,
            'assets': report['assets'], 'runtimeInputs': report['runtimeInputs'],
            'classesJarSha256': digest(classes_jar), 'androidJarSha256': digest(android_jar),
            'javacVersion': subprocess.check_output([str(javac), '-version'], text=True, stderr=subprocess.STDOUT).strip(),
        }
        entries = {'AndroidManifest.xml': RUNTIME / 'AndroidManifest.xml', 'classes.jar': classes_jar,
                   'proguard.txt': RUNTIME / 'consumer-rules.pro', 'R.txt': b'',
                   'META-INF/classic-gold-miner-provenance.json': encode_json(provenance)}
        entries.update({'assets/classic-gold-miner/' + p.relative_to(CONTENT).as_posix(): p for p in files_under(CONTENT)})
        staged = temporary / 'ClassicGoldMiner.aar'
        archive(staged, entries)
        errors = verify_aar(staged, report)
        if content_hashes() != report['assets'] or input_hashes() != report['runtimeInputs']:
            errors.append('Source files changed during compilation/packaging; rerun after edits finish')
        if errors: raise SystemExit('Packaged AAR verification failed; output not replaced:\n' + '\n'.join(errors))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        replacement = args.output.with_suffix(args.output.suffix + '.tmp')
        shutil.copyfile(staged, replacement)
        replacement.replace(args.output)
    result = {'schema': 1, 'passed': True, 'aar': str(args.output.resolve()), 'aarSha256': digest(args.output),
              'javaSourceCount': len(sources), 'assetCount': report['assetCount'],
              'verifiedStaticDependencyCount': report['verifiedStaticDependencyCount'],
              'provenance': provenance,
              'validation': 'Java compilation, bridge API, static offline dependency audit and exact packaged hashes. No Unity build or device gameplay performed by this script.'}
    (REPORT_DIR / 'runtime-build.json').write_bytes(encode_json(result))
    print(f"PASS: {len(sources)} Java sources, {report['assetCount']} assets; {args.output}\nSHA-256 {result['aarSha256']}")


if __name__ == '__main__':
    try:
        main()
    except (Exception, SystemExit) as failure:
        if isinstance(failure, SystemExit) and failure.code in (None, 0):
            raise
        REPORT_DIR.mkdir(parents=True, exist_ok=True)
        (REPORT_DIR / 'runtime-build.json').write_bytes(encode_json({
            'schema': 1, 'passed': False, 'error': str(failure),
            'validation': 'Build failed. An existing AAR may be older than source; verify-content.py --verify-aar is required before use.'
        }))
        raise
