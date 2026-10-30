# Age of War 1 runtime differential trace

On 2026-10-30 the fixed original SWF ran in local **Ruffle 0.7.1 Web/WASM**, in a separate Chrome tab. Its original 315 action byte arrays remain byte-for-byte present after instrumentation: the injector hashes their multiset before modification and verifies every hash/count in the serialized output. Only additional DoAction tags are compiled. The downloaded SWF is never overwritten. Input SHA-256 is `5f88517846ff0eb75eee3bd6dfebbf9fad75edbb4dbe20ed8ea4c0afb3dfd3a1`. Exact tool, hook, runtime and fixture hashes are in [metadata.json](../Tests/AgeOfWarParity/One/fixtures/metadata.json).

## Observed scenario and result

Normal difficulty; after the first battle frame's callbacks, the injected input calls the original `menu.add_to_tray(1)` three times. Observe 800 subsequent 40 Hz frame boundaries: 20 seconds of game time. In-game Math.random is explicitly replaced by a constant 0.1 after clearing property flags. Three recorded `RNG,0.1,0.1,0.1` values verify that this replacement took effect. AVM1's built-in `random(n)` opcode is **not** replaced. During this scenario the enemy unit-level is 1, so its `random(1)` always selects Clubman; integer cosmetic choices are outside the comparison.

The native runner compiles the production One/Shared sources with a temporary copy of `OneModel.cs` whose `Next()` alone returns 0.1. It does not change production RNG or accept a claim that native and Flash seeds are interchangeable. Both runners apply the same input after frame1 callbacks. Actor IDs are compared by original creation sequence; corpses are excluded from the observed roster because the original removes a dead actor from `_root.ennemies` immediately while its display clip remains.

**After fixes: 6,474 matching state rows, zero different rows, zero missing rows.** This is 800 global `F` rows and 5,674 live/newborn unit `U` rows, with numeric tolerance 1e-9 and exact string comparison, without shifting time. The global columns cover gold, XP, training timer/ID, AI technology/action/training timers, reserved enemy population, and both base HP values. Unit columns cover ID, type, side, X/Y, HP/dead state, scan counter, cached target/distances, outer action selection and nested action frame.

- First player unit is attached at frame41 at X=150, before its frame1 initialization (`health`, `dead`, scan and targets are still `undefined`). Initialization and its first movement happen at frame42; walk action frame remains1.
- Player's first hit is before the frame554 snapshot; enemy's first hit is before frame558. Both hit callbacks execute at the original action frame20 and remove15.8 HP.
- Enemy's first death, +20 gold/+40 XP and the next AI training start occur at frame674. Source animation frame actions execute old-to-new; unit enterFrame callbacks execute new-to-old, before charge/menu/AI/base callbacks. At frame719, player1's hit runs before enemy5's hit; at frame764 player3's hit runs before enemy5's.
- Before correction, 5,790 of 6,463 common rows differed, with 11 original-only and24 native-only rows. The saved before/after reports preserve that result.

The changes in `AgeOfWarOneBattle.cs` defer attached actor initialization until the following Tick, split animation frame actions from unit enterFrame callbacks, place unit callbacks before menu/AI, and quantize actor display-position writes to integral twips. A narrow native test first failed with `expected150, actual151.17432935400865`; the revised suite passes39 cases. The fixture comparison also failed before those changes and passes afterward.

## Display-property probe

The added probe creates an empty temporary clip, writes `_x`, reads it, then removes the clip. It does not alter a game object. The trace records ordinary input Number and resulting display position:

| Number expression | Display `_x` |
|---|---:|
| 0.01 / 0.049 | 0 |
| 0.05 / 0.099 | 0.05 |
| -0.01 / -0.049 | 0 |
| -0.05 / -0.099 | -0.05 |
| 150.1 | 150.1 |
| 150.1 + 0.7 (trace prints150.8) | 150.75 |
| 151.45 + 0.7 (trace prints152.15) | 152.1 |
| -151.45 - 0.7 | -152.1 |

This is `truncate(value * 20) / 20`, toward zero, including binary floating-point accumulation before truncation. Do not round all rule variables: HP, timers, distances and velocity variables are Numbers. The production setters now cover actor, projectile and particle display-position writes. The additional particle scenario below verifies reward, smoke and flame paths; other particle paths still have only source-derived coverage.

## Reproduce the saved comparison

From the repository root, with .NET10 at `$DOTNET_ROOT/dotnet` or `~/.dotnet/dotnet`:

```sh
bash scripts/test-age-of-war-one.sh
bash scripts/age-of-war-parity/one/test.sh
```

`native.py` writes its disposable project/source adapter and result to `/tmp/aow-parity-one`; `compare.py` returns nonzero on a differing or missing row. No Unity process is required. `ruffle-controlled-first800.csv` is the observed golden fixture. `ruffle-uncontrolled-first800.csv` preserves the **discarded RNG-control attempt**: initial `Math.random=...` was ineffective. It must not be used as a deterministic golden sample.

To recapture the original using FFDec26.4.0, OpenJDK17 and the already downloaded Ruffle package:

```sh
bash scripts/age-of-war-parity/one/prepare.sh
```

The script documents its default local paths and supports `AOW_ONE_SWF`, `FFDEC_HOME`, `AOW_JAVA_HOME`, `AOW_PARITY_DIR`, `AOW_RUFFLE_DIR`, `AOW_PARITY_PORT` overrides. It compiles the injector, verifies action hashes, creates a temporary instrumented SWF and starts a loopback-only server. Open a **new** browser tab at `http://127.0.0.1:8769/`, use the original preloader Play, menu Play, then Normal. No further game clicks are needed. At DONE800 the page suspends Ruffle and POSTs the trace only to the local server at `/tmp/aow-parity-one/reference.csv`. Compare that file with the native trace using `compare.py`. Stop the local server afterward.

## Trace format and source references

`F` / `U` field names are in `compare.py`. Original frame columns are one-based, while production visual `Frame` is zero-based; the parity runner reads the internal one-based actor action frame. `H` is an original hit-call observation; its frame column is the *previous* root snapshot counter, so `H,553` precedes snapshot554. `P` records callback ordering at selected intervals; instance-generated names can differ across reloads. `TWIP`, `RNG`, `CHILD`, `BOOT`, `READY`, `DONE` are instrumentation diagnostics and are not state-comparison rows.

Source paths are relative to `analysis/aow1/export/scripts`: `frame_13/DoAction_3.as:78-98` attaches actors; `DefineSprite_699_ennemy/frame_1/DoAction.as:1-50` initializes them, `:90-264` supplies enterFrame movement/target/death behavior; `DefineSprite_212_menu/frame_1/DoAction.as` trains the queue; `DefineSprite_33_base_comp/frame_1/DoAction.as` drives AI; Clubman's nested attack frame20 invokes hit. The ordering evidence comes from actually running those original bytes, not from textual script ordering.

## Remaining limits

This proves the recorded normal-difficulty Clubman scenario on this Ruffle build. It is **not Adobe Flash Player validation**, a full playthrough, a screenshot/pixel/audio comparison, or device acceptance. The 20-second fixture does not exercise turret/projectile collision, higher units/ages, specials, paused animation changes, post-removal property lifetime, `gotoAndPlay(0)`, or all display-object quantization paths. Native deterministic RNG remains a separate contract. The separate projectile fixture below resolves its recorded ground-impact removal case. Other cases remain explicit pending runtime comparisons; the broad source-derived console suite is complementary evidence, not a substitute for them.

## Additional 12-projectile runtime fixture

The separate `fixtures/projectiles/reference.csv` records a40-frame run which calls the original `create_bullet(400,400,30,10,id,1)` for each id1..12 after the first root snapshot. All315 original action byte arrays are preserved. The final comparison matches **131 projectile state rows** with no missing rows, plus **49 particle-creation events** counted per original frame and particle ID. Bullet2 rotation is explicitly excluded because its integer `random(6)` spin is not controlled; it does not influence that bullet's trajectory. The other projectile rotation fields remain compared. Particle positions, visual frames and random blood variants are not asserted by this scenario.

This run exposed delayed frame1 projectile initialization and display-property quantization, and resolved the earlier `removeMovieClip` uncertainty. Although the decompiled script has no `break`, after a clip is removed its later substeps no longer have usable coordinates. Original bullet9 emits one part7 explosion on the removal frame; the previous native implementation emitted8. Bullet6/7 likewise emitted excess batches. The port now stops effective substeps on removal, and the old static test expecting repeated damage was replaced. A new regression failed with expected1 versus actual8 impacts before the correction. The separate raw-position regression failed with expected400 versus actual430.3108891324554; both now pass.

The native projectile runner compiles a temporary copy of `OneEffects.cs` with a read-only Console.WriteLine at `CreateParticle` entry so it can observe creations even when that native particle also disappears before Capture. It still uses the same constant-float RNG adapter. The saved raw before-fix report includes the uncontrolled bullet2 spin; the final comparator documents and excludes that single field.

`bash scripts/age-of-war-parity/one/test.sh` runs both retained fixtures. To recapture the projectile SWF, run `bash scripts/age-of-war-parity/one/prepare-projectiles.sh` and open a new tab at `http://127.0.0.1:8772/`; use original Play → Play → Normal. Trace saves to `/tmp/aow-parity-one-projectiles/reference.csv`. This synthetic trajectory/ground-impact setup does not prove turret-to-projectile scheduling, target-overlap or base collision, specials, pause interleaving, particle motion, or late-age unit battles. The normal opening fixture still passes6474 rows after these changes.

## AVM1 properties and fifth-age ability follow-up

A separate original-SWF run records the AVM1 MovieClip property setters in `fixtures/properties/reference.csv`. This is independent of the AS3 Sprite probe used by the second edition. It uses a temporary empty clip, without touching game objects or random state. All315 original action byte arrays remain present. `_alpha` uses percentage units and stores an integral 1/256 multiplier: setting98 returns97.65625; setting-10 returns-9.765625; setting200 returns200. It does not clamp the stored value to0..100. Repeated subtraction of2 from100 reaches-0.390625 on write43; subtraction of1 reaches-0.390625 on write86. The native effect's existing smooth-double fade therefore retains particles too long. `_rotation=181` returns-179, -181 returns179, and ±720 returns0. The measured scale values show no corresponding fixed-step quantization; no scale-quantization patch is proposed.

The second new fixture, `fixtures/effects-special/reference.csv`, is a110-frame original run. After root snapshot1 it calls the original particle factory for reward/part1, smoke/part4 (rotation181), and flame/part6 at(400,300), then attaches the original `special5` linkage at its source depth99999. It invokes the actual fifth-age ability timeline without claiming to test the evolution/HUD/button eligibility path. Math.random=.1 is verified; the original integer random opcode remains uncontrolled. Only part4's random scale column is excluded. The comparator checks particle coordinates, alpha, rotation, reward ymove, special coordinates/currentframe/counter, every live strike, and each original bullet creation call.

Observed results:

- At snapshot1, all particles retain raw coordinates(400,300), alpha100, scale100, rotation0, and undefined frame1 variables. The special retains(0,0), frame1 and undefined cc. The corrected port defers their frame1 initialization to the next Tick.
- At snapshot2, reward Y=297 and ymove=3/1.1; at snapshot3, reward Y=294.25. The latter proves per-write twip truncation rather than ordinary-double accumulation. Its last visible snapshot is71, with Y265.1 and alpha1.5625; it is gone at72. Before correction, the native port retained it through78.
- Part4 first alpha is95.703125, rotation-179, and its last visible snapshot is24. Part6 first alpha is98.828125 and its last visible snapshot is86, alpha0.390625; it is gone at87. Before correction, the native port retained part6 through100.
- Special5 already emits all11 strikes at the correct times: callbacks occur after snapshots5,10,…55, at X180,230,…680/Y420. Each strike appears at the next snapshot and disappears on the following one. Special frame56 is the last visible frame; source frame57 removes it. These emission/cleanup boundaries already match production; only its initial attachment transform differs.

Before correction, the production source had181 differing rows among259 common Q/S/B/D rows, plus22 extra native rows. Six targeted native regressions failed with the corresponding measured values. The applied changes add display alpha/rotation setters, apply twip setters to particle positions, separate particle attachment from frame1 initialization, and separate special attachment from frame1 initialization. Rule variables such as ymove, gravity and velocity retain full precision. The production source now matches all259 original state/event rows with no differing or missing rows; all6 new regression cases pass. The exact applied diffs remain archived as `scripts/age-of-war-parity/one/applied-effects.patch` and `applied-effects-tests.patch`; they are audit artifacts and must not be applied again.

Two old static tests read random blood variants or sounds before the attached clip's first frame. Their setup now advances a real Tick before checking those values. This changes the observation boundary while preserving the expected variants and sound IDs. The existing39-case One suite passes after the production correction. All previous original traces remain green:6474 opening rows,131 projectile states and49 particle-creation events. These console/runtime-trace results do not claim a Unity or Android validation of this new patch.

Run all retained scenarios and the six property/lifecycle regressions against current production:

```sh
bash scripts/test-age-of-war-one.sh
bash scripts/age-of-war-parity/one/test.sh
```

Individual new checks are also reproducible:

```sh
python3 scripts/age-of-war-parity/one/native-scenario.py properties
python3 scripts/age-of-war-parity/one/native-scenario.py effects-special
python3 scripts/age-of-war-parity/one/compare-effects-special.py \
  Tests/AgeOfWarParity/One/fixtures/effects-special/reference.csv \
  /tmp/aow-parity-one-effects-special/native.csv
```

To recapture, run `bash scripts/age-of-war-parity/one/prepare-scenario.sh properties` (default localhost8774), or `AOW_PARITY_PORT=8775 bash scripts/age-of-war-parity/one/prepare-scenario.sh effects-special`, open a separate tab, and use the original Play → Play → Normal controls. The page saves `/tmp/aow-parity-one-<scenario>/reference.csv` and suspends Ruffle. Exact source/hook/fixture/instrumented-SWF hashes and exclusions are recorded alongside each fixture. These samples do not prove other ability timelines, target collision or damage, late-age unit combat, or Adobe Flash equivalence.
