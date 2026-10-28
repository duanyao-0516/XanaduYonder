# Snake rules and session checks

Run `scripts/test-snake.sh` from the repository root. The project compiles production sources directly with .NET 10, C# 7.3, warnings as errors, and no test package or Unity dependency.

## RED / GREEN evidence

- Initial RED: the five public API skeletons compiled, then all 31 behavior checks failed because the implementation was absent (`NotImplementedException`). Exit code: 1.
- Initial GREEN: implemented rules, snapshots and session; all 31 checks passed. Exit code: 0.
- Regression RED: added a custom 6 ms tick that becomes 1 ms after the fifth food. An 8 ms frame incorrectly committed three ticks because preflight skipped frames shorter than twice the old interval. Result: 31 passed, 1 failed (`3 != 0`). Exit code: 1.
- Regression GREEN: preflight simulates all potentially due ticks on isolated state before changing the real game. Result: 32 passed, 0 failed. Exit code: 0.
- Review RED: five checks exposed non-lost heading mismatches, inconsistent right-facing initialization, food retained after loss, campaign food beyond an earlier winning target, and per-tick clone/validation allocations. Result: 32 passed, 5 failed. The allocation fixture measured 519,200 bytes over 100 ordinary ticks.
- Review GREEN: added heading/loss/target invariants and cached the minimum interval, skipping preflight unless a third tick is possible. Result: 37 passed, 0 failed, including the 6-to-1 ms regression. Ordinary 100-tick allocations must stay below 20,000 bytes.

The shortened tick is a test fixture, not a production difficulty. Production classic starts at 240/200/160 ms and stops accelerating at 160/100/80 ms; campaign uses a fixed interval. Initial body geometry must support the fixed right-facing start, including across wrap edges. Non-lost snapshot headings must point from neck to head; a lost snapshot keeps the attempted collision heading and has no food. Campaign snapshots cannot contain food eaten after an earlier target would already have ended the game.

## Covered contracts

Four-way wrap and solid edges; normalized landing collision against obstacles and body; vacating-tail movement; immediate growth and ten-point score; full-board victory before target and without another RNG call; no-op terminal ticks; food selection from free-cell ranks; deterministic snapshot replay and defensive copies; strict snapshot occupancy/counters/state checks; classic speed thresholds; paused construction/restoration; three-second countdown; two-entry direction queue; one turn per tick; partial frame accumulation; atomic stall/backlog pause; terminal elapsed time; one-time result marker.

`SnakeRules` is a mutable configuration DTO; `SnakeGame` validates and copies it. `Rules`, `Body`, `GetSnapshot()` and session `Export()` cannot expose owned arrays. `Step()` returns false only when the game has already ended; collision consumes a logical tick and preserves the last legal body. Its direction records the failed attempt so a lost snapshot can prove a collision. The session restores paused and never serializes its queue, countdown or frame accumulator.

These checks verify the pure C# rules/session only. They do not establish Unity UI compilation, save-codec correctness, Android lifecycle behavior, touch behavior, or the playability of all random food sequences.
