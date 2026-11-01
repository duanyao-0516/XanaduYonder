# Cut the Rope Offline Integration Implementation Plan

> **For agentic workers:** Use subagent-driven-development to implement the independent tasks in this plan. User approved execution in the current worktree; continue through verification and test APK delivery.

**Goal:** Add Cut the Rope's ordinary 425-level game to the Unity hall, with a fullscreen offline Android WebView, phone touch support, and two completed system Back events within 2 seconds to return to the hall.

**Architecture:** Keep original gameplay and level data. Convert the downloaded Hx/DDS and AVIF images at build time into compatible PNG images while preserving 2x atlas coordinates. A small explicit offline GameInterface owns local progress, sound/pause events and host notifications; no portal, ad, login, analytics or remote-loading scripts are packaged. Unity launches a dedicated internal Android Activity; all local content is served through a fixed synthetic HTTPS origin and every other request is rejected.

**Tech Stack:** Unity 6000.3.27f1, C#, Java Android API 26–36, WebView/Canvas2D, JavaScript, Python/Node asset tooling, Playwright checks.

## Shared contracts

- Unity bridge: `com.xanaduyonder.cuttherope.CutTheRopeBridge.open(Activity, boolean soundEnabled)`, `isRunning()`, `getLastError()`.
- Native content URL: `https://appassets.androidplatform.net/assets/cut-the-rope/index.html`.
- Offline JS API: `window.CutTheRopeOffline.setHostPaused(boolean)`, `setSoundEnabled(boolean)`, `flush()`.
- Native JS interface `CutTheRopeNative`: `ready()`, `reportError(String)`, `readSave(String): String`, `writeSave(String, String): boolean`, `removeSave(String): boolean`. Native writes are synchronous durable writes; browser verification falls back to localStorage with the same serialized values. Failed writes must remain visible, not silently reported as successful.
- Normal game startup reaches the original menu. Intro/outro media scenes are skipped while their required progress transitions remain intact. Ordinary gameplay, stars, unlocks, settings and supported picture rewards stay intact. Ads/rewarded/IAP/xmas/portal links are not enabled.
- Runtime AAR: `Assets/Plugins/Android/CutTheRopeRuntime.aar`; content AAR: `Assets/Plugins/Android/CutTheRopeContent.aar`, generated from `artifacts/cut-the-rope/offline`.
- Test application label `割绳子测试`, isolated application ID `com.duanyao.xanaduyonder.cuttheropetest`; keep the full hall and existing entries. Do not overwrite the production app or disturb another worktree.

## Task 1 — Offline content and conversion

**Owner/files:** asset implementer; `scripts/cut-the-rope/`, `ThirdParty/CutTheRopeContent/`, `Tests/CutTheRopeContent/`. Original immutable input is `artifacts/cut-the-rope-4399-142014/game/` with its SHA-256 manifest.

- [x] Add failing checks for ordinary resource closure, 17×25 levels, serialized local save and disabled ad/network paths.
- [x] Extract the existing Hx decoder reproducibly, decode DXT blocks, export PNG while preserving actual dimensions/padding, alpha and orientation. Convert AVIF fonts/loader/pictures too.
- [x] Generate a patched game from the pinned original: fixed 2x, PNG routes and size manifest, skip intro/outro correctly, disable unused modes and external links. Reject changed source hashes or unmatched patch anchors.
- [x] Implement a real standalone GameInterface for local state, pause/audio and required promises/listeners. Package no portal SDK scripts or remote URLs.
- [x] Verify original/converted image parity, normal-menu → first-level → success → next-level, and all 425 level/resource loads at phone and desktop viewports with external requests blocked. Save errors/network/screenshots as evidence.

## Task 2 — Android runtime

**Owner/files:** Android implementer; `ThirdParty/CutTheRopeRuntime/`, `scripts/cut-the-rope/build-runtime.py`, `Tests/CutTheRopeRuntime/`.

- [x] Test the Back policy first: first event arms, second within 2000 ms exits, timeout resets; lifecycle reset prevents stale exit; cancelled gestures do not invoke a completed-Back callback.
- [x] Implement internal fullscreen Activity, guarded launch/teardown, native Back APIs across supported versions, first-Back message and second-Back durable save then finish.
- [x] Implement local-only HTTPS request handler with exact asset path validation, no navigation outside content, no INTERNET permission and no network fallback; test traversal and remote URLs.
- [x] Use an edge-to-edge viewport with original proportional game layout and Android-owned edge gestures, no webpage zoom/scroll/text selection, sound/focus/background pause and touch cancellation. Handle renderer/load failure by reporting a useful error and returning to the hall.
- [x] Build the Java AAR using the installed Unity SDK/JDK, package generated content AAR, verify exported components and permissions.

## Task 3 — Unity hall and APK build

**Owner/files:** Unity implementer; `Assets/Scripts/Runtime/CutTheRope/`, `AppController.CutTheRope.cs`, targeted hall catalog/cleanup hooks, `Assets/Editor/ProjectBuild.cs`, `scripts/build-cut-the-rope.sh`, appropriate tests.

- [x] Add tested hall entry/action in all three display modes; launcher rejects duplicates and restores hall input on return/failure.
- [x] Use the shared Lucide scissors icon for compact entries and the user's supplied logo unchanged for the large preview; preserve existing game entries.
- [x] Add isolated test build method with label `割绳子测试`, original settings restored in finally, no network permission and fresh-output verification.
- [x] Run targeted host/hall tests, existing core smoke checks, and build the actual integrated APK after runtime/content inputs are ready.

## Task 4 — Integration review and delivery

**Owner:** root, then independent reviewers.

- [x] Restore required existing generated dependencies from verified matching worktrees without changing their code; keep Unity invocations serialized.
- [x] Inspect all implementation diffs for requirement coverage, then independently review save, lifecycle, input and network boundaries; fix concrete findings.
- [x] Validate APK ZIP integrity, signature, application ID/label, complete content hashes, required Activities and absence of Internet permission/ad SDK/remote endpoints.
- [x] Run device/emulator tests if available; distinguish browser, static, compilation and Android runtime evidence. Produce the fresh APK, SHA-256 and concise verification report; document unverified device cases honestly.

## Initial build and acceptance evidence

- Initial accepted test APK: 411,227,099 bytes, isolated package `com.duanyao.xanaduyonder.cuttheropetest`, label `割绳子测试`. The build output path is reused by later builds; the hashes below identify this historical evidence, not the current file.
- SHA-256: `ce056b0845f37c4a7c307a82b193a95d2a52830f764040b0dd4d4ae688aca265`.
- Initial APK audit: signature, ZIP integrity, internal Activity, all 213 content files and all 425 ordinary levels verified; no Internet permission, no production test probes. Historical evidence: `artifacts/cut-the-rope/fullscreen/previous-apk-verification.json`.
- Checks passed: core 43, Unity launcher 19, native Back/router 42, content contracts 18, content integrity 4, phone touch regressions 5; Unity hall checks passed 3,867 assertions across all three layouts and 21 routes.
- Browser scene verification initialized/rendered all 425 levels at desktop and phone sizes and checked final-level completion persistence. This is resource/scene coverage, not manual completion of every puzzle. Evidence: `artifacts/cut-the-rope/evidence/all-levels.json`.
- That APK on Android API 36 / WebView 133: offline startup, ordinary quick taps, a real 120 ms rope swipe producing three stars, retry, update-preserved progress, background touch cancellation/resume, canceled edge Back ignored, and two completed edge Back gestures 1,481 ms apart returning to an interactive Unity hall. No app runtime errors. Evidence: `artifacts/cut-the-rope/apk-device/final-apk-report.json`.
- Earlier native harness additionally checked timeout/rearming, durable progress after force-stop, rotation, blocked external requests/navigation, invalid asset paths, and renderer/load failure recovery. These deeper checks used the same native runtime before the final JS input changes. Evidence: `artifacts/cut-the-rope/android-smoke/acceptance-report.json`.
- Independent source review passed. Physical-device gameplay, API 26–32, audible speaker output and phone-call interruptions remain outside the completed acceptance coverage.

## Fullscreen follow-up

The user requested proportional fullscreen rendering without borders after installing the test APK on the OPPO. The existing game already uses uniform scene scaling and a covering background. Native container padding incorrectly reserves all system gesture/cutout insets around the entire WebView: the 720×1600 reproduction renders only within `[60, 152, 660, 1536]`.

- [x] Remove native viewport padding and explicitly allow fullscreen display cutouts. Keep the original game camera, input mapping and completed system Back behavior.
- [x] Verify the new APK fills the viewport. OPPO 1080×2373 menu screenshot has bounds `[0, 0, 1080, 2373]`, and the user confirmed the fullscreen effect. Native policy tests remain 42/42; game JS is unchanged. Extended rotation/gameplay rechecks were interrupted by the user's next request and are not claimed complete.
- [x] Install the updated isolated test package on the user's OPPO with `adb install -r`, preserving app data. Fullscreen APK SHA-256: `3e4ed2d4e92ea46bf0758472032687809e9dc1aa813bef9f05ec34cab43c64f3`. Device evidence: `artifacts/cut-the-rope/fullscreen/oppo-report.json`.

## Language and size follow-up

The user requested Chinese menus with the original font where available, otherwise fixed English with no language switch. Existing bitmap fonts lack the required Chinese glyph coverage, so the approved fallback is original English. Keep image-embedded text unchanged and preserve all progress.

- [x] Enable the existing force-English menu behavior and keep only English resource availability, preserving the bitmap font's internal face indexing.
- [x] Normalize previously saved languages without changing level progress, unlocks, settings or picture rewards. Verify migration and failure behavior, including the corrected v1/v2 upgrade write boundary.
- [x] Exclude non-English font assets from generated content; retain pinned original inputs and exact English font bytes. Node 28/28, Python 4/4, desktop/phone menu checks and independent save review passed. All 17 map hashes are unchanged; all 425 scenes were not rerun for this language-only change.
- [x] Build, audit and install the updated test APK on the OPPO with `adb install -r`, preserving app data. APK SHA-256 `5bd4ddfcfe7a71b23e7adc963c238257e877a4a6d924f781bb6d5f562eb0343c`, 406,629,255 bytes, 195 game assets / 88,197,001 compressed bytes. OPPO shows English Play/Season labels; settings behavior and exact progress preservation were checked in the browser/VM. The user accepted this version before requesting final lobby artwork and Git delivery.

Before language trimming, Cut the Rope assets occupy 92,791,702 bytes in the APK; the nine non-English font pairs occupy 4,593,126 bytes. The full APK is 411,226,779 bytes. These are packaged sizes, not installed-data estimates.

## Artwork and master delivery

- [x] Replace the compact colored preview with the shared Lucide scissors resource and remove the unused shared BuildIcon extension. Use the supplied 72×72 logo unchanged with proportional scaling.
- [x] Verify the final Unity lobby render in all three layouts and rebuild the test APK with the updated artwork. Unity passed 3,817 assertions at 720×1320 and 720×1582; compact and large-logo screenshots were inspected. APK SHA-256 `fb8c8caf0eb86e8c779d1acb85de2017b58570299482f2875b92281daa2d92d1`, 406,646,183 bytes, with all 195 offline game assets and 425 levels verified. The game JS, content AAR and native AAR are unchanged from the accepted language/fullscreen implementation. This artwork build was compiled/audited; earlier gameplay reports retain their own historical hashes.
- [x] Include only the pinned original game snapshot and download manifest in a reproducible source archive, with an integrity-checked offline restore command. The archive is 19,188,246 bytes, SHA-256 `e8e5e8295dbcf12b99e513beb909fbd1411f8deae722bbccc5f1afdb192083c8`. Clean restore, repeated restore, file-conflict refusal and symlink refusal passed; all 219 restored files match the original snapshot.
- [x] Independently review the final runtime, artwork, build and restore changes and resolve the concrete findings.

The user authorized committing this completed change, integrating current `master`, and pushing `master`. Git delivery must preserve unrelated work, use a normal non-forced push, and verify remote/local commit identity afterward.
