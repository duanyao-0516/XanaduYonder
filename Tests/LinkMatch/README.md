# Link Match Core verification

Run `scripts/test-link-match.sh` from the repository root. The console harness uses .NET 10 and C# 7.3, matching the repository's pure Core test conventions.

The suite covers all five approved levels, strict board restoration invariants, zero/one/two-turn connections, all four outside edges at every board size, a corridor requiring exactly three turns, blocking, deterministic minimum-turn/minimum-step routing, invalid atomic submissions, static holes, read-only hints, terminal boards and defensive snapshots. An independent cell-by-cell directional BFS checks 400 original-size layouts and 160 layouts at each expanded size against the production geometric path enumerator: 720 oracle comparisons in total.

For each of five levels, 100 seeds generate repeatable full boards with four tiles per kind and replay the complete production-rule solution: 500 full deals, each independently regenerated to check determinism. Another 80 varied sparse masks per level verify exact multiset and hole preservation through both generator and game shuffle APIs: 400 sparse masks. Singleton rows, the final pair, and empty boards are checked across all five sizes; a paired-but-unplayable deadlock is also included.

Catalog assertions preserve levels 1–3 exactly and require level 4 = 8×12 / 24 kinds / 300 challenge seconds and level 5 = 12×16 / 48 kinds / 600 seconds. `LinkMatchLevel.Count`, `MaximumTileCount` and `MaximumKinds` derive from the catalog and return 5, 192 and 48. There is still exactly one four-copies-per-kind rule.

Before the expansion, SHA-256 fingerprints were captured for the tiles, ordered solution pairs and every path corner over seeds 0–99 on each original level. The test fixes those three fingerprints and verifies them after changes; `scripts/test-link-match.sh --legacy-fingerprints` prints the values. This catches seeded-board changes as well as path tie-break changes.

Generation has no random retry or solver timeout. Each finite scan selects a geometrically connected pair while ignoring fruit identity, removes it from the occupied mask, then assigns a shuffled pair identity to those two positions. Every even mask admits progress: either a row contains two consecutive occupied positions joined horizontally, or each occupied row contains one tile and two consecutive occupied rows join with at most one elbow. Each generated witness is replayed with `LinkMatchGame.TryMatch` before a deal is returned. Player choices can still create a deadlock; `FindMatch` promises only a current legal pair, and `Shuffle` constructs a complete new witness preserving all existing holes and fruit counts.

Initial Core red verification: 0 passed, 19 failed against behavior-free API declarations; the initial implementation and deadlock regression produced 20 passing cases. Expansion red verification: 20 passed, 3 failed because the five-level catalog, shared maximum properties and dense levels were absent. After the catalog implementation, 23 passed. A separate allocation regression then failed with 373,552 bytes for level 4 seed 0; removing per-candidate path arrays produced the final **24 passed, 0 failed**. These tests establish pure Core behavior; Unity compilation, rendering and device interactions are separate validation layers.

## Expanded-board measurements

Run `DOTNET_TieredCompilation=0 scripts/test-link-match.sh --benchmark`. The opt-in benchmark preconstructs fixtures, warms each operation, then measures 128 fixed seeds per case using `Stopwatch` and per-thread allocated bytes. The environment switch removes tiered-JIT transitions from comparisons. Do not use these local .NET measurements as Unity, Android, or interaction-performance claims.

Recorded on 2026-10-28 with .NET 10.0.14 after the focused path-allocation change:

| Board | Operation | Median ms | P95 ms | Mean allocated bytes | Maximum allocated bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| 96 | Generate | 0.072 | 0.116 | 64,330 | 69,488 |
| 96 | FindMatch, full | <0.001 | 0.003 | 192 | 200 |
| 96 | Reshuffle, full | 0.072 | 0.090 | 64,330 | 69,488 |
| 96 | Reshuffle, half remaining | 0.025 | 0.032 | 26,614 | 30,624 |
| 192 | Generate | 0.277 | 0.383 | 184,369 | 189,736 |
| 192 | FindMatch, full | 0.001 | 0.003 | 195 | 200 |
| 192 | Reshuffle, full | 0.276 | 0.369 | 184,369 | 189,736 |
| 192 | Reshuffle, half remaining | 0.079 | 0.099 | 67,793 | 73,240 |

The baseline allocated 337,000 bytes per 96-tile generation and 1,686,166 bytes per 192-tile generation on the same benchmark. Path candidate corners now remain value parameters; only an improving valid route creates a display-path array. Candidate traversal, tie-breaking, generator PRNG and pair selection are unchanged. Mean generation allocation fell by approximately 80.9% and 89.1%, respectively. A broad 1,536-bytes-per-tile ceiling across 16 fixed seeds guards this temporary-allocation regression without asserting machine-specific latency.
