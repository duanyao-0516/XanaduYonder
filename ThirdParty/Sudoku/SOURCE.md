# Sudoku Exchange Puzzle Bank

Upstream: https://github.com/grantm/sudoku-exchange-puzzle-bank

Pinned commit: `d8c8ebaee0c08c412cfba96af1923dfa61c83317`.

License: [Unlicense](LICENSE.txt), copied verbatim from the pinned source. It permits use, modification, commercial use and redistribution of the dataset. This repository redistributes only the selected puzzle givens, along with locally computed solutions and difficulty labels; no upstream solver code is included.

- `selected.tsv`: exact provenance of all 2,000 bundled puzzles. Columns are local stable ID, upstream source filename without extension, original 1-based line number, upstream 12-character digest, upstream rating, and original 81-digit givens.
- `source-prefixes.json`: pinned URLs, byte counts, line limits and SHA-256 checksums of downloaded source prefixes. The first import downloaded 1,800,000 bytes, not the full upstream corpus.
- `import-report.txt`: original selection run statistics. It examined 10,462 unique input boards; 2,702 could not be completed by the supported logical engine and were excluded. All examined input boards passed the independent exact uniqueness search.

Original line format: `digest12 givens81 rating`. Upstream labels and ratings are retained for provenance; they do not determine the app's difficulty. The selected corpus came from easy, medium and hard; no diabolical puzzle was needed.

Reproduction and verification are documented in [docs/sudoku-content.md](../../docs/sudoku-content.md).
