# Classic Gold Miner verification

Date: 2026-11-01. Branch: `p/duanyao.0516/codex/gold-miner-duo-package`, based on master `1f99287016f353fd92ee0ca94a761c2e6bd433e9`.

This record covers the final fullscreen mine revision, including the duo paused-resize fix. The earlier preview with separate drop-button panels has been superseded. The final package was built, audited and installed on the owned emulator; the user's preview saves were restored after validation.

## Source, engine and build checks

| Layer | Evidence | Scope |
| --- | --- | --- |
| Unity lobby | `artifacts/lobby-design/report.txt`: 3,777 assertions at 720×1320 and 720×1582 | 21 entries, three presentation modes, pointer binding/drag behavior, classic launcher lifecycle and native-return callback |
| Shared persistence | `artifacts/classic-gold-miner-validation/host/host.test.cjs` | Mode isolation, invalid/corrupt data preservation, no-run capture, failed save acknowledgement, explicit foreground resume and failed-resume recovery |
| Solo, seven suites | `artifacts/classic-gold-miner-validation/solo/{contract,runtime,error-save,shop-touch,layout,fullscreen-runtime,paused-timer}.test.cjs` | Snapshot boundaries; original atlas validation; first-press play/two-tap shopping; width migration of real old Android saves; all 30 source-map constructors; original Pod/Boulder/TweenData flight, collision, carrying, bomb and moving-entity behavior; safe-edge filtering; scene width reset and atomic resize |
| Solo paused timers | `solo/paused-timer.test.cjs`, using the bundled Phaser Timer and production helpers | After five seconds in the background, rebuilding preserves a 700 ms countdown remainder and a 300 ms transition remainder; foreground resumes their deadlines and the one-second tick cadence |
| Duo, eight completed suites | `artifacts/classic-gold-miner-validation/duo/{core,runtime,parity,fullscreen-layout,fullscreen-runtime,shop-touch,hud-boundary,paused-resize}.test.mjs`; `artifacts/classic-gold-miner/final-duo-suite-results.json` | All 30 maps, original values/assets, complete scene snapshots, hooks and delayed effects, shared bomb reservations, two independent pointers, canonical swing projection, width migration, two-tap shopping and HUD/safe-edge boundaries |
| Duo paused resize | `duo/paused-resize.test.mjs`, using bundled ScaleManager, Size, TimeStep and Game methods | Four shrink/growth cases with changed or unchanged aspect ratio; fresh parent measurement, canvas/input bounds, paused repaint, HUD recalculation and unchanged clocks/entities; regression failed before the fix and passed afterward |
| Native policies and compilation | `artifacts/classic-gold-miner/native-tests/`; pinned Unity OpenJDK and Android SDK 36 | Origin/path rejection, committed Back interval/reset, Activity dialog/lifecycle fixtures, system-WebView/inset integration and five compiled Java sources; no API 26 runtime claim |
| Resource gate | `artifacts/classic-gold-miner/content-validation.json` | 80 packaged web files, 31 resolved static dependencies; negative fixtures reject foreign URLs, traversal, missing resources/modes, frames and inline scripts |
| AAR reproducibility | `artifacts/classic-gold-miner/reproducibility.json` | Final independent rebuild is byte-identical: SHA-256 `00a8589819aed1ffd8fe8e35f7365a39bd9cfc4b422930044d4402fd432a5049`, 2,280,946 bytes |
| Browser runtime | Actual shipped Phaser pages, including a 640×320 CSS viewport | Fullscreen mines, proportionate sprites, floating bomb controls and original-aspect menu/shop scenes inspected; source-driven fixtures do not replace this rendering check |
| Independent review | Separate spec and code-quality reviews | Closed false save acknowledgement, trapped fatal-error recovery, stale native dialog resurrection, missing atlas-frame acceptance, incorrect moving-Tween capture, solo paused timers and duo paused resize; final resize review found no remaining issue |

The contract/state suites use bounded engine fixtures, not complete browsers. The solo fullscreen suite also executes the original Pod, Boulder and bundled TweenData methods against real saved snapshots and all source-map constructors. Shop tests invoke the shipped Phaser 2 `hitTest` and Phaser 3 `pointWithinHitArea`; the solo timer test uses the shipped Timer. Browser checks execute the actual rendered Phaser pages. Emulator checks below exercise the installed Android package, native lifecycle and injected touch events. These are distinct validation layers; none is a full campaign playthrough.

## Fullscreen Android integration

The isolated preview package is `com.duanyao.xanaduyonder.classicgoldminertest`, built through `scripts/build-classic-gold-miner.sh`. Its output is `artifacts/builds/XanaduYonder-0.9.4-classic-gold-miner-preview.apk`.

The final audited fullscreen preview recorded in `artifacts/classic-gold-miner/apk-verification.json` is 320,714,650 bytes, SHA-256 `796a0490f6af8b6f9e6cc5125f7233eb45bad81aca34fcb320c83998cea89bf7`. Its manifest declares only `VIBRATE`, with no `INTERNET`; all 80 packaged web files matched the final source and no embedded browser engine was found. The scoped preview build restored the project's formal package settings; unrelated Unity audio-import metadata changes were also reverted.

Device interaction is restricted to this task's independent emulator: `emulator-5590`, AVD `ClassicGoldMinerApi36`, files under `.local/classic-avd/`. It runs Android 16 / API 36 with system `com.android.webview` 133.0.6943.137. Its normal display is 720×1600 at density 320; narrower fullscreen layout was also exercised. ADB automation was explicitly authorized only for this serial, and every device command included it. The physical phone was not used for this validation.

Actual fullscreen Android checks completed:

- The lobby retains both old and classic Gold Miner items. Classic opens a landscape system WebView. The mine fills the viewport without the earlier permanent control panels; sprite proportions are retained and floating controls avoid the gesture edges. Screenshots include `final-solo.png` and `final-duo-small.png`.
- Solo responds to the first mine touch. The right floating bomb control operates on the carried object without also dropping the hook; evidence includes `fullscreen-solo-bomb.png` and `fullscreen-solo-bomb-result.json`.
- Duo receives a genuine two-pointer MotionEvent stream, one touch in each half of the mine, and both players act on their first DOWN. The left and right bomb controls affect their respective players while sharing the stock: two simultaneous valid uses reduce stock from 2 to 0. `fullscreen-duo-dual-mine.json` and `fullscreen-duo-both-bombs.json`, with matching screenshots, record this.
- Shop first taps show/select the item and preserve money; second taps purchase once. Solo stayed at 1881 on the first tap and changed to 1829 for the 52-price item on the second. Duo stayed at 700 and then changed to 689 for the 11-price item. See `fullscreen-{solo,duo}-shop-{first,second}.json` and the corresponding `shop-info`/`shop-bought` screenshots. Bounded save fixtures were used to reach shop and bomb scenarios; these checks do not demonstrate completing a campaign.
- Each mode has an independent save/Continue entry. Save/exit and cold application relaunch restore the same run, map/entity identities, money and remaining timer, including in-flight hooks. `final-solo-cold-resume.json` and `final-duo-cold-resume.json` record the fullscreen runs. Stable rendering is shown in `final-solo.png`, `final-duo.png` and `final-duo-small.png`; early transition-frame captures are not layout evidence. Solo and duo persistence remain separate.
- Home/background pauses gameplay; returning through the recent task displays Continue and stays frozen until explicitly resumed. `final-duo-paused.png` records this. On the final package, paused resizing covered landscape 1600×720 → 1280×640 → 1600×800 → 1280×640 → 1600×720. All four cases kept the timer at 37 seconds and retained ore identities; both same-aspect changes preserved the entire JSON snapshot exactly. The paused canvas repainted without cropping or stale margins. See `resize-fixed-result.json` and `resize-fixed-*-paused.png`; `final-duo-small.png` and `final-duo.png` also confirm correct rendering after Continue.
- A cancelled edge swipe is ignored. The first completed system Back shows the prompt; another within two seconds saves and returns to Unity. Waiting 2.3 seconds resets the window: the next Back remains in-game, followed by a within-window Back that exits. `back-check.json`, `back-first-prompt.png` and `back-return-lobby.png` cover the committed/cancelled gesture flow.

Screenshot, snapshot and Back-report filenames in this section are relative to `artifacts/classic-gold-miner/emulator/`. Earlier non-fullscreen screenshots remain as historical artifacts and do not establish the final layout.

## Geometry and validation limits

Wide-screen layout expands mine positions horizontally and projects the original canonical swing angles into the wider field. Character, mineral and hook sprites retain their proportions; source maps, prices, weights, hook-speed constants, timers and swing periods remain unchanged. Expanded travel distances can change the duration and difficulty of individual grabs, so the result is not claimed to reproduce the original difficulty exactly.

There is no claim of a full campaign playthrough, physical-phone gesture regression, API 26 runtime regression, or exhaustive coverage of Android vendors and WebView versions.

Final cleanup completed: both original emulator preview saves were restored from `emulator/{solo,duo}-preserved-before-update.json` and compared exactly after writing; `emulator/user-saves-restored.json` records the result. Display size was reset and the app was left in the Unity lobby. The final app-process log is `emulator/final-app-logcat.txt`; the targeted scan found no fatal exception, uncaught script exception or network-resolution failure. No physical-phone operation was performed.

## Preservation

The Unity Gold Miner runtime/core/save-store sources remain unchanged; the old `gold-miner` entry is retained beside `classic-gold-miner`. Its `gold-miner-v1.json` is separate from the classic solo and duo files. Untouched original H5 archives remain under ignored `artifacts/`, with hashes recorded in `ThirdParty/ClassicGoldMinerContent/README.md`.

Temporary verification programs are under ignored `artifacts/`. Existing lobby-check source was updated for the new entry and lifecycle.

## Delivery revision

After the preview was installed on the user's OPPO, the user supplied a 72×72 image for the classic entry's large lobby logo and requested commit/merge to remote master with no further tests or compilation. The image is preserved byte-for-byte as `Assets/Resources/Lobby/ClassicGoldMinerLogo.png` and is loaded by the classic entry's own preview callback. The earlier APK and validation above predate this logo revision and subsequent master integration; no rebuilt APK or runtime validation is claimed for this delivery revision.
