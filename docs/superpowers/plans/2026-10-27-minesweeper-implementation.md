# Minesweeper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox syntax for tracking. The user authorized complete execution; continue through verification without another approval checkpoint.

**Goal:** Implement the approved classic Minesweeper design as the seventh Xanadu game, with 50–200% zoom, panning, focus layout, overview, independent saves and records.

**Status:** Production modules, routing, test fixtures and the isolated build entry are implemented. Final integrated checks, Android/device results and delivery review remain tracked below; actual results belong in [verification-minesweeper.md](../../verification-minesweeper.md). No new release is implied.

**Architecture:** Pure rules live in Core/Minesweeper. Plain C# session/profile/persistence and gesture/viewport/control-layout models live beside their Unity adapters in Runtime/Minesweeper. AppController retains routing and shared-settings ownership; the module owns its board, dialogs, saves and generated audio.

**Tech Stack:** Unity 6000.3.27f1, UGUI, existing UiFactory/UiIconGraphic/Noto CJK, C# 7.3, .NET 10, Newtonsoft.Json.

---

## Mandatory constraints

- Work only in `/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`, branch `p/duanyao.0516/feature/minesweeper`. Do not switch, reset, clean, build in, or modify other worktrees. Each command supplies this working directory. Library/Temp/artifacts belong to this checkout and must not link to another checkout.
- Normative requirements: [approved design](../specs/2026-10-27-minesweeper-design.md), [readiness audit](../../minesweeper-development-readiness.md). The latest user correction makes the overview icon four small arrows pointing inward.
- Reuse existing title/navigation geometry, Noto font, UiFactory.Ink/Muted/Paper/Accent and icon mesh convention. Screenshot comparison checks those contracts. The HTML mockup establishes board/layout interactions; it does not override existing Xanadu chrome or introduce a dark theme into this light app.
- Independent tests compile production sources. UI fixtures inject memory or temporary storage. Only build the `minesweepertest` package; do not install or launch the production package.
- Existing six games and their saves remain compatible. No new networking, SDK, scene, asmdef, release or version bump.

## File ownership and dependency order

| Unit | New files | Existing modifications |
| --- | --- | --- |
| Rules/state/storage | Core/Minesweeper types, Game and Snapshot; Runtime/Minesweeper Session, Profile, SaveData, SaveCodec, SaveStore; Tests/Minesweeper, MinesweeperSession, MinesweeperPersistence; corresponding test scripts | None |
| Input/view mathematics | Runtime/Minesweeper Gesture, Viewport, ControlLayout and value types; Tests/MinesweeperInput; test-minesweeper-input.sh | None |
| Unity page | Runtime/Minesweeper Page, Layout, Dialogs, BoardView, Pointer, IconGraphic and Feedback | AppController.cs; generic GameFeedback.Pulse only |
| Validation/delivery | Assets/Editor/MinesweeperUiChecks.cs; test-minesweeper-ui.sh; build-minesweeper-validation.sh; verification-minesweeper.md | MiniGameUiChecks.cs seventh card assertion, ProjectBuild.cs isolated build, README/docs development |

All new Assets files and directories receive unique `.meta` files. Implementers own disjoint files; shared files and final commits remain with the root agent.

## Task 1 — Rules, session/profile and strict persistence

- [x] Write and run failing production-source tests for three difficulty specs, safe first reveal, deferred RNG, flags, flood/chord, deterministic loss priority, frozen terminal states and defensive snapshots.
- [x] Implement rules using `IRandomSource.Next(maxExclusive)` and partial Fisher–Yates; each operation returns one change result. Validate snapshots before restoring, recompute neighbor counts from mines, never save UI-derived numbers.
- [x] Add failing session/profile tests for monotonic foreground time, manual/dialog/background pause restoration, stale session IDs, terminal accounting once and three independent slots. Implement the session gate separately from overview's view-input gate.
- [x] Add strict JSON roundtrip/corruption/backup/temp-file tests before implementing Codec/Store. Enforce every field and invariant in design §11; save final board/time/result/records together; errors preserve in-memory play and valid backup.

Run from the isolated root:

```bash
scripts/test-minesweeper.sh
scripts/test-minesweeper-session.sh
scripts/test-minesweeper-persistence.sh
```

Expected: initial missing-feature failures, followed by passing real production-source checks. Important concrete assertions include:

```text
Ready + ToggleFlag => Ready, generated=false, elapsed=0, RNG calls=0
Reveal(flagged) => unchanged
Reveal(first) => first and all in-bounds neighbors have no mines
Chord(equal incorrect flags) => Lost, no intervening Won, deterministic detonated index
Save/restore terminal repeatedly => records increment exactly once
Decode(zoomScale=.5) => valid; Decode(.49 or 2.01) => rejected
```

## Task 2 — Viewport and whole-frame touch arbitration

- [x] Add failing tests for anchored zoom, clamp/centering, desired-vs-rendered center, normal/focus resize, overview fit and return, whitespace hit rejection.
- [x] Implement scale `.5..2`, +/- `.25`, reset `1`, midpoint anchored transform; use actual rendered transform for hit testing and store desired center separately from layout clamping.
- [x] Add failing whole-frame touch tests: 399/400 ms; movement exactly/above 12 design units; final UP moved without MOVE; second pointer cancels pending action; UP+DOWN same frame; third pointer, canceled/missing/outside pointer; two→one pan-only; rejoin and all-up release gate.
- [x] Implement pure input state machine with no Unity dependency, one transform per input frame, initial distance threshold 12 and terminal/overview/pause permissions passed explicitly. Unity adapter converts all Input.touches before calling it and suppresses synthesized mouse duplication.

```bash
scripts/test-minesweeper-input.sh
```

Expected: each new edge case first fails, then the complete deterministic suite passes. No gesture can commit a board command after cancellation or after having two pointers in that contact sequence.

- [x] Add a pure game-local `MinesweeperControlLayout` with 44 dp target conversion, single-row preference and two-row fallback; cover 360 dp, 320 dp and short-safe-area sizing without changing global UiFactory.

## Task 3 — Xanadu page and routing

- [x] First add a Unity UI fixture that fails because the seventh entry/page and required controls do not exist. Use temporary/in-memory saves, not normal player data.
- [x] Implement selection, normal board and focus board using existing UiFactory methods; title size 49/Ink/Bold and navigation geometry follow Sudoku. Use the existing Back/Restart/Book/Settings icon mesh. New local game symbols follow the same line thickness, color and centering; overview contains four inward arrows.
- [x] Implement BoardView with one clipped board input surface, original flag/mine/wrong-flag graphics, revealed digits and stable terminal presentation. Pointer owns all cell gestures, without per-cell buttons competing with a ScrollRect.
- [x] Implement persistent reveal/flag mode, zoom controls, readonly overview and focus toolbar; resize keeps desired board center, controls do not cover the board, compact save errors remain visible. Pause cover is opaque at every scale/layout.
- [x] Implement help, first-use guide, restart confirmation and result actions; apply design §7 state-specific restoration and hardware Back priority. Local generated audio owns and releases clips, uses the shared independent sound/haptic settings.
- [x] Wire initialization, seventh lobby card, Enter/Leave/Suspend, shared settings, Save/errors, end-state and focus/pause callbacks in AppController. Close shared modal on background directly without its user-dismiss resume callback. Add generic Pulse wrapper without game-specific logic in GameFeedback.

## Task 4 — Integration checks and isolated Android build

- [x] Run all four Minesweeper independent suites; run existing shared core/input/persistence and affected game/UI regressions. Resolve failures rather than weakening tests.
- [x] Run `scripts/test-minesweeper-ui.sh` and `scripts/test-minigame-ui.sh` sequentially in this checkout with graphics enabled. Inspect captured normal/focus/overview/pause/selection/result screens at 720×1320 and 720×1582, actual raycast dispatch and typography/color assertions.
- [x] Add `ProjectBuild.BuildAndroidMinesweeperValidation` and its script using the existing Watermelon try/finally wrapper, restoring package/product settings on every exit.
- [x] Build and verify the isolated Android package with the following command only:

```bash
scripts/build-minesweeper-validation.sh
```

Expected: nonempty `artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`, package `com.duanyao.xanaduyonder.minesweepertest`, version 0.4.0/code 4. Keep build logs and screenshots under ignored artifacts.

- [ ] Inspect connected physical devices; where available, install/launch only this test package and verify taps, hold/release, true multitouch, focus/overview/back, background/lock and persistence. Record exactly which scenarios were exercised; no synthetic input claim substitutes for physical multitouch.
  Device detected; isolated install waited while PKM110 was locked, then exited 1 without a detailed error. User unlock/confirmation and a retry are pending. Device scenarios remain unverified.
- [x] Run independent spec compliance review, fix findings, then code-quality review and fix findings. Re-run the directly affected checks after fixes.
- [x] Update user-facing gameplay/development/verification docs with actual commands, outcomes and remaining device limitations. Check `.meta`, diff whitespace, branch/worktree isolation and no unintended ProjectSettings changes. Commit scoped changes; do not merge/publish without an explicit request.

## Verification evidence

Implemented steps are marked above. Completed commands, outcomes and remaining limits are recorded in [docs/verification-minesweeper.md](../../verification-minesweeper.md); a checked implementation step does not certify integrated UI, Android or device behavior. Other checkouts remain outside this work; subsequent checks are limited to this worktree and its master baseline.
