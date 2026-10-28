# Link Match Implementation Plan

> **For agentic workers:** Use subagent-driven-development to implement the bounded tasks below, with spec and code review before delivery. User approved the design and UI on 2026-10-28; continue through implementation and verification without another design gate.

**Goal:** Implement the approved 连连看 design as the thirteenth offline Unity game, preserving the approved prototype's UI and all real game states.

**Architecture:** Pure Core owns connections, board mutations and solvable generation. Unity-independent Runtime model owns effective time, six slots, results and strict persistence. A dedicated UGUI page owns presentation and a single pointer submission path; AppController supplies navigation, settings and lifecycle.

**Tech Stack:** Unity 6000.3.27f1, C#, UGUI, Newtonsoft.Json, existing dotnet console test harnesses, official Fluent PNG assets.

## Approved references and fixed behavior

- `docs/link-match-research.md` is the complete rule/product contract; `docs/prototypes/link-match/` is the approved visual reference. Reference sizes: 388×784 and 360×640 safe content.
- Relaxed/challenge selected before play; 24/36/48 tiles, 6/9/12 patterns each four times, static board, two turns maximum including outside ring. Challenge 90/120/150 seconds with 3 hints and 2 manual shuffles. Automatic no-move recovery is free and pauses effective time.
- Preview never creates an attempt. Six independent mode×level slots. Existing attempts resume; restarting requires confirmation. Terminal capture is idempotent; next level resumes its existing slot; final level has no next action.
- Pause/help/settings/background conceal the entire board, stop effective time and require explicit continuation. Commit last pair only while time remains; save terminal state before presentation.

## Shared interfaces

Core namespace `XanaduYonder.Core.LinkMatch`:

```csharp
public enum LinkMatchMode { Relaxed, Challenge }
// LinkMatchLevel.Get(int number), Get(string id), All; Number, Id,
// Columns, Rows, Kinds, ChallengeSeconds. IDs: level-01, level-02, level-03.
// LinkMatchPoint: X, Y; outside ring coordinates may be -1 or Columns/Rows.
// LinkMatchPath: Points (corners including endpoints), Turns, Steps.
// LinkMatchPair: First, Second, Path.
// LinkMatchGame(level, int[] tiles): -1 means empty, fruit IDs are 0..Kinds-1.
// Game.Level, Remaining, Won, TileAt(index), GetTiles() (defensive copy).
// Game.TryMatch(first, second, out LinkMatchPath), FindMatch() -> pair or null.
// Game.Shuffle(uint seed): preserve empty mask and remaining multiset.
// LinkMatchGenerator.Generate(level, seed), Reshuffle(level, tiles, seed)
// return LinkMatchDeal with Tiles and Solution (ordered pairs of indices).
```

Runtime model namespace `XanaduYonder.Runtime.LinkMatch`:

```csharp
public enum LinkMatchOutcome { Playing, Won, TimedOut }
// LinkMatchSession(level, mode, uint seed); restoration from SessionData.
// Game, Mode, Outcome, ElapsedSeconds, RemainingSeconds, HintsUsed,
// ShufflesUsed, HintsRemaining, ShufflesRemaining, ResultRecorded, AttemptId.
// Enter(), Leave(), SetForeground(bool), SetBlocked(bool), Tick(double).
// TryMatch(int,int,out LinkMatchPath), TryHint() -> LinkMatchPair or null,
// TryShuffle(), EnsureMove() -> bool (whether automatic reshuffle happened).
// CanInteract, IsTerminal; Export() -> defensive SessionData.
// LinkMatchProfile: new or restore from SaveData; GetSlot(mode,levelId),
// GetLatestActive(), Start(mode,levelId,seed,bool fresh=false), Capture(session),
// GetRecord(mode,levelId), CompletedCount, Export(). Latest has only playing slots.
// LinkMatchSaveCodec.Encode(data), TryDecode(json,out data,out error).
// LinkMatchSaveStore(directory): Load(out warning), TrySave(data,out error), SavePath.
```

## Task 1 — rules, path finding and generation

Files: `Assets/Scripts/Core/LinkMatch/*.cs`, `Tests/LinkMatch/`, `scripts/test-link-match.sh` and Unity metadata.

- [x] Write failing tests for straight/L/Z/U/outside paths, blocked and >2-turn paths, wrong fruit/same index/empty cells, deterministic fewer-turn then shortest paths.
- [x] Implement a bounded orthogonal search over the board plus one empty outer ring. Return actual corner coordinates for the view.
- [x] Generate layouts with an explicit complete removal witness; validate by replay through the production matcher. Randomize pair placement and fruit assignment; reshuffles preserve empty positions and exact fruit counts. No unbounded random retry or search-timeout-as-impossible logic.
- [x] Exercise varied seeds and sparse masks; test empty boards, last pair, defensive copies and no mutation on rejected matches.
- [x] Run `scripts/test-link-match.sh`, retain red/green results, and review contract compliance before integration.

## Task 2 — session, profile and transactional storage

Files: `Assets/Scripts/Runtime/LinkMatch/LinkMatchSession.cs`, `LinkMatchProfile.cs`, `LinkMatchSaveData.cs`, `LinkMatchSaveCodec.cs`, `LinkMatchSaveStore.cs`; `Tests/LinkMatchPersistence/`, `scripts/test-link-match-persistence.sh`.

- [x] Test effective time under enter/leave/pause/foreground, exact expiry, last-pair win, quota consumption, no-move recovery and stale/terminal submissions before implementing these transitions.
- [x] Test independent six-slot resume, preview without attempts, latest unfinished slot, replacing only the chosen slot, idempotent completion and paired best-time/assistance records.
- [x] Persist `link-match-v1.json` with schema/game/content IDs, stable level IDs, attempt IDs, board, RNG state, counters, clock, terminal/record flags and six slot records. Validate real JSON token types before object conversion, including missing/null/unknown/invalid values and cross-field invariants.
- [x] Use bounded UTF-8 reads, temporary flush and atomic replacement; preserve only validated previous primary as backup; archive corrupt input and recover valid backup; report write failures without losing in-memory session.
- [x] Run persistence tests against actual temp directories, malformed JSON, main/backup corruption and storage failure. Never write another game's files.

## Task 3 — approved UGUI page and assets

Files: `Assets/Scripts/Runtime/LinkMatch/LinkMatchPage*.cs`, `LinkMatchBoardView.cs`, `LinkMatchGraphic.cs`, `LinkMatchInput.cs`, `LinkMatchPointer.cs`, `LinkMatchLayout.cs`, `Assets/Resources/LinkMatch/`, `THIRD_PARTY_NOTICES.md`.

- [x] Port paper/green colors, local Noto font, nav icon geometry, 19.5px margins, entry hierarchy and exact prototype spacing into width-scaled safe content. Use responsive short-screen compression and nonoverlapping square board targets.
- [x] Import the exact twelve approved PNGs with stable IDs, bilinear/clamped nonmipmapped textures; preserve MIT copyright/license and source hashes. Do not change the design prototype to conceal implementation differences.
- [x] Entry has latest active card, mode tabs and three direct-start/resume level cards. Browsing creates no attempt. Display actual progress and records instead of prototype fixtures.
- [x] Game shows level, mode, remaining tiles, actual clock, board, real path feedback and grouped hint/shuffle/pause tools. Selected tile/cancel/reselect/invalid feedback follow the approved rules.
- [x] One pointer owner commits only on release on the same tile; slide-out, second pointer, disable, resize, popup and lifecycle transitions cancel the gesture. No delayed callbacks mutate state.
- [x] Implement pause/help/settings/restart/timeout/win/last-level dialogs, opaque board concealment, explicit resume and persistent separately routed save errors. Terminal persistence precedes any visual feedback.
- [x] Add meaningful layout/input tests and Unity render assertions for both safe sizes, maximum board, outer route and save-error occupancy.

## Task 4 — host integration and baseline test repair

Files: `Assets/Scripts/Runtime/AppController.cs`, `Tests/Core/CoreTests.csproj`, relevant host checks, README and architecture docs.

- [x] Add the thirteenth lobby card using existing dynamic card layout; preview is decorative and button opens entry.
- [x] Build and enter with `SafeDesignFrame.WidthOnly=true`, restoring the prior policy after build. Wire Leave/Save/Suspend/Back/CheckEndState/foreground and common settings errors in every real host path.
- [x] Keep other games' save errors in the lobby; page displays its own and common settings errors. Restore normal host layout after leaving and test return from landscape games.
- [x] Reproduce CoreTests failure and add the same Newtonsoft version used by existing Sokoban tests as the narrow test-project dependency repair; do not refactor unrelated games.

## Task 5 — review, rendering and delivery

Files: `Assets/Editor/LinkMatchUiChecks.cs`, `scripts/test-link-match-ui.sh`, `scripts/build-link-match-validation.sh`, `docs/verification-link-match.md`, `docs/screenshots/link-match-*`.

- [x] Run new focused tests, then all applicable existing test scripts once; investigate actual failures with bounded fixes.
- [x] Independent spec review followed by code-quality review. Resolve demonstrated findings and rerun affected checks.
- [x] Compile and render Unity at the prototype's same safe area and deterministic review fixture; save entry/game/state screenshots and compare spacing, typography, fruit bounds, buttons and clipping.
- [x] Build Android validation APK using the project's supported path. If a device/emulator is available, install and exercise actual input, save/relaunch, pause/background and landscape return; report the actual device layer reached.
- [x] Update design status, implementation/asset docs and verification evidence. Record remaining device limitations honestly; no prototype-only gameplay, no stubs and no claims based solely on builds.

## Working tree and delivery scope

Branch: `p/duanyao.0516/codex/link-match-research`; base `0f6e4fd`. Keep existing approved research/prototype files. Do not edit the original checkout, merge into master, push or publish a release in this task. Local commits may capture completed implementation after verification.

## Completion evidence — 2026-10-28

All implementation and available automated/render/build checks above are complete; see `docs/verification-link-match.md`. The conditional device step reached a read-only ADB inventory with no connected device. Android installation, physical touch, device relaunch durability and performance remain unverified and are not represented as passing.
