# Only One Door Android Integration Implementation Plan

> Execute in the existing `p/duanyao.0516/codex/only-one-door-package` worktree. The user approved the WebView design and confirmed two consecutive system Back gestures to return to the lobby on 2026-11-01. The earlier research document remains the content and platform audit; this plan supersedes its proposed game-internal Back routing.

**Goal:** Add 只有一道门 to the Unity lobby, opening the bundled original game in a fullscreen landscape Android WebView, with offline operation, mobile controls, reliable checkpoint persistence and double Back to exit.

**Architecture:** Unity owns the lobby and launch/busy/return boundary. A dedicated non-exported Android Activity owns WebView, local resource interception, safe area, system Back and lifecycle. A narrowly patched original JS runtime and a local adapter own SDK-free continuations, game pause/input/audio/save coordination. Original PAK and MP3 content is retained byte for byte.

**Tech stack:** Unity 6000.3.27f1 C#, Java Android API 26–36, AndroidX WebKit 1.18.0, original Haxe/OpenFL/Stencyl JavaScript, Python deterministic packaging and verification.

## 1. Verify prerequisites and preserve baseline

- [x] Confirm worktree/branch and existing changes; do not modify other worktrees.
- [x] Read global instructions, engineering guardrails and existing integration research.
- [x] Confirm exit semantics: first committed system Back shows a hint; a second within a short window returns to the lobby. Canceled predictive gestures do not count. Game touch controls do not use an arbitrary swipe recognizer.
- [x] Verify source hashes, all PAK members and 19 MP3s; identify Android toolchain and devices.
- [x] Restore existing Kingdom Rush generated build inputs from a verified local source, then run its existing validation gate; never bypass it or remove an existing game.

## 2. Package the original game and local adaptation

Files: `ThirdParty/LevelDevil/`, `scripts/level-devil/`, generated `Assets/Plugins/Android/LevelDevilContent.aar`.

- [x] Preserve provenance and a hash manifest for the original release. Build only the game JS, PAK, MP3 and local entry/adapter, excluding both Poki SDKs and research fixtures.
- [x] Add deterministic patches with exact input hash and match-count assertions. Replace platform init/ads at the platform boundary, preserving asynchronous exactly-once continuations and normal user-triggered rewards/progress.
- [x] Expose a narrow local runtime API: `setHostState(active, soundEnabled)`, `requestExit()` and a fixed native message set. Separate user/game pause from host pause; clear all held keys and touch IDs on interruption.
- [x] Remove advertising presentation from affected controls, keeping equivalent explicitly free game actions. Preserve menus, scenes, unlock conditions and original control visibility preference.
- [x] Keep original save key/serialization; directly replace validated save values without deleting the last good save first. Back up/recover valid data without silently resetting corrupt saves.
- [x] Exit stops new player input and waits for any pending completion update and actual save success. Failure presents retry/stay; backgrounding pauses immediately and preserves the last committed checkpoint.
- [x] Verify offline startup, single/two-player input, ad continuations, pause/audio, storage failures and exit during completion in a clean browser context. Record evidence separately from production assets.

## 3. Android host

Files: `Assets/Plugins/Android/LevelDevilActivity.java`, `LevelDevilBridge.java`, focused helpers as needed, `AndroidManifest.xml`, `Assets/Editor/LevelDevilAndroidBuild.cs`.

- [x] Launch one non-exported sensor-landscape fullscreen Activity, hardware accelerated. Use safe insets for cutouts/system gestures and preserve the original aspect ratio without clipping the game.
- [x] Serve only the packaged allowlisted files at one fixed HTTPS origin using WebViewAssetLoader. Unknown URLs/methods fail closed. No INTERNET or other added permissions; deny permission prompts, navigation, popups, downloads and file access.
- [x] Support WebView API 26 fallback and platform Back callbacks on API 33+. First Back shows “再返回一次，回到大厅”; second within 2.5 seconds starts the save-and-exit handshake. Loading/error states remain escapable. Backgrounding resets the double-Back window.
- [x] Handle native pause/focus/audio interruption and renderer failure, clear pending lifecycle callbacks at destruction, and always release launch/busy state on failure or exit.
- [x] Pin AndroidX dependency in generated Gradle and enable AndroidX idempotently. Generate/verify the content AAR before Unity build.

## 4. Lobby integration

Files: `Assets/Scripts/Runtime/AppController.LevelDevil.cs`, `Assets/Scripts/Runtime/LevelDevil/LevelDevilLauncher.cs`, `AppController.cs`, necessary shared external-game input helper, `AppController.KingdomRush.cs`.

- [x] Add a 只有一道门 lobby card with an appropriate original-resource preview and offline/touch description.
- [x] Save collection state, stop feedback, disable lobby interaction while launching/running; pass the collection sound setting without replacing the game's own volume preference.
- [x] Centralize the two external games' running/input gate at AppController ownership. Preserve Kingdom Rush behavior, and restore lobby input on normal return and launch failure.
- [x] Unsubscribe return callbacks on destruction. Do not create a second Unity gameplay page for WebView content.

## 5. Validation and delivery

- [x] Run meaningful build/behavior checks for packaging, resource restrictions, Back timing, save/lifecycle and launch failure. Keep research fixtures out of the APK.
- [x] Build the actual Android collection with all existing games. Inspect the final APK's complete permission set and game asset hashes, not just source manifests.
- [x] On available Android hardware/emulator, exercise cold offline launch, small-screen controls, multi-touch, first/second/canceled Back, Home/lock/resume, save/reentry, audio and return to portrait lobby.
- [x] Obtain independent spec and code review, address reproducible issues, then repeat affected checks.
- [x] Report exactly what passed and any missing device/full-content validation. Update third-party notices and implementation instructions. No automatic merge/push.

## Delivered validation boundary

See [verification-level-devil.md](../../verification-level-devil.md) for the exact APK/hash and evidence. API 36 isolated-emulator checks completed; OPPO testing was explicitly deferred by the user. API 26–30 retain the navigation bar to keep system Back directly available. No all-level playthrough, physical audio/thermal/performance claim, or older-device validation is implied by the completed implementation checklist.
