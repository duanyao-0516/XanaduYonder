# Sokoban campaign v1

`tutorials.json` is the original teaching-level source. The immutable released catalog is `Assets/Resources/Sokoban/campaign.json`: 8 original tutorials plus 100 unchanged Boxoban maps. Every record has a complete legal L/U/R/D witness, verified by both an independent Python replay and the actual production `SokobanGame`.

## Content and ordering

| Stable ID | Title | Teaching focus | Witness moves / pushes |
| --- | --- | --- | --- |
| tutorial-001 | 初次搬运 | Four directions, standing behind a box and basic pushing | 8 / 3 |
| tutorial-002 | 转过墙角 | Walk around a box to change its pushing direction | 17 / 5 |
| tutorial-003 | 绕行就位 | Route around a wall to reach a goal | 6 / 3 |
| tutorial-004 | 目标仍在 | Move an occupied goal's crate; the goal remains available | 19 / 4 |
| tutorial-005 | 留出退路 | Leave access for a sideways push; avoid non-goal corners | 7 / 5 |
| tutorial-006 | 先后顺序 | Arrange two crates without blocking the remaining route | 14 / 3 |
| tutorial-007 | 试走与撤回 | Try a route and undo to restore player, boxes and counters | 10 / 4 |
| tutorial-008 | 长仓库 | Move the front crate to the distant goal before the rear crate | 29 / 14 |

Tutorials 001 and 008 exactly preserve the approved compact 8×8 and tall 8×13 maps from `docs/prototypes/sokoban/prototype.js`. Import fails if either fixture drifts. Other tutorials are original maps; witnesses are stored in `tutorials.json`, so importing never depends on solver timing. Teaching objectives describe intended practice, not a claim of completed user research or automated instruction inside the page.

Boxoban levels preserve the 100 existing verified candidates and their source IDs in ascending order. The first 20 are `warehouse-01`, then `warehouse-02` through `warehouse-05`, each containing 20. These are stable internal chapter IDs; the UI displays actual ranges 第 1–20 关 through 第 81–100 关, with regular levels numbered 1–100 independently of the eight tutorials. Source IDs 21, 46, 65 and 78 remain omitted because the earlier bounded search exceeded its budget; that is not evidence that they are impossible. No machine cost, solution length or upstream dataset label is presented as a human difficulty grade, and no witness is claimed to use the minimum number of moves. Human playtesting and difficulty tuning remain unverified.

## Reproduction and validation

Run from the repository root:

```sh
python3 scripts/import-sokoban-content.py
scripts/test-sokoban-content.sh
```

The importer reads only archived local sources; it does not download data. It checks the fixed upstream source SHA-256, source rows/IDs, unique identities and exact-map hashes, closed rectangles, tile alphabet, one player, matching nonzero boxes/goals, incomplete starting state and complete legal solutions. Whitespace is significant. Every selected media file and upstream license/README is compared to the archived source manifest before copying.

All files are staged on the destination filesystem. The production C# test parses the strict catalog, replays every witness, checks final box positions and counters against Python, and verifies full undo of each nonterminal solution prefix. Only then is the entire release directory atomically exchanged (macOS `renamex_np(RENAME_SWAP)` or Linux `renameat2(RENAME_EXCHANGE)`). Failed validation or publication leaves the previous good output untouched. Unsupported atomic-exchange platforms fail without replacing existing output. The editor should not be importing assets while manually regenerating the bundle.

`--check` rebuilds into staging and compares every byte to the published bundle without replacing it. Changing a map under the same `contentVersion` and `levelId` is rejected against the existing release. Catalog membership changes also require a new content version, so completed IDs cannot silently disappear from a same-version save. Issue a new ID for a replacement map and version the resulting catalog. Output uses stable import GUIDs and no generation timestamps.

The content test entry point covers 20 import/failure tests, 19 malformed production catalog cases, all 108 witnesses and full nonterminal undo. The current witnesses contain **4,596 moves and 1,203 pushes**. This is content/production-rule validation, not Unity rendering, sound-volume, touch, Android device or human difficulty acceptance.

## Release evidence

- `Assets/Resources/Sokoban/manifest.json`: catalog hash, all source and normalized map hashes, solution hashes, media mapping, licenses, chapter order and release-file hashes.
- `Assets/Resources/Sokoban/verification-report.json`: actual production C# validator name, catalog hash, per-level move/push totals and terminal player/box positions.
- Catalog SHA-256: `60ff2a7b26f9d21016ae5c604facfb6b64ce0d7761dfa4208d09ad3354636f64`.
- Boxoban source SHA-256: `272928a4e7c185fdf84daa523b298750b6ff08703cb0c20d3be7eff93acc5256` at commit `78011d8c86ad423bfb2e1005012393b4c3c24409`.

## Selected resources

Only seven unchanged Kenney PNGs and four unchanged short OGGs are included. Sprite imports disable mipmaps, use clamp wrapping and retain transparency. Runtime loads them under `Sokoban/Sprites/` and `Sokoban/Audio/`.

The seven PNG `.meta` files are canonical post-import output from **Unity 6000.3.27f1**, archived byte-for-byte under `unity-importer/Sprites/` with a pinned hash manifest. The importer copies all normalized fields, platform settings, GUIDs, sprite IDs and internal IDs instead of synthesizing partial metadata that Unity would rewrite. These source templates and all emitted metadata remain covered by the release hashes. Regression checks verify full template/output equality and reject a template with a dropped normalized field. An editor-version change requires an explicit template refresh and release regeneration after a real Unity import.

| Resource | Archived source | Use |
| --- | --- | --- |
| wall | `Blocks/block_06.png` | Exact prototype wall |
| crate | `Crates/crate_02.png` | Exact prototype normal crate |
| crate-goal | `Crates/crate_05.png` | Exact prototype occupied-goal crate |
| player-down | `Player/player_05.png` | Exact prototype player, facing down |
| player-up | `Player/player_08.png` | Matching outlined player facing up |
| player-right | `Player/player_17.png` | Matching outlined player facing right |
| player-left | `Player/player_20.png` | Matching outlined player facing left |
| move | `Audio/tick_001.ogg` | 0.023220 s |
| push | `Audio/click_001.ogg` | 0.100023 s |
| goal | `Audio/confirmation_001.ogg` | 0.289841 s |
| complete | `Audio/confirmation_002.ogg` | 0.539002 s |

PNG sources are from `ThirdParty/Sokoban/kenney-sokoban/PNG/Default size/`; audio sources are from `ThirdParty/Sokoban/kenney-interface-sounds/`. Floor and target markings remain the approved UI treatment; no unused floor/goal images, alternate crate themes, retina duplicates, sprite sheets or long audio are bundled. Source archives remain outside `Assets/Resources`.

The Kenney selections remain CC0-1.0; the 100 Boxoban maps remain Apache-2.0. The original tutorials and original tooling use the repository MIT license. The release includes both original upstream Apache license files, the unchanged upstream README with its author citation, both Kenney license files and the project MIT license. See `THIRD_PARTY_NOTICES.md` for attribution.
