#!/usr/bin/env python3
"""Regenerate the reviewed AS2 bridge with optional JPEXS 26.4.0.

Not called by the production builder. Review AS2 and changed hash lock together.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
NAMES = ('root-init', 'menu-ready', 'timer', 'pause-button', 'restore-select', 'restore-game', 'game-checkpoint')

def digest(data):
    return hashlib.sha256(data).hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ffdec-jar', required=True, type=Path)
    args = parser.parse_args()
    jar = args.ffdec_jar.resolve()
    # The exact compiler archive is documented in ../README.md. The installed
    # jar is also pinned here so a different compiler cannot silently regenerate.
    expected = '80f8ced0bfde98b434c28e9807e91fe5558255b2f712051a0e65221519bd392a'
    if digest(jar.read_bytes()) != expected:
        raise SystemExit('Unreviewed FFDec compiler jar')
    output = ROOT / 'artifacts/bomb-it/bridge-classes'
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run(['javac', '-cp', str(jar), '-d', str(output), str(HERE / 'CompileBridge.java')], check=True)
    files = []
    for name in NAMES:
        source = HERE / 'bridge' / (name + '.as')
        binary = HERE / 'bridge' / (name + '.bin')
        subprocess.run(['java', '-cp', str(output) + ':' + str(jar), 'CompileBridge',
                        str(ROOT / 'ThirdParty/BombItContent/original/game.swf'), str(source), str(binary)], check=True)
        files.append({'name': name, 'sourceSha256': digest(source.read_bytes()),
                      'bytes': binary.stat().st_size, 'sha256': digest(binary.read_bytes())})
    lock = {'schema': 1, 'compiler': 'JPEXS FFDec 26.4.0', 'compilerJarSha256': expected,
            'compilerSourceSha256': digest((HERE / 'CompileBridge.java').read_bytes()), 'files': files}
    (HERE / 'bridge/lock.json').write_text(json.dumps(lock, indent=2) + '\n')
    print(json.dumps(lock, indent=2))

if __name__ == '__main__':
    main()
