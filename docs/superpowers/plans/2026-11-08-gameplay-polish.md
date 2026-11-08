# Gameplay polish implementation plan

**Goal:** Complete the eight requested gameplay/usability changes with existing save compatibility.
**Architecture:** Each game owns rules, settings and end states. Shared result presentation may expose generic timing/review primitives without game-specific branches. Android native audio lifecycle stays in the Kingdom Rush runtime.
**Tech Stack:** Unity/C#, .NET checks, Android JNI/SDL/OpenAL, existing Python/shell build tools.

- [x] Read source/branch state and user guardrails; create feature branch from ae7ab04.
- [x] Produce Tetris layout proposal and obtain user direction; resolve Idiom setting semantics.
- [x] Merge Snake history; adapt current lobby, all lifecycle routes, icons and existing checks; implement frame-freeze preference and legacy save compatibility. Owner: snake_recovery.
- [x] Fix Kingdom Rush native lifecycle/audio with evidence-backed callback and teardown handling. Owner: kingdom_pause.
- [x] Implement GoldMiner versioned balance, progressive shop stock/prices, existing-run compatibility and actual simulation validation. Owner: gold_balance.
- [x] Implement Idiom persistent default-on fixed action sides and adapt existing persistence/UI checks. Owner: root.
- [x] Implement Tetris four-button layout, mirrored preference and same-piece triple-tap arbitration; adapt existing input/layout/storage checks. Owner: root/assigned worker.
- [x] Implement LinkMatch category selection and per-run stable artwork mapping; adapt existing persistence/UI checks. Owner: root/assigned worker.
- [x] Restrict terminal presentation to Sudoku, Minesweeper, Sokoban, Gomoku, 2048, Watermelon, Tetris, Snake and Dino; remove this turn's additions elsewhere. Implement review/reopen without repeat scoring, stale callbacks or live input. Owner: root/assigned worker.
- [x] Confirm Android cutout/background strategy and report source/device scope.
- [x] Run necessary targeted checks and serial Unity build/UI checks; perform available scoped device regression; independent review and resolve actual findings.
- [x] Finish merge and feature commits, update current docs and exact validation limits. Do not create a new release or overwrite existing v0.9.6 artifacts as part of these source changes.
