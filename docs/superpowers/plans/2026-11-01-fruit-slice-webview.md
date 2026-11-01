# Fruit Slice WebView Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement and review this plan. User approved the approach and execution on 2026-11-01; do not request approval again between tasks.

**Goal:** Add a fully offline full-screen Fruit Slice game to the Unity Android collection, with two completed system Back actions to leave and touch semantics appropriate to each screen.

**Architecture:** Unity retains the lobby beneath a dedicated FruitSliceActivity. The Activity serves bundled H5 assets from one fixed HTTPS origin, owns system Back, lifecycle, safe insets and disposal. The local Laya content owns gameplay, saves, contextual confirmation and rendering; platform SDKs and network features are removed at content build time.

**Tech Stack:** Unity C#, Android Java / system WebView, existing Laya release, Python content/build validation, real Android device.

## Confirmed behavior and interfaces

- Lobby single/double/directory views gain entry 21 in 敏捷挑战; portrait full-screen gameplay.
- First completed system Back shows 再返回一次回到大厅; second within 2 seconds finishes only FruitSliceActivity. Cancelled gestures and background transitions do not leave a pending Back confirmation.
- Shop acquisition/purchase/free unlock uses first tap to select/show details and second tap on the same target to confirm. Selection changes or leaving the page clear confirmation. Owned cosmetic selection may act immediately. Gameplay Down/Move responds immediately; no global double-tap or hover emulation.
- Revive, triple reward and formerly ad-gated cosmetic unlock are local/free actions. Keep per-action one-shot state and existing gameplay limits; do not leave SDK callback waits or fake third-party API success.
- Persist progress, diamonds, unlocked/equipped cosmetics and records. Going to the lobby ends the active round; backgrounding pauses and resumes it without elapsed wall-clock loss.
- Content origin: `https://appassets.androidplatform.net/fruit-slice/index.html`; APK assets under `fruit-slice/`.
- JS API: `window.XanaduFruitHost.setPaused(boolean)`, `setSoundEnabled(boolean)`, `cancelInput()`, `shutdown()`.
- Trusted native bridge: `window.XanaduFruitNative.ready()` and `reportError(string)`; no generic reflection/file/network API.
- Source files remain in ignored downloaded evidence. Reproducible transformation code and provenance are tracked; generated third-party content is placed in ignored `Assets/StreamingAssets/fruit-slice/`.

## Task 1 — Build baseline and behavioral checks

- [x] Restore the two validated Kingdom Rush AARs and the runtime-build report from the previously checked local worktree; run `python3 -B scripts/kingdom-rush/verify-build-input.py --root "$PWD"` in the destination.
- [x] Add focused behavioral verification for the Back two-step state machine and H5 confirmation/immediate-input rules before their implementation. Keep temporary self-verification fixtures under artifacts; retain reusable content integrity validation.

## Task 2 — Local H5 content

Files: `ThirdParty/FruitSliceContent/`, `scripts/fruit-slice/build-content.py`, `scripts/fruit-slice/verify-content.py`, generated `Assets/StreamingAssets/fruit-slice/`.

- [x] Verify the original manifest hashes; copy only required runtime assets.
- [x] Transform the actual bundle to remove platform SDKs, telemetry, external navigation, promotion and platform consent UI. Replace reward call sites with local business actions.
- [x] Implement contextual shop preview/confirmation, single-owner immediate slicing and cancellation, fixed portrait/safe viewport layout, wall-clock pause compensation, local storage validation and sound control.
- [x] Add a local-only HTML entry and the JS host API above. Validate all resource references and scan for reachable networking/platform APIs. Run content generation twice and compare deterministic output.
- [x] Restore the challenge scene's required `ui.Prefab.donghuaUI` registration after the first device build exposed its removal. Validate actual scene/inline-prefab runtime references and exercise the real Laya class lookup and main → 50-level selection → level 1 route; keep unused `handUI` excluded after the reference audit.

## Task 3 — Android host and Unity entry

Files: `Assets/Plugins/Android/FruitSlice*.java`, Android manifest, `Assets/Scripts/Runtime/FruitSlice/`, `AppController.FruitSlice.cs`, `AppController.cs`, lobby icon/logo assets.

- [x] Implement internal Activity/bridge, local HTTPS resource responses, strict external request/navigation refusal, JS/DOM storage/WebGL support, no INTERNET permission.
- [x] Implement current system Back callback with older-Android fallback, 2-second confirmation, safe insets, lifecycle calls, render failure UI, and complete teardown.
- [x] Add Unity launcher/lobby input ownership and native-game Back mutual exclusion. Add original-content-based lobby artwork for all three layouts.

## Task 4 — Build and device verification

Files: `Assets/Editor/FruitSliceAndroidBuild.cs`, `ProjectBuild.cs`, `scripts/build-fruit-slice.sh`, `docs/verification-fruit-slice.md`.

- [x] Add content preprocessing guard and isolated non-Development preview build (`com.duanyao.xanaduyonder.fruitslicetest`) without changing release identity.
- [x] Compile Java against the installed Android SDK; run focused behavior/content checks and Unity build serially.
- [x] Inspect APK content, permissions and debug flags; install the separate preview without replacing the user's production app.
- [x] Exercise final production content through CUA in the browser: main → visible level-selection first page → level 1, endless start, and timed 60-second start/countdown. The 50-level completeness result comes from content validation, not a rendered traversal. Keep browser results separate from physical-device acceptance.
- [ ] Verify real lobby entry, first-touch slicing, shop first-tap preview/second-tap action, free rewards, background timing, repeated open/close, and two completed system Back actions on the device.
- [x] Perform independent specification review, then code-quality review; resolve evidence-backed findings. Record exact completed and untested validation layers, artifact path/hash and remaining risks.

Final build status on 2026-11-01: the corrected 407-file content passes 707 asset-reference and 156 class-reference checks with 11 registered game classes and all 50 levels; six temporary VM suites pass. Content manifest SHA-256: `2e6cd014fb5eca43b0d8d15428aa915ef5213a5cd2d989c57c6d021ea33013d2`. The second Unity/Gradle build and APK verification passed; the isolated preview is 327,445,487 bytes, SHA-256 `f8b08a73682100ee6f7731da504bd2fd62d23055c6dcabd28073375babe985f0`, and was installed successfully on OPPO PKM110 at 12:29:11. The corrected app has not yet been reopened on the phone; full device acceptance remains unchecked. The initial missing-prefab incident, final evidence and remaining limits are recorded in `docs/verification-fruit-slice.md`.
