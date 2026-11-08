# Native Gold Miner source campaign, rules 5

New native runs use `GoldMinerBalanceProfile.Source` (5). Profiles Legacy (1), Native (3), and Classic (4) retain their previous targets, ore values, mechanics, inventories and 240 shipped layouts. The name Classic in rules 4 did **not** mean the H5 campaign. Existing native runs continue on their accepted profile; only a new run selects Source. Rules 5 keeps schema 1's existing native slots and adds separate source records, preserving historical records and backups. The removed H5 games' separate private save files are not read or overwritten.

## Authoritative input and extraction

The removed separate game shipped two different games selected by the user: solo 4399/230816 and duo 4399/232532. Extraction used their tracked, actually packaged sources, not reconstructed maps or difficulty estimates. `Assets/Resources/GoldMiner/Source/manifest.json` records the original file SHA-256s and the output hashes. `solo.json` preserves all original `LevelName`, background ID, ordered sprite definitions, decimal coordinates, scales and flips. `coop.json` preserves the ordered original map objects, ore types, prices, weights and image dimensions.

| Data | Native resource | Checked against the removed source |
| --- | --- | --- |
| Solo maps | Source/solo.json | 30 maps, 611 objects; every original field compared after JSON round trip |
| Duo maps | Source/coop.json | 30 maps, 594 objects; every original field compared after JSON round trip |
| Solo sprite metadata | solo.json `frames` | All 17 referenced frame rectangles, trim offsets and source dimensions |
| Sprite pixels | Source/solo-sprite*.png and duo-*.png | Only referenced ore pixels retained; the source menu/advertising/character atlas is not packaged |
| Goals and prices | docs/gold-miner/source-contract.json | Executed the original `Game.getGoal` and `ShopButton.getPrice`: 40 goals, 200 minimum/maximum price intervals |

`export-source-campaign.mjs` accepts an explicit source web directory (second argument; defaults to the ignored research copy). It is an extraction/review tool, never a build dependency. Native JSON is verified against pinned SHA-256 before parsing. Per-map SHA-256 signatures in `GoldMinerSourceSignatures` also bind saved initial entities to the shipped source positions, identities, dimensions, values and weights; changing both checkpoint and live copies cannot bypass this check. Generated bag outcomes are persisted separately from those immutable map definitions.

## Source-to-native evidence

Original source paths below are names recorded in the manifest. Their pre-removal snapshots remain only under ignored `artifacts/gold-miner-source-reference/` during this task; no JavaScript engine or H5 runtime is used by native Gold Miner.

| Contract | Original implementation | Native implementation |
| --- | --- | --- |
| Solo map selection | solo/game.js `Game.create`, original lines 5836–5840: 3 variants per group; after level 10 use `(level-6)%10` | SourceRules.MapGroup / LevelCatalog.CreateSourceStart |
| Solo cumulative target | solo/game.js `Game.getGoal`: `5*(27*n*n+28*n+75)` | SourceRules.Target; independently executed source contract |
| Duo maps/target/completion | duo/js/game-data.mjs MAPS; game.mjs freshRun, Target.create, Play.gameOver: map 1–30; `500+150*n` | Source catalog, Target, Completed |
| Solo actual timer | solo/game.js Game.create sets 61; changeTime decrements and ends at -1 | 3720 ticks at 60 Hz (62 source timer callbacks), HUD starts at 60, displays 59 after two seconds, and ends at the 62-second source cutoff |
| Duo timer | game.mjs freshRun.time=40; Play.update ends at 0 | 2400 ticks at 60 Hz |
| Solo object coordinates | solo/game.js Boulder adds x+35,y+50; source origin y46 | `(x+35)*.01`, `(y+4)*.01`; scale/flip retained |
| Duo object coordinates | game.mjs Ore uses MAPS x/y; hook y55 | `x*.01`, `(y-55)*.01` |
| Moving solo animals | Boulder types 6/10, 370px / 4200ms, flip chooses direction, yoyo | Per-entity original endpoints; 252 ticks per leg |
| Solo catch radius | Boulder.hitTestPoint compares distance with absolute sprite **width** | Source metadata radius uses original untrimmed width and scale; no native radius substitution |
| Duo catch radius | Ore constructor: `max(15,max(width,height)/2)+5` | Exact original source dimensions |
| Solo return speed | Pod.updateStatus: `10+strength-weight` px/frame | SourceRules.RetractPerTick |
| Duo return speed | Hero.grabSpeed=4, friction=`1-max(weight-strength,20)/100` | SourceRules.RetractPerTick |
| Solo TNT | Game.boomBounler destroys *other* objects within 100px; picked TNT still returns original value 1 | SourceSimulation.Catch and delayed Award |
| Solo bag | BoulderData.reciveSprite215 picks money/power/dynamite and variable, possibly negative weight | SourceRules.DrawBag; saved reward/weight never rerolled on resume |
| Solo delayed income/strength | adapter.js uses 900ms reward; dynamite500ms | 54/30 persisted ticks; timeout grants no unfinished reward |
| Duo bag | Play.handleLuckyEffect: power sets60 with probability .3/.6, otherwise1…600 | SourceSimulation.Award |
| Duo direction and boundary | Original Hero.update flips grabSin/grabCos while retaining the carried ore; even an empty returning hook can catch | Persisted SourceHook.TravelDirection is independent of Loaded state; boundary-held regression remains valid across save |
| Duo inventory | getShop generates shared bombCount; reserveBomb deducts before500ms effect | SharedBombs plus per-hook persisted BombTicks |
| Solo shop | ShopButton.canCreate/getPrice; Shop.buyItem has no funds check | SourceEconomy: same eligibility/ranges, debt permitted, random offers/prices persisted |
| Duo shop | random-comparator three-item selection; SHOP_TYPES price functions; funds check | Three distinct random offers, original price ranges and affordability |
| Duo ineffective items | shop4/5 are sold but Play has no stone/diamond value effect; pig does not move; ground bomb is ordinary value0 ore | Preserved as implemented; no behavior inferred from item descriptions |
| Timeout | Both originals settle immediately, discarding unfinished returns/rewards | SourceSimulation immediately marks settlement; old profiles keep their existing drain behavior |

Solo source prices after completed level n: dynamite `[1+2n,300+2n]`; strength `[100,399]`; clover `[1+2n,52n]`; rock book `[1,150]`; diamond polish `[201,100n+200]`. Duo ranges respectively `[1,300]`, `[100,399]`, `[1,100]`, `[1,150]`, `[200,499]`.

The source's solo save validator already bounded levels to 1…100000. Native retains that persistence boundary, not a ten-level ending; the UI must explicitly explain the boundary at level100000 instead of pretending it is normal campaign completion or offering an invalid next level. Duo alone completes at30.

## Geometry and random state

Native uses 100 source pixels per world unit, with one uniform rendering scale. Source field sizes are solo6.4×4.34 (from hook y46 to canvas bottom480) and duo5.5×3.45 (from y55 to400). The source fullscreen H5 adaptation stretched horizontal positions according to browser width; native preserves the canonical source map geometry and fits it uniformly. It does not apply the old native generator, nonoverlap constraints, extra margins, fixed animal tracks or reachability rebalance. Offscreen moving animals and edge objects remain at their source coordinates.

The originals used unseeded `Math.random`; native uses the existing saved xorshift stream for reproducible continuation. Random ranges, source bag branches, inventory lifetime and three-offer rule are preserved. Browser-specific random-sort call order is not a promised cross-engine sequence. Frame-based original motion is represented at native's fixed60Hz; this is not a claim of identical wall-clock animation under every browser frame rate.

## Validation scope

`scripts/test-gold-miner-source.sh` checks all60 layouts/1205 ordered objects, precise scales/flips/values/weights, original goal/price execution results, source identity corruption, source save round trips, 100px TNT destruction and its returned one-dollar reward, saved500ms shared bomb reservations, delayed-state corruption, purchased buff/inventory conservation, every simulation tick, deterministic in-flight continuation, exact timeout and solo debt. Existing core, catalog and persistence suites retain old mechanics/240 layouts/actual v1/v2 device-save fixtures and backup verification. Source map signatures protect checkpoints independently of live state.

These are source extraction, pure .NET rules, serialization and filesystem checks. They do not establish Unity rendering, compilation on every target, real touch behavior, full-campaign playthrough or device parity. Runtime UI and build/export checks are reported separately.
