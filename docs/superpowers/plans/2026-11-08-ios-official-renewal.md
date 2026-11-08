# Official iOS Renewal Implementation Plan

**Goal:** Automatically refresh this Mac's existing Personal Team installation on the paired iPhone, using Xcode and Apple device/signing tools only.

**Architecture:** A local Codex recurring task runs a bounded renewal command every six hours. The command uses an isolated snapshot of the accepted Xcode export, tracks the last successfully installed profile expiry, and attempts renewal within 72 hours of expiry. Unreachable devices are deferred; only a verified newer signed package is installed over the same app. It does not launch the game, change saved preferences, revoke certificates, or install third-party signing software.

**Tech Stack:** Xcode `xcodebuild`, `security`, `codesign`, `xcrun devicectl`, system Python, local Codex automation.

- [x] Verify Apple's documented local provisioning-profile refresh procedure against the current bundle. Back up only profiles whose team and application identifier exactly match this app; inspect both current and legacy cache directories. Accept renewal only when the embedded profile's expiration increases and covers the intended device.
- [x] Add `scripts/renew-ios.py` with status/check/force modes, bounded commands, a process lock, atomic installation ledger, explicit deferred/failure states, and strict bundle/team/device/signature checks. Keep device/account configuration and generated artifacts out of Git. Preserve the old local profiles if provisioning fails.
- [x] Prepare an isolated export snapshot and configuration for the accepted help package. Record input hashes so later diagnostic exports or source edits cannot silently change the app delivered by the renewal task.
- [x] Verify the script's non-mutating status path and meaningful failure cases (27 temporary checks), then perform one real official renewal and same-app install over USB while locked. Profile expiry advanced to 2026-11-15T07:29:15Z with the same certificate; all 29 saved files remained byte-identical. A subsequent check returned not_due.
- [x] Inspect wireless connectivity using Xcode's supported device pairing flow. Current LAN can ping the phone but CoreDevice wireless services remain unavailable; the script correctly defers. Wireless installation is not verified, and the real renewal trial uses USB.
- [x] Register one local Codex automation every six hours and re-read its persisted configuration: heartbeat `iphone`, ACTIVE, pinned executor hash, failed-runs-only notifications. Document runtime/network requirements and expiry limitations. Commit the scripts/docs locally without pushing.

## Acceptance and recovery

- Rebuilding with the same profile is not renewal. A later `ExpirationDate`, valid code signature, matching app/team/device, and successful install are all required before updating the installed-expiry ledger.
- When the iPhone is away, do not replace the installed-expiry ledger with a build-only result. Retry later and report when the remaining installed validity is critically low.
- The app continues to work until its installed signing authorization expires, subject to iOS trust verification. If it expires while unreachable, reconnect and reinstall over the existing app; do not uninstall it.
- Xcode account reauthentication, an unavailable signing identity, device unlock requirements, an offline/asleep Mac, and unavailable Apple services may require user intervention. The workflow is not an unconditional guarantee of uninterrupted availability.
