# Fruit Slice Startup and Free Blades Implementation Plan

> User requested these revisions on 2026-11-01. Continue within the existing isolated worktree. Keep emulator investigation isolated; the user subsequently requested the latest test APK on their phone to measure the complete lobby entry. The main workflow owns that installation and measurement.

**Goal:** Measure and improve game-entry latency using a dedicated Android emulator, remove the diamond economy and timed blade claim, and make every blade directly selectable.

**Architecture:** Keep Unity lobby and the offline Activity boundary. Diagnose native/WebView/resource/game-ready stages before changing startup. The owned H5 conversion removes economic UI and state while retaining scores, level progress and equipped blade. All 27 blade icons and equip controls must equip on their first tap. Remove acquisition countdown, diamond balances/earning/spending and result currency/triple rewards. Preserve gameplay scores, stars, progress, revival and the 60-second timed gameplay mode; that gameplay clock is separate from the retired blade-claim timer. Ignore retired currency/ownership/claim-time save fields while preserving a valid equipped blade.

**Tech Stack:** Unity C#, Android Java/WebView, pinned Laya H5, Python build/closure checks, temporary VM regression fixtures, dedicated API 36 ARM64 AVD.

## Task 1 — Baseline and startup diagnosis

- [x] Preserve the installed-preview APK and generated content under `artifacts/validation/fruit-slice/startup/`.
- [x] Create dedicated `XanaduFruitSliceValidationApi36` AVD, `emulator-5556`, API 36 ARM64 / WebView 133. Keep emulator investigation isolated; use separate preview/probe package identities.
- [x] Capture isolated Native/WebView probe baseline and repeated-entry samples on the dedicated AVD. The probe copies the three actual Native sources and byte-identical content; no debug WebView or network permission is enabled.
- [ ] Measure the complete Unity lobby → Fruit Slice entry. CUA mirror dragging has not successfully scrolled UGUI to the entry; this is a validation limitation, not a confirmed product defect.
- [x] Compare serial and final parallel-loading samples. No stable improvement was observed; exclude parallel loading from delivery and restore economy-serial content. Distinguish process-first samples from repeated entry without claiming OS/cache-cold conditions.
- [x] Trace `FruitSliceActivity`, Unity launcher, firstScene, mold dependencies and 52 level/banana JSONs; add bounded native stage diagnostics and compile against API 36. Evidence: `artifacts/validation/fruit-slice/startup-investigation.md`, `startup-native-javac.log`.
- [x] Add four bounded Unity entry logs in `AppController.FruitSlice.cs`, using Android elapsedRealtime and the same native tag; preserve Save and launch order. Independently reviewed and included in the successful Unity build.
- [x] Build/install the latest test APK on the user phone as explicitly requested, preserving app data. The main workflow owns device actions.
- [ ] Measure the user's click to Save, Bridge, Activity and WebView. Log capture is ready; user operation and feedback are pending.
- [ ] Implement a startup improvement only after full-entry evidence demonstrates it; no performance fix is claimed from the probe experiment.

## Task 2 — All blades selectable, no economy

- [x] Trace actual main, shop, results, pickups and save schema before editing `ThirdParty/FruitSliceContent` and `scripts/fruit-slice`.
- [x] Remove timed claim UI/logic, diamond presentation, currency earning/spending and result currency multipliers. Preserve gameplay scoring, stars, revival and selected blade; independent source/VM checks passed.
- [x] Make all 27 blades available. A first tap equips directly, with no lock, price, purchase confirmation or reward wait; all 27 icon handlers and both equip routes passed independent VM checks.
- [x] Migrate legacy saves by preserving valid equipped blade and progress while ignoring retired currency/ownership fields; independent VM checks passed.
- [x] Observe economy-serial probe home without diamonds/claim countdown, and first tap on the guitar blade changing it to “使用中” through CUA. This is one blade, not all 27.
- [x] Verify scene/class/resource closure, save migration, all 27 blade selections, result currency/triple removal and score/progress preservation through independent temporary behavioral fixtures. Ten gameplay/loading class blocks and scene animations remain unchanged.
- [ ] Complete latest-APK gameplay, result, gesture, lifecycle and audio interaction acceptance; source/VM checks and one observed guitar selection do not certify all UI paths.

## Task 3 — Review, package and evidence

- [x] Perform independent source review of economy and Native/C# instrumentation; no outstanding evidence-backed blocker was found in that review scope.
- [x] Build the non-Development ARM64 preview, verify the final APK and install on the dedicated emulator and user-requested phone.
- [x] Record available probe timings, emulator configuration, exact final artifact hash and pending full-entry/device limits in `docs/verification-fruit-slice.md`.
- [x] Restore only the four unrelated Unity-generated audio metadata changes; `git diff --check` passed. No commit or push is requested.

## Evidence checkpoint — 2026-11-01 13:18

`artifacts/validation/fruit-slice/startup/native-samples.log` contains three original-content samples (PID 2362, sessions 1/2/3) with ready-visual callback at **1964 / 1378 / 2059 ms**, and three economy-serial samples (PID 2613, sessions 1/2/3) at **1745 / 2029 / 1764 ms**. Values start at `FruitSliceBridge.open`; they exclude Unity preprocessing. Each first sample is process-first, followed by same-process repetition. Resource logs denote request starts, and visual callback is not physical-display or first-hit proof. Variation and differing content prevent an improvement claim from these values alone.

The original probe is `com.duanyao.fruitsliceprobe`, SHA-256 `cb6b54694d15bf76b0e9fc93fc87ced115bb5dc4992d29090d8862138bb8e210`, with 407 assets matching the preserved baseline APK. Economy-serial probe SHA-256 is `5e26c7f96736fbe1f33850b1f62a9f3c0d3cdba9f34731ba50a436e882013eb3`, with 406 assets. Per-file equality, no permissions, non-debug and signing results are in the respective `startup/probe/FruitSliceProbe*.json` reports.

Final parallel candidate PID 2913 produced 2399 / 2105 ms process-first/repeated visual callbacks, with no stable gain. Parallel v1 PID 2799 at 1675 ms has a different content hash and is excluded from the final-candidate comparison. Delivery retains economy-serial manifest `5de671bac329db9b9a609441c58585e455315d7404afcc19ddfcfda5b302343a`.

## Latest package checkpoint — 2026-11-01 13:25

Economy-serial preview build exited 0. APK is 327,432,435 bytes, SHA-256 `511bdec042f5172b201afff45a2213dfb0b5c915233e423a30de8b331b3f15cb`, package `com.duanyao.xanaduyonder.fruitslicetest`. Final validation matched all 406 assets, verified signing and non-debug status, and found no network permissions. Evidence: `artifacts/logs/build-fruit-slice-current-console.log` and `artifacts/validation/fruit-slice/apk.json`.

OPPO installation with `--no-incremental -r` returned Success without clearing data; last update 2026-11-01 13:25:20. The same APK installed successfully on `emulator-5556`. Logs are `startup/oppo-latest-install.log` and `startup/emulator-latest-install.log`. Stable serial probe reopening still showed the guitar equipped (`startup/economy-shop-emulator.png`); this is not the complete Unity route.

**Pending:** full Unity-to-game timing on the user-requested phone build and latest-APK gameplay/lifecycle/back/audio acceptance. `startup/oppo-user-test.log` capture is ready for user operation. Unity synchronous Save and pause-thread waiting are candidates only, not proven causes; no performance fix is claimed. Prior missing-prefab repair and its APK remain historical baseline evidence in `docs/verification-fruit-slice.md`. No commit or push is requested.
