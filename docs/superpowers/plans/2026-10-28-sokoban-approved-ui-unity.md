# Approved Sokoban UI — Unity Implementation Plan

> **For agentic workers:** Use subagent-driven-development for bounded implementation tasks, then independently review the complete integration.

**Goal:** Port the approved prototype at commit `597249b` into the Unity entry page, picker and game page, including all confirmed compact controls and number-only levels.

**Architecture:** Keep the existing Sokoban catalog, game, profile and atomic save contracts. The Sokoban page owns entry/game navigation; UI-only presentation maps tutorial IDs and the 100 regular IDs into the approved labels. The viewport always fits the complete board. Shared app navigation/settings and lifecycle remain owned by AppController.

**Tech Stack:** Unity 6000.3.27f1, C#, runtime UGUI, existing source sprites/font; standalone .NET and Unity Camera.Render validation.

## 1. Approved geometry and scope

- Baseline usable content is 388×784 CSS units (`Scale = rootWidth / 388`), excluding native system bars. Keep navigation 63 high, title 47.421875 high, information 45 high and the 152-high bottom controls at the safe bottom.
- Game: board background `#EBEEE2`, radius 15, 12 inner padding; no zoom, pan, fit controls or center dot. Undo `(0,24,166,48)`, restart `(0,80,166,48)`; arrows Up `(249,24)`, Down `(249,80)`, Left `(197,52)`, Right `(301,52)`, size 48.
- Entry: reproduce the intro, current-board preview, primary continue/start/next action, completion bar, horizontal chapter tabs and tutorial/number grids from the HTML/CSS. New profiles must not fabricate a started attempt. Existing saved attempts continue with the same board, counters, attempt ID and undo history.
- Tutorial cards remain height 64 with number/title/state, five columns at reference width. Regular numbers are height 44, six columns; visible groups are actual ranges 1–20 through 81–100. Tutorial 1–8 and regular 1–100 are display-only; stable IDs and source data remain unchanged.
- Picker: width 320 CSS units, 24 padding, chapter tabs, tutorial rows versus regular five-column number grid, no generic “进入” labels. Continue/current-level selection does not restart; changing an unfinished attempt retains the existing explicit confirmation.
- Entry → game only on user action. Game Back opens picker; picker Back closes it; picker link returns to entry; entry Back returns to lobby. Background/foreground preserves which view was showing. Settings/help block input on either view.

## 2. Page and presentation implementation

Owner: page implementer. Files: `Assets/Scripts/Runtime/Sokoban/SokobanPage*.cs`, `SokobanLayout.cs`, `SokobanGraphic.cs`, `SokobanBoardView.cs`; add focused page entry/selection/presentation files as needed with Unity metadata.

- [x] Add UI-only numbering/group projection without changing catalog JSON or save schema.
- [x] Add entry/game view state and an explicit `ContinueGame()` action; expose `IsEntry` for validation. On `Enter()` show entry, on foreground restore retain current view. Avoid creating a persisted fresh attempt just by opening the entry.
- [x] Build shared navigation/title plus separate entry and game content. Reproduce entry states from actual profile/session, and a proportional mini board without input or duplicate saves.
- [x] Apply approved board padding/background, stacked actions and compact dpad geometry; remove page zoom methods and zoom copy.
- [x] Rebuild the picker with the approved tabs, number grid and meaningful states; use the same labels in game/continue/result.
- [x] Keep save failure status visible in entry and game, callbacks scoped to this game and touch targets non-overlapping.

## 3. Automatic viewport and input

Owner: input implementer. Files: `SokobanViewport.cs`, `SokobanInput.cs`, `SokobanPointer.cs`, `Tests/SokobanInput/Program.cs`.

- [x] Replace zoom/pan geometry with centered automatic fit. Keep read-only `Zoom=1`, `CenterX=CenterY=.5` for compatibility with page save projection; normalize old saved view data at page load/save without discarding the trajectory.
- [x] Remove board swipe/pinch/pan behavior; direction controls keep one-press/one-release ownership. Multitouch, canceled/lost release, modal and background must never create a move.
- [x] Add meaningful regression checks for ignored board gestures, multitouch cancellation, fit at multiple board ratios, and surviving normal directional taps. Run `bash scripts/test-sokoban-input.sh` and retain evidence.

## 4. Integration verification

Owner: root. Files: `Assets/Editor/SokobanUiChecks.cs`, related host validation only where needed, documentation/screenshots.

- [x] Extend rendered UGUI checks for new-profile entry, actual saved resume/undo, settings/lifecycle, switch confirmation, picker Back, chapter ranges, zero zoom controls, geometry and real raycasts.
- [x] Test heights 1150/1320/1455/1582 at the real width-only 720 design frame, plus a narrow layout variant if supported. Include ordinary level 100 and complete tutorial/regular selectors.
- [x] Run `bash scripts/test-sokoban-ui.sh`; fix failures and inspect actual screenshots against prototype at equal usable dimensions. Produce side-by-side entry, game and picker references, including a tall board.
- [x] Run rule, persistence, input, content and existing lobby/UI regression suites. Old zoom fields must still load as valid old saves, but render fully fitted.
- [x] Independently review spec compliance then correctness/lifecycle. Resolve supported findings.
- [x] Build the isolated validation APK with `bash scripts/build-sokoban-validation.sh`, verify output/hash, update verification record and commit. Do not claim unperformed physical-device parity or publish/push.

## Acceptance evidence

Use fresh test logs, actual Unity renders and saved browser captures. Geometric agreement and successful tests are separate from physical-device testing. No mock records, disabled interactions, substitute assets, unimplemented entry actions or layout approximations are acceptable as final delivery.

Completion evidence: `docs/verification-sokoban.md`. Final Unity pass: 4,158 assertions; input: 593; existing UI: 213; content bundle reproduced exactly. Six equal-scale screenshot comparisons and the new isolated Android APK are saved. Physical-device validation is explicitly outstanding.
