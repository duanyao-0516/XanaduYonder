# Sokoban Implementation Plan

> **For agentic workers:** Use subagent-driven-development for the independent modules below, then perform specification and code-quality review before delivery.

**Goal:** Ship the approved Sokoban page as a playable offline Unity game, preserving its composition and adding verified content, durable progress and the eighth lobby entry.

**Architecture:** Pure C# owns levels, grid rules, action history and result accounting. Sokoban runtime classes own strict persistence, viewport/input and UGUI presentation; AppController only routes and forwards shared settings/lifecycle. Content is imported from the already archived licensed sources with a reproducible manifest.

**Tech Stack:** Unity 6000.3.27f1, UGUI, C# 7.3, Newtonsoft.Json 13.0.2, standalone .NET 10 checks, Python content importer.

## Frozen references and ownership

- UI: `docs/prototypes/sokoban/{index.html,style.css,prototype.js,preview-compact.png,preview-tall.png}`. Match paper/ink, shared icons, single-line header and counters, flexible board and unified bottom controls. No focus mode or permanent instructional footer.
- Rules/storage: `Assets/Scripts/Core/Sokoban/`, runtime `Sokoban{Session,Profile,SaveData,SaveCodec,SaveStore}.cs`, `Tests/Sokoban/`, `Tests/SokobanPersistence/`, associated scripts.
- Content: `Assets/Resources/Sokoban/`, `Data/Sokoban/campaign/`, import/check scripts and third-party notice updates.
- Page/input: remaining `Assets/Scripts/Runtime/Sokoban/` files; input and viewport standalone checks.
- Integration/validation: `AppController.cs`, `Assets/Editor/SokobanUiChecks.cs`, Unity/build scripts, documentation and validation report.

## Interface agreement

Content root JSON is `{ "schemaVersion": 1, "contentVersion": "sokoban-campaign-v1", "levels": [...] }`; each level follows the approved spec's schema including source, rows and solution. Original eight tutorials precede five neutral numbered chapters of twenty Boxoban levels. The compact approved map is tutorial-001 and tall approved map is tutorial-008 for direct render comparisons. All IDs and map bytes remain stable after import.

Core exposes `SokobanCatalog.Parse(string)`, `Count`, `ContentVersion`, `Get(string)` and `At(int)`; levels expose identity/title/chapter/order, dimensions, initial player/boxes, `IsWall(int)` and `IsGoal(int)`. Rules use shared `Direction`. Core/storage implementer publishes the final model API before page binding; the page can build independent geometry meanwhile.

Page integration surface follows existing pages: `Initialize(RectTransform, Action lobby, Action<Action> settings, Func<bool> canInput, Func<bool> sound, Func<bool> haptics, GameFeedback, Action<string> status)` with optional injected catalog/profile/store for isolated checks; `Enter`, `Leave`, `Suspend`, `SetForeground(bool)`, `Back`, `Save`, `Changed`, `CompletedCount`, `SaveError`, `SetHostSaveWarning(string)`. No fullscreen callback. Test fixtures must never read/write actual player saves.

## Task 1: Rules, session and persistence

- [x] Add failing standalone checks for wall/edge/two-box blocking, walking/pushing/goals, action counts, defensive snapshots, full undo, restart, terminal behavior and 50,000-action boundary.
- [x] Implement immutable levels and strict catalog import; separate static terrain from player/boxes. Replay every accepted restored history and compare all counters/positions.
- [x] Add completion deduplication and real best-moves/best-pushes tuples; one active attempt, foreground/modal gating and finite normalized viewport state.
- [x] Add failing malformed-JSON and filesystem recovery checks. Implement required-field/type/duplicate/depth/size validation, explicit content-version reset, atomic temp/backup/main writes and persistent failure state.
- [x] Run `scripts/test-sokoban.sh` and `scripts/test-sokoban-persistence.sh`; production replay all 108 solutions after content is available.

## Task 2: Content and resources

- [x] Build eight original teaching maps and verified witnesses, retaining the two approved render fixtures as real levels. Fill the remaining six with movement, covering targets, detours, ordering and multiple crates.
- [x] Import the 100 verified Boxoban candidates without changing source rows; deduplicate exact maps and retain author/source IDs.
- [x] Copy only selected Kenney sprites and short audio clips into Resources with configured Unity imports, source hashes and applicable notices.
- [x] Write reproducible output/manifests and run independent replay. Chapter names remain neutral; do not claim human difficulty calibration.

## Task 3: Approved Unity UI and controls

- [x] Reproduce the approved usable phone content in a responsive safe-area frame. Board size derives from the remaining height; tiles stay square and clipped to the board viewport.
- [x] Add level selection, help, completion, switch/restart confirmations and shared settings entry. Counts stay on one line through 50,000; long titles ellipsize.
- [x] Implement bounded 100–200% zoom, 25% buttons, fit reset, normalized center restore and resize. Add standalone viewport/gesture checks before implementation.
- [x] Implement fit single-swipe movement, zoomed drag-only behavior, anchored pinch and ownership cancellation. Second/third fingers, dialogs, resize, background and controls cannot commit pending movement.
- [x] Render production sprites with short model-first movement animation and one queued direction; honor global audio/haptics and stop on lifecycle changes.

## Task 4: Host integration and visual acceptance

- [x] Add eighth lobby card/record and lifecycle hooks without altering other game rules or saves; hide the generic footer on Sokoban and surface any save error within its measured layout.
- [x] Create isolated UGUI fixture with real camera render, raycast checks and geometry assertions at compact/long/narrow sizes. Capture tutorial-001 and tutorial-008 using the same usable frame as the prototype.
- [x] Compare reference and Unity screenshots, correct meaningful visual differences, and document font/platform rendering differences rather than claiming unmeasured pixel identity.
- [x] Run all relevant standalone suites, production content replay, Unity UI check and existing game regression checks; build a separate development APK using isolated Gradle/ADB configuration.
- [x] Check connected Android devices. If none exist, report device-only multi-touch/safe-area/performance acceptance as unverified; do not substitute desktop simulation for it.
- [x] Perform independent specification review followed by code-quality review; fix evidenced issues and rerun affected checks.
- [x] Update architecture/readme/spec status and validation record, then commit only this feature's files. Preserve the original checkout and unrelated branches.

## Execution evidence

Implemented and checked on 2026-10-28. See `docs/verification-sokoban.md` for the actual 233 Unity assertions, 83 input/layout assertions, core/persistence/content results, 20 existing standalone suites, 213 existing UGUI assertions, APK hash and physical-device installation/startup evidence. Device-only visual/multitouch/background/performance acceptance and human difficulty/audio calibration remain explicitly unverified; no release was published.
