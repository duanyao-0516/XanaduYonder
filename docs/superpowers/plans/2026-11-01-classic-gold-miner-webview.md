# Classic Gold Miner Implementation Plan

> **For agentic workers:** Use subagent-driven-development with disjoint file ownership and spec/code review. User has approved execution in this chat; continue without another handoff question.

**Goal:** Add an offline system-WebView classic Gold Miner lobby item with solo/duo touch play, independent complete save/resume, and two-Back exit.

**Architecture:** Unity launches a dedicated Android Activity. A strict local HTTPS asset origin serves the two original game adaptations and shared host UI. Each adapter serializes its own runtime state; native atomic files persist it without touching Unity Gold Miner.

**Tech Stack:** Unity C#, Android Java/system WebView, Phaser 2.4.6/3.55.2 JavaScript, Python deterministic AAR builder.

## Tasks and ownership

- [x] Shared host and Unity entry (root): `ThirdParty/ClassicGoldMinerContent/web/{index.html,shared/*}`, `Assets/Scripts/Runtime/AppController.ClassicGoldMiner.cs`, `ClassicGoldMiner/ClassicGoldMinerLauncher.cs`, bounded AppController registration/Busy changes. Implement the shared interface in the approved design; first exercise missing adapter/save failures, then round-trip and mode isolation using temporary validation under artifacts.
- [x] Solo adapter (solo implementer): own only `ThirdParty/ClassicGoldMinerContent/web/solo/`. Copy verified original game/resources; remove analytics/sponsor/remote references; preserve preloading; expose exact capture/restore; add two-tap shop and direct play controls. Exercise snapshots and shop behavior before/after change; do not edit old Unity GoldMiner.
- [x] Duo adapter (duo implementer): own only `ThirdParty/ClassicGoldMinerContent/web/duo/`. Copy verified package; implement full state reconstruction and pointer-owned controls; reserve explosive actions synchronously to avoid repeated delayed consumption; two-tap shop. Validate independent simultaneous input and same-run restore.
- [x] Native host (Android implementer): own `ThirdParty/ClassicGoldMinerRuntime/` and `scripts/classic-gold-miner/`. Compile Java into deterministic AAR, include web assets, strict origin allowlist, AtomicFile save interface, system Back (modern/legacy), lifecycle and renderer failure recovery. Do not bundle Chromium or request INTERNET. Test double-Back interval/cancel/timeout and file mode isolation with bounded validation.
- [x] Integration (root): prepare matching local KR build inputs and verify; create Unity metadata, build gate and isolated classic validation build; preserve current formal identity/settings after build; run current relevant lobby tests and Java/JS syntax/content gates.
- [x] Independent reviews: verify user requirements, data contracts, pause/exit races, network closure and touch behavior; resolve concrete findings before delivery.
- [x] Independent emulator validation (root only): build isolated APK, verify archive/permissions, install alongside formal app, run solo+duo save/exit/restart and shop interactions on the task-specific API 36 emulator (physical phone reserved for another chat), capture screenshots/logs. Report exact covered scenarios and remaining limitations.

## Shared calls

The signatures and navigation query meanings in the approved design are the contract. Android bridge class is `com.xanaduyonder.classicgoldminer.ClassicGoldMinerBridge`, with `open(Activity, boolean sound, boolean haptics)`, `isRunning()`, `getLastError()`. The AAR is `Assets/Plugins/Android/ClassicGoldMiner.aar`; assets root inside it is `assets/classic-gold-miner/`. All game content is tracked under `ThirdParty/ClassicGoldMinerContent/web/`, so clean checkouts build without ignored download folders.

## Validation commands

```sh
python3 scripts/classic-gold-miner/build-runtime.py
python3 scripts/classic-gold-miner/verify-content.py
git diff --check
```

Use JavaScript parse checks and browser/runtime snapshot scenarios for game logic; source-string matching alone is not gameplay verification. Use the existing Unity Android builder with an isolated package ID via a scoped editor method; never install over the user's formal package or delete its data. Do not commit, push, merge or release without a user request.

## Fullscreen revision after emulator feedback

- [x] Native/root: draw WebView edge-to-edge and publish current cutout/gesture insets to CSS; menus/HUD/overlays honor these insets while backgrounds fill the screen.
- [x] Solo: remove the persistent control row; full-viewport mine, proportional wide layout, first mine touch drops, floating right bomb consumes only its own action. Preserve old preview saves and remap all coordinate/tween/rope state on width changes.
- [x] Duo: remove header/side controls during play; full-viewport mine, stable left/right pointer ownership on first touch, two corner bombs for corresponding miners, shared inventory reservation remains atomic. Preserve old preview saves and remap all dependent runtime coordinates.
- [x] Shops: enlarge non-overlapping sprite plus price targets and Next Level, recompute after resize; keep first-select/second-buy behavior.
- [x] Independent review and regression: collision/geometry/save contracts, input fall-through and two-pointer ownership; rerun affected suites and rebuild the AAR/APK.
- [x] Final emulator: verify fullscreen screenshots, both mine actions/bombs, shop taps, save/resume and two-Back on the rebuilt APK. Update hashes and documented coverage. Paused resize also passed changed/same-aspect shrink and growth; original user preview saves and display size were restored.
