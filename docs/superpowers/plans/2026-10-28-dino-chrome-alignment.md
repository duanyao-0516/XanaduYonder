# Dino Chrome Alignment Implementation Plan

> Execute in this existing isolated Dino worktree with subagent-driven development, test-driven fixes, and independent review. The user approved the complete alignment and OPPO overwrite installation on 2026-10-28; no further design approval is needed.

**Goal:** Align speed, acceleration, jumping and obstacle spacing with the pinned Chromium normal mode, repair sprite borders and collision gaps, retain orientation adaptation and saves, then build and install on OPPO.

**Architecture:** Pure C# Core owns the Chrome 60 Hz model and converts original pixel units through the existing 1.35 sprite scale. Actual viewport width, not orientation names, drives the original narrow-screen initial-speed formula. Rendering owns day/night palette mapping and final pose, while existing page/session code retains inputs, pause, orientation, holiday hats and persistence.

**Tech Stack:** Unity 6000.3.27f1, C#, .NET 10 model tests, Unity UGUI render checks, Android IL2CPP, ADB.

## Accepted behavior

- Source revision `3df8d372a3f9d5c38a89d0bc496cc44c85af1feb`; normal mode only. Fixed 60 Hz avoids Chrome's callback-rate-dependent acceleration on 120 Hz displays.
- In original units, speed starts at `min(6, 6 * width / 600 * 1.2)` for width < 600, otherwise 6; increases by .001 each effective tick up to nominal 13. Convert original pixel units by SpriteScale, including distance and sprite geometry.
- Port actual Trex jump update, speed-dependent launch, minimum height/release, upper y threshold, and air speed-drop behavior. Preserve touch ownership and pause semantics.
- Generate obstacles using original minGap/maxGap, speed gates, groups, duplicate limit and bird heights/offset; retire the independent recovery-based spacing policy. Preserve sensible finite geometry at every supported viewport.
- Use original outer inset and detailed collision boxes, transformed correctly from top-left to bottom-left. Keep continuous collision detection to prevent tunneling and commit all positions at earliest contact. Do not treat white sprite pixels as proof of body contact.
- Render the original white border white in day and black in night, while preserving alpha, point sampling, rounded masking and colored celebration hats.
- Preserve UI layout, direction selection, records, package ID and user data. Save protocol unchanged. No changes to other worktrees or games.

## Task 1 — Core motion and obstacle generation

Files: `Assets/Scripts/Core/Dino/DinoRules.cs`, `DinoGame.cs`, `DinoObstacle.cs`, `DinoTraversal.cs`, new `DinoMotion.cs`, `Tests/Dino/Program.cs` and associated core test files.

- [x] Add failing assertions for initial speeds at narrow/wide normalized widths, .001/tick acceleration, 13 cap, Chrome jump trace/release/drop, group gap bounds and speed gates.
- [x] Replace the custom speed-cap search and spacing certificates with the pinned normal-mode rules. Keep model API integrations coherent; remove obsolete production planner dependencies rather than silently retain unrelated generation rules.
- [x] Trace generated sequences across portrait/landscape widths and compare motion to an independently derived source reference; verify no frame allocations and counter bounds.

## Task 2 — Collision

Files: `DinoCollision.cs`, dedicated collision tests and `Tests/Dino/AtlasGeometry.cs` (coordinated with core owner).

- [x] Add regression cases for head/leg empty-space deaths, edge-only contact, grouped cactus geometry and high-speed sweep entry fraction.
- [x] Move motion out of this file, then port original detailed boxes and coordinate transform. Return earliest contact fraction; support ground duck and airborne/landing transitions.
- [x] Core owner uses the returned fraction to stop all objects consistently. Tests exclude white pixels when checking visible geometry and distinguish official box approximation from pixel-perfect collision.

## Task 3 — Rendering

Files: `Assets/Scripts/Runtime/Dino/DinoTrackView.cs`, any Dino-only palette/shader resource, corresponding `Assets/Editor/DinoVisualChecks.cs` assertions.

- [x] Add a failing real-render check that original white sprite pixels map to black at night.
- [x] Implement white/dark mapping compatible with UGUI stencil and point sampling. Preserve immutable day/night material behavior, hats and source atlas bytes.
- [x] Render day→night→day, running/jumping/duck/dead poses and representative holiday hats in both orientations; inspect actual screenshots.

## Task 4 — Integration and delivery

Files: Dino-specific editor/page checks, `docs/dino-game.md`, `docs/verification-dino.md`, third-party notices if required by source-derived code.

- [x] Run `scripts/test-dino.sh`, session/persistence/presentation checks; resolve obsolete assumptions in tests without weakening new contracts.
- [x] Serially run page, ground and visual Unity checks with the existing workspace build lock. Review the full diff independently.
- [x] Run `scripts/build-dino.sh`; verify APK identity, hash and restored project settings.
- [x] Read existing OPPO test-package records, `adb -s 3B6F4NE8A66ZXTRL install -r` the built APK, then verify installed identity and byte-identical save. Do not uninstall or clear data.
- [x] Check launch, portrait/landscape and pause where device state permits; record actual validation limits and deliver APK plus selected screenshots.

## Completion evidence

All tasks completed in this worktree. Core20/0; session2125; persistence20; presentation8/90; Unity page19/412, ground59, visual1073. Android build succeeded; OPPO overwrite installed and records544/539 preserved byte-for-byte. See `docs/verification-dino.md` for APK hash, device evidence and limits.
