# 旸了个旸 modes and difficulty implementation plan

> **For agentic workers:** Use subagent-driven-development, review rule contracts and persistence independently.

**Goal:** Add independent challenge/endless runs, four challenge difficulties, configurable finite tools, and a compact board with the approved icon navigation.

**Architecture:** Core owns frozen run rules, scores, quotas and solvable board generation. The strict schema-2 store owns preferences and three independent run slots; the Unity page owns selection dialogs and presentation. Existing sheep-v1 games are intentionally not migrated, per user instruction.

**Tech Stack:** Unity 6000.3.27f1, C#, .NET 10 source checks, Newtonsoft JSON, Android ARM64.

- [x] Core: add Challenge/Endless/Tutorial and Easy/Normal/Hard/Extreme, snapshot limits/remaining counts, wave and score. Test 2/3/1 exhaustion and no-op consumption first. Advance a won endless board carrying quotas, revival and score; replay generated witnesses using real picks. Keep 144 tiles, kind counts 8/12/16/16, bounded difficulty candidates and visible geometry consistent with blocking.
- [x] Persistence: strict sheep-v2 schema, nullable independent challenge/endless/tutorial slots, remembered difficulty and checkbox, quota preferences 0–99. Round-trip all modes; reject malformed, inconsistent and legacy payloads; test backup recovery and frozen run config.
- [x] UI: mode chooser, difficulty dialog that commits only on Start, settings that affect only new runs, auto-advance endless waves and standard/custom record labels. Use existing home/gear geometry; add book left of gear and two-arrow restart right of home. Remove old bottom restart/help buttons and all tray header text/counter.
- [x] Layout: compact bottom-anchored reserve/tray/tool groups with 8px gaps; board absorbs extra height. Verify actual tile bounds, hit targets, overlap and animations at tall and short aspect ratios.
- [x] Integration: source suites, Unity compilation and UI checks, Android build and interaction smoke test. Independently review core/persistence/flow invariants. Update gameplay and validation documentation with actual outcomes.

## Frozen contracts

`SheepSnapshot` retains board arrays/undo state and adds `Mode`, `Difficulty`, `Wave`, `Score`, `RemoveLimit`, `UndoLimit`, `ShuffleLimit`, and corresponding `Remaining` fields. `SheepGame.AdvanceWave()` only succeeds on a won endless board. `Score` equals actual removed cards plus 144 per completed wave. Tutorial is level 1/Normal; challenge is level 2/selected difficulty; endless is level 2/Normal. Tool changes do not modify an active run. Standard endless records require initial 2/3/1 limits.

`SheepSaveData.Game` selects its slot using `ActiveMode`. Preferences contain `DefaultDifficulty`, `SkipDifficultyPrompt`, and tool limits. Canceling a difficulty dialog does not alter preferences or create a board. Continuing a stored run does not prompt for difficulty. Selecting remembered difficulty starts directly; settings can re-enable the prompt.

## Acceptance

Navigation left-to-right is house, restart, wordmark, book, gear. There is no tray title or tray count. Both modes are accessible without repeating the tutorial. Endless clears add exactly three points, invalid tools cost nothing, waves never replenish whole-run tools. A returned/paused process resumes its same board, score and limits. Challenge new-game dialog and settings use real checkboxes and bounded quota controls. Short-screen layouts retain all controls and do not overlap system safe areas.
