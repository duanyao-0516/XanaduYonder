# Game entry transitions

User requested merging remote master and eliminating unnecessary intermediate pages before games. Merge `origin/master` at `3cad77a` is preserved in `e062a19`; nine games coexist, with Minesweeper07, Zuma08 and Dino09. Only this existing worktree is modified.

Audit: 2048, Jump, Sheep, Watermelon, Sudoku and Minesweeper open their actual game or selection screen directly. Dino replaces the page with an adjustment label while waiting for orientation. Zuma hides the host before creating its own canvas, and destroys its own canvas before waiting for portrait on exit. Idiom displays genuine asynchronous dictionary loading inside its existing page; its 42,851-word parsing and saved-game validation must remain asynchronous, with errors visible.

- [x] Dino: immediately show real direction cards on entry, retain the current page during actual rotation, disable its controls while waiting, and replace it only after direction/safe-area stability. Keep cancellation, timeout and foreground checks. Add failing actual-page checks, then fix.
- [x] Zuma: retain host canvas until the complete game/selection/error screen is ready; then notify host to hide it. Disable host raycasting while waiting. On exit keep the paused Zuma canvas until portrait handling completes, then hand back to the lobby. Preserve navigation tokens and error paths. Add a failing navigation check, then fix.
- [x] Verify independent source review and real Unity page/navigation/shared lobby checks. Check all nine lobby cards and merged save/lifecycle dispatch.
- [x] Build the isolated Dino APK, overwrite install to OPPO preserving records, verify entry/back/rotation behavior and document the actual scope of validation.

No artificial delay, replacement loading page, synchronous dictionary load, or additional game rule changes are introduced. Android rotation itself is asynchronous and still requires its real readiness checks.

Completion: merge e062a19 includes master3cad77a. Dino page24/543, Zuma navigation9/39, shared UI231 all pass. Build succeeded and OPPO overwrite installed at2026-10-28 14:45:16; records544/539 preserved byte-for-byte. Device entry, Zuma return and landscape Dino background/resume verified. See docs/verification-dino.md for evidence and limits.
