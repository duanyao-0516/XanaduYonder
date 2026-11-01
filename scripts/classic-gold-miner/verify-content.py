#!/usr/bin/env python3
"""Audit offline game dependencies and optionally verify an existing AAR against current inputs."""
import argparse
from pathlib import Path
from runtime_tools import AAR, REPORT_DIR, digest, encode_json, verify_aar, verify_content


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify-aar', nargs='?', const=str(AAR), type=Path, help='Also verify packaged assets/native input hashes; default is the Unity plugin')
    parser.add_argument('--report', type=Path, default=REPORT_DIR / 'content-validation.json')
    args = parser.parse_args()
    report = verify_content()
    if args.verify_aar is not None:
        report['errors'].extend(verify_aar(args.verify_aar, report))
        if args.verify_aar.is_file(): report['aarSha256'] = digest(args.verify_aar)
    report['passed'] = not report['errors']
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_bytes(encode_json(report))
    for error in report['errors']: print('ERROR:', error)
    print(('PASS' if report['passed'] else 'FAIL') + f": {report['assetCount']} assets, {report['verifiedStaticDependencyCount']} static dependencies; report {args.report}")
    raise SystemExit(0 if report['passed'] else 1)


if __name__ == '__main__': main()
