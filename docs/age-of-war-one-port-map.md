# Age of War 1: native rule and timeline mapping

The implementation targets only `age-of-war-1-7k7k.swf`, SHA-256 `5f88517846ff0eb75eee3bd6dfebbf9fad75edbb4dbe20ed8ea4c0afb3dfd3a1`, 650×450, 40 fps. Source references below are relative to `/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow1/export/scripts`. `OneData.cs` contains the source EN/TU/TS/ES/EV arrays, hit rectangles extracted from the original display transforms, and 83 unit/turret action timelines. Production code does not read the SWF or depend on Unity.

## Boundary and coordinates

`AgeOfWarOneBattle(int difficulty, uint seed)` implements `IAgeOfWarBattle`; difficulty 1/2/3 is Normal/Harder/Impossible. Call `Tick` exactly 40 times per active application second. The original in-game pause continues selected Flash callbacks; application background must stop calling Tick altogether. No speed command exists in edition one. Pausing hides the menu, but already-visible base build/sell buttons remain available; PlaceTurret and SellTurret therefore still execute.

Visual X/Y are original world pixels, Y down, ground 425, world extent 0..1000. Camera translation is 0..-350. Frame is the zero-based frame of the current original action child timeline; variant attack frames remain absolute positions within that child. Unit assets include original nested placement matrices, shadows, and character filters; only team mirroring is applied by Capture. Base art is raw sprite8: player (0,374.25); enemy (1002.25,375), scaleX -1.0052490234375. Extension sprites14/20/26 retain distinct keys `base-extension/{extension}/{age}` and original placement. Turret slot commands use **1..4**.

One offers no population limit. `MaximumPopulation=0` denotes that rule; the five-entry production queue is separate. `BuyTurret(id,slot=0)` selects a cursor without charging, `PlaceTurret(slot)` charges on placement, and `Cancel` clears the cursor. This preserves the original absence of a second affordability check in create_turret. `BuyTurret(id,slot>0)` is an atomic convenience for tests/replay clients.

## Source ownership

| Native location | Original source | Behavior retained |
|---|---|---|
| `OneData` | `frame_13/DoAction.as` | All 16 EN, 15 TU/TS, 16 ES records and cumulative EV thresholds; original spellings |
| Constructor, roster, spawn, turret placement | `frame_13/DoAction_3.as` | 175 cash; 500 HP bases; unit X=150/860 plus random offset; turret X=52/948, Y=345/300/253/205; enemy slot4 checks slot3, as written |
| Training, purchase | `DefineSprite_212_menu/frame_1/DoAction.as:1-43,124-146` | Five slots; payment/refund result; individual EN training frames; immediate next queue start; training continues during pause |
| Evolution | `DefineButton2_188/BUTTONCONDACTION on(press).as` | Cumulative 4000/14000/45000/200000 XP; XP is not deducted; +300×new age current/max base HP |
| Slots, sale | Button186; Buttons42..45 | 1000/3000/7500; rounded 50% sale; existing turrets survive evolution |
| Units | `DefineSprite_699_ennemy/frame_1/DoAction.as` | ±0.7 movement; initial scan3 then5; forward-only roster selection and cached target; hitzone spacing; enemy-only difficulty multipliers; `damage-random*2`; death reward and removal after80 |
| Bases / AI | `DefineSprite_46_base_player/frame_1/DoAction.as`; `DefineSprite_33_base_comp/frame_1/DoAction.as` | AS round(health)<0; player end check even paused, enemy check only active; tier at1500/5000, evolution8000; exact asymmetric turret schedule and 0.3 random action every40 frames; six enemy production reservations; no player-style economy payment |
| Turrets | `DefineSprite_886_turret/frame_1/DoAction.as`; 15 child animation sprites | Scan every10; center-distance gate; aim at targetY-height/1.3; source TS interval; projectile emitted by child shoot callback, not cooldown reset; children already playing continue during pause |
| Special charge | `DefineSprite_78/frame_1/DoAction.as`; Buttons58/71/73/75/77 | Initial1999, ready2000; no XP cost; recharge continues paused. SpecialChargeFrame samples round(timer/2) before incrementing timer, as source78 does |
| Specials | Sprites978/977/966/973/976 | Age1 200-frame controller emitting every9; age2 every5; age3 living-friendly +1 HP per active frame with independently running300-frame timer; age4 aircraft every15 untilX700; age5 eleven moving strikes and frame57 cleanup |
| Projectile movement and collisions | Sprites917/888/915/902/901/913/911/909/907/905/904/903; 896/894/892/890 | All12 regular and4 special projectiles; original muzzle offsets, gravity, rotation, acceleration, substeps, bounds tests, oil streams and fragment damage |
| Particles | Sprites961/949/929/923/899/933/931/948/925/927/898/930 | All12 particle behaviors; fragment5 damage10, fragment11 damage30; oil8 damage parameter; 12-way blood/oil variant; pause-independent frame28 explosion removal |
| Nested animated fire | `DefineSprite_589/frame_1,31/DoAction.as` | Independent random initial phase and frame31→3 loop; tank/war-machine death, special projectiles and smoke; nested frames continue under in-game pause. War-machine685 is rendered as die-back / flame(depth12) / die-front to retain occlusion |
| Other nested timeline loops | Source sprite905→872; 904/903→175; 961→960; 973→971 | Projectile10/11/12 two-frame children, reward coin40-frame children, plane rotor3-frame children use independent display age and continue during pause |
| Tooltips | menu unit_info/turret_info; Buttons42..45/83/143/179/181/184/186/188 | Public `CaptureTooltip(action,id)` returns original `desc` and current `bdesc`; Special has no original hover text |

## Unit damage events

All frame numbers in this table are original one-based numbers. The full generated map is in `OneData.Timelines` with source sprite IDs.

| Unit | Melee animation callback | Stationary / walking ranged callback |
|---|---|---|
| 1 Club man | sprite237 frame20 hit | — |
| 2 Slingshot man | 263:20 hit | 278:26 / 280:30 ranged_hit |
| 3 Dino rider | 315:21 hit | — |
| 4 Sword man | random348 attack1/2; 33 / 76 hit | — |
| 5 Archer | random380; 38 / 79 hit | 393:15 / 404:16 ranged_hit |
| 6 Knight | 450:23 hit | — |
| 7 Dueler | random471; 19 / 62 hit | — |
| 8 Mousquettere | **482:13 ranged_hit**, even for melee action | 482:13 / 484:1 ranged_hit |
| 9 Canoneer | 497:27 hit | — |
| 10 Melee Infantry | 516:14 hit | — |
| 11 Infantry | 532:7 hit | 542:7 / 551:3 ranged_hit |
| 12 Tank | 573:30 hit | — |
| 13 God's Blade | 639:23 hit | — |
| 14 Blaster | random654; 21 / 44 hit | 661:6 / 662:21 ranged_hit |
| 15 War machine | 681:13 hit | — |
| 16 Super Soldier | same sprite663 actions as14, different ES values and holder filter | same as14 |

The unit ranged animations call direct damage methods; they do not spawn the turret projectile types. Source 8 deliberately uses ranged damage for the action labeled attack. Pausing units changes their action to idle, abandoning an in-progress attack; resuming starts a new action. Corpses also switch to idle while paused, matching the unconditional source else branch.

## Native HUD integration

`AgeOfWarOneHud` renders the original dynamic base-health text/bars, source special-button scenes and1000-frame recharge mask, source build/sell slot hotspots, and cursor/selected-turret preview. `Hit(stagePoint)` returns source scene hotspots after applying the player-root and camera transforms; `Pointer(stagePoint,visible,down)` drives hover/down button states. Root HUD still owns economy, description, queue and training fields. Reward particles carry `TextScene="one-reward"`, `TextField="cash"`, and their actual `"+ " + reward` string; its two original coin layers use the particle visual frame.

## Audio

Native event sound keys are `sound/{DefineSoundId}`. All direct unit/turret frame sounds are in OneData. Nested death sprite244 chooses one of239..243. Explosions use591; aircraft972; age5 strikes680. Root timeline frame13 starts1035 with SOUNDINFO flags0x14 (no multiple + loop count32767); frames14/15 issue flags0x20 (stop). Original pause does not stop music. No DefineButtonSound tags or menu click-sound scripts exist in this sample.

## Verification and explicit limits

`bash scripts/test-age-of-war-one.sh` compiles the Unity-independent source at C#7.3 under .NET10 and executes source-derived scenarios. Initial suite failed because no native implementation existed; later narrow tests exposed and fixed special5's cleanup boundary, missing random death sounds, particle variants, nested child playback, cursor placement, tooltip omissions, and paused projectile/coin/rotor child playback. The current suite checks economy, all ages, source pause distinctions, all16 attack paths, all6 ranged paths, all15 turret callbacks, projectile family hit/substep behavior, skill emission/healing, death rewards, deterministic replay, and original end thresholds.

The baseline suite is **source/static and native console execution evidence**. The additional bounded original-SWF/Ruffle comparisons below provide runtime evidence for their recorded scenarios; they do not prove complete Flash equivalence or Unity/Android acceptance. The following limits remain explicit:

1. The normal-difficulty three-Clubman opening now has an actual 800-frame original-SWF/Ruffle differential trace: 6474 state rows match after fixing delayed attach initialization, actor twip writes, old-to-new animation actions before new-to-old actor callbacks, and actor callbacks before menu/AI. See [runtime comparison](age-of-war-one-parity.md) for reproduction and exact scope. Special/turret/projectile callback interleaving and other scenarios remain pending; this is not a global all-gameplay equivalence claim.
2. A 40-frame original-SWF/Ruffle projectile fixture now confirms removal suppresses subsequent effective substeps: bullet9 creates one ground explosion, not eight; bullet6/7 likewise emit one impact batch. Native now stops effective collision processing when removed. The previous source-only test that expected repeated hits was wrong and has been replaced with runtime-backed assertions. All12 projectile trajectories (131 observed states) and49 particle creation events match in that fixture; see runtime comparison limits.
3. part8 uses uninitialized `xs`; native X remains finite, matching display-property rejection of nonfinite assignments. This property-set behavior needs direct fixed-SWF confirmation.
4. Sprite589 calls gotoAndPlay(random(25)), including0, and recharge sprite78 can request frame0 immediately after use. Native bounds these frame requests to1; the invalid/same-frame AVM1 goto edge needs direct confirmation. Flash's Math.random has no exported seed contract; native uses an explicit xorshift32 stream, so same numeric seed is only a native replay guarantee.
5. Full visual/audio compositing, nested flame occlusion, device-font appearance, all difficulty playthroughs, original pause input hit-testing and actual Android lifecycle are external integration acceptance, not established by these tests. Do not describe this document as all-gameplay/pixel equivalence verification.

### Additional runtime display-property corrections

The AVM1 property and particle/special5 traces in `docs/age-of-war-one-parity.md` now back the production setters and lifecycle: particle alpha stores1/256 multipliers, display rotation wraps at±180, particle position writes truncate to twips, and particle/special attachment precedes frame1 initialization. The production comparison matches259 observed state/event rows and6 new regressions. The original39-case suite and all earlier original trace samples also pass. The exact applied diffs remain audit artifacts under `scripts/age-of-war-parity/one/applied-effects*.patch`; no patch remains awaiting application. Special5 emission and frame57 cleanup timings were confirmed, not changed. Other ability/late-age battle paths and Adobe Flash equivalence remain outside the runtime evidence.
