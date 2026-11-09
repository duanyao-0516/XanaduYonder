# Native Gold Miner source campaign, rules 5

New native runs use `GoldMinerBalanceProfile.Source` (5). The migration takes the classic games' economic data and uses their ore distribution as a layout reference; it does not adopt their UI or ore artwork. Native gold, rocks, bags, animals, hooks, characters and backgrounds remain the presentation baseline. Bone and skull sprites supplement that style; TNT uses the existing native dynamite sprite. See [native ore artwork](native-ore-art.md).

Profiles Legacy (1), Native (3), and Classic (4) retain their previous targets, ore values, mechanics, inventories and 240 shipped layouts. The name Classic in rules 4 did **not** mean the H5 campaign. Existing native runs continue on their accepted profile; only a new run selects Source. Rules 5 keeps schema 1's existing native slots and adds separate source records, preserving historical records and backups. The removed H5 games' separate private save files are not read or overwritten.

Source's economic profile and layout catalog are versioned separately. `CatalogVersion=2` / `<mode>-source-v1` is the previously shipped canonical source geometry. Existing current levels and their retry checkpoints keep those positions. New levels use `CatalogVersion=3` / `<mode>-source-v2`, with an irregular native mine layout. Advancing an old Source run to the next level adopts the new layout; resuming or retrying its current level does not move objects or regenerate bag contents.

## Authoritative input and extraction

The removed separate game shipped two different games selected by the user: solo 4399/230816 and duo 4399/232532. Extraction used their tracked, actually packaged sources, not reconstructed maps or difficulty estimates. `Assets/Resources/GoldMiner/Source/manifest.json` records the original file SHA-256s and the output hashes. `solo.json` preserves all original `LevelName`, background ID, ordered sprite definitions, decimal coordinates, scales and flips. `coop.json` preserves the ordered original map objects, ore types, prices, weights and image dimensions.

| Data | Native resource | Checked against the removed source |
| --- | --- | --- |
| Solo maps | Source/solo.json | 30 maps, 611 objects; every original field compared after JSON round trip |
| Duo maps | Source/coop.json | 30 maps, 594 objects; every original field compared after JSON round trip |
| Solo sprite metadata | solo.json `frames` | All 17 referenced frame rectangles, trim offsets and source dimensions |
| Native artwork | GoldMiner/objects and GoldMiner/characters | Original native art is used; the 27 previously extracted classic ore PNGs and their extraction script were removed |
| Goals and prices | docs/gold-miner/source-contract.json | Executed the original `Game.getGoal` and `ShopButton.getPrice`: 40 goals, 200 minimum/maximum price intervals |

`export-source-campaign.mjs` accepts an explicit source web directory (second argument; defaults to the ignored research copy). It exports data and reference dimensions, not classic sprite pixels, and is a review tool rather than a build dependency. Native JSON is verified against pinned SHA-256 before parsing. Per-map signatures bind saved initial entities to their declared layout version and reference data; old source-v1 signatures remain valid. Changing both checkpoint and live copies must not bypass validation. Generated bag outcomes are persisted separately from immutable map definitions.

## Source-to-native evidence

Original source paths below are names recorded in the manifest. Their pre-removal snapshots remain only under ignored `artifacts/gold-miner-source-reference/` during this task; no JavaScript engine or H5 runtime is used by native Gold Miner.

| Contract | Original implementation | Native implementation |
| --- | --- | --- |
| Solo map selection | solo/game.js `Game.create`, original lines 5836–5840: 3 variants per group; after level 10 use `(level-6)%10` | SourceRules.MapGroup / LevelCatalog.CreateSourceStart |
| Solo cumulative target | solo/game.js `Game.getGoal`: `5*(27*n*n+28*n+75)` | SourceRules.Target; independently executed source contract |
| Duo maps/target/completion | duo/js/game-data.mjs MAPS; game.mjs freshRun, Target.create, Play.gameOver: map 1–30; `500+150*n` | Source catalog, Target, Completed |
| Solo actual timer | solo/game.js Game.create sets 61; changeTime decrements and ends at -1 | 3720 ticks at 60 Hz (62 source timer callbacks), HUD starts at 60, displays 59 after two seconds, and ends at the 62-second source cutoff |
| Duo timer | game.mjs freshRun.time=40; Play.update ends at 0 | 2400 ticks at 60 Hz |
| Solo object coordinates | solo/game.js Boulder adds x+35,y+50; source origin y46 | source-v1 keeps `(x+35)*.01`, `(y+4)*.01`; source-v2 uses these as the reference for native placement |
| Duo object coordinates | game.mjs Ore uses MAPS x/y; hook y55 | source-v1 keeps `x*.01`, `(y-55)*.01`; source-v2 adapts distribution to the native landscape mine |
| Moving solo animals | Boulder types 6/10, 370px / 4200ms, flip chooses direction, yoyo | source-v1 retains original endpoints; source-v2 fits tracks to its mine; 252 ticks per leg |
| Solo catch radius | Boulder.hitTestPoint compares distance with absolute sprite **width** | source-v1 retains original untrimmed width/scale; source-v2 aligns native visual size and collision size |
| Duo catch radius | Ore constructor: `max(15,max(width,height)/2)+5` | source-v1 retains the source dimensions; source-v2 uses native collision sizes |
| Solo return speed | Pod.updateStatus: `10+strength-weight` px/frame | Same weight/strength formula, with source-v2 travel adapted to its larger mine rather than copying pixel speed unchanged |
| Duo return speed | Hero.grabSpeed=4, friction=`1-max(weight-strength,20)/100` | Same weight/strength relationship, with source-v2 travel adapted to native field dimensions |
| Solo TNT | Game.boomBounler destroys *other* objects within 100px; picked TNT still returns original value 1 | SourceSimulation.Catch; source-v1 retains the original radius; picked TNT awards its value when it reaches the top |
| Solo bag | BoulderData.reciveSprite215 picks money/power/dynamite and variable, possibly negative weight | SourceRules.DrawBag; saved reward/weight never rerolled on resume |
| Solo income/strength delivery | adapter.js waits 900ms for ordinary rewards and 500ms for dynamite | Intentional native difference: award and resume swinging in the tick that reaches the top; floating text does not block input |
| Duo bag | Play.handleLuckyEffect: power sets60 with probability .3/.6, otherwise1…600 | SourceSimulation.Award |
| Duo direction and boundary | Original Hero.update flips grabSin/grabCos while retaining the carried ore; even an empty returning hook can catch | Persisted SourceHook.TravelDirection remains independent of Loaded state; boundaries follow the saved layout version; the old six-tick top reset no longer blocks input |
| Duo inventory | getShop generates shared bombCount; reserveBomb deducts before500ms effect | SharedBombs plus per-hook persisted BombTicks |
| Solo shop | ShopButton.canCreate/getPrice; Shop.buyItem has no funds check | SourceEconomy: same eligibility/ranges, debt permitted, random offers/prices persisted |
| Duo shop | random-comparator three-item selection; SHOP_TYPES price functions; funds check | Three distinct random offers, original price ranges and affordability |
| Duo ineffective items | shop4/5 are sold but Play has no stone/diamond value effect; pig does not move; ground bomb is ordinary value0 ore | Preserved as implemented; no behavior inferred from item descriptions |
| Timeout | Both originals settle immediately, discarding unfinished returns/rewards | SourceSimulation does not grant an unfinished return; rewards already delivered at the top are retained; old profiles keep their existing drain behavior |

Solo source prices after completed level n: dynamite `[1+2n,300+2n]`; strength `[100,399]`; clover `[1+2n,52n]`; rock book `[1,150]`; diamond polish `[201,100n+200]`. Duo ranges respectively `[1,300]`, `[100,399]`, `[1,100]`, `[1,150]`, `[200,499]`.

The source's solo save validator already bounded levels to 1…100000. Native retains that persistence boundary, not a ten-level ending; the UI must explicitly explain the boundary at level100000 instead of pretending it is normal campaign completion or offering an invalid next level. Duo alone completes at30.

## Native geometry and artwork

The previous source-v1 projection used 100 source pixels per world unit and a uniformly fitted field: solo 6.4×4.34 (hook y46 to canvas bottom480), duo 5.5×3.45 (y55 to400). Fitting that wide source canvas into a portrait native page left much of the lower mine empty. It also brought in classic ore art despite the intended scope being numerical data and placement reference.

source-v2 uses a 6×10.2 solo field and a 12×4.4 cooperative field. Extension and retraction speeds scale by 2.1 in solo and 1.65 in cooperative mode to keep retrieval times comparable after expanding the mine; source weights and shop effects remain unchanged. Ore is placed irregularly across the usable mine with depth variation, using source distribution as a reference rather than reproducing its canvas. The positions are simulation coordinates, not a visual stretch: hook travel, animal tracks, collision bounds and the uniformly rendered native sprites use the same space. Native ore artwork is restored. The nine established ore/animal categories retain their complete hook/load illustrations; Gold250 uses the native gold illustration, while bone, skull and TNT combine native object art with the native hook. Classic ore images are not packaged. Raw reference coordinates and image dimensions remain data for provenance and source-v1 compatibility, not an instruction to render the old images.

Layout changes intentionally alter exact paths and catch geometry. Travel speed is adapted to the new dimensions to keep traversal times comparable; target amounts, ore values, weight relationships, bag rules, shop eligibility and price ranges remain source-derived. This is a native adaptation, not a promise of pixel-identical maps or identical difficulty to the removed web games.

## Delivery timing and save compatibility

The old top-of-rope pause came from simulation gates: solo waited 54 ticks (0.9s) for ordinary rewards or 30 ticks (0.5s) for dynamite rewards; duo waited six ticks after its award. Native presentation did not show the corresponding original transfer animation, so this read as a frozen hook. New returns award in the arrival tick and resume swinging immediately. Collection text and effects can finish independently.

Old running Source saves may contain a reward that has reached the top but is still pending. On the first resumed simulation tick, that already-earned reward is committed once and the old pending/reset state is cleared. A later save/reload cannot award it a second time. Decoding alone does not perform a transaction. Already settled saves are not recalculated, and a load that has not reached the top at the deadline still earns no reward. The cooperative dynamite's 30-tick delay after reserving a bomb is a separate effect and remains unchanged.

## Random state

The originals used unseeded `Math.random`; native uses the existing saved xorshift stream for reproducible continuation. Random ranges, source bag branches, inventory lifetime and three-offer rule are preserved. Browser-specific random-sort call order is not a promised cross-engine sequence. Frame-based original motion is represented at native's fixed60Hz; this is not a claim of identical wall-clock animation under every browser frame rate.

## Validation scope

`scripts/test-gold-miner-source.sh` is the rules and save-contract entry point. It covers the reference maps/1205 ordered objects, values/weights, original goal/price execution evidence, source identity corruption, source save round trips, TNT reward, cooperative bomb reservations, purchased buffs/inventory, deterministic in-flight continuation, timeout and solo debt. The layout/timing correction additionally needs source-v1 save fixtures, single-award pending recovery, source-v2 geometry and current rendering checks. Existing core, catalog and persistence suites retain old mechanics/240 layouts/actual v1/v2 device-save fixtures and backup verification. Map signatures protect checkpoints independently of live state.

This document records the implementation contract and historical extraction evidence; it is not a passing test report for the layout/timing correction. Pure .NET rules, serialization and filesystem checks do not establish Unity rendering, compilation on every target, real touch behavior, full-campaign playthrough or device parity. Actual runtime UI and build/export results must be reported separately.


### 2026-11-09 correction verification

- `scripts/test-gold-miner-source.sh`: exit 0; 28,797 native-layout checks and 27,315 source/delivery assertions. Coverage includes all 60 maps / 1,205 objects, actual hook reachability, deep-mine travel, canonical coordinates, legacy pending-delivery fixtures and save continuation.
- `scripts/test-gold-miner-ui.sh`: exit 0; 16,574 assertions. Actual Unity Play Mode screenshots cover 720×1320 / 720×1582 solo and 1320×720 / 1582×720 cooperative layouts, native ore/load sprites, collection without a top wait, and the existing v4 UI flow. The new mine occupies at least 90% of available depth in these four views.
- Existing core and persistence suites: 53 and 32 passed. Independent source review completed. Android Debug 0.9.7 (versionCode 16) was rebuilt and replacement-installed on the connected OPPO on 2026-11-09. The signing certificate matched the existing Debug app; all 30 accessible data files, including 24 JSON save/backup files, were byte-identical before and after installation. The app was not launched; physical-device gameplay verification remains with the user.

Local reports and rendered screenshots are under `artifacts/logs/gold-native-restoration-*.log` and `artifacts/gold-miner-development/ui/`; these generated artifacts are not shipped resources.
