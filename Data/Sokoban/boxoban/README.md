# Boxoban source archive

Retrieved 2026-10-28 from the official Google DeepMind data repository, pinned at `78011d8c86ad423bfb2e1005012393b4c3c24409`.

- Repository: https://github.com/google-deepmind/boxoban-levels
- Source set: `unfiltered/test/000.txt`, all 1,000 source levels retained byte-for-byte in `unfiltered-test-000.txt`.
- License: Apache-2.0. Both upstream license files are retained unchanged. Upstream's citation and generation references are retained in `UPSTREAM-README.md`.
- Per-file source URLs, byte sizes and SHA-256 hashes: `source-manifest.json`.

This is a source archive for a later curated campaign. It does not mean all 1,000 levels have been solver-verified or playtested in XanaduYonder. The upstream labels `unfiltered`, `medium`, and `hard` describe dataset selection, not verified difficulty tiers for human players.

## Import format

Each level begins with `; N`, then ten ASCII rows of ten characters. `#` = wall, space = floor, `@` = player, `$` = box, `.` = target. Source IDs are 0 through 999. Keep significant spaces and source IDs. Future selected levels should use stable IDs such as `boxoban-unfiltered-test-000-0000`, preserve the source mapping, and record any conversion or edits.

## Validation performed

The initial source import checked all 1,000 levels for unique sequential source IDs, 10×10 cells, one player, four boxes, four targets, supported symbols, and closed outer walls. This archive's manifest describes that structural check only. A subsequent local verification phase found and independently replayed 100 candidate solutions; see [the verification report](../verification/README.md). No human difficulty grading, Unity import, or Android runtime verification has been performed.

## Generation evidence

Upstream documents the generation method in *Imagination-Augmented Agents for Deep Reinforcement Learning* (Racanière et al., 2017), Appendix D.4: start with boxes on targets, then search backward using walking and box-pulling actions. This supplies a construction-based solvability argument; the archived source text does not include solution traces. Locally derived witnesses are stored separately in `../verification/`. Medium and hard sets are filtered using agent performance as described in Appendix 7 of *An Investigation of Model-free Planning* (Guez et al., 2019), not a human-authored teaching progression. Before release, replay every selected solution with the production C# rules and playtest ordering.

- https://arxiv.org/html/1707.06203 (Appendix D.4)
- https://proceedings.mlr.press/v97/guez19a.html
- https://proceedings.mlr.press/v97/guez19a/guez19a-supp.pdf

Keep this archive and licensing notices separate from the repository's MIT-licensed original implementation. Do not relabel the upstream level data as MIT.
