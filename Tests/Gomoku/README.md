# Gomoku core checks

Run from this worktree:

```sh
scripts/test-gomoku.sh
scripts/test-gomoku.sh --benchmark
```

The dependency-free .NET 10 console project compiles the production `Assets/Scripts/Core/Gomoku/*.cs` with C# 7.3 and warnings as errors. It does not load Unity, install packages, or modify a game save. The current suite contains 22 behavior groups.

## Covered contracts

- Black-first legal turns; all four win directions; edge wrapping rejection; freestyle overlines; full-board draw; rejected play after terminal.
- Replay rejects null, out-of-range, duplicate, and post-terminal history. Exported moves, winning lines, constructor input, and copies cannot modify the source. Undo validates its entire count before changing the board. Deterministic random legal games additionally check replay and undo against prior snapshots.
- All 225 coordinate round trips; `H10` maps to row 5, column 7 (index 82). A–O is left to right; 1–15 is bottom to top. Parsing accepts lowercase ASCII columns, rejects whitespace, leading zeroes, and out-of-range values.
- Immediate own win takes priority over an opponent threat; gap-four blocks work horizontally and at a vertical edge, even with no optional search budget.
- A hard-mode continuous-four fixture selects 111, forces 112, then uses 96 to create an open four. Its horizontal reflection also passes. Every legal final defender reply is checked independently through the rules engine and still permits an immediate black win.
- Creating and defending crossing double threes; answering a remote jump-three counterattack before a slower own fork; center opening in all difficulties.
- Legal fallback on zero time or nodes; pre-search and active cancellation; unchanged source state; deterministic node-bounded runs; partial iterations cannot replace the last completed result; invalid options and terminal searches.

## Search implementation and limits

The AI owns a copied board, fixed per-ply candidate buffers, and a bounded direct-mapped transposition table. It uses iterative deepening negamax/alpha-beta, local open/closed/jump pattern scores, defensive ordering, and forced-defense leaf extensions. Global immediate-win and immediate-defense scans run before candidate truncation. Root tactical scans also run when the optional time/node budget is zero. A direct win, forced-four proof, or fallback reports `CompletedDepth = 0`; otherwise this field records the last fully completed negamax iteration.

Hard mode adds a bounded continuous-four proof. Each attacking move must leave an immediate winning square; the defender's immediate counter-win is checked globally. Two distinct winning squares prove the next win; a single square forces its defense before recursion. This phase uses at most one quarter of the search budget. Candidate pruning, finite depths, and budgets make the overall AI selective; it does not claim complete multi-step threat detection or competitive strength against external engines.

Default targets:

| Difficulty | Time target | Node cap | Depth cap | Positional candidate cap | TT entries |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 100 ms | 12,000 | 2 | 8 | 4,096 |
| Normal | 450 ms | 60,000 | 4 | 12 | 16,384 |
| Hard | 1,200 ms | 250,000 | 8 | 16 | 32,768 |

The timer is checked during search, generation, and evaluation; a finite tactical scan or a small batch of scoring work can finish after the target. Cancellation is distinct from timeout and throws `OperationCanceledException`. Completed work or partial board mutations remain private to that invocation and are never applied to the caller's game.

## Evidence recorded on 2026-10-28

The rule API skeleton produced 9 failing behavior groups before implementation; adding the AI contract produced 7 further failing groups before implementation. A later opening regression produced `center opening Easy got 82`; the center-opening correction now passes. The final run reports `Gomoku: 22 passed, 0 failed`.

One warmed host .NET benchmark run reported:

| Fixture | Difficulty | Move | Completed depth | Nodes | Elapsed |
| --- | --- | ---: | ---: | ---: | ---: |
| Opening | all | 112 | 0 | 0 | 0 ms |
| Six-stone middle game | Easy | 126 | 2 | 36 | 7 ms |
| Six-stone middle game | Normal | 126 | 4 | 764 | 131 ms |
| Six-stone middle game | Hard | 126 | 7 | 40,012 | 1,200 ms |
| Continuous-four proof | Hard | 111 | 0 | 2 | <1 ms |

Per-call managed allocations in that run were approximately 191 KB / 584 KB / 1.11 MB for Easy / Normal / Hard, mostly fixed buffers and the transposition table. Host tiered JIT, machine load, and positions affect timings and completed depths; node-budget fixtures are used for deterministic assertions. These results do not establish Android IL2CPP latency, cancellation latency, heat, battery use, device gameplay parity, or strength relative to established engines.
