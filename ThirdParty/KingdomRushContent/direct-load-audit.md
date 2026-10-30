# Direct resource and module load audit

`scripts/kingdom-rush/verify-direct-loads.py` inspects the actual `game.love` ZIP without executing Lua or operating a device. It complements atlas/frame checks by checking direct file dependencies and conservatively following literal `require` calls from the entry screens and all selected level data. Each generated report binds the complete source snapshot by content SHA256 and individual inspected source hashes.

```sh
python3 scripts/kingdom-rush/verify-direct-loads.py \
  --content artifacts/kingdom-rush-1/game.love \
  --template-review artifacts/kingdom-rush/dependency-review/evaluated-closure-review.json \
  --report artifacts/kingdom-rush-1/direct-load-audit.json
```

The tool exits **1** when a finite required dependency fails or review evidence is stale, **2** when there are unresolved expressions, and **0** only when its finite checks pass and its inventory contains no unresolved expressions. `INCOMPLETE` is intentionally not a passing result. The JSON retains every call with its source, line, API, argument and classification evidence. Lua comments are excluded, aliases and `pcall` are recognized, and embedded image/audio worker strings are scanned separately. The template review must refer to the exact same package SHA; without it, template-provided `load_file` and EXO arguments remain unresolved.

`direct-load-reviewed-sources.json` pins the source hashes used for manual profile/control-flow reasoning. Changes require an actual difference review before updating that manifest. Do not regenerate it merely to make verification pass. Finite media lists are derived from the inspected ZIP each time, not from the reference package recorded in that manifest.

## Entry and resource contracts

| Phase | Real source entry | Direct resources and module selection |
| --- | --- | --- |
| Boot | `main.lua`, `main_globals.lua`, `features.lua` | `KR_GAME=kr3`, `KR_TARGET=desktop`, `KR_PLATFORM=win`; ZIP require lookup uses root, `kr3-desktop`, `kr3`, `all-desktop`, `all`, and `lib`. Chinese locale is fixed by the profile. |
| Splash and slots | `screen_splash`, `screen_slots` via `director_data.item_props` | Splash loads `logo_shimmer.ogg` directly. Slot templates are read through `kui_db`; their frame and group closure is checked separately. |
| Loading page | `screen_loading` | Loaded by director before map/game transitions; its visible data comes from `data.director_data`. No direct standalone bitmap/audio constructor occurs in this controller. |
| Battle | `game`, `game_gui`, `level_utils`, `path_db`, `grid_db`, `wave_db` | Levels 45–76 each require `_data.lua`, `_grid.lua`, `_paths.lua`, and all three mode wave files. A level's main `.lua` and `_loc.lua` are optional in the actual source. Extra `load_file` spawner fields are separately checked. |
| Pause, win and loss | Classes inside `all-desktop/game_gui.lua` | Use the game's existing frame/audio databases; direct constructor inventory has no separate pause/win/loss bitmap file path. Their frame preload/conditional construction checks belong to the UI validator and device probes. |
| Initial campaign ending | Level 56 sets `next_item_name='kr1_end'` | `screen_kr1_end` reads desktop comics 19 and 20, uses existing `screen_credits`, and returns level 56/map. Requires `common`, `music_screen_kr1_end`, `MusicEndCredits1`, and `MusicSuspense`. |
| Selected extension ending | Level 75 sets `next_item_name='kr1_fa'` | `screen_kr1_fa` reads desktop comic 21, enters credits, and returns level 75/map after the reviewed original-source correction. Its music uses the real initial-generation credits group and track. |
| Return | Ending callback → director → map; explicit lobby return → saved slot → `love.event.quit` | Save/settings paths are runtime state, not package media. Their successful read/write and Activity lifecycle are separate runtime checks. |

Only levels 56 and 75 declare a special ending route in the selected 45–76 level scripts. To inspect their original substages in an isolated test identity, load the named screen through the director. `init` immediately displays the first comic and leaves `phase='credits'`. At least one update/draw per stage followed by one `ending:next_item()` advances resources deterministically: `kr1_end` needs three further calls (credits, second comic, done/map), while `kr1_fa` needs two (credits, done/map). Actual interaction uses comics `skip()` and credits `on_end_credits()` with their animation/tween waits. Do not combine both advancing mechanisms or call completion twice. Returning to map can invoke normal save code, so this belongs in a separate test identity.

## Confirmed findings

1. **Level 75 original ending has missing late dependencies.** The original APK contains no `data.credits_data_fa` module, no `music_screen_kr1_end_fa` group, and no `MusicEndCreditsFA` sound ID. Its final callback also returns the old level 33. The content owner corrected these exact source anchors to the existing original `data.credits_data`, real initial-generation credits music, and level 75. The verifier checks both ending modules, their literal sound IDs/groups, and their final map/level outcome. No replacement media or silent audio is generated.
2. **Four selected level data files referenced missing spawner files.** Snapshot `833fcb6f…` contains `mega_spawner` entries for `level67_spawner`, `level68_spawner`, `level70_spawner`, and campaign-only `level72_campaign_spawner`, but lacks the corresponding `kr3/data/levels/*.lua` files. The current `all/scripts.lua` `mega_spawner.insert` reads exactly that path and returns false when absent; update removes an uninitialized spawner. Initialization without a blue screen does not prove the intended side-spawn behavior. These are explicit failing finite checks; the content owner found all four authentic files in both original sources and is restoring them. A later package is only accepted after a new run.

The generated JSON, not this prose, is authoritative for a later package's exact status and hashes. A source correction must be followed by a new run.

Final candidate `212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641` restores all four original spawner files. Its independent direct-load run passes with 874 inspected sources, 2,293 call sites, 1,657 finite file contracts, 141 classified dynamic/optional calls, zero missing-file errors and zero unresolved arguments. The paired template evaluation is bound by package SHA, evaluated-data SHA and `staging_matches_archive=true`. Brand verification separately confirms both window titles, twelve source files and twelve physical texture pages against that same package.

## LÖVE 12 compatibility boundary

- Main-thread `isFile`, `isDirectory`, and `exists` adapters use `getInfo`; the verifier checks their presence. Image/audio workers have their own `getInfo` compatibility helper because Lua globals are not inherited into those worker states.
- Font loading enumerates seven packaged TTF/OTF files; Chinese labels use the real HarmonyOS Sans font and fallback. The old bitmap-font branch calls `Image:getData`, but there are no PNG bitmap fonts in the inspected font directory. This does not certify rasterization or all possible runtime aliases.
- Shader files are present and preloaded by `shader_db`, but GPU compilation and driver behavior remain runtime checks. File existence must not be reported as successful shader compilation.
- The native LÖVE 12 boot selects `love.errorhandler or love.errhand`. Defining only the legacy `love.errhand` in main does not replace the default error handler, so its custom fatal-file logging is not a reliable diagnostic path.
- The original native library registers Lua `print` as a function calling `SDL_Log('[LOVE] %s', ...)`, which reaches logcat as INFO under `SDL/APP`; this is separate from `klua.log`'s default `io.write` output. A late logcat dump can lose the earlier main-buffer error even when system-buffer Activity records remain. Start a streaming capture before reproduction; adding stdout flush is not a remedy for that retention gap.

## Reviewed dynamic argument classes

The raw lexical inventory initially had 140 dynamic/optional calls. Adding the actual `storage_io_generic` module exposed one additional runtime-state read. These 141 calls have now been individually classified under the pinned first-generation profile:

| Class | Evidence and bound |
| --- | --- |
| In-memory values | Numeric `setNewFont` size; 1×1 ImageData; canvas/path-canvas `newImageData`; image worker decoded data. These constructor arguments are not filenames. |
| Finite media/data | Actual fullhd atlas indexes and their Android ASTC/PNG selection, every sound-index filename under `sounds/files`, seven fonts, twelve shader files, 96 wave files and optional TSV/extra variants, selected level data/grid/path/locations, and seven cursor images. |
| UI template indirection | Literal KDB callers in the conservative module graph plus recursive `template_name` resolve through desktop-first lookup to eight files. Unused mobile fallback templates mentioning `p_outline_tint`/`p_bevel` are not dependencies of these eight files. No replacement shader is added for them. |
| Profile-disabled branches | Empty platform-services table, fixed locale, release launch with no debug/custom/balance flags, no imported legacy phone/.NET save, no level 251–269 loader, no bitmap-font PNGs, and absent `KR_NATIVE`/disabled MessagePack. |
| EXO | The independently evaluated final template set has no `render.exo`; selected level data and `firstgen_dependencies.exoskeletons` have none. Initial-generation hero/power selections have no EXO requirements, map animations 1 contain no `exo_name`, and foreign tower/cheat selections are normalized off. The supporting sources are pinned. |
| Spawner filenames | The four selected-level `load_file` values plus every truthy final-template `load_file` from the independent evaluation. This is the category that caught four real omissions. |
| External runtime state | Log streams, saves/settings, optional editor registry/list and custom waves, legacy XML/persistence helpers. Missing optional state is guarded and falls back to the original packaged waves. Storage I/O success and user-injected files are outside archive validation. |
| Developer/legacy utilities | Conversion directories `atlas/input`, `animations/immutable`, `animations/alterable`, `dds2pkm_lz4` are guarded and absent from the ZIP; old `image_db:load` has no selected callers. The package uses `load_atlas`. |

The report stores exact counts and per-call explanations, rather than treating all 141 as either missing or automatically safe. New/unreviewed load sites remain unresolved, and changed reviewed sources fail closed.

The inventory is lexical, not a Lua interpreter or a proof of every possible program path. Package-file closure does not prove entity behavior, ANI/frame preload order, shader compilation, rendering, save writes, interactions, or recovery after lifecycle changes. The separate template/ANI review and root's 96 initialization/render probes and explicit ending-phase tests provide additional evidence. No device action is performed by this verifier.
