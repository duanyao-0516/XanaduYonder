# Bomb It life investigation and Tetris result polish

**Goal:** Establish whether Bomb It's extra life works through the original death/retry chain, and replace Tetris's full-page result card with an arena-only presentation consistent with pause/countdown.

**Scope:** Preserve the current worktree and previous changes. Do not change Bomb It's original rules without reproduced evidence of a defect. Keep Tetris scoring, saves, result delay and board inspection behavior. The user's final instruction stops further work, defers Bomb It 7 and requests committing completed changes; no further package build or phone operation is performed.

## Bomb It

- [x] Trace original pickup, damage, life decrement, result and retry scripts; compare current sanitized SWF action bodies with the original.
- [x] Exercise the real SWF in desktop Ruffle using an artifacts-only diagnostic fixture. Verify the actual extra-life pickup, damage, remaining-life result and same-stage retry; keep test controls out of production assets.
- [x] Explain reserve lives versus damage protection from observed behavior. No gameplay defect was reproduced. Add the explicit 1→2→1 life and “重玩此关” example to the in-app guide, as requested.

## Tetris

- [x] Add an arena-only result layer in `TetrisView.cs`: “本局结束”, prominent current score, lines and best score, then “再来一局” and “查看棋盘”. Use the arena's existing rounded mask and .44 dim color. Existing top Home returns to the lobby.
- [x] Route only the Ended presentation in `TetrisPage.cs` to the new layer; preserve Ready/error cards, pause/countdown, result delay and inspect/reopen state machine.
- [x] Update existing `TetrisUiChecks.cs` for arena bounds, geometry, long scores, input gating and actual result actions; render both supported check sizes and inspect images.
- [x] Independently review changes and run focused desktop checks. Tetris passed 949 assertions at 388×784 and 388×688; Bomb It guide/feedback integration passed. No new APK was built after these final result/guide changes, following the user's stop-and-commit instruction.

Evidence: `artifacts/bomb-it/life-review/report.md`, `artifacts/bomb-it/life-diagnostic/result.json`, `artifacts/validation/tetris-ui/report.txt` and screenshots. The life fixture freezes unrelated AI and places an original pickup, then uses the original pickup/damage/gameover code and real retry button; it does not assign lives or directly invoke gameover/retry. Production gameplay bytes remain unchanged. The first Tetris layout check caught a 60px font line in a 59.381px title box; the result layer now accounts for rounded font metrics before positioning the remaining content. The final check passes.

Bomb It 7 research files remain only in ignored `artifacts/bomb-it-7/`. No 7-specific code, resources or build entries are included in the commit. Commit completed work on the current branch; no merge, push, release or iOS build.
