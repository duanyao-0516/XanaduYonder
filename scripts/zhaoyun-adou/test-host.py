#!/usr/bin/env python3
"""Run pure Java host boundary tests; no Android device or game process required."""
from pathlib import Path
import os
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ThirdParty/ZhaoyunAdouRuntime'
JAVA = Path(os.environ.get('UNITY_ANDROID_PLAYER', '/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer')) / 'OpenJDK/bin'

class HostBoundaryTests(unittest.TestCase):
    def test_actual_status_poll_selects_recovery_from_current_readiness(self):
        # Execute the production poll method with small Android/UI boundary stand-ins.
        # This checks the real branch order and chosen retry action, not a copied policy.
        activity = (SOURCE / 'src/com/xanaduyonder/zhaoyunadou/ZhaoyunAdouActivity.java').read_text()
        start = activity.index('    private void pollStatus() {')
        end = activity.index('    private void updateStartupWatchdog()', start)
        poll = activity[start:end]
        lifecycle_start = activity.index('    private void reconcileLifecycle() {')
        lifecycle_end = activity.index('    private void retrySaveAndResume()', lifecycle_start)
        lifecycle = activity[lifecycle_start:lifecycle_end]
        harness = r'''
package com.xanaduyonder.zhaoyunadou;
public final class StatusRecoveryHarness {
    boolean destroyed, exiting, nativeFrozen, ready;
    boolean configured = true;
    boolean resumed = true, focused = true, lifecyclePending, pendingActive;
    Boolean appliedActive;
    Object web = new Object(), failurePanel;
    CallbackEpoch statusEpoch = new CallbackEpoch();
    CallbackEpoch lifecycleEpoch = new CallbackEpoch();
    ForegroundBudget startupBudget = new ForegroundBudget(60000);
    Runnable startupWatchdog = () -> { throw new AssertionError("Unexpected startup timeout"); };
    Runnable statusPoll = () -> { };
    Handler handler = new Handler();
    static final class Handler { void removeCallbacks(Runnable ignored) { } }
    Runnable retry;
    boolean mayStay, mayReturn;
    int reloads, saves, frozen, statusCalls;
    long nextPoll;
    Status result;
    static final class SystemClock { static long elapsedRealtime() { return 0; } }
    static final class Status {
        final Boolean ready; final String error;
        Status(Boolean ready, String error) { this.ready = ready; this.error = error; }
        boolean isNull(String key) { return error == null; }
        String optString(String key, String fallback) { return error == null ? fallback : error; }
        boolean optBoolean(String key, boolean fallback) {
            return "paused".equals(key) || (ready == null ? fallback : ready);
        }
    }
    interface Callback { void complete(Status value); }
    void callApi(String method, String argument, CallbackEpoch epoch, long generation, Callback callback) {
        if ("setActive".equals(method)) { callback.complete(new Status(null, "Initial reward save failed after configure")); return; }
        if (!"status".equals(method)) throw new AssertionError("Unexpected API: " + method);
        statusCalls++;
        callback.complete(result);
    }
    static boolean ok(Status value) { return value.error == null; }
    static String error(Status value) { return value.error; }
    void showFailure(String title, String message, Runnable action, boolean stay, boolean leave) {
        retry = action; mayStay = stay; mayReturn = leave;
    }
    void retrySaveAndResume() { saves++; }
    void reloadGame() { reloads++; }
    void updateStartupWatchdog() { }
    void scheduleStatus(long delay) { nextPoll = delay; }
    void freezeAcknowledgedPause(Status ignored) { frozen++; }
    void thaw() { }
    void freezeWebView() { frozen++; }
__POLL__
__LIFECYCLE__
    static void check(boolean condition, String message) { if (!condition) throw new AssertionError(message); }
    static void errorCase(boolean cachedReady, Boolean pageReady, boolean expectReload) {
        StatusRecoveryHarness host = new StatusRecoveryHarness();
        host.ready = cachedReady;
        host.result = new Status(pageReady, "Initial reward transaction was not saved");
        host.pollStatus();
        check(host.retry != null, "Error must offer a retry");
        host.retry.run();
        check(host.reloads == (expectReload ? 1 : 0) && host.saves == (expectReload ? 0 : 1),
                "Current page ready=" + pageReady + " cached=" + cachedReady + " must " + (expectReload ? "reload initialization" : "retry save"));
        check(host.ready == Boolean.TRUE.equals(pageReady), "Ready cache must follow this status, even on error");
        check(host.mayStay == !expectReload && host.mayReturn == expectReload, "Recovery buttons must match initialization state");
        check(host.frozen == 0, "Ordinary status.paused cannot acknowledge host pause/mute");
    }
    static void lifecycleErrorCase(boolean cachedReady, Boolean pageReady, boolean expectReload) {
        StatusRecoveryHarness host = new StatusRecoveryHarness();
        host.ready = cachedReady;
        host.result = new Status(pageReady, "Initial reward transaction was not saved");
        host.reconcileLifecycle();
        check(host.statusCalls == 1, "setActive failure must query actual readiness before recovery");
        check(host.retry != null && !host.lifecyclePending, "Failed lifecycle acknowledgement offers a settled recovery");
        host.retry.run();
        check(host.reloads == (expectReload ? 1 : 0) && host.saves == (expectReload ? 0 : 1), "setActive error must choose recovery from actual ready=" + pageReady);
        check(host.ready == Boolean.TRUE.equals(pageReady), "setActive failure updates cached readiness");
        check(host.mayStay == !expectReload && host.mayReturn == expectReload, "Lifecycle recovery buttons match readiness");
    }
    public static void main(String[] args) {
        errorCase(false, false, true);
        errorCase(false, true, false);
        errorCase(true, false, true);
        errorCase(true, null, true);
        lifecycleErrorCase(false, false, true);
        lifecycleErrorCase(false, true, false);
        lifecycleErrorCase(true, false, true);
        lifecycleErrorCase(true, null, true);
        for (boolean pageReady : new boolean[]{false, true}) {
            StatusRecoveryHarness host = new StatusRecoveryHarness();
            host.result = new Status(pageReady, null);
            host.pollStatus();
            check(host.ready == pageReady && host.retry == null, "Successful status retains the correct readiness");
            check(host.nextPoll == (pageReady ? 1000 : 250), "Correct polling interval after status");
        }
        System.out.println("HOST_STATUS_RECOVERY_PASS: 10 scenarios");
    }
}
'''.replace('__POLL__', poll).replace('__LIFECYCLE__', lifecycle)
        with tempfile.TemporaryDirectory(prefix='zhaoyun-status-tests-') as directory:
            source = Path(directory) / 'StatusRecoveryHarness.java'
            source.write_text(harness)
            dependencies = [SOURCE / 'src/com/xanaduyonder/zhaoyunadou' / name
                            for name in ['CallbackEpoch.java', 'ForegroundBudget.java']]
            subprocess.run([str(JAVA / 'javac'), '-encoding', 'UTF-8', '-source', '8', '-target', '8', '-Xlint:-options',
                            '-d', directory, *map(str, dependencies), str(source)], check=True)
            result = subprocess.run([str(JAVA / 'java'), '-cp', directory, 'com.xanaduyonder.zhaoyunadou.StatusRecoveryHarness'],
                                    text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn('HOST_STATUS_RECOVERY_PASS', result.stdout)
            print(result.stdout.strip())

    def test_real_java_boundaries(self):
        names = ['AssetRequestPolicy.java', 'BackPressGate.java', 'CallbackEpoch.java', 'ForegroundBudget.java']
        sources = [SOURCE / 'src/com/xanaduyonder/zhaoyunadou' / name for name in names]
        for source in sources:
            self.assertTrue(source.is_file(), 'Host boundary implementation is missing: ' + source.name)
        with tempfile.TemporaryDirectory(prefix='zhaoyun-host-tests-') as directory:
            subprocess.run([str(JAVA / 'javac'), '-encoding', 'UTF-8', '-source', '8', '-target', '8', '-Xlint:-options', '-d', directory,
                            *map(str, sources), str(SOURCE / 'tests/HostBoundaryTest.java')], check=True)
            result = subprocess.run([str(JAVA / 'java'), '-cp', directory, 'com.xanaduyonder.zhaoyunadou.HostBoundaryTest'],
                                    check=True, text=True, capture_output=True)
            self.assertIn('HOST_BOUNDARIES_PASS', result.stdout)
            print(result.stdout.strip())

if __name__ == '__main__':
    unittest.main()
