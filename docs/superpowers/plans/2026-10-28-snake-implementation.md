# Snake Implementation Plan

> **For agentic workers:** Use subagent-driven-development with test-driven-development and independent review. Track execution below; the user approved the design and requested implementation in this session.

**Goal:** 在现有 Unity 安卓合集接入可玩的经典贪吃蛇与 12 关障碍模式，严格保持已批准的纸色/墨绿 UI，等级下拉采用数独样式。

**Architecture:** 纯 C# 规则和会话拥有蛇、随机状态、节拍、暂停与终局；独立 profile/codec/store 拥有六组经典续局、闯关续局与纪录。Unity 页面仅负责呈现和输入，AppController 只增加导航、生命周期及独立摘要。

**Tech Stack:** Unity 6000.3、UGUI、C# 7.3、现有 Newtonsoft.Json 13.0.2、.NET 10 独立测试、Unity Play Mode UI 截图。

## Scope and responsibility

- Worktree: `/Users/bytedance/.codex/worktrees/snake-design/XanaduYonder`, branch `p/duanyao.0516/feature/snake`.
- Approved design: `docs/superpowers/specs/2026-10-28-snake-design.md`; visual reference `docs/snake/ui-preview.html`.
- Production board remains 20×24. No fullscreen, zoom, hints footer or persistent reverse-direction explanation.
- Shared colors/navigation come from UiFactory. The new selector mirrors Sudoku's warm option surface and selected band without changing Sudoku behavior.
- Do not modify existing game save schemas, import candidate image packs, publish, push or merge.

## Task 1: Deterministic rules and session

**Create:** `Assets/Scripts/Core/Snake/{SnakeRules,SnakeSnapshot,SnakeGame}.cs`, `Assets/Scripts/Runtime/Snake/{SnakeSession,SnakeSessionData}.cs`, `Tests/Snake/Program.cs`, `Tests/Snake/SnakeTests.csproj`, `scripts/test-snake.sh` (plus Unity meta files).

- [x] Write failing tests for four wrap directions, solid edges, wrap landing body/obstacle, growth, vacating tail, food uniform-index selection, terminal no-op and final-cell victory before spawning food.
- [x] Write failing session tests for two queued turns, rejected reverse/duplicates, one turn per tick, exact speed thresholds, pause clearing input, three-second resume countdown and >250 ms/backlog auto-pause.
- [x] Implement immutable rules and defensively copied snapshots. Cell indices are `y * width + x`; rules hold obstacle indices and initial ordered body. Xorshift RNG state is part of the snapshot. `Step` commits a single cell transition, with full board before campaign target.
- [x] Run `scripts/test-snake.sh`; all assertions must pass against production C# source, including 480-cell final food and obstacle capacity.

```csharp
var game = new SnakeGame(SnakeRules.Classic(1, SnakeBoundary.Wrap), 123u);
var session = new SnakeSession(game);
session.Resume();
session.Advance(0.1); // countdown does not move or add active time
session.Pause();     // clears queued inputs and accumulator
```

## Task 2: Catalog, profile and real disk contract

**Create:** `Assets/Scripts/Runtime/Snake/{SnakeCatalog,SnakeProfile,SnakeSaveCodec,SnakeSaveStore}.cs`, `Assets/Resources/Snake/levels.json`, `Tests/SnakePersistence/{Program.cs,SnakePersistenceTests.csproj}`, `scripts/test-snake-persistence.sh`.

- [x] Add strict catalog parsing from the approved coordinate data; reject unknown versions/enums, duplicate/out-of-range walls, invalid spawn, disconnected areas and invalid targets/ticks. Keep the exact pack version and all 12 maps.
- [x] Add failing tests for six independent classic slots, one campaign slot, per-rule records, monotonic unlocks and terminal settlement exactly once.
- [x] Add failing actual-JSON tests for missing/null/wrong token types, duplicate keys, invalid body adjacency including wrap, score/food/length mismatch, occupied food, bogus full-board win, invalid RNG/state and incompatible content versions.
- [x] Implement `snake-v1.json` with flush + atomic replace and valid backup recovery; preserve incompatible files and valid records, make errors visible to the page. Restore sessions paused, never serialize input/animation state.
- [x] Run `scripts/test-snake-persistence.sh` using a temporary directory and the real Newtonsoft parser; confirm corrupted main recovery and old main preservation when writing fails.

## Task 3: Approved Unity UI and input

**Create:** `Assets/Scripts/Runtime/Snake/{SnakePage,SnakePage.Layout,SnakePage.Dialogs,SnakeBoardView,SnakeInput,SnakeDropdown,SnakeIconGraphic}.cs`.

- [x] Add Unity UI checks before implementation for 720×1320 and taller safe-frame layouts, navigation coordinates, full board visibility, right D-pad, left tools, absence of fullscreen/footer, and Sudoku-style dropdown options.
- [x] Use navigation x56/x536/x606, y34, 58-square buttons; centered brand; title on following row. Lay out board at x56/y240/608×729.6 and tools at y1000 with safe-frame responsive vertical spacing.
- [x] Draw beige tiles, blue-grey brick obstacles, green directional snake and red fruit with production UGUI geometry; wrap interpolation must split at edges rather than crossing the board.
- [x] Add actual mode and difficulty/level selection, preview rules/target/speed for campaign before entry, locked levels, and explicit replacement confirmation. Keep both modes visually consistent.
- [x] Direction button pointer-down, continuous swipe threshold and keyboard share session queue. Reject second-finger interference; cancellation/lifecycle/re-layout resets gesture and queue.
- [x] Add pause/countdown/help/settings, restart and boundary confirmation, win/loss/full-board dialogs and next level. Save at start/eat/terminal/pause/leave/background plus 2-second checkpoints. Surface save errors without restoring a normal footer.

## Task 4: Shell integration and regression

**Modify:** `Assets/Scripts/Runtime/AppController.cs`, `Assets/Editor/MiniGameUiChecks.cs`, `README.md`, `docs/architecture.md`.
**Create:** `Assets/Editor/SnakeUiChecks.cs`, `scripts/test-snake-ui.sh`, `docs/snake/verification-implementation.md`.

- [x] Add an eighth lobby card at the existing 350 height/24 spacing, independent page and record summary.
- [x] Enter/leave every game path suspends Snake, settings and foreground transitions pause it, back delegates to its modal/selection policy. Shared settings retain their existing file.
- [x] Run all new independent tests and existing core/input/persistence + Sudoku selection/profile + shared UI checks relevant to the integration.
- [x] Execute Unity Play Mode checks and inspect emitted PNGs for classic, obstacle, selector, pause and terminal states; verify layout and callbacks using actual UGUI objects.
- [x] Produce an isolated `.snaketest` Android validation APK if the installed toolchain supports it; record compilation, UI runtime and device validation separately. Do not claim Android touch validation from editor screenshots.
- [x] Independent spec and quality review; fix evidence-backed findings and rerun affected checks. Update verification record with exact results and remaining physical-device limitations.

## Acceptance examples

```text
20×24 empty board + initial length 4 + 476 food => WonFullBoard, score 4760, no further RNG call.
Active classic wrap140/length18 -> boundary dialog -> cancel => same board/rule/score, paused.
Confirm new solid boundary => a distinct session, length4/score0, previous rule slot retained.
Background or settings -> no ticks -> foreground => paused -> continue -> 3,2,1 -> movement.
Campaign 08 => 56 obstacles, target22, solid boundary, read-only boundary switch.
```

## Execution result

2026-10-28: Tasks 1–4 completed in this worktree. See [implementation verification](../../snake/verification-implementation.md) for exact commands, counts, screenshots and APK metadata. Independent reviews found and verified fixes for snapshot invariants, persistence settlement validation, multi-tick rendering, repeated recovery warnings and same-frame touch cancellation. Android compilation is complete; physical-device play, full 12-level human balancing and release publication remain outside this verification result. Changes remain uncommitted on the feature branch.

## UI refinement after OPPO feedback

The user's six corrections supersede the original baseline geometry above: use a rounded mode button; classic selector width 164 (campaign 260); 25-unit labels with bold 32-unit values and the label 长度; remove the D-pad center marker and move Up/Down inward by 48 units; anchor wrap icons to actual board-edge centers with centered rotation; clip the board/preview and draw a 16-unit rounded border. The board is now 648×777.6 at baseline x36/y252, with a compact control layout at band top1032. Preserve the shared navigation, 20×24 rules, 96×96 directional hit targets and all save data. Validate rendered geometry and existing interaction flows at both reference heights, rebuild the isolated APK and update the previously authorized OPPO installation.

## Interaction flow refinement

The subsequent whole-flow review supersedes the original paused entry after explicit starts. See [interaction-flow.md](../../snake/interaction-flow.md) for the complete route policy. Explicit lobby entry, difficulty selection, new game, resume and next level enter the countdown directly; lightweight overlays preserve their origin, destructive cancellation and system interruptions preserve pause. Stage-preview Back returns to its selector. Round-ending effects belong to the actual state transition. Board top is now268 with extra-height share0.45 to balance vertical whitespace. Targeted real-UGUI RED/GREEN checks cover both the redundant start pause and delayed dropdown-close lifecycle.
