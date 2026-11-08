# Shared Android and iOS build entry

User approved a shared master branch, one build entry, necessary repository organization, Android device verification, and commit/push without another confirmation. Continue in the existing iOS feature checkout; do not create a permanent platform branch or rewrite history.

## Public contract

```bash
./scripts/build.sh android
./scripts/build.sh android --configuration release
./scripts/build.sh android --configuration release --install DEVICE_SERIAL
./scripts/build.sh ios
```

Android defaults to the existing isolated Debug package. Release retains the original app identity and signing. iOS exports the filtered Xcode project; signing/install and the already deployed renewal snapshot remain separate. Explicit platform selection determines content, independent of the connected device. Invalid/duplicate options fail before accessing Unity, signing credentials, or devices. Existing three build scripts remain compatible delegates; per-game validation scripts retain their existing interfaces.

## Scope and steps

- [x] Confirm clean feature checkout, fetch origin/master, verify ancestry and the connected physical Android device. Origin/master is still 1171f57; the iOS branch contains five local commits above it. Device is OPPO PKM110, Android 16, ARM64, with the existing 0.9.5/build14 installation.
- [x] Add the dispatcher and scripts/build backend directory; share pinned Unity/root/module discovery without changing build, signing, freshness, or install semantics. Run shell syntax and temporary fixture routing/error/exit-propagation checks. Parent spec review precedes independent quality review.
- [x] Group the two iOS-only C# bridges under Runtime/Platform/iOS, preserving file bytes, namespaces, and .meta GUIDs. Do not move shared gameplay, save formats, or the broadly referenced ProjectBuild.cs.
- [x] Update README, development and architecture entry docs; add a documentation index and current platform/build reference. Preserve historical release/validation evidence and links rather than bulk moving old documents.
- [x] Back up the original Android app and build Release. Its installed debug certificate differs from the release certificate, so preserve the original app and install the isolated `.debug` package through the unified entry instead. Verified startup, 25-entry lobby rendering/scrolling, help, setting persistence after process restart, and four Android-only entry screens. All 18 original external Unity save files remain byte-identical. The user then explicitly stopped further diagnosis/regression; remaining game interaction checks are intentionally not run and are documented in `docs/verification-platform-integration.md`.
- [x] Run a real iOS export through the new entry to prove the filtered copy, iOS C# move and packed-resource exclusion checks; the 19-entry scope is source-verified; do not publish a new signing snapshot or install over the accepted iPhone app just for source organization.
- [x] Resolve evidence-backed review issues and document exact validation and remaining limits. Independent review found no remaining blocking issues.

Delivery follows this change: commit the intended files, fetch again, integrate master without force, push master, and verify remote SHA plus a clean worktree. No unrelated historical research/diagnostic branches are included. Git history and remote refs record the delivery outcome.

## Repository boundaries

Shared rules/persistence stay in Core; shared UI/lifecycle remain in Runtime. Platform-only managed bridge code belongs in Runtime/Platform, native iOS sources remain Plugins/iOS, and Android-specific containers keep their existing ownership. Scripts/build contains backend mechanics; build.sh is the supported user entry. Actual credentials, launchd configuration, provisioning profiles, device data and generated packages remain local and ignored.
