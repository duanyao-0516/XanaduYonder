# Sudoku selection and per-difficulty saves implementation plan

> **For agentic workers:** Use subagent-driven-development to implement this plan. Keep every change in the existing sudoku-game worktree.

**Goal:** Preserve all 2,000 puzzles and their numbers, retain one unfinished game per difficulty, simplify level selection, and add a book-shaped rules entry beside settings; install the verified development APK on the test phone.

**Architecture:** A Sudoku-only profile owns four optional slots and global completion records. Existing session snapshots keep their rules, undo history and paid hints; a versioned profile store imports the legacy single save once. The page selects or replaces only the selected difficulty's slot, and a pure selection policy supplies the primary action and replacement decision.

**Tech Stack:** Unity 6000.3.27f1 / uGUI, C# 7.3, existing Newtonsoft JSON, .NET console tests, Android ARM64.

## Boundaries and acceptance

- Worktree: `/Users/bytedance/.codex/worktrees/sudoku-game/XanaduYonder`, branch `p/duanyao.0516/feature/sudoku-game`. Do not change other worktrees, branches or Unity caches.
- Bank SHA256 before changes: `4dbe98f4eff574129e8e723a54d19bff416843f7e6400c4f0fc2e031fb8b2f71`. No puzzle or grading edits.
- Each difficulty has zero or one unfinished session. Entering a board creates a slot even before typing; merely opening the selection page or dropdown does not.
- New game in another difficulty preserves all existing slots. Choosing a different level within an occupied difficulty confirms replacement; cancel leaves every slot unchanged.
- Finished games release their slot; completion/independent-completion records survive. Main action is exactly Start, Continue or Replay with a level number, scoped to the dropdown.
- Native uGUI dropdown at left and action at right, same row. Fifty levels per page; current-game badge takes priority over an old completion badge. Previous/next are drawn chevron icons with disabled end states and comfortable hit targets.
- Book icon beside settings on both selection and board opens scrollable rules. Rules opened from play hide the board and stop time, then explicitly return to the same game; selection rules do not create a game.

## Task 1 — profile and compatibility (save agent)

Files: new `Assets/Scripts/Runtime/Sudoku/SudokuProfile.cs`, `SudokuProfileCodec.cs`, `SudokuProfileStore.cs`, their metas; `Tests/SudokuProfile/`, `scripts/test-sudoku-profile.sh`.

- [x] Add failing tests: capture four different difficulties and replace one; roundtrip undo/time/notes/paid hint; terminal captures release only one slot; global completion remains; copies do not alias.
- [x] Add failing compatibility tests: migrate active v1, terminal v1, untouched default v1; v2 takes precedence; corrupt primary recovers backup; corrupt v2 never resurrects legacy v1; invalid write preserves existing files.
- [x] Implement `SudokuProfile`: `Slots[4]`, `CompletedIds`, `UnassistedIds`, `SelectedDifficulty`, `Pages[4]`, `GetSlot(d)`, `Capture(data)`, `CreateGame(puzzle)`, `Copy()`, `TryValidate(out error)`.
- [x] Implement strict schema 2 at `sudoku-v2.json`, validated nested existing snapshots, bounded I/O and atomic backup. Keep `sudoku-v1.json` untouched after import.
- [x] Run `scripts/test-sudoku-profile.sh`; report red/green evidence.

## Task 2 — selection policy and page (root)

Files: new `SudokuLevelSelection.cs`, `SudokuIcon.cs`, `Tests/SudokuSelection/`, `scripts/test-sudoku-selection.sh`; modify `SudokuPage.cs`, `SudokuPage.Levels.cs`, `SudokuPage.Dialogs.cs`.

- [x] Write selection-policy tests for start-first-unfinished, continue selected difficulty, replay after all complete, no cross-difficulty replacement, and same-level resume.
- [x] Implement the policy with slot existence, never revision/time as the started signal.
- [x] Wire profile load/save and validate each saved puzzle against the unchanged bank. Keep a non-persisted preview session until a board is entered; do not capture that placeholder.
- [x] Draw icons using uGUI geometry. Add a difficulty dropdown, per-difficulty page memory, compact summary, grid and circular chevron controls. Save navigation without starting the timer.
- [x] Add top book entries and scrollable opaque rules, preserving pause/background semantics and cancelling stale hint work on session switches.
- [x] Run profile/selection/session/core tests, then independently review requirements and data/lifecycle correctness.

## Task 3 — build and delivery (root)

- [x] Run existing game regression scripts and verify unchanged bank hash. Build only this worktree with `ADB_SERVER_SOCKET=tcp:localhost:5041 ANDROID_ADB_SERVER_PORT=5041 scripts/build-android.sh`.
- [x] Use only isolated validation emulator `emulator-5562`: test old-save import, all four slots, replacement/cancel, dropdown/paging, icons/rules, timer/background, terminal record and restart recovery. Do not use another AI's emulator.
- [x] Back up the physical phone's current four-game files and installed APK, verify matching signing certificate, install with `adb -s 3B6F4NE8A66ZXTRL install -r` (never uninstall/clear).
- [x] Verify all old-game save contents and migrated Sudoku game, inspect the new page and rules on the phone without resetting user's progress.
- [x] Update current docs and add dated verification evidence. Commit only this task's files locally; no remote push or merge.
