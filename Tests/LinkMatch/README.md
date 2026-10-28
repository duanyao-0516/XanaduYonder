# Link Match Core verification

Run `scripts/test-link-match.sh` from the repository root. The console harness uses .NET 10 and C# 7.3, matching the repository's pure Core test conventions.

The suite covers the three approved levels, strict board restoration invariants, zero/one/two-turn connections, all four outside edges, a corridor requiring exactly three turns, blocking, deterministic minimum-turn/minimum-step routing, invalid atomic submissions, static holes, read-only hints, terminal boards and defensive snapshots. An independent cell-by-cell directional BFS checks 400 varied layouts against the production geometric path enumerator.

For each of three levels, 100 seeds generate repeatable full boards with four tiles per fruit and replay the complete production-rule solution. Another 80 varied sparse masks per level verify exact multiset and hole preservation through both generator and game shuffle APIs. Singleton rows, the final pair, an empty board and a paired-but-unplayable deadlock are included.

Generation has no random retry or solver timeout. Each finite scan selects a geometrically connected pair while ignoring fruit identity, removes it from the occupied mask, then assigns a shuffled pair identity to those two positions. Every even mask admits progress: either a row contains two consecutive occupied positions joined horizontally, or each occupied row contains one tile and two consecutive occupied rows join with at most one elbow. Each generated witness is replayed with `LinkMatchGame.TryMatch` before a deal is returned. Player choices can still create a deadlock; `FindMatch` promises only a current legal pair, and `Shuffle` constructs a complete new witness preserving all existing holes and fruit counts.

Initial red verification: 0 passed, 19 failed against behavior-free API declarations; the first green run passed all 19. The additional deadlock regression expands the final suite to 20 cases. These tests establish pure Core behavior; Unity compilation, rendering and device interactions are separate validation layers.
