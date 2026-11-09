# Bomb It touch controls and lifecycle implementation plan

> **For agentic workers:** Use subagent-driven-development for the independently owned tasks below. User approved the complete design on 2026-11-09 and requested Android replacement installation on OPPO. Do not operate the phone to test or launch gameplay.

**Goal:** Preserve the game viewport across control styles, use floating four-way movement or large radial buttons, correctly pause game time, and restart the real single/two-player mode after confirmation.

**Architecture:** A narrowly scoped, audited SWF adapter exposes real game phase, mode and pause operations. The host owns pause reasons and player generations. The control module owns pointer identities and key release; controls overlay a viewport calculated independently of style and player count. Network policy remains offline.

**Tech Stack:** Existing AVM1 SWF, pinned Ruffle 0.7.1, local HTML/CSS/JavaScript, Python deterministic content builder, Unity Android toolchain.

## Workspace and delivery

- Work on `p/duanyao.0516/bugfix/bomb-it-touch-pause` in the isolated worktree. Preserve unrelated GoldMiner changes in the original checkout.
- Keep source version 0.9.7 unless installation requires a higher build code. User requested a testable Android package, not a GitHub publication or iOS build.
- Build an APK with the existing matching signing configuration; replace-install on OPPO `3B6F4NE8A66ZXTRL` without uninstalling, clearing data, or launching it.
- Delivery update from the user: defer installation and provide the exact `adb install -r` command. Do not install or launch the app on this turn.

## Tasks

- [x] SWF integration: add a fixed game-state/pause/mode bridge from reviewed ActionScript, pin and audit only necessary altered tags. Preserve all other original game/resource bytes; keep advertising/network gates. Expose phase, player count, manual pause and remaining time. Preserve sub-interval active time when compensating pauses. Initialize requested mode after root/menu initialization, then use original difficulty and character flow.
  - Owner files: `ThirdParty/BombItContent/sanitizer/`, new bridge sources/build tooling, `scripts/bomb-it/build-content.py`, corresponding content documentation.
- [x] Touch controls: update `adapter/controls.js`, `adapter/style.css`, `adapter/index.html`. First pointer down is neutral, origin is the real touch; one moving pointer per player; dead zone and direction hysteresis; first-down ownership survives crossing midpoint. Half-screen zones apply only in battle and exclude tools, HUD, bomb and system safe areas. Radial dpad uses true four-sector hit testing with neutral center. Style switches release keys, persist choice and never restart.
- [x] Host coordination: update `adapter/host.js`. Keep viewport independent of input style and player count. Read authoritative SWF phase; synchronize native SWF pause and shell pause. Maintain manual pause and temporary reasons independently. Background return requires explicit resume. Confirmed mode change destroys/recreates player using generation guards and actual game-mode initialization; cancellation leaves current game unchanged.
- [x] Regression checks: extend existing browser check tooling, using real pinned runtime and SWF. First reproduce old viewport shrink and eccentric-touch movement. Verify the new behavior, multi-touch, HUD/menu access, pause reasons, long pause greater than 180 seconds, near-expiry/fractional time, mode cancel/confirm and stale generation guards. Inspect screenshots with safe insets and both modes/styles. No phone interaction tests.
- [x] Review, build and delivery: independent spec/code review completed; blocking findings resolved. `./scripts/build.sh android --configuration debug` exited 0; runtime/payload guards and exact Bomb It APK content checks passed. Produced 0.9.7/build 16 for the existing debug package. Per the latest user instruction, provide the exact `adb install -r` command and leave installation and phone validation to the user; no device launch or gameplay tests performed. An intermittent AudioContext rejection in artificial immediate-exit failure injection is recorded in `docs/bomb-it.md`, without claiming it fixed.

## Accepted behavior

The game rectangle is identical for joystick/dpad and one/two players on the same screen. Floating joystick has no press-only movement and retains actual origin even near a clipped edge. Large dpad may overlap noninteractive game edges on narrow screens but must not hide existing HUD buttons. Menus remain directly touchable. Pause freezes remaining time as well as updates; closing a modal never clears an existing manual pause. Changing player count presents cancel/confirm and enters the selected original difficulty/character menus before play.
