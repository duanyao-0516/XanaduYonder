# Game UI feedback corrections

**Goal:** Correct the Level Devil settings glyph, simplify Tetris pause/countdown inside its full arena, and give Snake's disc visible pressed feedback.

**Scope:** Keep all existing work and game/save behavior. Root owns the Level Devil SVG; separate tasks own Tetris view/page and Snake disc/input. No phone gameplay or unrelated visual changes.

- [x] Replace the Level Devil hand-written outline with the existing native settings gear geometry; inspect the real menu at its rendered size.
- [x] Confine Tetris pause and countdown to the rounded arena containing Hold, board and Next; use a compact paused label/actions and keep original ready/end/error overlays unchanged. Verify geometry, clipping, input gating and real screenshots.
- [x] Shade the active Snake disc sector and clear it on drag-out, neutral center, release, cancelled ownership, pause and disable. Verify real mesh/pointer behavior and capture held/released screenshots.
- [x] Run focused existing desktop checks, independently review the changes, build Android debug and cover-install on OPPO under the user's standing instruction. Do not launch or operate the game.

## Desktop evidence

- Level Devil: `check-menu-settings.cjs` passed five groups with the production web engine; native feedback ACKs are mocked. The actual 20px gear was inspected in `artifacts/level-devil/menu-settings/map-with-tools.png`.
- Tetris: `test-tetris-ui.sh` passed 385 assertions, including actual UGUI hit testing of Continue, arena geometry, rounded clipping and disabled gameplay controls. Inspected pause/countdown screenshots at 388×784 and 388×688 in `artifacts/validation/tetris-ui/`.
- Snake: `test-snake-ui.sh` passed 21,477 assertions, including mesh colors, drag, release, OS cancellation, multiple pointers and lifecycle cleanup. Inspected released/held disc screenshots at 720×1320 and held at 720×1582 in `artifacts/validation/snake-ui/`. Core 42 and persistence 211 checks also passed.
- Independent read-only review found no blocking issue in these three corrections. These checks do not establish phone touch behavior; the user will validate on OPPO.
- Build inputs are frozen in `artifacts/game-ui-feedback/build-inputs.json`. The Android incremental data manifest was moved to that directory's `cache-backup/` before building so the changed Level Devil AAR is repacked and checked.

## Android delivery

`./scripts/build.sh android --configuration debug` exited 0; bundled runtime/resource checks passed. The resulting `artifacts/builds/XanaduYonder-0.9.7-debug.apk` is 233,736,340 bytes, SHA-256 `8f447e3bfd38fd780220f7dc5183b469a9fd66b8d8c45bc3035a4446103a923b`. OPPO PKM110 replacement install returned `Success` using `adb install -r`. No phone game was launched or operated. Source hashes matched after the build, and ProjectSettings has no diff. Exact build/install evidence is in `artifacts/game-ui-feedback/build-result.json` and `install-oppo.log`.
