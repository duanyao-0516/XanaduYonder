# Level Devil OPPO Follow-up Implementation Plan

> Continue in the existing worktree. Execute the independent native and web tasks with subagents, then perform independent review and device validation. The user's four changes and the chosen aspect-preserving fullscreen layout are the approved scope; no branch merge or unrelated game changes.

**Goal:** Fill OPPO's display with original game/background, provide a visible original-art return-to-hall control, fix real system Back and reduce measured repeated startup latency without changing levels or enabling networking.

**Architecture:** Android owns the full window, safe insets, actual Back callbacks and save-before-exit handshake. The web adapter retains the original gameplay coordinates and aspect ratio, fills the remaining screen with scene background and moves original touch controls to the sides. Resolution-specific preload work can be reduced only after proving which assets the runtime actually uses; the original PAK and audio stay byte identical.

**Tech Stack:** Existing Unity lobby, Java Android host, bundled Stencyl/OpenFL/Haxe JS, deterministic Python packager, Android 16 OPPO and browser behavior checks.

## Evidence before edits

- [x] Read current rules, preserve existing implementation and checkpoint data. Archive delivered APK as `artifacts/level-devil/oppo-followup/baseline-f7a990f2.apk`.
- [x] Reproduce on OPPO PKM110 (2373×1080 landscape). Old screenshot shows game only from x478 to2014 and y120 to981; the native aspect cap is not the only source of margins.
- [x] Confirm real Back path in `backs-before.log`: ColorOS's first edge swipe displays its own “再次滑动返回”, then the actual callback is legacy `onBackPressed` because predictive Back is disabled. Existing API>=33 guard drops this callback.
- [x] User chose: preserve original proportions, fill side regions with game background, move controls outward. No stretched or cropped game world.
- [x] Measure baseline click-to-interactive launch and identify critical preload work; distinguish native setup from JS asset/image initialization.

## Native host

Files: `Assets/Plugins/Android/LevelDevilActivity.java` and focused existing helpers only as necessary.

- [x] Let legacy `onBackPressed` reach the same committed-Back gate on every supported API, while retaining platform `OnBackInvokedCallback`. Prove the actual callback sequence rather than guessing from SDK level.
- [x] Implement visible transparent navigation with full viewport and safe insets; verify actual predictive and legacy Back on the isolated API 36 emulator.
- [ ] Confirm ColorOS no longer adds a protection swipe on OPPO. Deferred: the user requested emulator testing while another task uses the phone.
- [x] Replace the fixed 854:480 native WebView sizing with the full content viewport. Keep cutout and gesture safety in the controls' layout contract, not a second shrunken game surface.
- [x] Accept the new fixed local `exit-intent` message and route it through the same original save/exit handshake. No generic JS bridge or force-close shortcut.
- [x] Add bounded startup/Back phase logs for actual phone diagnosis; no save contents or external data in logs.

## Original artwork and mobile layout

Files: `ThirdParty/LevelDevil/adapter/`, exact patches in `scripts/level-devil/build-content.py`, related focused browser checks.

- [x] On scenes49/174 (world maps) and125 (player choice), add one host return control using original actor759/sprite760 visuals. Isolate its handler from existing in-game Back controls.
- [x] Clicking this control sends one `exit-intent`; native requests a checkpoint save, and only success returns to Unity. Storage failure remains visible and retryable.
- [x] Preserve the 427×240 logical gameplay/collision world; fit it to usable height without integer-only undersizing, fill side background and position original single/duo controls near the safe screen edges.
- [x] Validate real touch-coordinate mapping, shared-key ownership, drag/cancel, scene replacement, original Hide_Controls setting and the return button's touch target on wide and 16:9 screens.

## Startup

- [x] Verify the original preloader's image counts and startup scale selection. Prototype only the selected-resolution/common-image preload set if all subsequent loads and fallback contracts are proven.
- [x] Keep all original packed members and on-demand assets. Keep source hashes, PAK CRC checks and native per-file integrity checks.
- [x] Measure original and optimized content on the same isolated emulator, record native phases and screenshot upper bounds, and retain high-load outliers. Preserve all four image sets.
- [x] Remove only the first boot presentation delays; verify save loading and subsequent original transitions.
- [ ] Compare final latency on OPPO. Deferred at the user's request; emulator timing is not a phone speedup claim.

## Delivery

- [x] Run focused failure/behavior checks first, then the existing original-runtime regression against the final exact AAR.
- [x] Build the independent full collection, inspect APK permissions/asset hashes/signature and compare all generated Java sources with production.
- [x] Validate the final exact APK on the isolated emulator: actual side swipes, cancel/rearm, visible return buttons, fullscreen controls, save/reentry and background recovery. Preserve saves.
- [ ] Install and repeat on OPPO when it is available; explicitly deferred by the user.
- [x] Obtain independent review, update the validation report with precise device evidence and limitations, and leave changes unmerged.

## Touch cancellation follow-up

Real Android multitouch CANCEL left the original emulated mouse latch down, consuming the next menu press.

- [x] Fix only the canceled primary mouse state while preserving other touch owners; test primary/secondary cancellation and host-inactive cleanup.
- [x] Reproduce the original failure and prove the first subsequent real touchscreen menu press works in the final APK.

Final deliverable: APK `46b3ef94…959dc0`, content `016eb687…6a18f1`. All current implementation and isolated-emulator checks completed; only the explicitly deferred OPPO items remain. See `docs/verification-level-devil.md` for exact artifacts, startup samples and validation limits.
