# Chromium Dino reference snapshot

Pinned Chromium revision: `3df8d372a3f9d5c38a89d0bc496cc44c85af1feb`.

This directory is reference material, not a dependency read by the game runtime.

- `source/`: 17 original files decoded from official Gitiles, including the unchanged repository `LICENSE` and source copyright headers.
- `manifest.json`: fixed source URLs, lengths, SHA-256 values, commit and fetch metadata.
- `parameters.json`: literals parsed from these source files, with separately labeled formula descriptions and 60 Hz unit conversions.
- `chrome-156.0.8078.48-comparison.json`: hash comparison with the official tag matching this workstation's desktop Chrome. It does not identify the phone's Chrome version.

See [the Chinese explanation](../dino-chromium-config.md) for units, runtime control flow, and differences from this project's independent implementation.

From the repository root, run `python3 scripts/fetch-dino-chromium-config.py --verify` for offline verification. Run the same script without `--verify` to download the pinned revision again. Use `--ref main` only when explicitly updating this reference; preserve/review the resulting revision and source changes together.
