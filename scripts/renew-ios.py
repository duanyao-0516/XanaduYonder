#!/usr/bin/env python3
"""Renew one existing Personal Team app using Apple's local tools only.

Configuration and the schema_version=1 ledger are local JSON files. The installed
record requires bundle_id, team_id, device_id, profile_expiration (UTC ISO 8601),
signing_certificate_sha256, app_version, app_build, and app_executable. A pending
record has the same fields plus app_path and archive_sha256. Only a successful
CoreDevice installation advances installed. No command launches or uninstalls.

Exit 0: healthy/not_due/deferred/renewed; exit 1: failed. Read the JSON status,
not just the exit code. --status only reads configuration and the ledger.
"""

import argparse
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import urllib.parse
import uuid
import zipfile

UTC = dt.timezone.utc
PATH_KEYS = ('xcode_archive', 'work_dir', 'derived_data', 'ledger_path', 'log_dir')
IDENTITY_KEYS = ('bundle_id', 'team_id', 'device_id')
RUN_ID = r'\d{8}T\d{6}Z-[0-9a-f]{12}'
OWNER_FILE = '.xanadu-renewal-owner.json'


class RenewalError(Exception):
    """Safe, non-sensitive error suitable for the result JSON."""


class Deferred(RenewalError):
    pass


class StaleCandidate(RenewalError):
    pass


def require(condition, message):
    if not condition:
        raise RenewalError(message)


def now():
    return dt.datetime.now(UTC)


def iso(value):
    return value.astimezone(UTC).isoformat(timespec='seconds').replace('+00:00', 'Z')


def date(value):
    try:
        parsed = value if isinstance(value, dt.datetime) else dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
        return parsed.replace(tzinfo=UTC) if parsed.tzinfo is None else parsed.astimezone(UTC)
    except (AttributeError, TypeError, ValueError):
        raise RenewalError('invalid_profile_expiration') from None


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path):
    try:
        value = json.loads(path.read_text())
    except (OSError, ValueError):
        raise RenewalError('unreadable_json_file') from None
    require(isinstance(value, dict), 'json_object_required')
    return value


def atomic_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor, temporary = tempfile.mkstemp(prefix='.' + path.name + '.', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w') as stream:
            json.dump(value, stream, indent=2, sort_keys=True)
            stream.write('\n')
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        directory = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def configuration(path):
    config = load_json(path)
    for key in IDENTITY_KEYS + ('core_device_id', 'app_name'):
        require(isinstance(config.get(key), str) and re.fullmatch(r'[A-Za-z0-9_.-]+', config[key]), 'invalid_config_' + key)
    require(re.fullmatch(r'[0-9a-f]{64}', config.get('archive_sha256', '')), 'invalid_archive_sha256')
    for key in PATH_KEYS:
        require(isinstance(config.get(key), str) and Path(config[key]).is_absolute(), 'absolute_path_required_' + key)
        config[key] = Path(config[key]).resolve()
    require(config['work_dir'] != config['derived_data'], 'work_and_derived_paths_must_differ')
    for key, default in (('renew_before_hours', 72), ('minimum_new_validity_hours', 120)):
        value = config.get(key, default)
        require(isinstance(value, (int, float)) and not isinstance(value, bool) and 0 < value <= 168, 'invalid_config_' + key)
        config[key] = value
    require(config['minimum_new_validity_hours'] > config['renew_before_hours'], 'invalid_renewal_thresholds')
    return config


def read_ledger(config):
    ledger = load_json(config['ledger_path'])
    require(ledger.get('schema_version') == 1, 'unsupported_ledger_schema')
    record = ledger.get('installed')
    require(isinstance(record, dict), 'installed_record_required')
    for key in IDENTITY_KEYS:
        require(record.get(key) == config[key], 'installed_identity_mismatch_' + key)
    require(re.fullmatch(r'[0-9a-f]{64}', record.get('signing_certificate_sha256', '')), 'installed_certificate_fingerprint_required')
    for key in ('app_version', 'app_build', 'app_executable'):
        require(isinstance(record.get(key), str) and record[key], 'installed_' + key + '_required')
    require(Path(record['app_executable']).name == record['app_executable'], 'invalid_installed_executable')
    date(record.get('profile_expiration'))
    require(ledger.get('pending') is None or isinstance(ledger['pending'], dict), 'invalid_pending_record')
    return ledger


def report(status, ledger=None, reason=None, **extra):
    result = dict(status=status, checked_at=iso(now()))
    if reason:
        result['reason'] = reason
    if ledger:
        result['installed_expiration'] = ledger['installed']['profile_expiration']
        remaining = (date(result['installed_expiration']) - now()).total_seconds() / 3600
        result['installed_remaining_hours'] = round(remaining, 2)
        result['critical'] = remaining <= 24
        result['pending'] = ledger.get('pending') is not None
    result.update(extra)
    return result


class Runner:
    def __init__(self, directory):
        self.directory = directory
        directory.mkdir(parents=True, exist_ok=False, mode=0o700)
        self.counter = 0

    def run(self, arguments, label, timeout=90, capture=False):
        """Capture plist output in memory only; never copy it into a log."""
        self.counter += 1
        log = self.directory / ('%03d-%s.log' % (self.counter, label))
        with log.open('wb') as stream:
            process = subprocess.Popen(arguments, stdin=subprocess.DEVNULL,
                                       stdout=subprocess.PIPE if capture else stream,
                                       stderr=stream, start_new_session=True)
            try:
                output, _ = process.communicate(timeout=timeout)
            except BaseException:
                # Stop the complete build process group before restoring profiles.
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                    process.wait(timeout=5)
                except ProcessLookupError:
                    pass
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait(timeout=5)
                raise RenewalError(label + '_interrupted_or_timed_out') from None
        require(process.returncode == 0, label + '_failed')
        return output or b''

    def device(self, arguments, label, timeout=40):
        destination = self.directory / (label + '-' + uuid.uuid4().hex + '.json')
        self.run(['/usr/bin/xcrun', 'devicectl'] + arguments +
                 ['--timeout', str(timeout), '--json-output', str(destination), '--quiet'],
                 label, timeout=timeout + 10)
        data = load_json(destination)
        require(data.get('info', {}).get('outcome') == 'success', label + '_unsuccessful_json')
        require(isinstance(data.get('result'), dict), label + '_invalid_json')
        return data['result']


def decode_profile(runner, path):
    try:
        profile = plistlib.loads(runner.run(['/usr/bin/security', 'cms', '-D', '-i', str(path)],
                                          'profile-decode', timeout=20, capture=True))
    except (ValueError, plistlib.InvalidFileException):
        raise RenewalError('unreadable_provisioning_profile') from None
    require(isinstance(profile, dict), 'invalid_provisioning_profile')
    return profile


def profile_matches(profile, config):
    return (profile.get('TeamIdentifier') == [config['team_id']] and
            profile.get('Entitlements', {}).get('application-identifier') ==
            config['team_id'] + '.' + config['bundle_id'])


def profile_directories():
    return (Path.home() / 'Library/MobileDevice/Provisioning Profiles',
            Path.home() / 'Library/Developer/Xcode/UserData/Provisioning Profiles')


class ProfileTransaction:
    """Write-ahead journal supports recovery after a terminated previous run."""
    def __init__(self, config, runner):
        self.config, self.runner = config, runner
        self.journal = config['work_dir'] / 'profile-transaction.json'

    def restore(self):
        if not self.journal.exists():
            return
        state = load_json(self.journal)
        require(state.get('bundle_id') == self.config['bundle_id'] and state.get('team_id') == self.config['team_id'], 'profile_recovery_identity_mismatch')
        entries = state.get('entries')
        require(isinstance(entries, list), 'invalid_profile_recovery_journal')
        for entry in entries:
            source, backup = Path(entry['source']), Path(entry['backup'])
            require(source.parent in profile_directories() and source.suffix in ('.mobileprovision', '.provisionprofile'), 'unsafe_profile_recovery_source')
            require(backup.is_relative_to(self.config['work_dir']) and not backup.is_symlink(), 'unsafe_profile_recovery_backup')
            if not backup.exists():
                continue  # Crash before the backup was completed; source not removed.
            require(sha256(backup) == entry['sha256'], 'profile_backup_hash_mismatch')
            require(profile_matches(decode_profile(self.runner, backup), self.config), 'profile_backup_identity_mismatch')
            if source.exists() and sha256(source) == entry['sha256']:
                continue
            # Do not overwrite a concurrently written profile, even at the old name.
            target = source if not source.exists() else source.with_name(source.stem + '-restored-' + uuid.uuid4().hex + source.suffix)
            source.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(backup, target)
        self.journal.unlink()

    def begin(self):
        entries = []
        backup_dir = self.config['work_dir'] / 'profile-backups' / uuid.uuid4().hex
        for directory in profile_directories():
            if not directory.exists():
                continue
            for source in sorted(directory.iterdir()):
                if source.suffix not in ('.mobileprovision', '.provisionprofile') or not source.is_file() or source.is_symlink():
                    continue
                try:
                    profile = decode_profile(self.runner, source)
                except RenewalError:
                    continue  # An undecodable file is never touched.
                if profile_matches(profile, self.config):
                    entries.append(dict(source=str(source), backup=str(backup_dir / (str(len(entries)) + source.suffix)), sha256=sha256(source)))
        backup_dir.mkdir(parents=True, exist_ok=False, mode=0o700)
        atomic_json(self.journal, dict(bundle_id=self.config['bundle_id'], team_id=self.config['team_id'], entries=entries))
        for entry in entries:
            source, backup = Path(entry['source']), Path(entry['backup'])
            temporary = backup.with_suffix('.copying')
            shutil.copy2(source, temporary)
            require(sha256(temporary) == entry['sha256'], 'profile_changed_during_backup')
            os.replace(temporary, backup)
            require(sha256(source) == entry['sha256'], 'profile_changed_before_removal')
            source.unlink()

    def commit(self):
        # Retain backups; do not restore stale cached profiles after a verified build.
        self.journal.unlink()


def ensure_device(config, runner, executable):
    """Fail closed on unavailable, locked-before-first-unlock, or active game."""
    try:
        listing = runner.device(['list', 'devices'], 'device-list')
        devices = listing.get('devices')
        require(isinstance(devices, list), 'invalid_device_list')
        match = [item for item in devices if item.get('identifier') == config['core_device_id'] and
                 item.get('hardwareProperties', {}).get('udid') == config['device_id']]
        require(len(match) == 1, 'target_not_discovered')
        details = runner.device(['device', 'info', 'details', '--device', config['device_id']], 'device-details')
        require(details.get('identifier') == config['core_device_id'] and
                details.get('hardwareProperties', {}).get('udid') == config['device_id'], 'device_identity_mismatch')
        require(details.get('connectionProperties', {}).get('pairingState') == 'paired', 'device_not_paired')
        require(details.get('connectionProperties', {}).get('tunnelState') == 'connected', 'device_not_connected')
        require(details.get('deviceProperties', {}).get('developerModeStatus') == 'enabled', 'developer_mode_unavailable')
        lock = runner.device(['device', 'info', 'lockState', '--device', config['device_id']], 'device-lock')
        require(lock.get('deviceIdentifier') == config['core_device_id'], 'lock_device_mismatch')
        require(lock.get('unlockedSinceBoot') is True and isinstance(lock.get('passcodeRequired'), bool), 'device_unlock_state_unknown_or_first_unlock_required')
        processes = runner.device(['device', 'info', 'processes', '--device', config['device_id']], 'device-processes')
        require(processes.get('deviceIdentifier') == config['core_device_id'], 'process_device_mismatch')
        require(isinstance(processes.get('runningProcesses'), list), 'process_state_unknown')
        expected_suffix = '/' + config['app_name'] + '.app/' + executable
        for process in processes['runningProcesses']:
            url = process.get('executable')
            require(isinstance(url, str), 'process_executable_unknown')
            if urllib.parse.unquote(urllib.parse.urlparse(url).path).endswith(expected_suffix) and not lock['passcodeRequired']:
                raise Deferred('game_running_while_device_unlocked')
    except RenewalError as error:
        raise Deferred(str(error)) from None


def ensure_installed_app(config, runner, installed):
    """Never replace a missing app or a manually installed different version.

    Version fields cannot detect different binaries sharing the same build number.
    Manual releases must advance their build number and refresh this snapshot.
    """
    try:
        result = runner.device(['device', 'info', 'apps', '--device', config['device_id'],
                                '--bundle-id', config['bundle_id']], 'installed-app')
        require(result.get('deviceIdentifier') == config['core_device_id'], 'installed_app_device_mismatch')
        require(result.get('matchingBundleIdentifier') == config['bundle_id'], 'installed_app_query_mismatch')
        apps = result.get('apps')
        require(isinstance(apps, list) and len(apps) == 1 and isinstance(apps[0], dict),
                'installed_app_missing_or_ambiguous')
        app = apps[0]
        require(app.get('bundleIdentifier') == config['bundle_id'], 'installed_app_bundle_mismatch')
        require(all(isinstance(app.get(key), str) and app[key] for key in ('version', 'bundleVersion')),
                'installed_app_version_unknown')
        require(app['version'] == installed['app_version'] and app['bundleVersion'] == installed['app_build'],
                'installed_app_version_changed')
    except RenewalError as error:
        raise Deferred(str(error)) from None


def extract_snapshot(config, destination):
    archive = config['xcode_archive']
    require(archive.is_file() and sha256(archive) == config['archive_sha256'], 'archive_hash_mismatch')
    destination.mkdir(parents=True, exist_ok=False, mode=0o700)
    mark_generated(config, destination, 'export')
    with zipfile.ZipFile(archive) as bundle:
        paths = set()
        for item in bundle.infolist():
            path = PurePosixPath(item.filename)
            require(not path.is_absolute() and path.parts and path.parts[0] == 'xcode' and
                    '..' not in path.parts and '\\' not in item.filename, 'unsafe_archive_path')
            require(item.filename not in paths, 'duplicate_archive_path')
            paths.add(item.filename)
            mode = item.external_attr >> 16
            require(not stat.S_ISLNK(mode) and (stat.S_IFMT(mode) in (0, stat.S_IFREG, stat.S_IFDIR)), 'unsupported_archive_link_or_type')
        for item in bundle.infolist():
            path = destination / item.filename
            if item.is_dir():
                path.mkdir(parents=True, exist_ok=True)
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                with bundle.open(item) as source, path.open('xb') as target:
                    shutil.copyfileobj(source, target)
                os.chmod(path, 0o755 if (item.external_attr >> 16) & 0o111 else 0o644)
    project = destination / 'xcode/Unity-iPhone.xcodeproj'
    require((project / 'project.pbxproj').is_file(), 'snapshot_xcode_project_missing')
    return project


def mark_generated(config, path, kind):
    atomic_json(path / OWNER_FILE, dict(bundle_id=config['bundle_id'], team_id=config['team_id'],
                                      run_id=path.name, kind=kind))


def cleanup_generated(config, path, kind):
    """Delete only a marked, direct child of this executor's dedicated root."""
    root = config['work_dir'] / 'exports' if kind == 'export' else config['derived_data']
    if not path.exists():
        return
    expected = dict(bundle_id=config['bundle_id'], team_id=config['team_id'], run_id=path.name, kind=kind)
    try:
        if (path.parent != root or not re.fullmatch(RUN_ID, path.name) or path.is_symlink() or
                (path / OWNER_FILE).is_symlink() or load_json(path / OWNER_FILE) != expected):
            return
        shutil.rmtree(path)
    except (OSError, RenewalError):
        # Cleanup cannot turn an already successful installation into a failure.
        config['_cleanup_warning'] = True


def prune_old_builds(config, ledger):
    root = config['derived_data']
    if not root.exists():
        return
    keep = [Path(record['app_path']).resolve() for record in (ledger['installed'], ledger.get('pending'))
            if record and record.get('app_path')]
    try:
        children = list(root.iterdir())
    except OSError:
        config['_cleanup_warning'] = True
        return
    for child in children:
        if not any(app.is_relative_to(child.resolve()) for app in keep):
            cleanup_generated(config, child, 'derived')


def read_plist(path):
    try:
        with path.open('rb') as stream:
            value = plistlib.load(stream)
    except (OSError, ValueError, plistlib.InvalidFileException):
        raise RenewalError('unreadable_app_plist') from None
    require(isinstance(value, dict), 'invalid_app_plist')
    return value


def app_binary(bundle, info):
    executable = info.get('CFBundleExecutable')
    require(isinstance(executable, str) and Path(executable).name == executable and executable not in ('', '.', '..'), 'invalid_app_executable')
    path = bundle / executable
    require(path.is_file(), 'missing_app_executable')
    return path


def verify_app(config, ledger, runner, app, minimum_hours=None):
    require(app.is_dir() and app.name == config['app_name'] + '.app', 'signed_app_missing')
    info, old = read_plist(app / 'Info.plist'), ledger['installed']
    require(info.get('CFBundleIdentifier') == config['bundle_id'], 'app_bundle_mismatch')
    require(info.get('CFBundleShortVersionString') == old['app_version'] and str(info.get('CFBundleVersion')) == old['app_build'], 'app_version_changed')
    require(info.get('CFBundleExecutable') == old['app_executable'], 'app_executable_changed')
    require(info.get('DTPlatformName') == 'iphoneos' and 'iPhoneOS' in info.get('CFBundleSupportedPlatforms', []), 'app_not_iphoneos')
    require(set(info.get('UIDeviceFamily', [])) == {1, 2}, 'app_device_families_changed')
    binaries = [app_binary(app, info)]
    frameworks = list((app / 'Frameworks').rglob('*.framework'))
    require(any(item.name == 'UnityFramework.framework' for item in frameworks), 'unity_framework_missing')
    binaries.extend(app_binary(item, read_plist(item / 'Info.plist')) for item in frameworks)
    binaries.extend((app / 'Frameworks').rglob('*.dylib'))
    for binary in binaries:
        require('arm64' in runner.run(['/usr/bin/lipo', '-archs', str(binary)], 'verify-arm64', capture=True).decode().split(), 'app_missing_arm64')
    runner.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], 'verify-codesign')
    profile = decode_profile(runner, app / 'embedded.mobileprovision')
    require(profile_matches(profile, config), 'profile_identity_mismatch')
    require(profile.get('LocalProvision') is True, 'profile_not_personal_team')
    require(config['device_id'] in profile.get('ProvisionedDevices', []), 'profile_device_missing')
    expires, created = date(profile.get('ExpirationDate')), date(profile.get('CreationDate'))
    require(created <= now(), 'profile_not_yet_valid')
    if expires <= date(old['profile_expiration']):
        raise StaleCandidate('profile_expiration_did_not_increase')
    minimum_hours = config['minimum_new_validity_hours'] if minimum_hours is None else minimum_hours
    if (expires - now()).total_seconds() < minimum_hours * 3600:
        raise StaleCandidate('new_profile_validity_too_short')
    entitlements = plistlib.loads(runner.run(['/usr/bin/codesign', '-d', '--entitlements', ':-', str(app)], 'read-entitlements', capture=True))
    require(entitlements.get('application-identifier') == config['team_id'] + '.' + config['bundle_id'], 'signed_app_identifier_mismatch')
    require(entitlements.get('com.apple.developer.team-identifier') == config['team_id'], 'signed_team_mismatch')
    require(not entitlements.get('get-task-allow', False) or profile['Entitlements'].get('get-task-allow') is True, 'debug_entitlement_not_allowed')
    with tempfile.TemporaryDirectory(prefix='leaf-', dir=runner.directory) as temporary:
        prefix = Path(temporary) / 'certificate'
        runner.run(['/usr/bin/codesign', '-d', '--extract-certificates=' + str(prefix), str(app)], 'extract-signing-leaf')
        leaf = Path(str(prefix) + '0').read_bytes()
        require(leaf in profile.get('DeveloperCertificates', []), 'signing_leaf_not_allowed_by_profile')
        fingerprint = hashlib.sha256(leaf).hexdigest()
        require(fingerprint == old['signing_certificate_sha256'], 'signing_identity_changed')
        requirement = '=certificate leaf = H"' + hashlib.sha1(leaf).hexdigest() + '"'
        runner.run(['/usr/bin/codesign', '--verify', '--strict', '-R', requirement, str(app)], 'verify-signing-leaf')
    return dict(bundle_id=config['bundle_id'], team_id=config['team_id'], device_id=config['device_id'],
                app_version=old['app_version'], app_build=old['app_build'], app_executable=old['app_executable'],
                profile_expiration=iso(expires), profile_uuid=profile.get('UUID'),
                signing_certificate_sha256=fingerprint, archive_sha256=config['archive_sha256'],
                app_path=str(app), verified_at=iso(now()))


def build_candidate(config, ledger, runner):
    run_id = now().strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex[:12]
    export = config['work_dir'] / 'exports' / run_id
    # A replaced profile can invalidate an incremental build without triggering
    # CodeSign. A fresh DerivedData directory forces packaging and signing.
    derived = config['derived_data'] / run_id
    require(not derived.exists(), 'derived_data_already_exists')
    transaction = ProfileTransaction(config, runner)
    try:
        project = extract_snapshot(config, export)
        derived.mkdir(parents=True, exist_ok=False, mode=0o700)
        mark_generated(config, derived, 'derived')
        transaction.begin()
        runner.run(['/usr/bin/xcodebuild', '-project', str(project), '-scheme', 'Unity-iPhone',
                    '-configuration', 'Release', '-sdk', 'iphoneos', '-destination', 'id=' + config['device_id'],
                    '-derivedDataPath', str(derived), '-allowProvisioningUpdates',
                    'DEVELOPMENT_TEAM=' + config['team_id'], 'CODE_SIGN_STYLE=Automatic',
                    'ARCHS=arm64', 'ONLY_ACTIVE_ARCH=NO', 'build'], 'xcodebuild', timeout=2400)
        record = verify_app(config, ledger, runner, derived / 'Build/Products/Release-iphoneos' / (config['app_name'] + '.app'))
        # Persist pending before ending the transaction, so a later install failure
        # never loses the newer signed app or advances installed prematurely.
        ledger['pending'] = record
        atomic_json(config['ledger_path'], ledger)
        transaction.commit()
        return record
    except BaseException:
        transaction.restore()
        raise
    finally:
        cleanup_generated(config, export, 'export')
        # Persisted pending, rather than an in-memory assignment, owns the app.
        try:
            pending = load_json(config['ledger_path']).get('pending')
            if not pending or not Path(pending.get('app_path', '')).resolve().is_relative_to(derived):
                cleanup_generated(config, derived, 'derived')
        except RenewalError:
            config['_cleanup_warning'] = True


def execute(config, ledger, runner, force=False):
    transaction = ProfileTransaction(config, runner)
    transaction.restore()
    remaining = (date(ledger['installed']['profile_expiration']) - now()).total_seconds() / 3600
    if not force and ledger.get('pending') is None and remaining > config['renew_before_hours']:
        return report('not_due', ledger)
    ensure_device(config, runner, ledger['installed']['app_executable'])
    ensure_installed_app(config, runner, ledger['installed'])
    record = ledger.get('pending')
    if record:
        path = Path(record.get('app_path', '')).resolve()
        require(path.is_relative_to(config['derived_data']) or path.is_relative_to(config['work_dir']), 'pending_app_outside_work_area')
        require(record.get('archive_sha256') == config['archive_sha256'], 'pending_snapshot_mismatch')
        try:
            # A good pending package may have waited days for the phone. Reuse it
            # while at least 24 hours remain; fresh builds still require 120 hours.
            record = verify_app(config, ledger, runner, path, minimum_hours=24)
        except StaleCandidate:
            ledger['pending'] = None
            atomic_json(config['ledger_path'], ledger)
            record = None
    if record is None:
        record = build_candidate(config, ledger, runner)
    # The phone may have disappeared, locked, or started the game while building.
    ensure_device(config, runner, ledger['installed']['app_executable'])
    ensure_installed_app(config, runner, ledger['installed'])
    try:
        result = runner.device(['device', 'install', 'app', '--device', config['device_id'], record['app_path']], 'device-install', timeout=240)
        require(result.get('deviceIdentifier') == config['core_device_id'], 'install_device_mismatch')
        installed = result.get('installedApplications')
        require(isinstance(installed, list) and len(installed) == 1 and installed[0].get('bundleID') == config['bundle_id'], 'install_confirmation_missing')
    except RenewalError as error:
        raise Deferred('pending_install_' + str(error)) from None
    ledger['installed'] = dict(record, installed_at=iso(now()))
    ledger['pending'] = None
    atomic_json(config['ledger_path'], ledger)
    prune_old_builds(config, ledger)
    return report('renewed', ledger, log_dir=str(runner.directory))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True, type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--status', action='store_true')
    mode.add_argument('--check', action='store_true')
    mode.add_argument('--force', action='store_true')
    args = parser.parse_args(argv)
    ledger, runner, lock, config = None, None, None, None
    try:
        config = configuration(args.config)
        ledger = read_ledger(config)
        remaining = (date(ledger['installed']['profile_expiration']) - now()).total_seconds() / 3600
        if args.status:
            result = report('healthy' if remaining > config['renew_before_hours'] and ledger.get('pending') is None else 'deferred', ledger,
                            reason=None if remaining > config['renew_before_hours'] and ledger.get('pending') is None else 'renewal_due_or_pending')
        else:
            config['work_dir'].mkdir(parents=True, exist_ok=True, mode=0o700)
            lock = (config['work_dir'] / 'renewal.lock').open('a+')
            try:
                fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Deferred('another_renewal_is_running') from None
            # Re-read after locking: another run may have installed since startup.
            ledger = read_ledger(config)
            if (not args.force and ledger.get('pending') is None and
                    (date(ledger['installed']['profile_expiration']) - now()).total_seconds() > config['renew_before_hours'] * 3600 and
                    not (config['work_dir'] / 'profile-transaction.json').exists()):
                result = report('not_due', ledger)
            else:
                run_id = now().strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex[:12]
                runner = Runner(config['log_dir'] / run_id)
                result = execute(config, ledger, runner, args.force)
    except Deferred as error:
        result = report('deferred', ledger, str(error))
    except RenewalError as error:
        result = report('failed', ledger, str(error))
    except (Exception, KeyboardInterrupt) as error:
        result = report('failed', ledger, 'unexpected_' + type(error).__name__)
    finally:
        if lock is not None:
            lock.close()
    if runner is not None:
        result['log_dir'] = str(runner.directory)
    if config and config.get('_cleanup_warning'):
        result['cleanup_warning'] = True
    print(json.dumps(result, sort_keys=True))
    return 1 if result['status'] == 'failed' else 0


if __name__ == '__main__':
    os.umask(0o077)
    # Convert cancellation into normal unwinding so profile rollback can finish.
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    signal.signal(signal.SIGTERM, interrupted)
    sys.exit(main())
