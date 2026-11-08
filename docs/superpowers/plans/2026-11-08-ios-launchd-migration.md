# Native macOS iOS renewal schedule

User request: replace the Codex schedule with a Mac scheduled script that runs independently of Codex. The accepted renewal executor, frozen app, signature identity, expiry thresholds, and same-app installation behavior remain unchanged.

Use a current-user LaunchAgent in the Aqua login session, not a root daemon. Run at 00:15, 06:15, 12:15, and 18:15 local time with StartCalendarInterval, plus RunAtLoad to check after login. macOS coalesces missed sleeping intervals at wake; no power-management settings are changed. A short-lived caffeinate assertion prevents idle sleep only while the command is running.

Place the verified executor, wrapper, frozen ZIP, configuration, ledger, and future outputs in ~/Library/Application Support/XanaduYonder/ios-renewal. This removes the Desktop checkout dependency and uses the normal user account/keychain session. Absolute plist paths, a fixed Xcode toolchain, and a minimal PATH remove interactive-shell dependencies. The wrapper verifies the executor SHA-256 before execution.

- [x] Pause and verify the existing Codex heartbeat, preserving its configuration.
- [x] Deploy a hash-checked wrapper and copy the frozen input/state under an exclusive renewal lock. Refuse pending transactions. Keep migration backups and point the legacy config to the same canonical ledger to prevent divergent state.
- [x] Install and bootstrap the current-user LaunchAgent; verify an actual launchd --check execution and its exit status. The deployed launcher explicitly uses /bin/zsh -f.
- [x] Run one temporary unscheduled launchd --force verification to test real background Xcode authentication, signing, USB installation, and data preservation. Completed at 2026-11-08T07:46:19Z, with expiry 2026-11-15T07:39:32Z, same signing identity, and 29 byte-identical saved files. Removed the temporary job; the permanent job always uses --check.
- [x] Verify the four daily schedule entries, final config, normal not-due check (exit 0), and paused Codex heartbeat. Update the runbook and commit locally, without pushing.

The Mac must be logged in and able to run; phone reachability, Apple network access, and a valid Xcode login/keychain remain required. Closing Codex or Xcode's UI does not remove this schedule. Wireless installation remains unverified on the current network. Logs replace the former Codex task notification channel; no new notification app or privacy permission is required.
