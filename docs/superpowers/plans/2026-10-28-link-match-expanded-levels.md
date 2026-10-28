# Link Match Expanded Levels Implementation Plan

> **For agentic workers:** Use subagent-driven-development for the independent Core, persistence and asset tasks; the parent integrates UGUI and runs serial Unity verification. The user approved 96 tiles and selected 192 tiles for the fifth level; Fluent assets may extend beyond fruit. Continue implementation in this worktree without another design gate.

**Goal:** Add levels 4 and 5 with the approved dense boards, preserve existing levels and saves, and deliver real Unity UI examples plus an updated validation APK.

**Architecture:** Core retains the four-copies-per-kind rule and adds two catalog entries. Runtime migrates strictly validated v1 six-slot saves into v2 ten-slot profiles by mode and stable level ID. Presentation uses a catalog-sized tile pool and the same safe-area geometry, proportionally reducing tile decoration while leaving navigation and tools unchanged.

**Tech Stack:** Unity 6000.3.27f1, C# / UGUI, Newtonsoft.Json, .NET 10 tests, original Microsoft Fluent Emoji 3D PNGs.

## Approved design

| Level | Grid | Tiles | Kinds | Copies | Challenge |
| --- | --- | --- | --- | --- | --- |
| 1–3 | Existing 4×6 / 6×6 / 6×8 | 24 / 36 / 48 | 6 / 9 / 12 | 4 | Existing 90 / 120 / 150 s |
| 4 | 8×12 | 96 | 24 | 4 | 300 s |
| 5 | 12×16 | 192 | 48 | 4 | 600 s |

Both modes expose all five levels before play. Relaxed remains unlimited; challenge keeps 3 hints and 2 manual shuffles. Automatic deadlock recovery remains free. The fifth grid doubles both dimensions of level 3, giving exactly half-size cell edges and one-quarter area. At 360×640 safe content, cells are 51.328 / 35.5 / 25.664 px for levels 3/4/5, or 48.25 / 32.167 / 24.125 with a save-warning row. These are geometry calculations, not claims of device usability.

All 48 assets use the same Fluent 3D variant and fixed upstream commit as the original 12. Keep original IDs 0–11 byte-identical; append 12 food, 12 animal and 12 nature/object icons in the order recorded by `docs/prototypes/link-match-density/manifest.json`. Assets must stay readable through distinct silhouettes, not subtle same-color variants. Broadly applicable UI says 图案; preserve the first three levels' artwork and rules.

## Task 1 — catalog and bounded Core behavior

Files: `Assets/Scripts/Core/LinkMatch/LinkMatchRules.cs`, `LinkMatchGenerator.cs`, `LinkMatchGame.cs`, `LinkMatchPathFinder.cs`, `Tests/LinkMatch/Program.cs` and README.

- [x] Add failing catalog tests for `Get(4)` = 8×12/24/300 and `Get(5)` = 12×16/48/600; preserve all old IDs and values.
- [x] Add exactly `new LinkMatchLevel(4,"level-04",8,12,24,300)` and `new LinkMatchLevel(5,"level-05",12,16,48,600)`; expose catalog `Count`, `MaximumTileCount`, `MaximumKinds` to consumers. Retain four copies and existing path semantics.
- [x] Extend production-rule witness replay and sparse-mask reshuffle coverage to both sizes. Exercise all outer edges, full/empty/final-pair boards, no mutation on invalid submission and defensive copies.
- [x] Run `scripts/test-link-match.sh`, recording actual red/green results. Measure generation, find-match and reshuffle latency / allocation for 96 and 192, optimizing only demonstrated hot spots without changing deterministic old behavior.

## Task 2 — strict v1 migration and ten-slot persistence

Files: Runtime `LinkMatchSaveData.cs`, `LinkMatchProfile.cs`, `LinkMatchSaveCodec.cs`, `LinkMatchSaveStore.cs`, `Tests/LinkMatchPersistence/Program.cs` and README. Session APIs remain unchanged unless a demonstrated compatibility issue requires a local fix.

- [x] Add immutable legacy fixtures created by the original v1 codec, including unfinished challenge levels 1–3 and terminal best-run records. Verify v1 decode before any migration edits.
- [x] Adopt schema/content version 2 and `link-match-v2.json`; new index is `mode * LinkMatchLevel.Count + level.Number - 1`. Arrays and checks use 10 slots; tile bounds use catalog maximum.
- [x] Provide separate strict legacy decoding: require schema/content 1, old level IDs, at most 48 cells, exactly six records/slots and each old index's matching identity. Map old 0/1/2→0/1/2 and 3/4/5→5/6/7 by identity, never raw `Array.Copy`. Validate the entire resulting profile and preserve attempt IDs, clocks, counters, RNG, activity order and matching best tuples.
- [x] Load current primary/backup first. Only when neither current file exists may valid old primary/backup migrate; never roll back a corrupt newer save to stale legacy data. Atomically write the migrated v2 profile while retaining v1. A migration write failure returns the live migrated profile with a persistent warning and later retry support.
- [x] Test all ten slots, malformed schema/type/shape, old challenge indexing, legacy backup recovery, current corruption without legacy fallback, failed migration writes, restart/next-level resume and idempotent final records. Run `scripts/test-link-match-persistence.sh`.

## Task 3 — original assets and UI consistency

Files: `Assets/Resources/LinkMatch/`, `LinkMatchBoardView.cs`, `LinkMatchPage.Entry.cs`, `LinkMatchPage.Dialogs.cs`, `LinkMatchPage.Layout.cs`, `AppController.cs`, browser prototype and density study.

- [x] Verify all 36 new upstream PNGs and fixed-source hashes, copy originals into Resources with full importer metadata matching the existing textures, append manifest entries and preserve the full MIT license.
- [x] Allocate tile/texture arrays from catalog maximum and render the active board only. Keep icon IDs stable and offline; dense tile insets/radii/shadows scale relative to the same screen's level-3 cell size, with a visible selected border and nonoverlapping full-cell touch targets.
- [x] Expand entry to five scrolling level cards and accurate ten-slot progress. Replace `Number == 3` final-level checks with catalog last level; level 3/4 now lead to the next existing slot, level 5 has no next action. Adapt generic copy to 图案 without altering navigation ownership.
- [x] Provide an equal-safe-area 48/96/192 design comparison and update the interactive prototype for five levels and 48 kinds. Keep the density study explicitly presentation-only.

## Task 4 — real rendering, integration and delivery

Files: `Assets/Editor/LinkMatchUiChecks.cs`, `Tests/LinkMatchInput/Program.cs`, verification docs and screenshots.

- [x] Add actual UGUI checks at 360×640 and 388×784 for 96/192 tile count, in-bounds nonoverlapping geometry, dense adjacent click/drag/multipointer cancellation, outside paths, warning occupancy, pause and ten-slot resume. Prove old final level now advances and new final level does not.
- [x] Run the three focused .NET suites, broader affected Core tests and existing UI checks. Run Unity serially with the worktree's isolated Gradle/ADB environment; capture actual Unity 96/192 screenshots.
- [x] Independently review migration and functional/UI parity; fix confirmed issues, rerun affected checks. Build the independent `com.duanyao.xanaduyonder.linkmatchtest` APK and verify signature/package/hash.
- [x] Preserve the OPPO's old game save before any app update, install the new validation APK without uninstalling, and verify successful launch plus migration evidence without treating installation as user gameplay validation. If physical UI automation is unavailable, report the remaining device input/performance check explicitly and hand the new levels to the user.
- [x] Update architecture, development, asset and verification records with actual outcomes. Make a local commit after checks; do not push, merge or publish.

## Completion evidence

Implementation, original asset verification, three independent reviews, focused regression, production UGUI, Android build, OPPO update installation and actual v1→v2 field-by-field migration checks are complete. See `docs/verification-link-match-expanded.md`. Physical dense-board interactions and device performance remain for user testing; installation is not presented as that validation.
