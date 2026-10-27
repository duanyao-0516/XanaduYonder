# Sheep difficulty calibration

Run the fast regression with:

```sh
scripts/test-sheep-difficulty.sh
```

It plays 50 deterministic seeds (`1001..1050`) per difficulty using `greedy`,
the original `generous` observation contract, and normal inventory: 2 RemoveThree, 3 Undo, 1 Shuffle, and 1 Revive. It checks these
fixed acceptance targets:

| Difficulty | Completion target |
| --- | --- |
| Easy | at least 90% |
| Normal | 50% through 90% |
| Hard | at most 45% |
| Extreme | at most 15% |

Each adjacent difficulty must also reduce completion by at least 12 percentage
points. These are regression criteria for a named deterministic player, **not
predictions of human win rates**. Do not alter the player or thresholds simply to
make a generator pass. Use additional seed ranges when validating a tuned change;
seeds repeatedly consulted during tuning are no longer unseen holdouts.

For a larger report without asserting the regression thresholds:

```sh
scripts/test-sheep-difficulty.sh --benchmark --samples 100 --seed-start 2001 \
  --policy both --tools both --output artifacts/sheep-difficulty/report.json
```

For a conservative player that sees only currently selectable cards, the tray,
and the reserve, add `--observation exposed` (benchmark mode only):

```sh
scripts/test-sheep-difficulty.sh --benchmark --samples 100 --seed-start 1 \
  --policy greedy --tools default --observation exposed \
  --output artifacts/sheep-difficulty/exposed-report.json
```

The default `--observation generous` remains unchanged so the fixed regression
and original baseline stay comparable. The exposed contract does not know any
covered face and does not remember faces that become covered again after Undo.
It is conservative, rather than an exact model of which partially covered icons
a person can recognize. The acceptance thresholds are deliberately not reused
for exposed reports.

`--policy visible3` and `--policy visible3wide` are opt-in because they are slower.
Both search at most three picks: visible3 uses beam width 8, while visible3wide
uses width 12 as a separate cross-check. `--policy both` deliberately still means
greedy plus visible3; it does not include visible3wide. The wider player is an
evaluation policy and must not be used to select generation candidates. The original generator takes
about one second for the default 200-game regression and about a minute for each
400-game `visible3` report on the development machine. Timings depend on hardware
and board structure. The 400-game visible3wide/default baseline took 154 seconds.
Use `--help` for all options.

## Information and strategy boundary

- Each real turn calls the actual `SheepGame.CanPick`, `Pick`, and tool APIs.
- The player receives only a snapshot, never the generator's solution order.
- The default `generous` observation deliberately includes every non-blind board icon, even when a
  card is fully occluded by another rendered card. This is a permissive benchmark
  information contract, not exact pixel visibility. Only the top card of each
  blind stack is known. Other blind kinds are
  replaced with `-1`; kind totals and future random state are not used to infer
  them. This gives the player extra ordinary-card knowledge relative to a
  human who cannot identify a completely covered face. With `exposed`, every
  covered board kind is `-1`, including ordinary cards.
- `greedy` prefers an immediate triple, an already exposed three-card cluster,
  tray matches, and directly revealed known matches. It avoids a seventh
  unmatched card. Equal scores choose the lowest ID deterministically.
- `visible3` adds at most three picks of forward search with a beam width of
  eight; `visible3wide` uses the same depth with width twelve. Both use the
  selected observation contract. Every simulated reveal whose
  kind was unknown remains unknown and cannot be selected in that search.
- At six tray cards without an immediate completion, the tool heuristic first
  undoes the previous non-clearing choice and tries another ID, then tries
  RemoveThree and Shuffle. If a pick loses, it tries remaining Undo or Revive.
  It does not inspect a future shuffle or optimize across all tool sequences.
- Loss is handled before any move search. The core correctly makes `CanPick`
  false for every card in a lost state.

The harness checks masking and verifies that permuting kinds hidden by the
selected observation contract changes none of the three policies' first decisions. It also
checks that every hypothetical legal first pick preserves all unknown kinds. These are information-boundary checks;
they do not prove that the heuristic represents every human strategy.

## Evidence and interpretation

Output reports completed games, pick and cleared-card quantiles (P10/P50/P90),
initial legal choices, initial exposed triple kinds, and totals of tools used.
JSON additionally retains every seed's layout, tray occupancy histogram, and
other diagnostic counts. Picks include repeated selections after Undo and cards
returned from the reserve, so a win can take more than 144 picks.

At baseline commit `8ced40d704de2428ebf334f3ab4bb2fb2f5c676b`, seeds `1..100`
produced under the `generous` observation contract:

| Policy and tools | Easy | Normal | Hard | Extreme |
| --- | ---: | ---: | ---: | ---: |
| greedy, none | 73/100 | 50/100 | 28/100 | 21/100 |
| greedy, default | 97/100 | 85/100 | 66/100 | 64/100 |
| visible3, none | 76/100 | 45/100 | 33/100 | 30/100 |
| visible3, default | 96/100 | 87/100 | 78/100 | 73/100 |
| visible3wide, default | 96/100 | 85/100 | 77/100 | 76/100 |

The first fast regression against that baseline failed as expected:
Easy 47/50, Normal 43/50, Hard 32/50, Extreme 36/50. The old generator's witness
pressure therefore did not establish a reliable Hard-to-Extreme distinction
under the generous observation contract with default tools.

The added `exposed` contract on the same locked baseline commit, seeds `1..100`,
`greedy` with default tools completed **79/100 Easy, 43/100 Normal, 20/100 Hard,
23/100 Extreme**. This separate conservative-information baseline also lacked a
clear Hard-to-Extreme completion gap. These values must not be mixed with the
generous-mode acceptance thresholds.

Per-seed JSON includes `Observation: "exposed"` for exposed reports; an omitted
`Observation` field means the original generous contract, preserving the original
baseline schema. Adding exposed mode was checked against all 800 original greedy
rows (no tools and default tools), with complete per-field equality.

## Production rater parity

`scripts/test-sheep-difficulty.sh --check-rater` independently compares the
optimized production rater to this test player over all twelve layouts and
Easy/Normal/Tutorial/Endless snapshots. It checks greedy and width-eight planning,
generous and exposed observations, repeated determinism, all four tools, unchanged
input snapshots, and identical ratings with custom zero/ninety-nine quotas.
The production rater participates in candidate selection; its own outputs are not
an independent difficulty assessment. The test player is separately implemented,
but shares the named strategies. Wider-search checks and unseen seeds are still
required when assessing generalization.
